using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRageMath;

namespace OreDetectorReforged.Detector.Test
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class BoulderTest : MySessionComponentBase
    {
        public override void UpdateAfterSimulation()
        {
            List<MyVoxelBase> list = new List<MyVoxelBase>();
            BoundingSphereD area = new BoundingSphereD(Session.Player.GetPosition(), 30000);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref area, list);
            foreach (var vb in list)
            {
                if (vb.Closed) continue;
                if (!vb.BoulderInfo.HasValue) continue;
                var sectorIdLod = vb.BoulderInfo.Value.SectorId >> 51;
                if (sectorIdLod != 0) continue;
                var pos = Vector3D.Transform(Vector3D.Zero, vb.WorldMatrix);
                var planet = MyAPIGateway.Entities.GetEntityById(vb.BoulderInfo.Value.PlanetId) as MyPlanet;
                var gps = Session.GPS.Create((vb.BoulderInfo.Value.SectorId >> 51).ToString(), "", pos, true, true);
                gps.DiscardAt = Session.ElapsedPlayTime + TimeSpan.FromMilliseconds(16);
                Session.GPS.RemoveLocalGps(gps);
                Session.GPS.AddLocalGps(gps);
            }
        }
    }
}
