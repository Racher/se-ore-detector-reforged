using System;
using System.Collections.Generic;
using System.Linq;
using BigGustave;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    // Draws where the planet ore model predicts veins to be: one line segment per _mat.png pixel in a patch
    // around the player, running from the top of the vein to its bottom.
    //
    // This is the validator for docs/planet-ore-model.md. Run "/odr excavate" first to strip non-ore voxels
    // at LOD0, then "/odr vein" — the drawn segments should thread through the exposed ore. Segments that
    // float in empty space mean the slope correction or the surface lookup is wrong, which is the failure
    // mode that cost the most time historically.
    //
    // Trigger: "/odr vein [range]" (default 64 m, matching the region /odr excavate exposes).
    //          "/odr vein 0" clears.
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class VeinCommand : MySessionComponentBase
    {
        const string Command = "/odr vein";
        const string Tag = "OreDetector Vein";
        const int DefaultRange = 64;
        const int Step = 4;

        static readonly Dictionary<string, Png> PngCache = new Dictionary<string, Png>();
        static readonly List<MyTuple<Vector3D, Vector3D>> Segments = new List<MyTuple<Vector3D, Vector3D>>();

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
            Segments.Clear();
            PngCache.Clear();
        }

        static void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith(Command)) return;
            sendToOthers = false;

            try
            {
                var rest = messageText.Substring(Command.Length).Trim();
                var range = DefaultRange;
                if (rest.Length > 0 && !int.TryParse(rest, out range))
                {
                    MyAPIGateway.Utilities.ShowMessage(Tag, "Usage: /odr vein [range]   (0 clears)");
                    return;
                }

                Segments.Clear();
                if (range <= 0)
                {
                    MyAPIGateway.Utilities.ShowMessage(Tag, "Cleared");
                    return;
                }

                MyAPIGateway.Utilities.ShowMessage(Tag, Collect(range));
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, e.ToString());
            }
        }

        static string Collect(int range)
        {
            if (MyAPIGateway.Session.Camera == null) return "No camera";
            var worldPos = MyAPIGateway.Session.Camera.Position;
            var planet = MyGamePruningStructure.GetClosestPlanet(worldPos);
            if (planet == null) return "No planet nearby";

            var local = (Vector3)Vector3D.Transform(worldPos, planet.GetViewMatrix());
            var localSurface = (Vector3)planet.GetClosestSurfacePointLocal(ref local);

            // Walk a tangent-plane grid around the surface point under the player.
            var tangentX = Vector3.Normalize(Vector3.CalculatePerpendicularVector(localSurface));
            var tangentY = Vector3.Normalize(Vector3.Cross(localSurface, tangentX));

            var skippedThin = 0;
            for (var j = -range; j < range; j += Step)
                for (var i = -range; i < range; i += Step)
                {
                    var p = localSurface + tangentX * j + tangentY * i;
                    var face = (int)Base6Directions.GetClosestDirection(p);
                    var png = GetFace(planet.Generator.FolderName, face);
                    var faceXy = Vector2.Clamp(PlanetCubemapHelper.LocalToFace(p, face), -Vector2.One, Vector2.One);
                    var texel = Vector2I.Min(new Vector2I(png.Width - 1, png.Width - 1),
                        Vector2I.Floor(faceXy * png.Width / 2 + png.Width / 2));
                    var blue = png.GetPixel(texel.X, texel.Y).B;

                    var ore = planet.Generator.OreMappings.FirstOrDefault(e => e.Value == blue);
                    if (ore == null) continue;
                    if (ore.Depth < 1.5f)
                    {
                        skippedThin++;
                        continue;
                    }

                    var surface = planet.GetClosestSurfacePointLocal(ref p);
                    var surfaceRadius = surface.Length();
                    var cosSlope = PlanetMatHelper.ShapeNormalZ(planet, surface);

                    // Depths are perpendicular to the surface; divide by cosSlope to get the radial offset.
                    // Half a metre of slack at each end so the segment spans the whole vein.
                    var top = ore.Start / cosSlope - .5;
                    var bottom = (ore.Start + ore.Depth) / cosSlope + .5;
                    var dir = Vector3D.Normalize(surface);
                    Segments.Add(new MyTuple<Vector3D, Vector3D>(
                        Vector3D.Transform(dir * (surfaceRadius - top), planet.WorldMatrix),
                        Vector3D.Transform(dir * (surfaceRadius - bottom), planet.WorldMatrix)));
                }

            return $"{Segments.Count} vein segments in {range}m" +
                   (skippedThin > 0 ? $" ({skippedThin} thin veins skipped)" : "");
        }

        public override void Draw()
        {
            if (Segments.Count == 0) return;
            var square = MyStringId.GetOrCompute("Square");
            foreach (var segment in Segments)
            {
                var delta = segment.Item2 - segment.Item1;
                var length = delta.Length();
                if (length < 1e-3) continue;
                MyTransparentGeometry.AddLineBillboard(square, Color.Green, segment.Item1,
                    (Vector3)(delta / length), (float)length, .2f);
            }
        }

        static Png GetFace(string folderName, int face)
        {
            var key = folderName + "/" + face;
            Png png;
            if (PngCache.TryGetValue(key, out png)) return png;
            png = PlanetMatHelper.LoadPlanetFacePng(folderName, face);
            PngCache[key] = png;
            return png;
        }
    }
}
