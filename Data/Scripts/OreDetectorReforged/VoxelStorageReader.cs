using VRageMath;
using VRage.Voxels;
using System;
using Sandbox.ModAPI;
using System.Diagnostics;
using ParallelTasks;

namespace OreDetectorReforged
{
    class BackgroundVoxelStorageReader : VoxelStorageReader, IWork
    {
        public WorkOptions Options => MyAPIGateway.Parallel.DefaultOptions;

        public void DoWork(WorkData workData = null)
        {
            try
            {
                for (; !SessionComponent.tasks.IsAddingCompleted; MyAPIGateway.Parallel.Sleep(0))
                {
                    var task = SessionComponent.tasks.Take();
                    var sliceEnd = Stopwatch.GetTimestamp() + TimeSpan.FromMilliseconds(15).Ticks;
                    do
                    {
                        var taskTime = Stopwatch.GetTimestamp();
                        ReadRange(task);
                        task.elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - taskTime);
                        task.finished.Enqueue(task);
                    }
                    while (Stopwatch.GetTimestamp() < sliceEnd && SessionComponent.tasks.TryTake(out task));
                }
            }
            catch (InvalidOperationException) { }
            catch (Exception e)
            {
                SessionComponent.lastException = e;
            }
        }
    }

    class VoxelStorageReader
    {
        readonly float[] scores = new float[SessionComponent.materialOreMapping.oreNames.Length];
        readonly Vector3I[] pSs = new Vector3I[SessionComponent.materialOreMapping.oreNames.Length];
        readonly MyStorageData storageData = new MyStorageData(MyStorageDataTypeFlags.ContentAndMaterial);

        public VoxelStorageReader()
        {
            storageData.Resize(Vector3I.One * 16);
        }

        public void ReadRange(SearchTask task)
        {
            var contentMin = SessionComponent.config.voxelContentMin0to255;
            var vbi = task.chunk.vbi;
            var w = task.chunk.w;
            var v = task.chunk.v;
            var result = task.result;
            var level = Math.Max(0, w - 4);
            const int mask = int.MaxValue - 15;
            var minV = v >> level & mask;
            var centerV = vbi.size1 >> level + 1;
            using (var pin = vbi.vb.Pin())
            {
                if (vbi.vb.MarkedForClose)
                    return;
                vbi.vb.Storage.ReadRange(storageData, MyStorageDataTypeFlags.ContentAndMaterial, level + 1, minV, minV + 15);
            }
            var centerS = centerV - minV;
            var oreMap = vbi.oreMap;
            Array.Clear(scores, 0, scores.Length);
            var prevPs = v - minV;
            for (var iS = 0; iS < storageData.SizeLinear; ++iS)
            {
                var m = oreMap[storageData.Material(iS)];
                if (m == 255 || level == 0 && storageData.Content(iS) < contentMin || w <= 0 && m != -w)
                    continue;
                Vector3I pS;
                storageData.ComputePosition(iS, out pS);
                var score = 1000000f + Vector3.Distance(pS, centerS);
                if (w <= 0)
                    score -= 0.5f * Vector3.Distance(pS, prevPs);
                if (score <= scores[m])
                    continue;
                scores[m] = score;
                pSs[m] = pS;
            }
            for (var m = 0; m < scores.Length; ++m)
                if (scores[m] != 0)
                    result.Add(new V31Byte(pSs[m] + minV, m));
        }
    }
}
