using Sandbox.ModAPI;
using System;
using VRageMath;
using VRage.Game.Components;
using System.Linq;
using Sandbox.Game.Entities;

namespace OreDetectorReforged.Detector.Test
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
}
