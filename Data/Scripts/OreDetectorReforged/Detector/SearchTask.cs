using System;
using System.Collections.Generic;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    class SearchTask
    {
        public BoundingSphereD area;
        public string minedOre;
        public Func<Vector3D, bool> resultCb;
        public Action finishCb;
        public List<IDetectorPage> pages;

        public SearchTask(BoundingSphereD area, string minedOre, Func<Vector3D, bool> resultCb, Action finishCb)
        {
            this.area = area;
            this.minedOre = minedOre;
            this.resultCb = resultCb;
            this.finishCb = finishCb;
        }

        public SearchTask(BoundingSphereD area, string minedOre, int count, Action<List<Vector3D>> finishCb)
        {
            var results = new List<Vector3D>(count);
            this.area = area;
            this.minedOre = minedOre;
            this.resultCb = (v) =>
            {
                results.Add(v);
                return results.Count < count;
            };
            this.finishCb = () => finishCb(results);
        }

        public SearchTask(ValueTuple<BoundingSphereD, string, Func<Vector3D, bool>, Action> t) : this(t.Item1, t.Item2, t.Item3, t.Item4)
        {
        }

        public SearchTask(ValueTuple<BoundingSphereD, string, int, Action<List<Vector3D>>> t) : this(t.Item1, t.Item2, t.Item3, t.Item4)
        {
        }
    }
}
