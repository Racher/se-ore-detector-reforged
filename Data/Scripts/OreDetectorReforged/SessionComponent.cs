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
        public const int msgId = 43818;
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
        Detector playerDetector;
        int counter;
        int threadsStarted;

        void UpdateMain()
        {
            if (counter % 10 != 0)
                return;
            var sw = Stopwatch.GetTimestamp();
            try
            {
                configReader.Update();
                for (; threadsStarted < (config?.searchBackgroundThreads ?? 0); ++threadsStarted)
                    MyAPIGateway.Parallel.StartBackground(new BackgroundVoxelStorageReader());
                playerDetector.Update();
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
            if (lastException != null)
            {
                VRage.Utils.MyLog.Default.WriteLine(lastException.ToString());
                MyAPIGateway.Utilities.ShowMessage("", lastException.ToString());
                lastException = null;
            }
            if (config?.debugNotifications ?? false)
            {
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
            }
        }

        public override void UpdateAfterSimulation()
        {
            UpdateMain();
            UpdateNotifications();
            ++counter;
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            materialOreMapping = new MaterialOreMapping();
            tasks = new BlockingCollection<SearchTask>(new ConcurrentQueue<SearchTask>());
            configReader = new ConfigReader();
            playerDetector = new Detector(new PlayerDetectorIO());
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(msgId, EntityComponent.HandleDetectorRequest);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(msgId, EntityComponent.HandleDetectorRequest);
            tasks?.CompleteAdding();
            tasks?.Dispose();
        }
    }
}
