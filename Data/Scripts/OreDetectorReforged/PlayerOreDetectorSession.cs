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

        bool TryAddOreTask(int orei)
        {
            if (Session.Player == null || MyAPIGateway.Session.ControlledObject == null)
                return false;
            var controlledGridGroup = (MyAPIGateway.Session.ControlledObject as IMyShipController)?.CubeGrid?.GetGridGroup(GridLinkTypeEnum.Electrical);
            var grids = new List<IMyCubeGrid>();
            var center = Session.Camera.Position;
            var broadcastpos = MyAPIGateway.Session.ControlledObject.Entity.GetPosition();
            SearchTask task = null;
            foreach (var gridgroup in MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Electrical, new List<IMyGridGroupData>()))
            {
                grids.Clear();
                gridgroup.GetGrids(grids);
                var controlled = controlledGridGroup == gridgroup;
                if (!controlled && !grids.Any(grid => grid.GetFatBlocks<IMyRadioAntenna>().Any(e =>
                    e.IsWorking && e.EnableBroadcasting && e.HasLocalPlayerAccess() && Vector3D.DistanceSquared(e.GetPosition(), broadcastpos) <= e.Radius * e.Radius)))
                    continue;
                foreach (var grid in grids)
                    foreach (var e in grid.GetFatBlocks<IMyOreDetector>())
                    {
                        if (!e.IsWorking || !e.HasLocalPlayerAccess() || !controlled && !e.BroadcastUsingAntennas)
                            continue;
                        var settings = TerminalOreDetector.GetStorage(e);
                        if (settings == null || !settings.Whitelist[orei])
                            continue;
                        var range = settings.range - Vector3D.Distance(center, e.GetPosition());
                        if (range > (task?.area.Radius ?? 1.5))
                            task = CreateOreTask(new BoundingSphereD(center, range), orei, settings.color, settings.count);
                    }
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
