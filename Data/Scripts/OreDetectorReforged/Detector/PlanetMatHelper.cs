using System;
using System.IO;
using BigGustave;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    static class PlanetMatHelper
    {
        static BinaryReader ReadPlanetMat(string folderName, int f)
        {
            var file = "Data/PlanetDataFiles/" + folderName + "/" + PlanetCubemapHelper.GetFaceMatInfix(f) + "_mat.png";
            foreach (var mod in MyAPIGateway.Session.Mods)
                try
                {
                    return MyAPIGateway.Utilities.ReadBinaryFileInModLocation(file, mod);
                }
                catch
                {
                    // ignored
                }

            return MyAPIGateway.Utilities.ReadBinaryFileInGameContent(file);
        }

        public static Png LoadPlanetFacePng(string folderName, int f)
        {
            Png png;
            using (var filestream = ReadPlanetMat(folderName, f))
            {
                png = Png.Open(filestream.BaseStream);
            }

            return png;
        }

        public static double ShapeNormalZ(MyPlanet planet, Vector3D localSurface)
        {
            const float texStep = 1f / 2048 / 64;
            var f = (int)Base6Directions.GetClosestDirection(localSurface);
            var faceSize = (float)(planet.AverageRadius * Math.PI * .5);
            var mMapStepScale = faceSize * texStep;
            var fxy = PlanetCubemapHelper.LocalToFace(localSurface, f);
            var x = PlanetCubemapHelper.FaceToCube(new Vector2(fxy.X + 2 * texStep, fxy.Y), f);
            var y = PlanetCubemapHelper.FaceToCube(new Vector2(fxy.X, fxy.Y + 2 * texStep), f);
            var a = localSurface.Length();
            var b = planet.GetClosestSurfacePointLocal(ref x).Length();
            var c = planet.GetClosestSurfacePointLocal(ref y).Length();
            var zx = b - a;
            var zy = c - a;
            var norm = new Vector3(zx, zy, mMapStepScale);
            norm.Normalize();
            return norm.Z;
        }
    }
}