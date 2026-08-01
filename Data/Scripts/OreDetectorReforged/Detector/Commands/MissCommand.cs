using System;
using Sandbox.ModAPI;
using VRage.Game.Components;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class MissCommand : MySessionComponentBase
    {
        const string Command = "/odr miss";

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
        }

        static void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (messageText != Command) return;
            sendToOthers = false;

            try
            {
                DetectorServer.ShowMiss = !DetectorServer.ShowMiss;
                MyAPIGateway.Utilities.ShowMessage(typeof(MissCommand).FullName,
                    DetectorServer.ShowMiss ? "showMiss on" : "showMiss off");
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(typeof(MissCommand).FullName, e.ToString());
            }
        }
    }
}