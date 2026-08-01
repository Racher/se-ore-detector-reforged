# Decisions and project state

Why the project looks the way it does, and where it was left. Engine facts live in
[engine-notes.md](engine-notes.md); this file records choices.

## State as of 2026-08-01

Development happens on **`26-04-14`**, which is 424 commits ahead of `origin/main` and is the only living line of
work. Everything below it has been consolidated: nine local branches were deleted after confirming they were either
already ancestors of `26-04-14` or dead ends (table at the end).

`origin/main` still points at the last released state, **2.3.4**. The work on `26-04-14` is unreleased and
deliberately so — it is not in a publishable state and no update is planned. In particular the Steam Workshop item
(`2807946569`) must **not** be updated from this branch as-is: see "Open loose ends".

## The two-mod split

The mod used to be one thing: a detection engine plus the GPS markers, terminal block controls, config file and
multiplayer settings sync that made it usable. In April 2026 the player-facing half was removed
(`6b65c0e remove PlayerOreGps`, `0cdc206 remove storage id`) and moved to a separate repository,
`se-ore-detector-reforged-gps`.

Removed here: `PlayerOreGps.cs`, `TerminalOreDetector.cs`, `Config.cs`, `SyncModStorage.cs`, `OreDetectorData.cs`,
`Data/EntityComponents.sbc`, `thumb.jpg`.

The reasoning: the detection engine is the part other mod authors want to depend on, and it was carrying a UX design
— whitelists, marker colours, update periods, per-block terminal settings — that had nothing to do with finding ore
and that every consumer would have wanted differently. Splitting them made the engine a clean dependency and let the
GPS mod serve as a worked example of consuming the API.

The consequence is that **this mod alone now does nothing observable**. It has no blocks, no UI and no GPS output;
it only answers API calls. That is intended, but it is a hard break for existing Workshop subscribers.

## Breaking API change

The Programmable Block surface was replaced, not extended:

| | Before (2.3.4) | After |
|---|---|---|
| Property | `ReforgedDetectN` | `DetectOre` |
| Payload | `ValueTuple<BoundingSphereD, string, int, Action<IList<Vector3D>>>` | `MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>` |
| Results | N nearest, as a list | single nearest |
| Errors | none | `Exception` passed to the callback |

`ValueTuple` → `MyTuple` was forced: the `ValueTuple` shape broke in-game scripting with
"Property ReforgedDetectN is not of type ValueTuple" (fixed in 2.3.4 and then superseded here). Dropping the
"N results" parameter followed from the search design — the priority queue stops as soon as the nearest hit is
confirmed, and asking for N results throws away that early exit. Callers that want more can search again from a
different origin. A `ListNaturalOres` message was added so consumers can discover valid ore names instead of
hardcoding them. Full contract in [api.md](api.md).

## Abandoned: caching parsed planet data into save files

The last thing attempted. Planet first-access costs ~750 ms per cube face, almost all of it PNG decode plus
classification, and the obvious fix is to persist the parsed per-ore pixel arrays and pyramids so only the first
load in the world's lifetime pays.

Dropped for two reasons. There is no convenient mod lifecycle hook for a cross-session cache shared between worlds,
and cache invalidation needs a content hash of the PNG bytes for modded generators, since the `(folderName, face)`
key is only safe for vanilla. More decisively, the cost isn't worth chasing: normal worlds have one of each planet,
so this is a one-off first-load latency, not a recurring cost. Measurements and the alternatives already ruled out
are in [planet-load-perf.md](planet-load-perf.md).

An uncommitted refactor extracting `LoadPixels()` from `LoadFace()` — the seam that would have made the parsed data
serializable — was reverted as part of this cleanup, since its only motivation was this feature.

`PermaCache.cs`, an earlier attempt at the same idea, was removed from the working line and survives only on the
`tests` branch.

## Abandoned: biome and height support

Ice lakes, oasis and polar-cap deposits are placed by biome rules keyed on the `_mat.png` red channel rather than by
an `OreMapping` blue value. Several attempts to reproduce those rules — `redToMaterialGroup`, `redToIceSinLatMin`,
`PreparedMaterialGroup`, latitude thresholds, height pyramids — all produced markers that were confidently wrong,
which is worse than no result. `6e2130c rm PushTop, use defSurfaceMaterial` closed it out. Ice in asteroids and
normal ice veins still work; biome-driven surface ice does not.

## Rejected: PngCs, and the 2024 rewrite

`rework-24-04` (archived, 27 commits) was a clean-slate rewrite from the 2023 2.3.3 point. It replaced the vendored
BigGustave decoder with **PngCs** — a much larger library, ~50 files against 25 — and added an MSTest project.
BigGustave was kept: PngCs brought chunk-writing, metadata, interlacing and colour-model machinery that this mod
never needs, and gave no decode-speed advantage that survived the sandbox tax. The same branch compared
`GZipStream`/`MyCompressionStreamLoad` against `DeflateStream`; the VRage path is ~25% slower standalone, and the
difference is invisible in-game for the same reason.

The MSTest project is also why testing settled where it did: unit tests outside the game can't see voxel storage or
planet generators, which is where every interesting bug lived. Verification moved in-game instead — see below.

## Testing: chat commands, not a test project

There are no automated tests, and there is no test branch. Verification happens through the `/odr` chat commands in
`Detector/Commands/`, each a single self-contained `MySessionComponentBase` that does nothing until its command is
typed. [api.md](api.md) lists them and the recipes worth repeating.

This is the third arrangement and the first one that survives. Unit tests came first, in the `TestProject1` MSTest
project on the abandoned 2024 rewrite; they could not see voxel storage or planet generators, which is where every
interesting bug actually lived. Then came a `Detector/Test/` folder of `MySessionComponentBase` scaffolds that ran
on world load or on player movement — those worked, but they were always-on and had to be kept out of shipping
code, so they were moved to a `tests` branch and immediately rotted against the API they were testing.

Folding them into the command pattern fixes both problems: they live on the working line so they compile or break
loudly with the code, and they cost nothing until invoked. The ports are:

| Command | From |
|---|---|
| `/odr planet` | `PlanetMatHelperTest` + `PlanetCubemapHelperTest` (the round-trip check) |
| `/odr vein` | `PlanetOreGenerationTest`, minus its voxel stripping — that is `/odr excavate` |
| `/odr recall` | `AsteroidLodRecallTest` |
| `/odr boulders` | `BoulderTest` |
| `/odr preload` | `DetectorPageLoadTest`, rebuilt on the public search API instead of a test-only `FindNearest` hook |

Three were dropped as already superseded rather than ported. `LodSearchAlignmentTest` is exactly what
`/odr lod` plus `/odr excavate` already do together. `AsteroidSearch42Test` was 379 lines prototyping the
coarse-to-fine search that `DetectorPageAsteroid` has since become — the production code is the artefact now.
`PngLoadTest` timed a PNG decode that `/odr perf` reports directly as `PlanetLoadTicks`.

`PermaCache.cs`, from the dropped save-file cache, was not carried over.

## Open loose ends

Not blocking anything, but they are the known-wrong things in the tree:

- **`thumb.jpg` was deleted** with the GPS split and never replaced, while `deploy.cmd` still copies it (and still
  copies `Data/*.sbc`, which no longer exists). Publishing from this branch would need a new thumbnail. Left as-is
  rather than guessed at.
- **`metadata.mod` still says `<ModVersion>1.0</ModVersion>`** and never tracked the real version.
- **`description.steamtxt` links the GPS mod as `id=TBD`** because that mod has no Workshop item yet.
- **Updating Workshop item `2807946569` from this branch** would silently turn a working player-facing mod into a
  no-op for every existing subscriber, since all GPS and terminal functionality now lives in the unpublished
  companion mod.

## Branch archive

Eleven branches were reduced to two on 2026-08-01, once the tests worth repeating had been folded into `/odr`
commands. No history was rewritten and nothing was pushed, so the remote still carries the old branches until
someone decides to prune it.

**Local branches now:**

| Branch | Head | Why |
|---|---|---|
| `26-04-14` | `cfe5f93` | The working line. |
| `main` | `c133259` | Last released state (2.3.4). Left where it is pending a decision on renaming it `v2`. |

**Deleted, content still reachable from `26-04-14`:**

| Branch | Head | What it was |
|---|---|---|
| `2026-planet-pixel-cache` | `96dfd4e` | Planet pixel/pyramid caching; landed. Still on `origin`. |
| `2026-asteroid-voxel-cache` | `bc3d70e` | Asteroid LOD4/LOD2 cache; landed. Still on `origin`. |
| `26-03-29-height-and-biome` | `6e2130c` | Biome/height attempt, ended by removing it. Still on `origin`. |
| `2026-04-16-ring-iterator` | `4a7481f` | Grid ring iterator; landed. |
| `t` | `eb45e62` | Scratch branch. |
| `tests` | `33b9f6f` | Test harness snapshot. Its base `bc3d70e` is an ancestor of `26-04-14`, so every test file is still readable with `git show bc3d70e:Data/Scripts/OreDetectorReforged/Detector/Test/<name>.cs`. Only the two tip commits are local-dead, and `origin/tests` still has them. |

**Deleted and gone.** These were local-only dead ends with no remote copy; their commits are now unreachable and
will be garbage collected. What was learned from them is written up in the sections above — that write-up is the
artefact now, not the code.

| Branch | Head | Branched from | What it was |
|---|---|---|---|
| `rework-24-04` | `9924b70` | `9b40487` (2023-07-06) | 2024 clean-slate rewrite: PngCs instead of BigGustave, an MSTest project, gzip/deflate experiments. Conclusions in "Rejected: PngCs, and the 2024 rewrite". |
| `2026-04-18-perf-log` | `5031fe7` | `befb24f` | Instrumentation behind [planet-load-perf.md](planet-load-perf.md), plus `DetectorPageLoadTest.cs`. Measurements kept in that document; the load test lives on as `/odr preload`. |
| `2026-04-09-fail` | `37acb82` | `4019202` | `AsteroidRunOnce.cs` experiment. Abandoned, nothing salvaged. |
| `claude/infallible-chandrasekhar` | `1cfdc6f` | `cdb0707` | Index-bounds work against the 64-grid plan; superseded by the shipped 32×32 grid. |

**Remote branches still present** (pruning them needs a push, which is a separate decision):
`origin/main`, `origin/26-04-14`, `origin/tests`, `origin/2026-asteroid-voxel-cache`,
`origin/2026-planet-pixel-cache`, `origin/26-03-29-height-and-biome`, `origin/v1` (pre-2.0 archive, worth keeping).
