using System;
using Sandbox.ModAPI;
using ParallelTasks;
using System.Diagnostics;

namespace OreDetectorReforged
{
    abstract class MyWorkData : WorkData
    {
        public Task Start() => MyAPIGateway.Parallel.Start(doWork, finish, this);
        public Task StartBackground() => MyAPIGateway.Parallel.StartBackground(doWork, finish, this);
        abstract protected void DoWork();
        abstract protected void Finish();

        static readonly Action<WorkData> doWork = DoWork;
        static readonly Action<WorkData> finish = Finish;
        static void DoWork(WorkData workData)
        {
            var data = workData as MyWorkData;
            try
            {
                data.DoWork();
            }
            catch (Exception e)
            {
                SessionComponent.lastException = e;
            }
        }
        static void Finish(WorkData workData)
        {
            var data = workData as MyWorkData;
            try
            {
                data.Finish();
            }
            catch (Exception e)
            {
                SessionComponent.lastException = e;
            }
        }
    }
}
