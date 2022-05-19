using System.Collections.Generic;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using OreDetectorReforged.Detector;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class PlayerOreDetectorSession : MySessionComponentBase
    {
        const string gpsDesc = "Reforged";
        int counter;
        List<int>[] gpss;

        public override void UpdateAfterSimulation()
        {
            var period = MaterialMappingHelper.Static.naturalOres.Length * SyncSessionComponent.detFrequencyDivider;
            counter = (counter + 1) % period;
            if (counter % SyncSessionComponent.detFrequencyDivider != 0)
                return;
            if (gpss == null)
            {
                gpss = new List<int>[MaterialMappingHelper.Static.naturalOres.Length];
                for (var i = 0; i < gpss.Length; ++i)
                    gpss[i] = new List<int>();
            }
            if (MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.None)
                for (var i = 0; i < gpss.Length; ++i)
                    RemoveMarkers(i);
            var o = counter / SyncSessionComponent.detFrequencyDivider % MaterialMappingHelper.Static.naturalOres.Length;
            var detector = GetDetector(o);
            if (detector == null)
            {
                RemoveMarkers(o);
                return;
            }
            var settings = TerminalOreDetector.GetStorage(detector);
            var ore = MaterialMappingHelper.Static.naturalOres[o];
            var area = new BoundingSphereD(detector.GetPosition(), settings.range);
            DetectorServer.Add(new SearchTask(area, ore, settings.count, (results) =>
            {
                RemoveMarkers(o);
                if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                    foreach (var result in results)
                    {
                        var gps = Session.GPS.Create(ore, gpsDesc, result, true);
                        gps.GPSColor = settings.color;
                        Session.GPS.AddLocalGps(gps);
                        gpss[o].Add(gps.Hash);
                    }
            }));
        }

        IMyOreDetector GetDetector(int o)
        {
            var shipController = Session.ControlledObject as IMyShipController;
            if (shipController == null)
                return null;
            var maxrange = 1.5f;
            IMyOreDetector det = null;
            foreach (var detector in shipController.CubeGrid.GetFatBlocks<IMyOreDetector>())
            {
                var settings = TerminalOreDetector.GetStorage(detector);
                if (!detector.IsWorking || settings == null || !settings.Whitelist[o] || settings.range < maxrange)
                    continue;
                maxrange = settings.range;
                det = detector;
            }
            return det;
        }

        void RemoveMarkers(int ore)
        {
            foreach(var gps in gpss[ore])
                Session.GPS?.RemoveLocalGps(gps);
            gpss[ore].Clear();
        }
    }
}
