using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Voxels;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class ExcavateCommand : MySessionComponentBase
    {
        const int Range = 64;

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
            if (messageText != "/odr excavate") return;
            sendToOthers = false;

            if (MyAPIGateway.Session.Camera == null) return;
            var targets = FindNearby();
            RemoveStoneAtLod0(targets);
            MyAPIGateway.Utilities.ShowMessage(typeof(ExcavateCommand).FullName, $"Removed stone in {targets.Count} voxel map(s)");
        }

        static List<MyVoxelBase> FindNearby()
        {
            var result = new List<MyVoxelBase>();
            var boundingSphereD = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, Range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref boundingSphereD, result);
            return result;
        }

        static void RemoveStoneAtLod0(List<MyVoxelBase> targets)
        {
            var hasOre = new bool[256];
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                hasOre[mat.Index] = !string.IsNullOrEmpty(mat.MinedOre) && !mat.MinedOre.Equals("Stone");

            foreach (var target in targets)
            {
                if (target.RootVoxel != target) continue;
                var size = target.Storage.Size;
                var storageCenter = Vector3I.Round((Vector3)Vector3D.Transform(MyAPIGateway.Session.Camera.Position, target.GetViewMatrix())) + size / 2;
                var min = Vector3I.Max(Vector3I.Zero, storageCenter - Range);
                var max = Vector3I.Min(size - 1, storageCenter + Range);
                var data = new MyStorageData();
                data.Resize(max - min + 1);
                using (target.Pin())
                {
                    if (target.Closed)
                        continue;
                    target.Storage.ReadRange(data, MyStorageDataTypeFlags.ContentAndMaterial, 0, min, max);
                }

                for (var i = 0; i < data.SizeLinear; i++)
                    if (!hasOre[data.Material(i)])
                        data.Content(i, 0);

                using (target.Pin())
                {
                    if (target.Closed)
                        continue;
                    target.Storage.WriteRange(data, MyStorageDataTypeFlags.Content, min, max);
                }
            }
        }
    }
}