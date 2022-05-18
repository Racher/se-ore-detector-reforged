using VRage.Game.Components;
using System;
using System.Linq;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRageMath;
using System.Collections.Concurrent;
using Sandbox.Game.Entities;
using Sandbox.Engine.Voxels.Planet;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.GameSystems;
using VRage.ModAPI;
using System.Collections;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;

namespace OreDetectorReforged.Detector.Test
{

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class PlanetOreGenerationTest : MySessionComponentBase
    {
        Vector3D prevp;

        static void Main()
        {
            lines.Clear();
            var wpos = MyAPIGateway.Session.Player.GetPosition();
            var planet = MyGamePruningStructure.GetClosestPlanet(wpos);
            if (planet == null)
                return;
            var lpos = (Vector3)Vector3D.Transform(wpos, planet.GetViewMatrix());
            var r = 100;
            {
                var localSurface = (Vector3)planet.GetClosestSurfacePointLocal(ref lpos);
                var x = Vector3.Normalize(Vector3.CalculatePerpendicularVector(localSurface));
                var y = Vector3.Normalize(Vector3.Cross(localSurface, x));
                var s = 4;
                for (var j = -r; j < r; j += s)
                    for (var i = -r; i < r; i += s)
                    {
                        var p = localSurface + x * j + y * i;

                        MyPlanetOreMapping ore = null;
                        {
                            var f = (int)Base6Directions.GetClosestDirection(p);
                            var png = PlanetMatHelper.materialPngs.Get(planet.Generator, f);
                            var fxy = PlanetCubemapHelper.LocalToFace(p, f);
                            var tx = Vector2I.Min(new Vector2I(png.Width - 1, png.Width - 1), Vector2I.Floor(fxy * png.Width / 2 + png.Width / 2));
                            var px = png.GetPixel(tx.X, tx.Y);
                            ore = planet.Generator.OreMappings.FirstOrDefault(e => e.Value == px.B);
                        }
                        if (ore == null)
                            continue;

                        var psurf = planet.GetClosestSurfacePointLocal(ref p);
                        var cosSlope = PlanetMatHelper.ShapeNormalZ(planet, psurf);
                        var start = ore.Start / cosSlope - 0.5f;
                        var end = (ore.Start + ore.Depth) / cosSlope + 0.5f;
                        var pend = Vector3D.Transform(psurf * (1 - end / psurf.Length()), planet.WorldMatrix);
                        var pstart = Vector3D.Transform(psurf * (1 - start / psurf.Length()), planet.WorldMatrix);

                        lines.Add(new KeyValuePair<Vector3D, Vector3D>(pstart, pend));
                    }
            }
            {
                var min = Vector3I.Round(lpos) + planet.Storage.Size / 2 - r;
                var max = min + 2 * r;
                var data = new VRage.Voxels.MyStorageData();
                data.Resize(Vector3I.One * (max + 1 - min));
                planet.Storage.ReadRange(data, VRage.Voxels.MyStorageDataTypeFlags.ContentAndMaterial, 0, min, max);
                var rareMaterial = new BitArray(256);
                foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                    if (mat.IsRare)
                        rareMaterial[mat.Index] = true;
                for (var i = 0; i < data.SizeLinear; ++i)
                {
                    if (rareMaterial[data.Material(i)]) continue;
                    Vector3I p;
                    data.ComputePosition(i, out p);
                    data.Set(VRage.Voxels.MyStorageDataTypeEnum.Content, ref p, 0);
                }
                planet.Storage.WriteRange(data, VRage.Voxels.MyStorageDataTypeFlags.Content, min, max);
            }
        }

        static List<KeyValuePair<Vector3D, Vector3D>> lines = new List<KeyValuePair<Vector3D, Vector3D>>();

        public override void Draw()
        {
            base.Draw();
            var matSquare = MyStringId.GetOrCompute("Square");
            foreach (var line in lines)
                MyTransparentGeometry.AddLineBillboard(matSquare, Color.Green, line.Key, Vector3D.Normalize(line.Value - line.Key), (float)(line.Value - line.Key).Length(), 0.2f);
        }

        public override void UpdateAfterSimulation()
        {
            var position = Session.Player.GetPosition();
            try
            {
                if (Vector3D.Distance(prevp, position) > 100)
                    Main();
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(ModContext.ModName, e.ToString());
            }
            prevp = position;
        }
    }
}
