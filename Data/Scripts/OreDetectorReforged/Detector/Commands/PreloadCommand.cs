using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    // Forces every planet in range to decode and classify all six of its _mat.png faces, then reports what
    // that cost. Reproduces the ~750 ms per cube face baseline in docs/planet-load-perf.md.
    //
    // Faces load lazily, only when a search actually visits them, so a normal search touches one or two.
    // To reach all six this submits one search per natural ore from the planet centre: a search for an ore
    // that planet doesn't have finds nothing and therefore walks every face root, which is what loads them.
    // Everything goes through the public search API — no test-only entry points.
    //
    // Run "/odr perf reset" first for a clean window, and expect the game to hitch. Faces stay cached for
    // the session, so a second run reports near zero.
    //
    // Trigger: "/odr preload [range]" (default 10000000 m, i.e. anything loaded).
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class PreloadCommand : MySessionComponentBase
    {
        const string Command = "/odr preload";
        const string Tag = "OreDetector Preload";
        const double DefaultRange = 1e7;

        static int _outstanding;
        static int _planets;
        static int _errors;
        static long _startTicks;
        static long _startLoadTicks;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
        }

        static void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith(Command)) return;
            sendToOthers = false;

            try
            {
                var rest = messageText.Substring(Command.Length).Trim();
                var range = DefaultRange;
                if (rest.Length > 0 && !double.TryParse(rest, out range))
                {
                    MyAPIGateway.Utilities.ShowMessage(Tag, "Usage: /odr preload [range]");
                    return;
                }

                Start(range);
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, e.ToString());
            }
        }

        static void Start(double range)
        {
            if (MyAPIGateway.Session.Camera == null) return;
            if (_outstanding != 0)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, $"still running ({_outstanding} search(es) left)");
                return;
            }

            var ores = MaterialMappingHelper.NaturalOres;
            if (ores == null)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, "Detector is not yet initialized");
                return;
            }

            var candidates = new List<MyVoxelBase>();
            var sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, candidates);

            var planets = new List<MyPlanet>();
            foreach (var voxel in candidates)
            {
                var planet = voxel as MyPlanet;
                if (planet != null && planet.RootVoxel == planet)
                    planets.Add(planet);
            }

            if (planets.Count == 0)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, "no planets in range");
                return;
            }

            _planets = planets.Count;
            _errors = 0;
            _startTicks = Stopwatch.GetTimestamp();
            _startLoadTicks = DetectorServer.PlanetLoadTicks;
            _outstanding = planets.Count * ores.Length;

            MyAPIGateway.Utilities.ShowMessage(Tag,
                $"loading {planets.Count} planet(s) x {ores.Length} ore(s), expect a hitch...");

            foreach (var planet in planets)
            {
                // Centre on the planet with a radius past the surface so every face root is in range.
                var area = new BoundingSphereD(planet.PositionComp.WorldAABB.Center, planet.MaximumRadius * 2);
                foreach (var ore in ores)
                    DetectorServer.SubmitSearch(area, ore, OnSearchFinished);
            }
        }

        // Dispatched on the main thread by DetectorServer.UpdateAfterSimulation.
        static void OnSearchFinished(Vector3D position, Exception error)
        {
            if (error != null) _errors++;
            if (--_outstanding != 0) return;

            var msPerTick = 1000.0 / Stopwatch.Frequency;
            var wallMs = (Stopwatch.GetTimestamp() - _startTicks) * msPerTick;
            var loadMs = (DetectorServer.PlanetLoadTicks - _startLoadTicks) * msPerTick;
            var faces = _planets * 6;

            MyAPIGateway.Utilities.ShowMessage(Tag,
                $"done: {_planets} planet(s), {wallMs:N0} ms wall\n" +
                $"  face load: {loadMs:N0} ms total, ~{loadMs / faces:N0} ms/face over {faces} faces\n" +
                (_errors > 0 ? $"  {_errors} search(es) failed\n" : "") +
                "  (per-face figure assumes all faces were cold; re-run reports ~0)");
        }
    }
}
