## [Unreleased]

Everything since 2.3.4, on the `26-04-14` branch. Not released and not planned for release — this would be a 3.0.0
and it breaks existing users in two ways. See [docs/decisions.md](docs/decisions.md).

### Changed

- **Breaking:** Programmable Block property `ReforgedDetectN` replaced by `DetectOre`. Payload is now
  `MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>` — a single nearest result plus an error channel,
  instead of `ValueTuple<..., int, Action<IList<Vector3D>>>` returning N results
- Planet search rebuilt around a per-ore, per-face sparse quadtree over the generator `_mat.png`, with 4×4
  sub-pixel resolution on large planets and sub-pixel exhaustion tracking
- Asteroid search rebuilt as a coarse LOD4 scan with LOD2 confirmation, switching between a sparse leaf list and a
  dense bit pyramid by occupancy
- Detector caches are now components on the voxel map itself, created lazily per voxel map
- Ore vocabulary is resolved on `UpdateBeforeSimulation` so other mods can finish modifying definitions first

### Added

- Mod API message handlers: detect ore (`7330961312834800629`) and list natural ores (`7330961312834800630`)
- Errors are reported to callers instead of being swallowed
- `/odr` diagnostic chat commands, replacing the old always-on in-game test components: `perf`, `server-perf`,
  `miss`, `lod`, `excavate`, `planet`, `vein`, `recall`, `boulders`, `preload`

### Removed

- **Breaking:** all player-facing functionality — GPS markers, OreDetector terminal controls, config file,
  multiplayer settings sync. Moved to the `se-ore-detector-reforged-gps` companion mod
- Biome-driven deposit support (ice lakes, oasis, polar caps); it was never accurate enough to be useful

## [2.3.4] - 2026-03-29

### Fixed

- Fix: In-game scripting "Property ReforgedDetectN is not of type ValueTuple"
- Fix: `break;` in asteroid scanning

## [2.3.3] - 2023-07-06

### Fixed

- Boulder fix: Scan for foreign ores (Star Trek Modpack)
- Boulder fix: Don't scan lod1/disappearing boulders

## [2.3.2] - 2023-02-02

### Fixed

- Block terminal setting save/load

## [2.3.1] - 2022-12-23

### Fixed

- Mod msg handler

## [2.3.0] - 2022-12-04

### Added

- Config file (%appdata%\SpaceEngineers\Storage\OreDetectorReforgedConfig.xml)
- Optional gps angle info

## [2.2.1] - 2022-08-27

### Fixed

- Multiplayer crash (IMyRadioAntenna.get_Radius())

## [2.2.0] - 2022-08-27

### Added

- Broadcast feature

### Changed

- The search center is now the camera position (not a random detector on the vessel)

## [2.1.1] - 2022-08-22

### Fixed

- Trivial SpawnsInAsteroids bug (now searches Lord Wiader's Tiered Systems titanium)

## [2.1.0] - 2022-05-19

### Added

- Config support

### Removed

- UpdatePeriod terminal slider (now scans 1 ore type every 2nd update)

## [2.0.4] - 2022-05-16

### Fixed

- Skip invalid material (Planet 26)

## [2.0.3] - 2022-05-15

### Fixed

- Ore index overflow with AwwScrap

## [2.0.2] - 2022-05-14

### Fixed

- Union settings when using multiple ore detectors
- Remove markers when none is active
- Increase whitelist size to 128bit
- Config refactor

## [2.0.1] - 2022-05-10

### Fixed

- Fix multiplayer disconnect ModStorage KeyNotFound

## [2.0.0] - 2022-05-08

### Added

- Use planet generation _mat.png
- Nearest neighbor search, infinite range
- OreDetector block terminal: range, period, whitelist, count, color
- ProgrammableBlock API: "ReforgedDetectN"

### Removed

- Persistent GPS signals (config, colors)
- Fancy voxel-walk (how the next close voxel was selected when the current was mined)
- Voxelhand support
- Meteorite support
- HandDrill support
- Client side PB calculation

## [1.0.1] - 2022-04-06

### Fixed

- Fixed a terminal property server/client issue

## [1.0.0] - 2022-04-05

### Added

- Initial version
