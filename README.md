# Overview

Substitute the vanilla ore detection.

Default performance: 1 client side background thread => ~1600m range @100m/s on planet surface.

**Player: gps signals** when EquippedTool is HandDrill or ControlledVessel has working OreDetector.

IngameScripting:
```
public void Main(string argument, UpdateType updateSource)
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    var detectors = new List<IMyOreDetector>();
    GridTerminalSystem.GetBlocksOfType(detectors);
    var ores = detectors.First().GetValue<Dictionary<string, Vector3D>>("Ores");
    Echo(string.Join(Environment.NewLine, ores));
}
```

# Config tweaking / How does it work?

## Basics

**Search tasks**:
A MyVoxelBase.Storage.ReadRange is called on background threads (4096voxel ~1ms) looking for rare ore materials (except ice on planets).
Voxel write operations will be briefly blocked.

**Task collecting**:
Active detectors produce search tasks and read/write cache/output every 10 or 100 updates (player or block) on worker threads.

**Voxel Level Of Detail**:
The base game seems to reads voxels on lod2 which contains many false positives. This mod uses lod3 to lod1.  
Expected VoxelSize=1m (vanilla default) (compatibility risk).

**Cache**:
Each detector has it's own cache (~10MB). Cleared when config changes.

**IngameScripting**:
Activated by GetValue call. The result will be empty initially.  
The calculation is done by the owner of the detector block and the result is sent to the server.  
The mod uses a hard coded hopefully unique MyAPIGateway.Multiplayer message handler id 43818 (compatibility risk).

## Config

**Where**  
Edit gps signal OreDetectorReforgedConfig description.  
Change ore gps signal color (or hide #000000).

**searchFrequency**  
max number of search tasks queued per second per detector.

**searchBackgroundThreads**  
The shared search task queue is consumed by this many background threads.

**seachVolumeLimit512MChunks** (~range limit)  
Big chunks are 512meter boxes per voxelmap. Only a limited number of them are considered for scanning.

**searchSubChunkSpread0to5**  
When a lod3/lod2 chunk contains any ore: not only the inside but neighboring chunk children will be scanned as well on lod2/lod1.  
Since higher lod scans can be wrong this reduces the number of missed chunks. (spread 0 => 8 children)(spread 5 => 160 children)

**voxelContentMin0to255**  
Voxels may have low content resulting in mostly air indicator. Use this to filter those out.

**refreshGpsRangeMeters**  
Lod1 chunks that contain an ore result within this range are refreshed.
