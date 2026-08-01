using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BigGustave;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    // Reports everything the planet ore model derives at the player's location: which cube face and texel
    // the position maps to, what the _mat.png says there, and the surface values (slope, latitude, height)
    // that feed the vein-depth math in docs/planet-ore-model.md.
    //
    // This is the "why is there no ore here" tool. Stand on a spot, run the command, and compare what the
    // model claims against what /odr lod and /odr excavate show is actually in voxel storage.
    //
    // Trigger: "/odr planet". Also verifies that the cube-face conversions round-trip, so a regression in
    // PlanetCubemapHelper shows up as a non-zero error instead of as silently misplaced markers.
    //
    // Decoding a face PNG costs ~750 ms, so decoded faces are cached for the session.
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class PlanetProbeCommand : MySessionComponentBase
    {
        const string Command = "/odr planet";
        const string Tag = "OreDetector Planet";

        static readonly Dictionary<string, Png> PngCache = new Dictionary<string, Png>();

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
            PngCache.Clear();
        }

        static void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (messageText != Command) return;
            sendToOthers = false;

            try
            {
                Probe();
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, e.ToString());
            }
        }

        static void Probe()
        {
            if (MyAPIGateway.Session.Camera == null) return;
            var worldPos = MyAPIGateway.Session.Camera.Position;
            var planet = MyGamePruningStructure.GetClosestPlanet(worldPos);
            if (planet == null)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, "No planet nearby");
                return;
            }

            var local = (Vector3)Vector3D.Transform(worldPos, planet.GetViewMatrix());
            var face = (int)Base6Directions.GetClosestDirection(local);
            var faceXy = Vector2.Clamp(PlanetCubemapHelper.LocalToFace(local, face), -Vector2.One, Vector2.One);

            var png = GetFace(planet.Generator.FolderName, face);
            var width = png.Width;
            var texel = Vector2I.Min(new Vector2I(width - 1, width - 1),
                Vector2I.Floor(faceXy * width / 2 + width / 2));
            var pixel = png.GetPixel(texel.X, texel.Y);

            // Blue selects the ore; red is the biome channel (unsupported, reported for reference only).
            var ore = planet.Generator.OreMappings.FirstOrDefault(e => e.Value == pixel.B);

            var surface = planet.GetClosestSurfacePointLocal(ref local);
            var surfaceRadius = surface.Length();
            var cosSlope = PlanetMatHelper.ShapeNormalZ(planet, surface);
            var slopeDeg = Math.Acos(Clamp1(cosSlope)) * 180 / Math.PI;
            var latitudeDeg = Math.Asin(Clamp1(Vector3D.Normalize(surface).Y)) * 180 / Math.PI;
            var heightFraction = (surfaceRadius - planet.MinimumRadius) /
                                 Math.Max(1.0, planet.MaximumRadius - planet.MinimumRadius);

            var report = new StringBuilder();
            report.Append($"{planet.Generator.FolderName} face={PlanetCubemapHelper.GetFaceMatInfix(face)} ")
                .Append($"texel={texel.X},{texel.Y}/{width}\n")
                .Append($"  pixel: B={pixel.B} R={pixel.R}\n")
                .Append($"  surface: radius={surfaceRadius:N0}m lat={latitudeDeg:F1}deg ")
                .Append($"height={heightFraction:F2} slope={slopeDeg:F1}deg (cos={cosSlope:F3})\n");

            if (ore == null)
            {
                report.Append("  ore: none at this pixel");
            }
            else
            {
                // Matches DetectorPagePlanet: veins thinner than 1.5m are not generated as continuous voxels.
                var skipped = ore.Depth < 1.5f;
                var depth = ore.Start + ore.Depth - 1f;
                var oreRadius = surfaceRadius - depth / cosSlope;
                var orePos = Vector3D.Transform(Vector3D.Normalize(surface) * oreRadius, planet.WorldMatrix);
                report.Append($"  ore: {ore.Type} start={ore.Start}m depth={ore.Depth}m")
                    .Append(skipped ? " [SKIPPED: depth < 1.5]\n" : "\n")
                    .Append($"  target: {depth:F1}m below surface, {depth / cosSlope:F1}m radially");

                var gps = MyAPIGateway.Session.GPS.Create($"probe {ore.Type} d{depth:F0}m", "", orePos, true);
                gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(60);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);
            }

            var roundTripError = RoundTripErrorDeg(local, face, texel, width);
            if (roundTripError > 0.01)
                report.Append($"\n  WARNING: cubemap round-trip error {roundTripError:F3}deg");

            MyAPIGateway.Utilities.ShowMessage(Tag, report.ToString());
        }

        static double Clamp1(double v)
        {
            return Math.Max(-1.0, Math.Min(1.0, v));
        }

        // local -> face -> cube should return the original direction; texel -> cube should land within
        // half a texel of it. Non-zero means PlanetCubemapHelper is inconsistent with itself.
        static double RoundTripErrorDeg(Vector3 local, int face, Vector2I texel, int width)
        {
            var back = Vector3.Normalize(PlanetCubemapHelper.FaceToCube(PlanetCubemapHelper.LocalToFace(local, face), face));
            var direct = Vector3.Normalize(local);
            var viaTexel = Vector3.Normalize(
                PlanetCubemapHelper.TexToCube(new Vector2(texel.X + .5f, texel.Y + .5f), width, face));
            var texelDeg = Math.Acos(Clamp1(Vector3.Dot(direct, viaTexel))) * 180 / Math.PI;
            var halfTexelDeg = 90.0 / width;
            return Math.Max(
                Math.Acos(Clamp1(Vector3.Dot(direct, back))) * 180 / Math.PI,
                Math.Max(0, texelDeg - halfTexelDeg));
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
