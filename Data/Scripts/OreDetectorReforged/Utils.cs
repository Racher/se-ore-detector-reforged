using VRageMath;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System;
using System.Linq;
using Sandbox.Definitions;
using System.Collections.Concurrent;

namespace OreDetectorReforged
{
    public class Config
    {
        public int searchFrequencyPlayer = 1000;
        public int searchFrequencyScript = 1000;
        public int searchBackgroundThreads = 1;
        public int seachVolumeLimit512MChunks = 300;
        public int searchSubChunkSpread0to5 = 2;
        public int voxelContentMin0to255 = 129;
        public int refreshGpsRangeMeters = 100;
        public bool debugNotifications = false;
        public bool debugGps = false;
    }
    static class MyTransforms
    {
        public static Vector3I GetCenter(Vector3I v, int lod) => v + (1 << lod >> 1);
        public static Vector3D ChunkCenterW(ChunkInfo chunk) => Lodn1ToWorld(chunk.vbi.vb, GetCenter(chunk.v, chunk.w) * 4 + 3);
        public static Vector3I WorldToLod(MyVoxelBase vb, Vector3D p, int lod) => Vector3D.Round((Vector3D.Transform(p, vb.GetViewMatrix()) + (vb.Size >> 1)) / (1 << lod));
        public static Vector3D Lodn1ToWorld(MyVoxelBase vb, Vector3I p) => Vector3D.Transform(new Vector3D(p - vb.Size) / 2, vb.WorldMatrix);
    }
    class SearchTask
    {
        public readonly List<V31Byte> result = new List<V31Byte>();
        public readonly ConcurrentQueue<SearchTask> finished;
        public ChunkInfo chunk;
        public TimeSpan elapsed;
        public SearchTask(ConcurrentQueue<SearchTask> finished)
        {
            this.finished = finished;
        }
    }
    struct CachePage
    {
        public readonly List<ulong> keys;
        public readonly List<V31Byte> searchs;
        public readonly List<V31Byte> ores;
        public CachePage(List<ulong> keys, List<V31Byte> searchs, List<V31Byte> ores)
        {
            this.keys = keys;
            this.searchs = searchs;
            this.ores = ores;
        }
        public void Clear()
        {
            keys.Clear();
            searchs.Clear();
            ores.Clear();
        }
    }
    class SearchCandidate
    {
        public ChunkInfo topChunk;
        public List<V31Byte> pageSearch;
        public V31Byte v;
        public void Return() => pageSearch.Add(v);
    }
    class VoxelBaseInfo
    {
        public readonly MyVoxelBase vb;
        public readonly Vector3I center1;
        public readonly long entityId;
        public readonly Vector3I size1;
        public readonly byte[] oreMap;
        public VoxelBaseInfo(MyVoxelBase vb, Vector3I center1)
        {
            this.vb = vb;
            this.center1 = center1;
            entityId = vb.EntityId;
            size1 = vb.Size >> 1;
            oreMap = SessionComponent.materialOreMapping.GetOreMap((vb as MyPlanet) != null);
        }
    }
    struct ChunkInfo
    {
        public readonly VoxelBaseInfo vbi;
        public readonly Vector3I v;
        public readonly int w;
        public ChunkInfo(VoxelBaseInfo vbi, Vector3I v, int w)
        {
            this.vbi = vbi;
            this.v = v;
            this.w = w;
        }
        public ChunkInfo(ChunkInfo topChunk, V31Byte v)
        {
            vbi = topChunk.vbi;
            this.v = topChunk.v + v.V;
            w = v.W;
        }
    }
    struct V31Byte
    {
        readonly Vector3UByte v;
        readonly byte w;
        public V31Byte(Vector3I v, int w)
        {
            this.v = new Vector3UByte(v);
            this.w = (byte)w;
        }
        public Vector3I V => v;
        public int W => w;
    }
    struct ListSorter<T>
    {
        public ListSorter(List<long> indexes)
        {
            this.indexes = indexes;
            indexes.Clear();
        }
        public void AddKey(long cost) => indexes.Add((cost << 16) + indexes.Count);
        public void Sort(List<T> elements)
        {
            indexes.Sort();
            for (var i = 0; i < indexes.Count; i++)
            {
                var j = (int)(indexes[i] & ushort.MaxValue);
                if (i == j)
                    continue;
                var r = i;
                var temp = elements[r];
                do
                {
                    elements[r] = elements[j];
                    r = j;
                    j = (int)(indexes[j] & ushort.MaxValue);
                    indexes[r] = r;
                }
                while (j != i);
                elements[r] = temp;
            }
        }
        readonly List<long> indexes;
    }
    class MaterialOreMapping
    {
        public byte[] GetOreMap(bool planet) => planet ? oreMappingNoIce : oreMapping;
        public readonly string[] oreNames;
        public readonly byte[] oreMapping;
        public readonly byte[] oreMappingNoIce;

        public MaterialOreMapping()
        {
            var oreNames = new List<string>();
            oreMapping = Enumerable.Repeat((byte)255, 256).ToArray();
            oreMappingNoIce = Enumerable.Repeat((byte)255, 256).ToArray();
            foreach (var material in MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Where(material => material.IsRare))
            {
                var idx = oreNames.FindIndex(s => s == material.MinedOre);
                if (idx == -1)
                {
                    idx = oreNames.Count;
                    oreNames.Add(material.MinedOre);
                }
                oreMapping[material.Index] = (byte)idx;
                if (material.MinedOre != "Ice")
                    oreMappingNoIce[material.Index] = (byte)idx;
            }
            this.oreNames = oreNames.ToArray();
        }
    }
}
