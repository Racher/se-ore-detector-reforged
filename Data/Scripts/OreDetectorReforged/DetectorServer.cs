using VRage.Game.Components;
using System;
using System.Collections.Generic;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using System.Diagnostics;
using VRage.Game;
using System.Collections.Concurrent;
using Sandbox.ModAPI;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class DetectorServer : MySessionComponentBase
    {
        readonly static PriorityQueue<Node> pq = new PriorityQueue<Node>(6, new Node.Comparer());
        readonly static List<MyVoxelBase> vbs = new List<MyVoxelBase>();
        readonly static BlockingCollection<SearchTask> tasks = new BlockingCollection<SearchTask>(new ConcurrentQueue<SearchTask>());
        readonly static ConcurrentQueue<SearchTask> finished = new ConcurrentQueue<SearchTask>();

        public static void Add(SearchTask task)
        {
            vbs.Clear();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref task.area, vbs);
            task.pages = new List<IDetectorPage>(100);
            foreach (var vb in vbs)
            {
                if (vb.RootVoxel != vb)
                    continue;
                var cloud = vb.Components.Get<DetectorComponent>() ?? new DetectorComponent(vb);
                if (cloud.data.Length > 0)
                    task.pages.AddRange(cloud.data);
            }
            vbs.Clear();
            tasks.Add(task);
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);
            MyAPIGateway.Parallel.StartBackground(ProcessTasks);
        }

        public override void UpdateAfterSimulation()
        {
            SearchTask task;
            while (finished.TryDequeue(out task))
                task.finishCb();
        }

        protected override void UnloadData()
        {
            tasks?.CompleteAdding();
            tasks?.Dispose();
        }

        static void ProcessTasks()
        {
            try
            {
                for (; !tasks.IsAddingCompleted; MyAPIGateway.Parallel.Sleep(0))
                {
                    var task = tasks.Take();
                    var sliceEnd = Stopwatch.GetTimestamp() + TimeSpan.FromMilliseconds(15).Ticks;
                    do
                    {
                        string[] oreNames;
                        MyDefinitionManager.Static.GetOreTypeNames(out oreNames);
                        var ore = Array.IndexOf(oreNames, task.minedOre);
                        for (var page = 0; page < task.pages.Count; ++page)
                            task.pages[page].Setup(pq, task.area.Center, page, ore);
                        while (pq.Count > 0)
                        {
                            var r = task.pages[pq.Top.p].Pop();
                            if (r.IsZero())
                                continue;
                            if (task.area.Contains(r) == ContainmentType.Disjoint || !task.resultCb(r))
                                break;
                        }
                        pq.Clear();
                        finished.Enqueue(task);
                    }
                    while (Stopwatch.GetTimestamp() < sliceEnd && tasks.TryTake(out task));
                }
            }
            catch (InvalidOperationException) { }
        }
    }
}
