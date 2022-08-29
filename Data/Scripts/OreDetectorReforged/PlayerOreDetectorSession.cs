using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
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

        static bool IsAntennaInRange(IMyRadioAntenna e) => Vector3D.DistanceSquared(e.GetPosition(), MyAPIGateway.Session.Player.GetPosition()) < e.Radius * e.Radius;

        static bool CheckAntennaToLocalPlayer(IMyRadioAntenna e) => e.IsWorking && e.HasLocalPlayerAccess() && IsAntennaInRange(e);

        bool IsBroadcasted(IMyOreDetector e) => e.BroadcastUsingAntennas && antennaGrids.Contains(e.CubeGrid);

        bool TryAddOreTask(int orei)
        {
            if (Session.Player == null)
                return false;
            var center = Session.Camera.Position;
            SearchTask task = null;
            antennaGrids.Clear();
            foreach (var e in AntennaSet.Get.Where(CheckAntennaToLocalPlayer))
                MyAPIGateway.GridGroups.GetGroup(e.CubeGrid, GridLinkTypeEnum.Logical, antennaGrids);
            foreach (var det in DetectorSet.Get)
            {
                if (!(det.IsWorking && det.HasLocalPlayerAccess() && (IsBlockControlled(det) || IsBroadcasted(det))))
                    continue;
                var settings = TerminalOreDetector.GetStorage(det);
                if (settings == null || !settings.Whitelist[orei])
                    continue;
                var range = settings.range - Vector3D.Distance(center, det.GetPosition());
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
                        CalculateAngle(gps);
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

        protected void CalculateAngle(IMyGps gps)
        {
            Vector3D playerPos = Session.ControlledObject.Entity.GetPosition();
            float naturalGravityInterference = 0;
            Vector3D gravity = MyAPIGateway.Physics.CalculateNaturalGravityAt(playerPos, out naturalGravityInterference);
            if (gravity != Vector3D.Zero)
            {
                gravity.Normalize();
                Vector3D lookAt = gps.Coords - playerPos;
                lookAt.Normalize();
                float num = MathHelper.ToDegrees(MyUtils.GetAngleBetweenVectors(lookAt, gravity)) - 90f;
                gps.ContainerRemainingTime = $"{num:F2}°";
            }
        }
    }
}
