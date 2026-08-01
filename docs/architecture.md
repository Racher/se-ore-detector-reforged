# Architecture

The mod is a search engine with no UI. A request names an ore and a sphere; the engine returns the nearest matching
voxel position. See [api.md](api.md) for the entry points and [engine-notes.md](engine-notes.md) for the Space
Engineers behaviour the design is built around.

## Search flow

`DetectorServer` (`MyUpdateOrder.AfterSimulation`) owns one background thread and a
`BlockingCollection<Action>` (FIFO) of work.

1. **`SubmitSearch`** runs on the main thread. It resolves the ore name to an index, collects every root
   `MyVoxelBase` in the sphere via `MyGamePruningStructure.GetAllVoxelMapsInSphere`, and attaches the right page
   component to each (see below). The populated `SearchTask` is posted to the queue.
2. **The background thread** runs the task. It clears the shared `PriorityQueue<Node>`, calls `PushRoot()` on every
   page to seed it, then pops nodes nearest-first and dispatches each to its owning page's `Process()`. The loop
   breaks as soon as the best remaining node is farther than the confirmed result — so cost tracks the distance to
   the nearest hit, not the search radius.
3. **`UpdateAfterSimulation`** drains a `ConcurrentQueue<SearchTask>` of finished work and invokes the caller's
   callback on the main thread, then returns the task to a pool.

`SearchTask` instances are pooled (`Rent`/`Return`) and each carries a pre-created `Action` delegate, so a steady
stream of searches allocates nothing per query. Exceptions from the background thread are captured on the task and
delivered through the same callback rather than crashing the thread.

`Node` is a 16-byte struct — `(distance, page index, payload, face)` — ordered by distance alone. The payload is a
`ulong` whose meaning is private to the page that pushed it.

## Detector pages

A page is the per-voxel-map cache and traversal strategy. Each page class is itself a `MyComponentBase` attached to
the `MyVoxelBase`; there is no shared component wrapping them. All three implement `IDetectorPage`
(`PushRoot` / `Process` / `WorldToLocal`) and are created lazily by `GetOrCreate`, so a voxel map costs nothing until
it is first searched.

Dispatch is by size, in `DetectorServer.SubmitSearch`:

| Condition | Page |
|---|---|
| `MyPlanet` | `DetectorPagePlanet` |
| `Storage.Size.AbsMax() > 16` | `DetectorPageAsteroid` |
| otherwise | `DetectorPageBoulder` |

Voxel maps whose `BoulderInfo.SectorId >> 51` is non-zero are skipped entirely — see the fake-boulder note in
[engine-notes.md](engine-notes.md).

### `DetectorPagePlanet`

Data comes from the planet's ore-mapping PNG rather than from voxel storage, because reading a planet's worth of
voxels is not viable.

- On first access to a cube face, `PlanetMatHelper.LoadPlanetFacePng` decodes that face's 2048×2048 `_mat.png`
  through the vendored `BigGustavePng` decoder.
- Each pixel's blue channel selects an `OreMapping` entry, giving both the ore type and the vein depth. Pixels are
  bucketed into a 32×32 grid of cells, deduplicated, and packed into 16 bits as `(y:6, x:6, depth:4)`.
- A sparse quadtree pyramid per ore per face (6 levels, `LevelOffsets[z] = (4^z - 1) / 3`) marks which cells contain
  anything, so traversal skips empty regions. Roughly 3% of pixels are non-empty, and per-ore occupancy is far
  lower, which is what makes the pyramid worth its memory.
- `PushRoot()` seeds one node per cube face. `Process()` descends the pyramid; at leaves, `TryGrid()` → `TryPixel()`
  converts the (sub)pixel to a world position using the vein math in
  [planet-ore-model.md](planet-ore-model.md) and reads voxel storage to confirm the ore is really there. The PNG
  gives a seed point; voxel storage is the ground truth.
- Large planets use a 4×4 **sub-pixel** subdivision (`_subGrid`, scaled from `averageRadius`) because one PNG pixel
  covers too much ground to aim at. Exhausted sub-pixels are tracked as a 16-bit mask in the upper half of the
  packed pixel word, so a repeated search never re-probes the same spot.

### `DetectorPageAsteroid`

- On first access, samples voxel storage at LOD4 (`CoarseLod`) to find which regions contain each ore.
- Representation branches on sparsity: at or below `SparseThreshold` (256) LOD4 leaves, an ore is a flat `int[]` of
  leaf indices; above it, a full `BitArray` octree pyramid. The list wins on memory for the common few-veins case;
  the pyramid wins on traversal for dense ice clouds, which can be 1024³ at ~20% fill.
- At a leaf, `FindNearestLod2Voxel()` reads LOD2 (`FineLod`) storage to pinpoint the actual voxel. The coarse pass
  narrows; the fine pass confirms. A full LOD4 scan expanded into 8×8×8 LOD2 children recovers 97%+ of LOD2 nodes.
- `RangeChanged` invalidates the whole cache, which matters because asteroid cleanup can revert a body wholesale.

### `DetectorPageBoulder`

Boulders (≤ 16 m) are small enough to brute-force: read LOD1 storage once, collect every ore-filled voxel position
into `Dictionary<int, List<Vector3B>>`, then linear-search by distance. Cache invalidated on `RangeChanged`.

## Supporting pieces

**`MaterialMappingHelper`** — resolves the ore vocabulary once, on `UpdateBeforeSimulation` rather than at load, so
other mods have finished mutating generator and voxel definitions first. Enumerates planet ore channels plus
asteroid-spawning materials, excludes Stone, and builds `NaturalOres[]` (≤ 254), the name→index dictionary,
`MatIdxToOreIdx[256]`, and the `AsteroidWhitelist` bit set. Everything downstream indexes ores by the position in
this array, so it must be stable for the session.

**`PlanetCubemapHelper` / `PlanetMatHelper`** — cube-face ↔ local-direction conversions and the surface-slope
correction used to turn "depth below surface" into a radial offset. Documented in
[planet-ore-model.md](planet-ore-model.md).

**`BigGustavePng/`** — vendored pure-C# PNG decoder. `System.Drawing` is not available in the SE modding sandbox, so
the decoder has to be in-tree.

**`Microsoft/PriorityQueue.cs`** — vendored struct-comparer priority queue, shared and reused across searches to
avoid per-query allocation.

**`Detector/Commands/`** — the `/odr` diagnostic chat commands. Not part of the API; see [api.md](api.md).
