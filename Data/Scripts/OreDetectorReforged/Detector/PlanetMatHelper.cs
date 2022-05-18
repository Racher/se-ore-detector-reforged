using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using BigGustave;
using System.IO;
using VRageMath;
using VRage.Game;
using System.Linq;
using Sandbox.Game.Entities;

namespace OreDetectorReforged.Detector
{
    static class PlanetMatHelper
    {
        public static readonly PermaCache<MyPlanetGeneratorDefinition, int, Png> materialPngs = new PermaCache<MyPlanetGeneratorDefinition, int, Png>(LoadPlanetFacePng);

        public static MyPlanetMaterialPlacementRule GetRule(MyPlanet planet, MyPlanetMaterialPlacementRule[] materialRules, Vector3 surfacePos)
        {
            var length = surfacePos.Length();
            var height = (length - planet.MinimumRadius) / (planet.MaximumRadius - planet.MinimumRadius);
            var sinLat = (float)(surfacePos.Y / length);
            var cosSlope = (float)ShapeNormalZ(planet, surfacePos);
            return materialRules.FirstOrDefault(r => r.Check(height, sinLat, cosSlope));
        }

        static BinaryReader MyReadPlanetMat(MyPlanetGeneratorDefinition planet, int f)
        {
            var file = "Data/PlanetDataFiles/" + planet.FolderName + "/" + PlanetCubemapHelper.GetFaceMatInfix(f) + "_mat.png";
            foreach (var mod in MyAPIGateway.Session.Mods)
            {
                try { return MyAPIGateway.Utilities.ReadBinaryFileInModLocation(file, mod); }
                catch { }
            }
            return MyAPIGateway.Utilities.ReadBinaryFileInGameContent(file);
        }

        public static Png LoadPlanetFacePng(MyPlanetGeneratorDefinition planet, int f)
        {
            Png png;
            using (var filestream = MyReadPlanetMat(planet, f))
                png = Png.Open(filestream.BaseStream);
            return png;
        }

        public static double ShapeNormalZ(MyPlanet planet, Vector3D localSurface)
        {
            const float texStep = 1f / 2048 / 64;
            var f = (int)Base6Directions.GetClosestDirection(localSurface);
            float faceSize = (float)(planet.AverageRadius * Math.PI * .5);
            var m_mapStepScale = faceSize * texStep;
            var fxy = PlanetCubemapHelper.LocalToFace(localSurface, f);
            var x = PlanetCubemapHelper.FaceToCube(new Vector2(fxy.X + 2 * texStep, fxy.Y), f);
            var y = PlanetCubemapHelper.FaceToCube(new Vector2(fxy.X, fxy.Y + 2 * texStep), f);
            var a = localSurface.Length();
            var b = planet.GetClosestSurfacePointLocal(ref x).Length();
            var c = planet.GetClosestSurfacePointLocal(ref y).Length();
            var zx = b - a;
            var zy = c - a;
            var norm = new Vector3(zx, zy, m_mapStepScale);
            norm.Normalize();
            return norm.Z;
        }

        public static float GetHeight(MyPlanet planet, Vector3 cpos, float depth)
        {
            var spos = planet.GetClosestSurfacePointLocal(ref cpos);
            return (float)(spos.Length() - depth / ShapeNormalZ(planet, spos));
        }
    }
}
