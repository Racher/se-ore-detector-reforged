A Space Engineers mod that facilitates long-range, low-cost ore detection for other mods and in-game scripts.

This mod is the **detection engine only** — it has no blocks, no terminal controls and no GPS markers, and on its own
produces nothing visible in game. It answers API calls. The player-facing half is a separate, UX-focused
[companion mod (github)](https://github.com/Racher/se-ore-detector-reforged-gps), which also serves as a worked
example of consuming the API.

## Documentation

- [docs/api.md](docs/api.md) — how to call it from a Programmable Block or another mod
- [docs/architecture.md](docs/architecture.md) — how the search engine works
- [docs/engine-notes.md](docs/engine-notes.md) — how Space Engineers generates and stores ore
- [docs/planet-ore-model.md](docs/planet-ore-model.md) — planet vein-position math
- [docs/decisions.md](docs/decisions.md) — project state, abandoned directions, loose ends

## Status

Active development is on the `26-04-14` branch. `main` is the last released version (2.3.4) and is 424 commits
behind. The current branch contains a breaking API change and drops all player-facing functionality, and is **not**
in a releasable state — no Workshop update is planned. See [docs/decisions.md](docs/decisions.md).

## Setup

- Check out the repo into `%AppData%\SpaceEngineers\Mods\`
- In-game, go to World Settings → Mods → (Home Icon)`se-ore-detector-reforged`
- Use `deploy.cmd` to copy the necessary files to a clean folder and publish that from the In-game UI.

## Build prerequisites

- Building locally is optional (the game builds from source on save load)
- Space Engineers installed at `C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\` — the `.csproj` HintPaths
  reference DLLs in its `Bin64/`
- MSBuild (ships with Visual Studio's ".NET desktop development" workload)
- A .NET Framework targeting pack of 4.6.2 or newer (4.7.2 / 4.8 work — MSBuild rolls forward to satisfy the 4.6.2
  target). Not to be confused with .NET 5/6/7/8, which are a separate product line and not used here.
