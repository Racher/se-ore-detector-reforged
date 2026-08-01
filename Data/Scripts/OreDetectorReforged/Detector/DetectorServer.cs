using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class DetectorServer : MySessionComponentBase
    {
        // Action queue: accepts both SearchResult.task and cache-clear lambdas from page components.
        internal static readonly BlockingCollection<Action> Tasks = new BlockingCollection<Action>(new ConcurrentQueue<Action>());

        // Accessed by SearchResult.RunOnBackground (same assembly).
        internal static readonly ConcurrentQueue<SearchTask> Finished = new ConcurrentQueue<SearchTask>();
        internal static readonly ConcurrentQueue<IMyGps> Misses = new ConcurrentQueue<IMyGps>();
        internal static readonly PriorityQueue<Node, Node.Comparer> Pq = new PriorityQueue<Node, Node.Comparer>(1, new Node.Comparer());
        internal static bool ShowMiss;
        static readonly List<MyVoxelBase> AllVoxels = new List<MyVoxelBase>();

        // Perf counters (read by PerfCommand for /odr perf reporting).
        internal static long UpdateThreadTicks;
        internal static long DetectorThreadTicks;
        internal static long PlanetLoadTicks;
        internal static long AsteroidLoadTicks;
        internal static long BoulderLoadTicks;
        internal static long PerfWindowStartTicks = Stopwatch.GetTimestamp();

        public static void SubmitSearch(BoundingSphereD area, string oreName, Action<Vector3D, Exception> onFinished)
        {
            if (MaterialMappingHelper.NaturalOresToIdx == null)
            {
                onFinished(Vector3D.Zero, new InvalidOperationException("Detector is not yet initialized"));
                return;
            }

            int currOre;
            if (!MaterialMappingHelper.NaturalOresToIdx.TryGetValue(oreName, out currOre))
            {
                onFinished(Vector3D.Zero, new ArgumentException($"Key {oreName} not in {string.Join(",", MaterialMappingHelper.NaturalOresToIdx.Keys)}"));
                return;
            }

            AllVoxels.Clear();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref area, AllVoxels);
            var sr = SearchTask.Rent();
            sr.CurrOre = currOre;
            sr.AreaRadius = (float)area.Radius;
            sr.AreaCenter = area.Center;
            sr.QuasiNearest = 0.95f;
            sr.OnFinished = onFinished;
            sr.Pages.Clear();
            foreach (var vb in AllVoxels)
            {
                if (vb.RootVoxel != vb) continue;
                if (vb.BoulderInfo.HasValue && vb.BoulderInfo.Value.SectorId >> 51 > 0) continue;
                var planet = vb as MyPlanet;
                if (planet != null)
                    sr.Pages.Add(DetectorPagePlanet.GetOrCreate(planet));
                else if (vb.Storage.Size.AbsMax() <= 16)
                    sr.Pages.Add(DetectorPageBoulder.GetOrCreate(vb));
                else
                    sr.Pages.Add(DetectorPageAsteroid.GetOrCreate(vb));
            }

            Tasks.Add(sr.Task);
        }

        public override void LoadData()
        {
            MyAPIGateway.Parallel.StartBackground(ProcessOnBackground);
        }

        protected override void UnloadData()
        {
            Tasks?.CompleteAdding();
            Tasks?.Dispose();
        }

        public override void UpdateAfterSimulation()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                SearchTask sr;
                while (Finished.TryDequeue(out sr))
                {
                    sr.Dispatch();
                    sr.Return();
                }

                if (ShowMiss)
                {
                    IMyGps miss;
                    if (Misses.TryDequeue(out miss))
                        MyAPIGateway.Session.GPS.AddLocalGps(miss);
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(typeof(DetectorServer).FullName, e.ToString());
            }

            UpdateThreadTicks += sw.ElapsedTicks;
        }

        static void ProcessOnBackground()
        {
            foreach (var action in Tasks.GetConsumingEnumerable())
                try
                {
                    var sw = Stopwatch.StartNew();
                    action();
                    DetectorThreadTicks += sw.ElapsedTicks;
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage(typeof(DetectorServer).FullName, e.ToString());
                }
        }
    }
}