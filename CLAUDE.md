# CLAUDE.md

## What this is

A Space Engineers mod that provides long-range, low-cost ore detection to **other mods and in-game scripts**. It has
no player-facing UI of its own — no blocks, no terminal controls, no GPS markers. The player-facing half lives in a
separate repo, `se-ore-detector-reforged-gps`.

Development happens on `26-04-14`. `main` is the last released state (2.3.4) and is far behind. No release is
planned; see [docs/decisions.md](docs/decisions.md).

## Build & language constraints

- Target framework: .NET 4.6.2, platform x64
- C# language version: **6** — no tuple syntax, no `is` pattern matching, no `out var`, no local functions
- Building locally is optional (the game compiles from source on save load). When building, Space Engineers
  must be installed at `C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\` — `.csproj` HintPaths
  reference its `Bin64/` DLLs
- No automated tests; verify changes by loading the mod in Space Engineers and running the `/odr` diagnostic
  commands. Each lives in one self-contained file under `Detector/Commands/` and does nothing until invoked —
  add new debug functionality that way rather than as an always-on component. [docs/api.md](docs/api.md) lists
  which command verifies which piece of the engine

## Code style

- Be GC conscious: Generation 0 is cheap. Avoid repeated allocations for mid-to-long-lived objects to prevent
  Gen 1/2 promotion
- Keep the style simple — other mod authors may read this as a template for using the dependent API

## Documentation

Read the relevant one before changing code in that area:

| Document | Covers |
|---|---|
| [docs/api.md](docs/api.md) | The public contract: PB property, mod message IDs, payloads, error semantics, `/odr` commands. Changing any of it is a breaking change |
| [docs/architecture.md](docs/architecture.md) | `DetectorServer`, `SearchTask`, the three page components, supporting helpers |
| [docs/engine-notes.md](docs/engine-notes.md) | How Space Engineers itself generates and stores ore. The constraints the design is built on — LOD trustworthiness, sparsity, sandbox performance |
| [docs/planet-ore-model.md](docs/planet-ore-model.md) | Reverse-engineered vein-position math. **Read before touching `DetectorPagePlanet.TrySubPixel` or `PlanetMatHelper.ShapeNormalZ`** |
| [docs/planet-load-perf.md](docs/planet-load-perf.md) | Closed investigation into planet load cost |
| [docs/decisions.md](docs/decisions.md) | Why things are the way they are, what was abandoned and why, open loose ends, branch archive |

Two things that come up constantly and are easy to get wrong:

- **Only LOD0/LOD1 are accurate on planets.** LOD2+ is shifted toward the planet centre.
- **LOD child boxes are offset negatively**: subdivide as `[2*x-1, 2*x]`.
