using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using BigGustave;
using System.IO;
using VRageMath;
using VRage.Game;
using VRage.Game.Components;
using System.Linq;
using Sandbox.Game.Entities;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class PlanetMatHelperTest : MySessionComponentBase
    {
        int counter;

        public override void UpdateAfterSimulation()
        {
            if (++counter % 10 != 0)
                return;
            try
            {
                var planet = MyGamePruningStructure.GetClosestPlanet(Session.Player.GetPosition());
                var l = (Vector3)Vector3D.Transform(Session.Player.GetPosition(), planet.GetViewMatrix());
                var f = (int)Base6Directions.GetClosestDirection(l);
                var fxy = PlanetCubemapHelper.LocalToFace(l, f);
                var surf = planet.GetClosestSurfacePointLocal(ref l);
                var png = PlanetMatHelper.materialPngs.Get(planet.Generator, f);
                var tx = Vector2I.Min(new Vector2I(png.Width - 1, png.Width - 1), Vector2I.Floor(fxy * png.Width / 2 + png.Width / 2));
                var px = png.GetPixel(tx.X, tx.Y);
                var ore = planet.Generator.OreMappings.FirstOrDefault(e => e.Value == px.B);
                var matgroup = planet.Generator.MaterialGroups.FirstOrDefault(e => e.Value == px.R);
                var rule = PlanetMatHelper.GetRule(planet, matgroup.MaterialRules, surf);
                var ruleIdx = Array.IndexOf(matgroup.MaterialRules, rule);
                var slope = Math.Acos(PlanetMatHelper.ShapeNormalZ(planet, surf)) * 180 / Math.PI;
                var gps = Session.GPS.Create(PlanetCubemapHelper.GetFaceMatInfix(f) + " " + tx + " " + ore?.Type + " " + rule?.Layers[0].Material + " " + Math.Round(slope) + " " + rule?.Slope.ToStringAcos(), "", Vector3D.Transform(surf, planet.WorldMatrix), true);
                gps.DiscardAt = Session.ElapsedPlayTime + TimeSpan.FromMilliseconds(160);
                Session.GPS.RemoveLocalGps(gps);
                Session.GPS.AddLocalGps(gps);
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(ModContext.ModName, e.ToString());
            }
        }
    }

    static class PlanetMatHelper
    {
        public static readonly PermaCache<MyPlanetGeneratorDefinition, int, Png> materialPngs = new PermaCache<MyPlanetGeneratorDefinition, int, Png>(LoadPlanetFacePng);

        public static MyPlanetMaterialPlacementRule GetRule(MyPlanet planet, MyPlanetMaterialPlacementRule[] materialRules, Vector3 surfacePos)
        {
            var length = surfacePos.Length();
            var height = (length - planet.MinimumRadius) / (planet.MaximumRadius - planet.MinimumRadius);
            var sinLat = (float)(surfacePos.Y / length);
            var cosSlope = (float)ShapeNormalZ(planet,surfacePos);
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

        static ulong GetGeneratedOres(MyPlanet planet)
        {
            string[] oreTypeNames;
            MyDefinitionManager.Static.GetOreTypeNames(out oreTypeNames);
            var stoneIdx = Array.IndexOf(oreTypeNames, "Stone");
            var generatedOres = 0ul;
            Action<string> Add = (material) =>
            {
                var i = Array.IndexOf(oreTypeNames, MyDefinitionManager.Static.GetVoxelMaterialDefinition(material).MinedOre);
                if (i != stoneIdx)
                    generatedOres |= 1ul << i;
            };
            foreach (var oreChannel in planet.Generator.OreMappings)
                Add(oreChannel.Type);
            foreach (var biome in planet.Generator.MaterialGroups)
                foreach (var rule in biome.MaterialRules)
                    foreach (var layer in rule.Layers)
                        Add(layer.Material);
            return generatedOres;
        }
        public static PermaCache<MyPlanet, ulong> planetGeneratedOres = new PermaCache<MyPlanet, ulong>(GetGeneratedOres);

        public static ulong GetGeneratedOres()
        {
            var universe = new BoundingSphereD(new Vector3D(), double.MaxValue);
            var vbs = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref universe, vbs);
            var union = 0ul;
            foreach (var vb in vbs)
            {
                if (vb.RootVoxel != vb)
                    continue;
                var planet = vb as MyPlanet;
                if (planet == null)
                    continue;
                var other = planetGeneratedOres.Get(planet);
                union |= other;
            }
            return union;
        }
    }
}
