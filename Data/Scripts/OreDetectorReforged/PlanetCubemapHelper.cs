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

    static class PlanetCubemapHelper
    {
        public static string GetFaceMatInfix(int f)
        {
            switch (f)
            {
                case 0: return "front";
                case 1: return "back";
                case 2: return "right";
                case 3: return "left";
                case 4: return "up";
                case 5: return "down";
                default: return null;
            }
        }

        static Vector2 Project(float x, float y, float z)
        {
            if (z < 1E-20f)
                z = 1E-20f;
            return new Vector2(x / z, y / z);
        }

        public static Vector2 LocalToFace(Vector3 l, int f)
        {
            switch (f)
            {
                case 0: return Project(-l.X, -l.Y, -l.Z);
                case 1: return Project(l.X, -l.Y, l.Z);
                case 2: return Project(l.Z, -l.Y, -l.X);
                case 3: return Project(-l.Z, -l.Y, l.X);
                case 4: return Project(-l.X, -l.Z, l.Y);
                case 5: return Project(l.X, -l.Z, -l.Y);
                default: throw new Exception();
            }
        }

        public static Vector3 FaceToCube(Vector2 t, int f)
        {
            switch (f)
            {
                case 0: return new Vector3(-t.X, -t.Y, -1);
                case 1: return new Vector3(t.X, -t.Y, 1);
                case 2: return new Vector3(-1, -t.Y, t.X);
                case 3: return new Vector3(1, -t.Y, -t.X);
                case 4: return new Vector3(-t.X, 1, -t.Y);
                case 5: return new Vector3(t.X, -1, -t.Y);
                default: throw new Exception();
            }
        }

        public static Vector3 TexToCube(Vector2 tx, int width, int f) => FaceToCube((tx * 2 / width) - 1, f);
    }
}
