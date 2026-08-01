using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Voxels;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    class DetectorPageBoulder : MyComponentBase, IDetectorPage
    {
        readonly Action _clearCache;
        readonly float _hMax;
        readonly float _hMin;
        readonly Vector3I _storageSize;
        readonly Vector3 _up;
        readonly MyVoxelBase _vb;
        Dictionary<int, List<Vector3B>> _ores;
        MatrixD _viewMatrix;

        DetectorPageBoulder(MyVoxelBase vb)
        {
            _vb = vb;
            _storageSize = vb.Storage.Size;
            _viewMatrix = vb.GetViewMatrix();
            _clearCache = () => _ores = null;
            var planet = MyGamePruningStructure.GetClosestPlanet(vb.PositionLeftBottomCorner);
            if (planet == null) return;
            var upPlanet = Vector3D.Transform(vb.PositionLeftBottomCorner, planet.GetViewMatrix()).Normalized();
            var p0 = Vector3D.Transform(Vector3D.Transform(upPlanet * planet.MinimumRadius, planet.WorldMatrix), vb.GetViewMatrix());
            var p1 = Vector3D.Transform(Vector3D.Transform(upPlanet * planet.MaximumRadius, planet.WorldMatrix), vb.GetViewMatrix());
            _up = (p1 - p0).Normalized();
            var d0 = Vector3.Dot(p0, _up);
            var d1 = Vector3.Dot(p1, _up);
            _hMin = Math.Min(d0, d1);
            _hMax = Math.Max(d0, d1);
        }

        public void PushRoot(float radius, PriorityQueue<Node, Node.Comparer> pq, int currOre, int page, Vector3 centerLocal)
        {
            if (_ores?.ContainsKey(currOre) == false) return;
            var s2 = new Vector3(_storageSize / 2);
            var offset = centerLocal - Vector3.Clamp(centerLocal, -s2, s2);
            var dVert = Vector3.Dot(_up, offset);
            var dVertClamp = Math.Min(Math.Max(_hMin, dVert), _hMax);
            var dHoriz = (offset - _up * dVert).Length();
            var d = dHoriz + Math.Abs(dVert - dVertClamp);
            if (d <= radius)
                pq.Push(new Node(d, page, 0, 0));
        }

        public void Process(ref Node cell, SearchTask task, PriorityQueue<Node, Node.Comparer> pq, Vector3 centerLocal)
        {
            Load();
            List<Vector3B> oreList;
            if (!_ores.TryGetValue(task.CurrOre, out oreList)) return;
            var dMinSq = float.MaxValue;
            Vector3 pMin = Vector3D.Zero;
            foreach (var p in oreList)
            {
                var pf = new Vector3(p);
                var dsq = Vector3.DistanceSquared(centerLocal, pf);
                if (dsq < dMinSq)
                {
                    dMinSq = dsq;
                    pMin = pf;
                }
            }

            task.Distance = cell.D * task.QuasiNearest;
            task.Vb = _vb;
            task.LocalPos = pMin;
        }

        public Vector3 WorldToLocal(Vector3D v)
        {
            return Vector3D.Transform(v, ref _viewMatrix);
        }

        internal static IDetectorPage GetOrCreate(MyVoxelBase vb)
        {
            var o = vb.Components.Get<DetectorPageBoulder>();
            if (o == null)
            {
                vb.Components.Add(o = new DetectorPageBoulder(vb));
                vb.RangeChanged += o.VbOnRangeChanged;
            }

            return o;
        }

        internal long EstimateMemoryBytes()
        {
            if (_ores == null) return 0;
            long total = 0;
            foreach (var kvp in _ores)
                total += kvp.Value.Capacity * 3;
            return total;
        }

        void VbOnRangeChanged(MyVoxelBase storage, Vector3I minVoxelChanged, Vector3I maxVoxelChanged, MyStorageDataTypeFlags changedData)
        {
            try
            {
                DetectorServer.Tasks.Add(_clearCache);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        void Load()
        {
            if (_ores != null) return;
            var loadSw = Stopwatch.StartNew();
            _ores = new Dictionary<int, List<Vector3B>>();
            var matOre = MaterialMappingHelper.MatIdxToOreIdx;
            var data = new MyStorageData();
            data.Resize(_storageSize >> 1);
            using (_vb.Pin())
            {
                if (_vb.Closed) return;
                _vb.Storage.ReadRange(data, MyStorageDataTypeFlags.ContentAndMaterial, 1, Vector3I.Zero, data.Size3D - 1);
            }

            for (var i = 0; i < data.SizeLinear; ++i)
            {
                if (data.Content(i) < 96) continue;
                var o = matOre[data.Material(i)];
                if (o == 255) continue;
                List<Vector3B> oreList;
                if (!_ores.TryGetValue(o, out oreList))
                    _ores.Add(o, oreList = new List<Vector3B>());
                Vector3I p;
                data.ComputePosition(i, out p);
                oreList.Add(new Vector3B(2 * p - _storageSize / 2));
            }

            DetectorServer.BoulderLoadTicks += loadSw.ElapsedTicks;
        }
    }
}