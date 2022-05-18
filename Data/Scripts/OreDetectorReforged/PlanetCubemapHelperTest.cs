using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRageMath;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class PlanetCubemapHelperTest : MySessionComponentBase
    {
        int counter;
        public override void UpdateAfterSimulation()
        {
            if (++counter % 100 != 0)
                return;
            try
            {
                var planet = MyGamePruningStructure.GetClosestPlanet(Session.Player.GetPosition());
                var l = (Vector3)Vector3.Transform(Session.Player.GetPosition(), planet.GetViewMatrix());
                for (var f = 0; f < 6; ++f)
                {
                    var fxy = Vector2.Clamp(PlanetCubemapHelper.LocalToFace(l, f), -Vector2.One, Vector2.One);
                    var l1 = PlanetCubemapHelper.FaceToCube(fxy, f);
                    var surf = planet.GetClosestSurfacePointLocal(ref l1);

                    var tx = Vector2I.Floor(fxy * 1024 + 1024);

                    var gps = Session.GPS.Create(PlanetCubemapHelper.GetFaceMatInfix(f) + tx.X + " " + tx.Y, "", Vector3D.Transform(surf, planet.WorldMatrix), true);
                    gps.DiscardAt = Session.ElapsedPlayTime + TimeSpan.FromSeconds(2);
                    Session.GPS.RemoveLocalGps(gps);
                    Session.GPS.AddLocalGps(gps);
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(ModContext.ModName, e.ToString());
            }
        }
    }
}
