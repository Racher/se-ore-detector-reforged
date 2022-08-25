using System.Collections.Generic;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using OreDetectorReforged.Detector;
using System.Linq;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class PlayerOreDetectorSession : MySessionComponentBase
    {
        int counter;
        List<int>[] gpss;
        readonly HashSet<IMyCubeGrid> antennaGrids = new HashSet<IMyCubeGrid>();

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
                for (var _orei = 0; _orei < gpss.Length; ++_orei)
                    RemoveMarkers(_orei);
            var orei = counter / SyncSessionComponent.detFrequencyDivider % MaterialMappingHelper.Static.naturalOres.Length;
            if (!TryAddOreTask(orei))
                RemoveMarkers(orei);
        }

        static IMyShipController ShipController => MyAPIGateway.Session.ControlledObject as IMyShipController;

        static bool IsBlockControlled(IMyTerminalBlock e) => ShipController != null && MyAPIGateway.GridGroups.HasConnection(ShipController.CubeGrid, e.CubeGrid, GridLinkTypeEnum.Logical);

        static bool CheckAntennaToLocalPlayer(IMyRadioAntenna e) => e.IsWorking && e.HasLocalPlayerAccess() && Vector3D.DistanceSquared(e.GetPosition(), MyAPIGateway.Session.Player.GetPosition()) < e.Radius * e.Radius;

        bool IsBroadcasted(IMyOreDetector e) => e.BroadcastUsingAntennas && antennaGrids.Any(a => MyAPIGateway.GridGroups.HasConnection(a, e.CubeGrid, GridLinkTypeEnum.Logical));

        bool TryAddOreTask(int orei)
        {
            if (Session.Player == null)
                return false;
            var center = Session.Camera.Position;
            SearchTask task = null;
            antennaGrids.Clear();
            foreach (var e in AntennaSet.Get.Where(CheckAntennaToLocalPlayer))
                antennaGrids.Add(e.CubeGrid);
            foreach (var detector in DetectorSet.Get)
            {
                if (!detector.IsWorking || !detector.HasLocalPlayerAccess())
                    continue;
                var settings = TerminalOreDetector.GetStorage(detector);
                if (settings == null || !settings.Whitelist[orei])
                    continue;
                if (!IsBlockControlled(detector) && !IsBroadcasted(detector))
                    continue;
                var range = settings.range - Vector3D.Distance(center, detector.GetPosition());
                if (range <= (task?.area.Radius ?? 1.5))
                    continue;
                task = CreateOreTask(new BoundingSphereD(center, range), orei, settings.color, settings.count);
            }
            if (task != null)
                DetectorServer.Add(task);
            return task != null;
        }

        SearchTask CreateOreTask(BoundingSphereD area, int orei, Color color, int count)
        {
            var ore = MaterialMappingHelper.Static.naturalOres[orei];
            return new SearchTask(area, ore, count, (results) =>
            {
                RemoveMarkers(orei);
                if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                    foreach (var result in results)
                    {
                        var gps = Session.GPS.Create(ore, "Reforged", result, true);
                        gps.GPSColor = color;
                        Session.GPS.AddLocalGps(gps);
                        gpss[orei].Add(gps.Hash);
                    }
            });
        }

        void RemoveMarkers(int ore)
        {
            foreach(var gps in gpss[ore])
                Session.GPS?.RemoveLocalGps(gps);
            gpss[ore].Clear();
        }
    }
}
