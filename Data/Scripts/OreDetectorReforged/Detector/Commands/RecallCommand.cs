using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Voxels;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    // Measures how much ore the asteroid page's coarse-to-fine strategy actually finds, against a full
    // fine-LOD scan as ground truth. This is where the "a full LOD4 scan expanded into 8x8x8 LOD2 children
    // finds 97%+ of LOD2 nodes" figure in docs/engine-notes.md comes from.
    //
    // Re-run this after changing CoarseLod, FineLod, the content thresholds, or the child-offset arithmetic
    // in DetectorPageAsteroid. A recall drop toward ~50% means the negative child offset was lost: LOD child
    // boxes are not offset at higher levels, so the 2x2x2 expansion must cover [c-1, c], not [c, c+1].
    //
    // Ice is excluded — a single 1024^3 ice asteroid at ~20% fill would swamp the average.
    //
    // Trigger: "/odr recall [range]" (default 20000 m). Scans each asteroid once per session.
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class RecallCommand : MySessionComponentBase
    {
        const string Command = "/odr recall";
        const string Tag = "OreDetector Recall";
        const int DefaultRange = 20000;

        const int CoarseLod = 4;
        const int FineLod = 2;
        const byte CoarseContentMin = 1;
        const byte FineContentMin = 96;

        static readonly ConcurrentQueue<Result> Results = new ConcurrentQueue<Result>();
        static int _pending;
        static long _totalCoarseToFineTicks;
        static long _totalFullTicks;
        static int _totalFound;
        static int _totalTruth;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
        }

        // Only drains results; idle cost is one comparison per tick.
        public override void UpdateAfterSimulation()
        {
            Result result;
            while (Results.TryDequeue(out result))
            {
                _totalFullTicks += result.FullTicks;
                _totalCoarseToFineTicks += result.CoarseToFineTicks;
                _totalTruth += result.Truth;
                _totalFound += result.Found;
                _pending--;

                if (result.Truth > 0)
                {
                    var gps = MyAPIGateway.Session.GPS.Create(
                        $"recall {result.Found * 100.0 / result.Truth:F1}% ({result.Found}/{result.Truth})",
                        "", result.Position, true);
                    gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(120);
                    MyAPIGateway.Session.GPS.AddLocalGps(gps);
                }

                if (_pending != 0) continue;

                var msPerTick = 1000.0 / Stopwatch.Frequency;
                var recall = _totalTruth == 0 ? 100 : _totalFound * 100.0 / _totalTruth;
                MyAPIGateway.Utilities.ShowMessage(Tag,
                    $"done: recall {recall:F2}% ({_totalFound}/{_totalTruth} lod{FineLod} nodes)\n" +
                    $"  full lod{FineLod} scan: {_totalFullTicks * msPerTick:N0} ms\n" +
                    $"  lod{CoarseLod}->lod{FineLod}:    {_totalCoarseToFineTicks * msPerTick:N0} ms");
                _totalFullTicks = 0;
                _totalCoarseToFineTicks = 0;
                _totalTruth = 0;
                _totalFound = 0;
            }
        }

        static void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith(Command)) return;
            sendToOthers = false;

            try
            {
                var rest = messageText.Substring(Command.Length).Trim();
                var range = DefaultRange;
                if (rest.Length > 0 && !int.TryParse(rest, out range))
                {
                    MyAPIGateway.Utilities.ShowMessage(Tag, "Usage: /odr recall [range]");
                    return;
                }

                Start(range);
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, e.ToString());
            }
        }

        static void Start(int range)
        {
            if (MyAPIGateway.Session.Camera == null) return;
            if (_pending != 0)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, $"still running ({_pending} asteroid(s) left)");
                return;
            }

            var hasOre = new bool[256];
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                hasOre[mat.Index] = !string.IsNullOrEmpty(mat.MinedOre) && !mat.MinedOre.Equals("Stone") &&
                                    !mat.MinedOre.Equals("Ice");

            var candidates = new List<MyVoxelBase>();
            var sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, candidates);

            var started = 0;
            foreach (var voxel in candidates)
            {
                if (voxel is MyPlanet || voxel.BoulderInfo.HasValue || voxel.RootVoxel != voxel) continue;
                if (voxel.Storage.Size.AbsMax() <= 16) continue;
                started++;
                _pending++;
                var captured = voxel;
                var position = voxel.PositionComp.WorldAABB.Center;
                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    var result = Compare(captured, hasOre);
                    result.Position = position;
                    Results.Enqueue(result);
                });
            }

            MyAPIGateway.Utilities.ShowMessage(Tag,
                started == 0 ? "no asteroids in range" : $"scanning {started} asteroid(s)...");
        }

        static Result Compare(MyVoxelBase voxel, bool[] hasOre)
        {
            var result = default(Result);
            var size = voxel.Storage.Size;

            // Ground truth: one full scan at the fine LOD.
            var fineMax = (size - 1) >> FineLod;
            var fineData = new MyStorageData();
            fineData.Resize(fineMax + 1);
            var swFull = Stopwatch.StartNew();
            using (voxel.Pin())
            {
                if (voxel.Closed) return result;
                voxel.Storage.ReadRange(fineData, MyStorageDataTypeFlags.ContentAndMaterial, FineLod, Vector3I.Zero,
                    fineMax);
            }

            for (var i = 0; i < fineData.SizeLinear; i++)
                if (fineData.Content(i) >= FineContentMin && hasOre[fineData.Material(i)])
                    result.Truth++;
            swFull.Stop();
            result.FullTicks = swFull.ElapsedTicks;

            // Strategy under test: full coarse scan, then fine reads only around coarse hits.
            var coarseData = new MyStorageData();
            coarseData.Resize(size >> CoarseLod);
            var blockData = new MyStorageData();
            blockData.Resize(Vector3I.One << (CoarseLod - FineLod));
            var swCoarse = Stopwatch.StartNew();
            using (voxel.Pin())
            {
                if (voxel.Closed) return result;
                voxel.Storage.ReadRange(coarseData, MyStorageDataTypeFlags.ContentAndMaterial, CoarseLod,
                    Vector3I.Zero, coarseData.Size3D - 1);

                var visited = new HashSet<Vector3I>();
                for (var i = 0; i < coarseData.SizeLinear; i++)
                {
                    if (coarseData.Content(i) < CoarseContentMin || !hasOre[coarseData.Material(i)]) continue;
                    Vector3I c;
                    coarseData.ComputePosition(i, out c);

                    // Negative child offset: expand over [c-1, c], not [c, c+1].
                    var min = c - 1;
                    for (var it = new Vector3I_RangeIterator(ref min, ref c); it.IsValid(); it.MoveNext())
                    {
                        if (!visited.Add(it.Current)) continue;
                        voxel.Storage.ReadRange(blockData, MyStorageDataTypeFlags.ContentAndMaterial, FineLod,
                            it.Current << (CoarseLod - FineLod),
                            ((it.Current + 1) << (CoarseLod - FineLod)) - 1);
                        for (var j = 0; j < blockData.SizeLinear; j++)
                            if (blockData.Content(j) >= FineContentMin && hasOre[blockData.Material(j)])
                                result.Found++;
                    }
                }
            }

            swCoarse.Stop();
            result.CoarseToFineTicks = swCoarse.ElapsedTicks;
            return result;
        }

        struct Result
        {
            public long CoarseToFineTicks;
            public int Found;
            public long FullTicks;
            public Vector3D Position;
            public int Truth;
        }
    }
}
