using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    class SearchTask
    {
        static readonly Stack<SearchTask> Pool = new Stack<SearchTask>();
        internal readonly List<IDetectorPage> Pages = new List<IDetectorPage>();

        // pre-created once so Tasks.Add(sr.task) reuses the same delegate across pool cycles
        internal readonly Action Task;
        Exception _exception;
        Vector3[] _localPositions;
        internal Vector3D AreaCenter;
        internal float AreaRadius;

        // input fields (written on main thread before task submission, read on background thread)
        internal int CurrOre;

        internal float Distance;

        // result fields
        internal Vector3 LocalPos;
        internal Action<Vector3D, Exception> OnFinished;
        internal float QuasiNearest;
        internal MyVoxelBase Vb;

        SearchTask()
        {
            Task = RunOnBackground;
        }

        internal static SearchTask Rent()
        {
            return Pool.Count > 0 ? Pool.Pop() : new SearchTask();
        }

        // Called on main thread after Dispatch to return the task to the pool.
        internal void Return()
        {
            OnFinished = null;
            Pages.Clear();
            Pool.Push(this);
            Vb = null;
            _exception = null;
        }

        // Called on main thread by DetectorServer.UpdateAfterSimulation.
        internal void Dispatch()
        {
            OnFinished(Vb == null ? Vector3D.Zero : Vector3D.Transform(LocalPos, Vb.WorldMatrix), _exception);
        }

        // Runs on background thread; posted to Tasks as the pre-created delegate.
        void RunOnBackground()
        {
            try
            {
                Execute();
            }
            catch (Exception e)
            {
                _exception = e;
            }

            DetectorServer.Finished.Enqueue(this);
        }

        void Execute()
        {
            // initialize result fields before the try so they are valid even if an exception occurs
            Distance = AreaRadius;
            LocalPos = default(Vector3);
            Vb = null;

            var pq = DetectorServer.Pq;
            pq.Clear();
            if (_localPositions == null || _localPositions.Length < Pages.Count)
                _localPositions = new Vector3[Pages.Count];
            for (var p = 0; p < Pages.Count; p++)
            {
                _localPositions[p] = Pages[p].WorldToLocal(AreaCenter);
                Pages[p].PushRoot(Distance, pq, CurrOre, p, _localPositions[p]);
            }

            while (pq.Count > 0)
            {
                var node = pq.Top;
                pq.Pop();
                if (Distance < node.D) break;
                Pages[node.P].Process(ref node, this, pq, _localPositions[node.P]);
            }
        }
    }
}