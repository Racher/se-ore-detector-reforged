using VRage.Game;
using System.Diagnostics;
using VRage.Game.Components;
using System;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRageMath;
using System.Collections.Concurrent;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class SessionComponent : MySessionComponentBase
    {
        public static Config config;
        public static int configVersion;
        public static MaterialOreMapping materialOreMapping;
        public static TimeSpan mainTime;
        public static TimeSpan workTime;
        public static TimeSpan backTime;
        public static Exception lastException;
        public static string debugString;
        public static BlockingCollection<SearchTask> tasks;

        ConfigReader configReader;
        Detector detector;
        int counter;
        int threadsLaunched;

        void OnSessionReady()
        {
            if (MyAPIGateway.Session.Player == null)
                return;
            try
            {
                materialOreMapping = new MaterialOreMapping();
                tasks = new BlockingCollection<SearchTask>(new ConcurrentQueue<SearchTask>());
                configReader = new ConfigReader();
                configReader.Update();
                detector = new Detector(new PlayerDetectorIO());
                var terminalProp = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Vector3D>, IMyOreDetector>("Ores");
                terminalProp.Getter = (e) => e.Components.Get<EntityComponent>()?.GetResult();
                terminalProp.Setter = (e, _) => e.Components.Get<EntityComponent>()?.Reset();
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(terminalProp);
            }
            catch (Exception e)
            {
                lastException = e;
            }
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Session.OnSessionReady += OnSessionReady;
        }

        void UpdateMain()
        {
            if (counter % 10 != 0)
                return;
            var sw = Stopwatch.GetTimestamp();
            try
            {
                configReader?.Update();
                for (; threadsLaunched < (SessionComponent.config?.searchBackgroundThreads ?? 0); ++threadsLaunched)
                    MyAPIGateway.Parallel.StartBackground(new BackgroundVoxelStorageReader());
                detector?.Update();
            }
            catch (Exception e)
            {
                lastException = e;
            }
            mainTime += TimeSpan.FromTicks(Stopwatch.GetTimestamp() - sw);
        }

        void UpdateNotifications()
        {
            if (counter % 100 != 0)
                return;
            if (!(config?.debugNotifications ?? true))
                return;
            MyAPIGateway.Utilities.ShowNotification("Simulation/Main CPU " + mainTime.ToString(), 1600);
            MyAPIGateway.Utilities.ShowNotification("Thread/Worker CPU " + workTime.ToString(), 1600);
            MyAPIGateway.Utilities.ShowNotification("Background CPU " + backTime.ToString(), 1600);
            mainTime = default(TimeSpan);
            workTime = default(TimeSpan);
            backTime = default(TimeSpan);
            if (debugString != null)
            {
                MyAPIGateway.Utilities.ShowNotification("debugString " + debugString, 1600);
                debugString = null;
            }
            if (lastException == null)
                return;
            VRage.Utils.MyLog.Default.WriteLine(lastException.ToString());
            MyAPIGateway.Utilities.ShowMessage("", lastException.ToString());
            lastException = null;
        }

        public override void UpdateAfterSimulation()
        {
            UpdateMain();
            UpdateNotifications();
            ++counter;
        }

        protected override void UnloadData()
        {
            tasks.CompleteAdding();
            tasks.Dispose();
        }
    }
}
