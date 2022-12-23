## [2.3.1] - 2022-12-23
[h3] Fixed [/h3]
- Mod msg handler

## [2.3.0] - 2022-12-04
[h3] Added [/h3]
- Config file (%appdata%\SpaceEngineers\Storage\OreDetectorReforgedConfig.xml)
- Optional gps angle info

## [2.2.1] - 2022-08-27
[h3] Fixed [/h3]
- Multiplayer crash (IMyRadioAntenna.get_Radius())

## [2.2.0] - 2022-08-27
[h3] Added [/h3]
- Broadcast feature
[h3] Changed [/h3]
- The search center is now the camera position (not a random detector on the vessel)

## [2.1.1] - 2022-08-22
[h3] Fixed [/h3]
- Trivial SpawnsInAsteroids bug (now searches Lord Wiader's Tiered Systems titanium)

## [2.1.0] - 2022-05-19
[h3] Added [/h3]
- Config support
[h3] Removed [/h3]
- UpdatePeriod terminal slider (now scans 1 ore type every 2nd update)

## [2.0.4] - 2022-05-16
[h3] Fixed [/h3]
- Skip invalid material (Planet 26)

## [2.0.3] - 2022-05-15
[h3] Fixed [/h3]
- Ore index overflow with AwwScrap

## [2.0.2] - 2022-05-14
[h3] Fixed [/h3]
- Union settings when using multiple ore detectors
- Remove markers when none is active
- Increase whitelist size to 128bit
- Config refactor

## [2.0.1] - 2022-05-10
[h3] Fixed [/h3]
- Fix multiplayer disconnect ModStorage KeyNotFound

## [2.0.0] - 2022-05-08
[h3] Added [/h3]
- Use planet generation _mat.png
- Nearest neighbor search, infinite range
- OreDetector block terminal: range, period, whitelist, count, color
- ProgrammableBlock API: "ReforgedDetectN"
[h3] Removed [/h3]
- Persistent GPS signals (config, colors)
- Fancy voxel-walk (how the next close voxel was selected when the current was mined)
- Voxelhand support
- Meteorite support
- HandDrill support
- Client side PB calculation

## [1.0.1] - 2022-04-06
[h3] Fixed [/h3]
- Fixed a terminal property server/client issue

## [1.0.0] - 2022-04-05
[h3] Added [/h3]
- Initial version