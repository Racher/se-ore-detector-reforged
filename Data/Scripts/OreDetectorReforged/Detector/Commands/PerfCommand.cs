using System;
using System.Diagnostics;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class PerfCommand : MySessionComponentBase
    {
        const string Command = "/odr perf";
        const string ServerCommand = "/odr server-perf";
        const ushort NetId = 27491;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NetId, OnNetMessage);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NetId, OnNetMessage);
        }

        static void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith(ServerCommand))
            {
                sendToOthers = false;
                try
                {
                    var rest = messageText.Substring(ServerCommand.Length).Trim();
                    var reset = rest.Equals("reset", StringComparison.OrdinalIgnoreCase);
                    HandleServerCommand(reset);
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage(typeof(PerfCommand).FullName, e.ToString());
                }

                return;
            }

            if (!messageText.StartsWith(Command)) return;
            sendToOthers = false;

            try
            {
                var rest = messageText.Substring(Command.Length).Trim();
                var reset = rest.Equals("reset", StringComparison.OrdinalIgnoreCase);

                MyAPIGateway.Utilities.ShowMessage("OreDetector Perf", BuildReport(reset));
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(typeof(PerfCommand).FullName, e.ToString());
            }
        }

        static void HandleServerCommand(bool reset)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                MyAPIGateway.Utilities.ShowMessage("OreDetector ServerPerf", BuildReport(reset));
                return;
            }

            var payload = new byte[1];
            payload[0] = reset ? (byte)1 : (byte)0;
            MyAPIGateway.Multiplayer.SendMessageToServer(NetId, payload);
        }

        static void OnNetMessage(ushort handlerId, byte[] data, ulong senderSteamId, bool fromServer)
        {
            try
            {
                if (fromServer)
                {
                    var msg = Encoding.UTF8.GetString(data);
                    MyAPIGateway.Utilities.ShowMessage("OreDetector ServerPerf", msg);
                    return;
                }

                if (!MyAPIGateway.Multiplayer.IsServer) return;
                var reset = data != null && data.Length > 0 && data[0] != 0;
                var reply = Encoding.UTF8.GetBytes(BuildReport(reset));
                MyAPIGateway.Multiplayer.SendMessageTo(NetId, reply, senderSteamId);
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(typeof(PerfCommand).FullName, e.ToString());
            }
        }

        static string BuildReport(bool reset)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            var elapsedTicks = Math.Max(1, nowTicks - DetectorServer.PerfWindowStartTicks);
            var msPerTick = 1000.0 / Stopwatch.Frequency;
            var elapsedMs = elapsedTicks * msPerTick;

            long planetMem = 0, asteroidMem = 0, boulderMem = 0;
            int planetCount = 0, asteroidCount = 0, boulderCount = 0;
            foreach (var entity in MyEntities.GetEntities())
            {
                var vb = entity as MyVoxelBase;
                if (vb == null) continue;
                var planet = vb.Components.Get<DetectorPagePlanet>();
                if (planet != null)
                {
                    planetCount++;
                    planetMem += planet.EstimateMemoryBytes();
                }

                var asteroid = vb.Components.Get<DetectorPageAsteroid>();
                if (asteroid != null)
                {
                    asteroidCount++;
                    asteroidMem += asteroid.EstimateMemoryBytes();
                }

                var boulder = vb.Components.Get<DetectorPageBoulder>();
                if (boulder != null)
                {
                    boulderCount++;
                    boulderMem += boulder.EstimateMemoryBytes();
                }
            }

            var updPct = DetectorServer.UpdateThreadTicks * 100.0 / elapsedTicks;
            var detPct = DetectorServer.DetectorThreadTicks * 100.0 / elapsedTicks;

            var msg =
                $"Perf over {elapsedMs / 1000.0:N1}s:\n" +
                $"  update thread: {DetectorServer.UpdateThreadTicks * msPerTick:N1} ms ({updPct:N2}%)\n" +
                $"  detector thread: {DetectorServer.DetectorThreadTicks * msPerTick:N1} ms ({detPct:N2}%)\n" +
                $"  planet Load: {DetectorServer.PlanetLoadTicks * msPerTick:N1} ms\n" +
                $"  asteroid Load: {DetectorServer.AsteroidLoadTicks * msPerTick:N1} ms\n" +
                $"  boulder Load: {DetectorServer.BoulderLoadTicks * msPerTick:N1} ms\n" +
                $"  planet pages: {planetCount}, mem ~{planetMem / 1024.0:N1} KB\n" +
                $"  asteroid pages: {asteroidCount}, mem ~{asteroidMem / 1024.0:N1} KB\n" +
                $"  boulder pages: {boulderCount}, mem ~{boulderMem / 1024.0:N1} KB";

            if (reset)
            {
                DetectorServer.UpdateThreadTicks = 0;
                DetectorServer.DetectorThreadTicks = 0;
                DetectorServer.PlanetLoadTicks = 0;
                DetectorServer.AsteroidLoadTicks = 0;
                DetectorServer.BoulderLoadTicks = 0;
                DetectorServer.PerfWindowStartTicks = nowTicks;
            }

            return msg;
        }
    }
}