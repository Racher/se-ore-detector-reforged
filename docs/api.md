# Public API

This mod has no player-facing UI. Everything it does is exposed through two interfaces: a Programmable Block
terminal property (for in-game scripts) and `RegisterMessageHandler` message IDs (for other mods).

Both are served by the same background search engine — see [architecture.md](architecture.md).

## Ore names

Every entry point identifies ore by its **mined-ore name** (`"Iron"`, `"Nickel"`, `"Ice"`, …), not by index.
The valid set is built at world load by `MaterialMappingHelper` and depends on the loaded planet generators and
voxel material definitions, so it is **not** a fixed list — modded ores appear automatically. `"Stone"` is always
excluded, and the list is capped at 254 entries.

Use the `ListNaturalOres` message (below) to discover valid names rather than hardcoding them.

## Programmable Block: `DetectOre`

A terminal property registered on every `IMyProgrammableBlock`.

```csharp
MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>
```

| Item | Meaning |
|---|---|
| `Item1` | Search area. `Center` is the search origin; `Radius` bounds the search. |
| `Item2` | Ore name. |
| `Item3` | Completion callback, invoked on the **main thread**. |

The callback receives the world position of the nearest matching ore voxel and a `null` exception on success.

- **Not found**: `Vector3D.Zero` and a `null` exception. Distinguish "found at world origin" from "not found" by
  checking against `Vector3D.Zero` — in practice the origin is never a valid ore location.
- **Failure**: `Vector3D.Zero` and a non-null exception. Two cases are raised synchronously, before the search is
  queued: `InvalidOperationException("Detector is not yet initialized")` if the world hasn't finished loading
  definitions, and `ArgumentException` if the ore name is unknown (the message lists the valid names). Anything
  thrown on the background thread is captured and delivered through the same channel.

Working example: [`Data/Scripts/OreDetectorReforged/ScriptSample.cs`](../Data/Scripts/OreDetectorReforged/ScriptSample.cs).

```csharp
var area = new BoundingSphereD(Me.GetPosition(), 15000);
Action<Vector3D, Exception> callBack = (v, e) => Me.CustomData = e?.ToString() ?? v.ToString();
Me.SetValue("DetectOre", new MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>(area, "Nickel", callBack));
```

## Mod API: message handlers

For other mods, via `MyAPIGateway.Utilities.SendModMessage(id, payload)`.

### `7330961312834800629` — detect ore

Payload is the same tuple as the PB property:
`MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>`, with identical semantics.

### `7330961312834800630` — list natural ores

Payload is `Action<string[]>`. The callback is invoked **synchronously**, during your `SendModMessage` call, with a
defensive copy of the ore name array. It receives `null` if `MaterialMappingHelper` hasn't run yet — that happens on
`UpdateBeforeSimulation`, deliberately late so other mods can finish modifying voxel and generator definitions first.
Ask again on a later tick rather than caching a `null`.

## Behaviour and limits

- **Nearest-first, unbounded.** The search walks a global priority queue across every voxel map in the sphere and
  returns as soon as the nearest candidate is confirmed. Cost scales with the distance to the nearest hit, not with
  the search radius, so a large radius is cheap when ore is close.
- **Planets return the horizontally nearest ore**, not the nearest in 3D. Vein depth is not part of the distance
  metric.
- **Biome-driven deposits are not found** (ice lakes, oasis, polar caps). See [engine-notes.md](engine-notes.md).
- **Thin veins are skipped.** Planet `OreMapping` entries with `Depth < 1.5` are ignored — the engine doesn't
  generate them as continuous voxels.
- **Asteroid resolution is coarse.** The initial scan is at LOD4 (16 m); small or partial veins can be missed.
- **First planet access costs ~750 ms per cube face** while the `_mat.png` is decoded. See
  [planet-load-perf.md](planet-load-perf.md).
- **Server-side.** Searches run wherever the session component lives; there is no client-side calculation and no
  network protocol beyond the `/odr server-perf` diagnostic.

## Debug chat commands

Not part of the API, but the only interactive surface the mod has, and the replacement for what used to be a
separate in-game test harness. All are local-session diagnostics.

| Command | Effect |
|---|---|
| `/odr perf` | Report thread time, per-page-type load time, and estimated cache memory. `/odr perf reset` zeroes the window. |
| `/odr server-perf` | Same report from the server; relayed over secure message id `27491`. |
| `/odr miss` | Toggle GPS markers on search misses — points the engine probed and rejected. |
| `/odr lod <lod>[,<lod>...]` | Draw wireframe boxes around ore voxels at the given LODs within 64 m (e.g. `/odr lod 1,2,4`). No arguments clears it. |
| `/odr excavate` | Remove all stone at LOD0 within 64 m, exposing ore for visual verification. Destructive — test worlds only. |
| `/odr planet` | Report what the ore model derives at your position: cube face, texel, `_mat.png` values, matched `OreMapping`, surface slope, latitude, height. Warns if the cubemap conversions stop round-tripping. |
| `/odr vein [range]` | Draw the predicted vein segment for every ore pixel in a patch around you (default 64 m; `0` clears). |
| `/odr recall [range]` | Compare the asteroid coarse-to-fine strategy against a full fine-LOD scan and report recall % and timing (default 20 km). |
| `/odr boulders [range]` | GPS-mark boulders in range, split into searched and skipped-as-fake (default 30 km). |
| `/odr preload [range]` | Force every planet in range to decode all six faces, and report the cost per face. Causes a long hitch. |

### Verification recipes

These are the checks worth repeating after touching the corresponding code.

- **Planet vein math** (`DetectorPagePlanet.TrySubPixel`, `PlanetMatHelper.ShapeNormalZ`): stand on a planet, run
  `/odr excavate` then `/odr vein`. The drawn segments should thread through the exposed ore. Segments floating in
  cleared space mean the slope correction or surface lookup is wrong. `/odr planet` explains any single spot.
- **Asteroid coarse-to-fine** (`DetectorPageAsteroid`, `CoarseLod`/`FineLod`/thresholds): fly to a field and run
  `/odr recall`. Expect 97%+. A drop toward ~50% means the negative child offset was lost.
- **LOD alignment in general**: `/odr excavate` then `/odr lod 1,2,4` — the LOD1 boxes should sit on the exposed
  ore, and LOD2+ boxes on a planet will visibly sit lower, which is the shift documented in
  [engine-notes.md](engine-notes.md).
- **Planet load cost**: `/odr perf reset`, then `/odr preload`, then `/odr perf`.
