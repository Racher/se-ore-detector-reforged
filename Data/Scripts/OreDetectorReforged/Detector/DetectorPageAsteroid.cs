using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.Components;
using VRage.Voxels;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    class DetectorPageAsteroid : MyComponentBase, IDetectorPage
    {
        // At or below this many LOD4 leaves, an ore is stored as a flat list of leaf
        // pyramid-flat-indices (sparse). Above, we allocate a full BitArray pyramid (dense).
        // The pyramid wins on traversal cost for ice-sized clouds; the list wins on memory
        // for the common case of a few veins per ore.
        const int SparseThreshold = 256;

        const int FineLod = 2;
        const int CoarseLod = 4;
        static readonly MyStorageData StorageData = new MyStorageData();
        readonly Action _clearCache;
        readonly Vector3I _storageSize;
        readonly int _storageSizeMax;
        readonly MyVoxelBase _vb;
        readonly int _wMax;

        // Per-ore storage. Mutually exclusive: at most one of densePyramids[o] / sparseLeaves[o]
        // is non-null. densePyramids == null is the "not loaded yet" sentinel.
        BitArray[] _densePyramids;
        int[] _sparseCounts;
        int[][] _sparseLeaves;
        MatrixD _viewMatrix;

        DetectorPageAsteroid(MyVoxelBase vb)
        {
            _vb = vb;
            _storageSize = vb.Storage.Size;
            _storageSizeMax = _storageSize.AbsMax();
            _wMax = 0;
            while (1 << CoarseLod << _wMax < _storageSizeMax)
                _wMax++;
            _viewMatrix = vb.GetViewMatrix();
            _clearCache = () =>
            {
                _densePyramids = null;
                _sparseLeaves = null;
                _sparseCounts = null;
            };
            StorageData.Resize(Vector3I.One << (CoarseLod - FineLod));
        }

        public void PushRoot(float radius, PriorityQueue<Node, Node.Comparer> pq, int currOre, int page, Vector3 centerLocal)
        {
            // Always push a single root sentinel (i=0, w=0). Process expands lazily — for sparse
            // ores, that means leaf-list expansion only when the asteroid is actually reachable
            // under the current task.distance, not the looser initial radius.
            if (_densePyramids == null)
            {
                if (!MaterialMappingHelper.AsteroidWhitelist[currOre]) return;
            }
            else
            {
                var bits = _densePyramids[currOre];
                if (bits != null)
                {
                    if (!bits[0]) return;
                }
                else if (_sparseLeaves[currOre] == null)
                {
                    return;
                }
            }

            var d = GetDistance(0, centerLocal);
            if (d <= radius)
                pq.Push(new Node(d, page, 0, 0));
        }

        public void Process(ref Node cell, SearchTask task, PriorityQueue<Node, Node.Comparer> pq, Vector3 centerLocal)
        {
            if (_densePyramids == null)
            {
                _densePyramids = new BitArray[256];
                Load();
            }

            var ore = task.CurrOre;
            var nodeIndex = (int)cell.I;
            int x, y, z, w;
            DecodePyramidIndex(nodeIndex, out x, out y, out z, out w);

            var bits = _densePyramids[ore];
            if (bits != null)
            {
                if (w == _wMax)
                {
                    var q = FindNearestLod2Voxel(x, y, z, task, centerLocal);
                    if (q.Item1 >= float.MaxValue)
                        bits[nodeIndex] = false;
                    else if (q.Item1 < task.Distance)
                        AcceptHit(q.Item2, q.Item1, task);
                    return;
                }

                var min = new Vector3I(x, y, z) * 2;
                var max = min + 1;
                var anyChildSet = false;
                for (var vit = new Vector3I_RangeIterator(ref min, ref max); vit.IsValid(); vit.MoveNext())
                {
                    var c = vit.Current;
                    var childIndex = PyramidFlatIndex(c.X, c.Y, c.Z, w + 1);
                    if (!bits[childIndex]) continue;
                    anyChildSet = true;
                    var d = GetDistance(childIndex, centerLocal);
                    if (d < task.Distance)
                        pq.Push(new Node(d, cell.P, childIndex, 0));
                }

                if (!anyChildSet)
                    bits[nodeIndex] = false;
                return;
            }

            var leaves = _sparseLeaves[ore];
            if (leaves == null) return;

            if (w != _wMax)
            {
                // Speculative root from PushRoot-before-Load. Expand into actual leaves now.
                var count = _sparseCounts[ore];
                for (var i = 0; i < count; ++i)
                {
                    var leafIdx = leaves[i];
                    var d = GetDistance(leafIdx, centerLocal);
                    if (d < task.Distance)
                        pq.Push(new Node(d, cell.P, leafIdx, 0));
                }

                return;
            }

            var sq = FindNearestLod2Voxel(x, y, z, task, centerLocal);
            if (sq.Item1 >= float.MaxValue)
            {
                // Prune: linear search + swap-with-last; count is bounded by sparseThreshold.
                var sparseCount = _sparseCounts[ore];
                for (var i = 0; i < sparseCount; ++i)
                {
                    if (leaves[i] != nodeIndex) continue;
                    leaves[i] = leaves[--sparseCount];
                    _sparseCounts[ore] = sparseCount;
                    return;
                }
            }
            else if (sq.Item1 < task.Distance)
            {
                AcceptHit(sq.Item2, sq.Item1, task);
            }
        }

        public Vector3 WorldToLocal(Vector3D v)
        {
            return Vector3D.Transform(v, ref _viewMatrix);
        }

        internal static IDetectorPage GetOrCreate(MyVoxelBase vb)
        {
            var o = vb.Components.Get<DetectorPageAsteroid>();
            if (o == null)
            {
                vb.Components.Add(o = new DetectorPageAsteroid(vb));
                vb.RangeChanged += o.VbOnRangeChanged;
            }

            return o;
        }

        internal long EstimateMemoryBytes()
        {
            long total = 0;
            if (_densePyramids != null)
                foreach (var t in _densePyramids)
                    if (t != null)
                        total += (t.Length + 7) / 8;
            if (_sparseLeaves != null)
                foreach (var t in _sparseLeaves)
                    if (t != null)
                        total += t.Length * 4;
            return total;
        }

        void VbOnRangeChanged(MyVoxelBase storage, Vector3I minVoxelChanged, Vector3I maxVoxelChanged, MyStorageDataTypeFlags changedData)
        {
            try
            {
                if (maxVoxelChanged - minVoxelChanged + 1 == _storageSize)
                    DetectorServer.Tasks.Add(_clearCache);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        void Load()
        {
            var loadSw = Stopwatch.StartNew();
            _sparseLeaves = new int[256][];
            _sparseCounts = new int[256];
            var matOre = MaterialMappingHelper.MatIdxToOreIdx;

            var data = new MyStorageData();
            using (_vb.Pin())
            {
                if (_vb.Closed)
                    return;
                data.Resize(_storageSize >> CoarseLod);
                _vb.Storage.ReadRange(data, MyStorageDataTypeFlags.ContentAndMaterial, CoarseLod, Vector3I.Zero, data.Size3D - 1);
            }

            var totalNodes = PyramidFlatIndex(0, 0, 0, _wMax + 1);
            var leafLevelStart = ((1 << (3 * _wMax)) - 1) / 7;
            var leafMask = (1 << _wMax) - 1;

            // Per ore: either accumulating into a sparse leaf set, or promoted to a full BitArray
            // pyramid. Once a set exceeds sparseThreshold, it is consumed to seed the pyramid and
            // subsequent leaves walk up the pyramid directly.
            var sparseSets = new HashSet<int>[256];

            for (var i = 0; i < data.SizeLinear; ++i)
            {
                if (data.Content(i) == 0)
                    continue;
                var o = matOre[data.Material(i)];
                if (o == 255)
                    continue;

                Vector3I p;
                data.ComputePosition(i, out p);
                var min = Vector3I.Max(Vector3I.Zero, p - 1);
                for (var vit = new Vector3I_RangeIterator(ref min, ref p); vit.IsValid(); vit.MoveNext())
                {
                    var np = vit.Current;
                    var bits = _densePyramids[o];
                    if (bits != null)
                    {
                        WalkUp(bits, np.X, np.Y, np.Z);
                        continue;
                    }

                    var set = sparseSets[o] ?? (sparseSets[o] = new HashSet<int>());
                    if (!set.Add(PyramidFlatIndex(np.X, np.Y, np.Z, _wMax)))
                        continue;
                    if (set.Count <= SparseThreshold)
                        continue;

                    bits = new BitArray(totalNodes);
                    foreach (var leafIdx in set)
                    {
                        var lo = leafIdx - leafLevelStart;
                        WalkUp(bits, lo & leafMask, (lo >> _wMax) & leafMask, (lo >> (2 * _wMax)) & leafMask);
                    }

                    _densePyramids[o] = bits;
                    sparseSets[o] = null;
                }
            }

            for (var o = 0; o < 256; ++o)
            {
                if (sparseSets[o] == null)
                    continue;
                var arr = new int[sparseSets[o].Count];
                sparseSets[o].CopyTo(arr);
                _sparseLeaves[o] = arr;
                _sparseCounts[o] = arr.Length;
            }

            DetectorServer.AsteroidLoadTicks += loadSw.ElapsedTicks;
        }

        void WalkUp(BitArray bits, int x, int y, int z)
        {
            for (var level = _wMax; level >= 0; --level, x >>= 1, y >>= 1, z >>= 1)
            {
                var idx = PyramidFlatIndex(x, y, z, level);
                if (bits[idx]) break;
                bits[idx] = true;
            }
        }

        BoundingBox GetBounds(int nodeIndex)
        {
            int x, y, z, level;
            DecodePyramidIndex(nodeIndex, out x, out y, out z, out level);
            const int fineCellSize = 1 << FineLod;
            var cellSize = _storageSizeMax >> level;
            var localCenter = new Vector3I(x, y, z) * cellSize - _storageSize / 2f + (cellSize - fineCellSize) / 2f;
            return new BoundingBox(localCenter - cellSize * 0.5f, localCenter + cellSize * 0.5f);
        }

        float GetDistance(int nodeIndex, Vector3 centerLocal)
        {
            return GetBounds(nodeIndex).Distance(centerLocal);
        }

        MyTuple<float, int> FindNearestLod2Voxel(int x, int y, int z, SearchTask task, Vector3 centerLocal)
        {
            var blockOrigin = new Vector3I(x, y, z) << (CoarseLod - FineLod);
            using (_vb.Pin())
            {
                if (!_vb.Closed)
                    _vb.Storage.ReadRange(StorageData, MyStorageDataTypeFlags.ContentAndMaterial, FineLod, blockOrigin, blockOrigin + StorageData.Size3D - 1);
            }

            var matOre = MaterialMappingHelper.MatIdxToOreIdx;
            var best = new MyTuple<float, int>(float.MaxValue, -1);
            for (var i = 0; i < StorageData.SizeLinear; ++i)
            {
                if (StorageData.Content(i) < 96 || matOre[StorageData.Material(i)] != task.CurrOre) continue;
                Vector3I localPos;
                StorageData.ComputePosition(i, out localPos);
                localPos += blockOrigin;
                var j = PyramidFlatIndex(localPos.X, localPos.Y, localPos.Z, _wMax + CoarseLod - FineLod);
                var d = GetDistance(j, centerLocal) * task.QuasiNearest;
                if (!(d < best.Item1)) continue;
                best.Item1 = d;
                best.Item2 = j;
            }

            return best;
        }

        void AcceptHit(int leafIndex, float distance, SearchTask task)
        {
            var bb = GetBounds(leafIndex);
            task.LocalPos = bb.Center;
            task.Distance = distance;
            task.Vb = _vb;
        }

        static int PyramidFlatIndex(int x, int y, int z, int w)
        {
            return ((1 << (3 * w)) - 1) / 7 + (z << w << w) + (y << w) + x;
        }

        static void DecodePyramidIndex(int i, out int x, out int y, out int z, out int w)
        {
            for (w = 0; i >= 1 << (3 * w); ++w)
                i -= 1 << (3 * w);
            var mask = (1 << w) - 1;
            z = (i >> (2 * w)) & mask;
            y = (i >> (1 * w)) & mask;
            x = (i >> (0 * w)) & mask;
        }
    }
}