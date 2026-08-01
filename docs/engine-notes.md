# Space Engineers engine notes

Reverse-engineered facts about how Space Engineers generates and stores voxel ore. These describe the **game**, not
this mod, and were established by probing live worlds. They are the constraints the mod's design falls out of.

Most of them can be re-checked in game with the `/odr` commands — `/odr recall` for the LOD claims, `/odr vein` and
`/odr planet` for the planet ones, `/odr boulders` for the boulder filter. See
[api.md](api.md#verification-recipes).

The planet vein-position math has its own document: [planet-ore-model.md](planet-ore-model.md).

## Voxel storage and LODs

- **Only LOD0 and LOD1 are trustworthy on planets.** LOD2 and above are shifted toward the planet centre and do not
  line up with the actual veins. Worse, the shifted voxels vanish when the region is updated, so a search that
  believed them would report ore that isn't there. Planet confirmation reads LOD1.
- **LOD bounding boxes are not offset at higher levels.** When subdividing into 2×2×2 children, the child
  coordinates must be offset in the negative direction — `[2*x-1, 2*x]`, not `[2*x, 2*x+1]`. Getting this backwards
  produces a search that misses roughly half of everything and was the cause of a long stretch of dead ends.
- **A full LOD4 scan expanded into 8×8×8 LOD2 children finds 97%+ of the LOD2 nodes.** This is what makes the
  asteroid coarse/fine split viable: the coarse pass is allowed to be approximate because the fine pass confirms.
- `Storage.ReadRange` is usable at LOD1 and LOD2 with box sizes 4, 8, 16.

## Planets

- Ore locations come from the generator's `_mat.png`, **2048×2048 per cube face** for vanilla planets. Blue channel
  selects the ore (via an `OreMapping` entry); red encodes biome.
- **~3% of pixels are non-empty blue**, and up to ~0.5% for any single ore. Per-ore data is very sparse — Pertam
  gold, for instance, touches about 10% of 64×64 cells. Sparse structures pay off heavily.
- A pixel packs into 16 bits as `(x:6, y:6, colour:4)` within a cell; a 64×64 cell holds roughly 12 pixels per ore.
- **One pixel is too coarse to aim at on a large planet.** Planets span 19–120 km diameter (planet spawn tool
  limits), so a 4×4 sub-pixel subdivision is needed before the derived position is close enough to hit ore.
- **1 m thick ore layers are not generated as continuous sheets.** Finding them would mean widening the confirmation
  box for every probe. Ignored entirely — `OreMapping` entries with `Depth < 1.5` are skipped.
- **Ignore the vertical extent of a pixel chunk**: pick the horizontally-nearest (sub)pixel. This is why planet
  results are "nearest horizontally", not nearest in 3D.
- `MyPlanet.GetClosestSurfacePointLocal` is the best available surface lookup. Everything about depth and slope is
  built on it.
- **Biome-driven deposits are out of scope.** Ice lakes, oasis and polar-cap ice are placed by biome rules (red
  channel) rather than by an `OreMapping` blue value, and reproducing those rules faithfully was a repeated failure
  — see [decisions.md](decisions.md).
- The vanilla `_mat.png` blue channel has a repeating texture (~128×128) that could be exploited for compression.
  Never used.

## Asteroids

- Ore generation spheres have a radius of **12–48 m**, which sets the useful coarse-scan resolution: LOD4 (16 m) is
  fine enough to not miss whole veins, but small or partial ones are still missed.
- Ice asteroids get very large — 1024×1024×1024 at roughly 20% fill. Any per-ore structure has to degrade
  gracefully into that case, which is why the asteroid page switches from a leaf list to a dense bit pyramid above a
  threshold.
- Asteroids can be reverted wholesale by world cleanup; `RangeChanged` has to invalidate the entire cache, not patch
  it.

## Boulders

- Boulders are 16 m and **rotated**, so their storage frame is not axis-aligned with the world.
- LOD1 is accurate for them and cheap enough to read exhaustively.
- **Fake boulders**: entries with `BoulderInfo.SectorId >> 51 > 0` are skipped. This is an old empirical fix — those
  entries do not correspond to voxels that exist for the player. The underlying reason was never established; the
  filter has simply always been necessary.

## Modding sandbox

- **Mod IL runs 3–4× slower than the same code outside the game.** Measured across both PNG decoding and plain
  array-crunching loops, with no single hot spot — the cost scales with instructions executed. This is the single
  most important performance fact in the project: it means micro-optimisation inside a hot loop buys very little,
  and only removing work wholesale matters. Full measurements in [planet-load-perf.md](planet-load-perf.md).
- `System.Drawing` is unavailable, hence the vendored PNG decoder.
- Voxel and generator definitions are still being mutated by other mods during load, so any enumeration of ore types
  must happen on `UpdateBeforeSimulation`, not at load time.
