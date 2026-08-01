using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    // Marks every boulder in range with a GPS labelled by its SectorId LOD bits, splitting them into the
    // two groups DetectorServer treats differently.
    //
    // Boulders whose (SectorId >> 51) is non-zero are skipped by the search entirely. That filter is purely
    // empirical — those entries do not correspond to voxels the player can see, and the reason was never
    // established (docs/engine-notes.md). This command is how to re-check it: run it, then fly to the
    // markers. Anything tagged "fake" that turns out to be a real, minable boulder means the filter is
    // wrong and the search is silently skipping valid targets.
    //
    // Trigger: "/odr boulders [range]" (default 30000 m).
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class BoulderCommand : MySessionComponentBase
    {
        const string Command = "/odr boulders";
        const string Tag = "OreDetector Boulders";
        const int DefaultRange = 30000;
        const int MaxMarkers = 100;

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
                if (rest.Length > 0 && !int.TryParse(rest, out range))
                {
                    MyAPIGateway.Utilities.ShowMessage(Tag, "Usage: /odr boulders [range]");
                    return;
                }

                MyAPIGateway.Utilities.ShowMessage(Tag, Mark(range));
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(Tag, e.ToString());
            }
        }

        static string Mark(int range)
        {
            if (MyAPIGateway.Session.Camera == null) return "No camera";

            var candidates = new List<MyVoxelBase>();
            var sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, candidates);

            var real = 0;
            var fake = 0;
            var marked = 0;
            foreach (var voxel in candidates)
            {
                if (voxel.Closed || !voxel.BoulderInfo.HasValue) continue;
                var sectorLod = voxel.BoulderInfo.Value.SectorId >> 51;
                if (sectorLod == 0) real++;
                else fake++;

                if (marked >= MaxMarkers) continue;
                marked++;
                var position = Vector3D.Transform(Vector3D.Zero, voxel.WorldMatrix);
                var name = sectorLod == 0
                    ? $"boulder {voxel.Storage.Size.AbsMax()}m"
                    : $"boulder FAKE sectorLod={sectorLod}";
                var gps = MyAPIGateway.Session.GPS.Create(name, "", position, true);
                gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(120);
                MyAPIGateway.Session.GPS.RemoveLocalGps(gps);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);
            }

            return $"{real} searched, {fake} skipped as fake, {marked} marked" +
                   (real + fake > MaxMarkers ? $" (capped at {MaxMarkers})" : "");
        }
    }
}
