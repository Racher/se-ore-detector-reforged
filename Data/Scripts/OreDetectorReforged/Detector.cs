using System.Collections.Generic;
using VRageMath;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace OreDetectorReforged
{
    class Detector : MyWorkData
    {
        readonly ChunkCollector chunkCollector = new ChunkCollector();
        readonly List<ChunkInfo> topChunks = new List<ChunkInfo>();
        readonly Stack<SearchTask> recycledTasks = new Stack<SearchTask>();
        readonly OreCache oreCache = new OreCache();
        readonly List<SearchCandidate> candidates = new List<SearchCandidate>();
        readonly List<long> candidateSortKeys = new List<long>();
        readonly int[] closeOreDists = new int[SessionComponent.materialOreMapping.oreNames.Length];
        readonly ChunkInfo[] closeOreMarks = new ChunkInfo[SessionComponent.materialOreMapping.oreNames.Length];
        readonly Vector3D[] closeOreResult = new Vector3D[SessionComponent.materialOreMapping.oreNames.Length];
        readonly IDetectorIO detectorIO;
        readonly ConcurrentQueue<SearchTask> finished = new ConcurrentQueue<SearchTask>();
        readonly List<KeyValuePair<ChunkInfo, Color>> debugGpss = new List<KeyValuePair<ChunkInfo, Color>>();
        int cacheConfigVersion;
        int queuedTasks;
        int scans;
        bool busy;
        TimeSpan workTime;
        TimeSpan backTime;

        public Detector(IDetectorIO detectorIO)
        {
            this.detectorIO = detectorIO;
        }

        public void Update()
        {
            if (busy || SessionComponent.config == null)
                return;
            busy = true;
            chunkCollector.CollectVoxelBases(detectorIO.GetPosition());
            Start();
        }

        void CheckCache()
        {
            if (cacheConfigVersion != SessionComponent.configVersion)
            {
                oreCache.Clear();
                scans = 0;
            }
            cacheConfigVersion = SessionComponent.configVersion;
            oreCache.EnsureCapacity(SessionComponent.config.seachVolumeLimit512MChunks * 2);
        }

        void MergeResult()
        {
            backTime = default(TimeSpan);
            debugGpss.Clear();
            var spread = SessionComponent.config.searchSubChunkSpread0to5;
            for (SearchTask task; finished.TryDequeue(out task); recycledTasks.Push(task))
            {
                --queuedTasks;
                backTime += task.elapsed;
                var chunk = task.chunk;
                if (task.chunk.w <= 0)
                    oreCache.UpdateOre(task.chunk, task.result.Count > 0 ? task.result[0] : new V31Byte(default(Vector3I), 255));
                else if (task.chunk.w == 4)
                    foreach (var lv in task.result)
                    {
                        oreCache.AddOre(chunk, lv);
                        debugGpss.Add(new KeyValuePair<ChunkInfo, Color>(new ChunkInfo(chunk.vbi, (chunk.v & (int.MaxValue - 255)) + lv.V, 0), Color.Red));
                    }
                else if (task.result.Count > 0)
                {
                    const int neighborR = 3;
                    var neighborR2 = 3 + spread * 8;
                    var min = Vector3I.One * (1 - neighborR);
                    var max = Vector3I.One * neighborR;
                    for (var vit = new Vector3I_RangeIterator(ref min, ref max); vit.IsValid(); vit.MoveNext())
                    {
                        var d = 2 * vit.Current - 1;
                        if ((d * d).RectangularLength() > neighborR2)
                            continue;
                        var cchunk = new ChunkInfo(chunk.vbi, chunk.v + (vit.Current << chunk.w - 1), chunk.w - 1);
                        if (!cchunk.v.IsInside(default(Vector3I), chunk.vbi.size1))
                            continue;
                        if (!oreCache.AddPending(cchunk))
                            continue;
                        debugGpss.Add(new KeyValuePair<ChunkInfo, Color>(cchunk, Color.Gray));
                    }
                }
            }
        }

        void IterChunks()
        {
            chunkCollector.ListTopChunks(topChunks);
            Array.Clear(closeOreDists, 0, closeOreDists.Length);
            Array.Clear(closeOreResult, 0, closeOreResult.Length);
            var candidateCount =  detectorIO.TaskPerCollect + 1000;
            candidates.EnsureCapacity(candidateCount);
            while (candidates.Count < candidateCount)
                candidates.Add(new SearchCandidate());
            var sorter = new ListSorter<SearchCandidate>(candidateSortKeys);
            candidateSortKeys.EnsureCapacity(candidateCount);
            foreach (var topChunk in topChunks)
            {
                var page = oreCache.GetTopPage(topChunk);
                while (page.searchs.Count > 0 && candidateSortKeys.Count < candidateCount)
                {
                    var v = page.searchs.Pop();
                    var candidate = candidates[candidateSortKeys.Count];
                    candidate.topChunk = topChunk;
                    candidate.pageSearch = page.searchs;
                    candidate.v = v;
                    var chunk = new ChunkInfo(topChunk, v);
                    var d = topChunk.vbi.center1 - MyTransforms.GetCenter(chunk.v, chunk.w);
                    var d2 = (d * d).RectangularLength();
                    sorter.AddKey(d2);
                }
                foreach (var ore in page.ores)
                {
                    if (ore.W == 255)
                        continue;
                    var chunk = new ChunkInfo(topChunk.vbi, topChunk.v + ore.V, 0);
                    var d = chunk.vbi.center1 - MyTransforms.GetCenter(chunk.v, chunk.w);
                    var d2 = (d * d).RectangularLength() - int.MaxValue;
                    if (d2 > closeOreDists[ore.W] || d2 == closeOreDists[ore.W] && chunk.v.CompareTo(closeOreMarks[ore.W].v) >= 0)
                        continue;
                    closeOreDists[ore.W] = d2;
                    closeOreMarks[ore.W] = chunk;
                }
            }
            sorter.Sort(candidates);
        }

        void AddTask(ChunkInfo chunk)
        {
            ++queuedTasks;
            var task = recycledTasks.Count > 0 ? recycledTasks.Pop() : new SearchTask(finished);
            task.result.Clear();
            task.chunk = chunk;
            task.elapsed = default(TimeSpan);
            try
            {
                SessionComponent.tasks.Add(task);
            }
            catch (InvalidOperationException) { }
            debugGpss.Add(new KeyValuePair<ChunkInfo, Color>(task.chunk, Color.White));
        }

        void AddTasks()
        {
            var taskLimit = Math.Max(0, Math.Min(detectorIO.TaskPerCollect, 2 * detectorIO.TaskPerCollect - queuedTasks));
            var refreshRangeSqr = SessionComponent.config.refreshGpsRangeMeters * SessionComponent.config.refreshGpsRangeMeters / 4;
            for (var m = 0; m < closeOreDists.Length && taskLimit > 0; ++m)
                if (closeOreDists[m] + int.MaxValue <= refreshRangeSqr)
                {
                    var ore = closeOreMarks[m];
                    AddTask(new ChunkInfo(ore.vbi, ore.v, -m));
                    --taskLimit;
                }
            if (candidateSortKeys.Count > taskLimit)
            {
                for (var i = taskLimit; i < candidateSortKeys.Count; ++i)
                    candidates[i].Return();
                candidateSortKeys.RemoveRange(taskLimit, candidateSortKeys.Count - taskLimit);
            }
            for (var i = 0; i < candidateSortKeys.Count; ++i)
            {
                ++scans;
                AddTask(new ChunkInfo(candidates[i].topChunk, candidates[i].v));
            }
        }

        protected override void DoWork()
        {
            var sw = Stopwatch.GetTimestamp();
            CheckCache();
            MergeResult();
            IterChunks();
            AddTasks();
            workTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw);
        }

        protected override void Finish()
        {
            busy = false;
            var sw = Stopwatch.GetTimestamp();
            for (var m = 0; m < closeOreDists.Length; ++m)
                closeOreResult[m] = closeOreDists[m] == 0 ? default(Vector3D) : MyTransforms.ChunkCenterW(closeOreMarks[m]);
            detectorIO.UpdateResult(closeOreResult);
            SessionComponent.backTime += backTime;
            SessionComponent.workTime += workTime;
            SessionComponent.mainTime += TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw);
            SessionComponent.debugString = oreCache.OreCount.ToString() + " ore/scan " + scans.ToString();
            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None && SessionComponent.config.debugGps)
                foreach (var pair in debugGpss.Take(2000))
                {
                    var coord = MyTransforms.ChunkCenterW(pair.Key);
                    var gps = MyAPIGateway.Session.GPS.Create("", "", coord, true, true);
                    gps.GPSColor = pair.Value;
                    gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(1.66);
                    MyAPIGateway.Session.GPS.RemoveLocalGps(gps.GetHashCode());
                    MyAPIGateway.Session.GPS.AddLocalGps(gps);
                }
            debugGpss.Clear();
        }
    }
}
