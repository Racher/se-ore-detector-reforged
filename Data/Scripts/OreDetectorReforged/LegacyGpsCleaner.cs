using VRage.Game.Components;
using VRageMath;
using Sandbox.Definitions;
using System.Collections.Generic;
using VRage.Game;
using Sandbox.ModAPI;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class LegacyGpsCleaner : MySessionComponentBase
    {
        static void ClearGps()
        {
            if (MyAPIGateway.Session.GPS == null || MyAPIGateway.Session.Player == null)
                return;
            foreach (var gps in MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId))
            {
                if (gps.Coords != Vector3D.Zero || gps.Name != "OreDetectorReforgedConfig")
                    continue;
                MyAPIGateway.Session.GPS.RemoveGps(MyAPIGateway.Session.Player.IdentityId, gps.Hash);
                MyAPIGateway.Session.GPS.RemoveLocalGps(gps.Hash);
            }
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            Session.OnSessionReady += ClearGps;
        }

        protected override void UnloadData()
        {
            Session.OnSessionReady -= ClearGps;
        }
    }
}
