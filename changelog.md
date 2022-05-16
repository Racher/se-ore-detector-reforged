# Changelog

## [2.0.4] - 2022-05-16
### Fixed
- Skip invalid material on (Planet 26)

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