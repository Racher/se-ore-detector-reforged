using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;
using VRageRender;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector.Commands
{
    // Visualizes ore voxels at one or more LODs as wireframe boxes around the player.
    //
    // Trigger: chat command "/odr lod <lod>[,<lod>...]" (e.g. "/odr lod 1,2,4").
    // "/odr lod" with no args clears the visualization.
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class LodCommand : MySessionComponentBase
    {
        const string Command = "/odr lod";
        const byte VoxelContentMin = 1;
        const int Range = 64;
        const bool IncludeIce = true;

        static readonly Color[] Palette =
        {
            Color.Red, Color.Green, Color.Yellow, Color.Cyan, Color.Magenta, Color.Blue, Color.White
        };

        static ValueTuple<BoundingBoxD, MatrixD>[][] _drawLayers;

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
            var rest = messageText.Substring(Command.Length).Trim();
            sendToOthers = false;

            try
            {
                if (rest.Length == 0)
                {
                    _drawLayers = null;
                    MyAPIGateway.Utilities.ShowMessage(typeof(LodCommand).FullName, "Cleared");
                    return;
                }

                var lods = ParseLods(rest);
                if (lods == null)
                {
                    MyAPIGateway.Utilities.ShowMessage(typeof(LodCommand).FullName,
                        "Usage: /odr lod <lod>[,<lod>...] e.g. /odr lod 1,2,4");
                    return;
                }

                if (MyAPIGateway.Session.Camera == null) return;
                var cameraPos = MyAPIGateway.Session.Camera.Position;
                var targets = FindNearby(cameraPos);

                var layers = new ValueTuple<BoundingBoxD, MatrixD>[lods.Length][];
                var label = new StringBuilder();
                for (var i = 0; i < lods.Length; i++)
                {
                    layers[i] = CollectOrePoints(cameraPos, targets, lods[i]);
                    if (i > 0) label.Append(' ');
                    label.Append("lod").Append(lods[i]).Append("pts=").Append(layers[i].Length);
                }

                _drawLayers = layers;
                MyAPIGateway.Utilities.ShowMessage(typeof(LodCommand).FullName, label.ToString());
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(typeof(LodCommand).FullName, e.ToString());
            }
        }

        static int[] ParseLods(string arg)
        {
            var parts = arg.Split(',');
            var result = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                int v;
                if (!int.TryParse(parts[i].Trim(), out v) || v < 0 || v > 8)
                    return null;
                result[i] = v;
            }

            return result;
        }

        static List<MyVoxelBase> FindNearby(Vector3D cameraPos)
        {
            var result = new List<MyVoxelBase>();
            var sphere = new BoundingSphereD(cameraPos, Range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, result);
            return result;
        }

        static ValueTuple<BoundingBoxD, MatrixD>[] CollectOrePoints(Vector3D cameraPos, List<MyVoxelBase> targets,
            int lod)
        {
            var hasOre = new bool[256];
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                hasOre[mat.Index] = !string.IsNullOrEmpty(mat.MinedOre) && !mat.MinedOre.Equals("Stone") &&
                                    (!IncludeIce || !mat.MinedOre.Equals("Ice"));

            var points = new List<ValueTuple<BoundingBoxD, MatrixD>>();
            foreach (var target in targets)
            {
                if (target.RootVoxel != target) continue;
                var size = target.Storage.Size;
                var storageCenter = Vector3I.Round((Vector3)Vector3D.Transform(cameraPos, target.GetViewMatrix())) +
                                    size / 2;
                var min = Vector3I.Max(Vector3I.Zero, storageCenter - Range) >> lod;
                var max = Vector3I.Min(size - 1, storageCenter + Range) >> lod;
                var data = new MyStorageData();
                data.Resize(max - min + 1);
                using (target.Pin())
                {
                    if (target.Closed)
                        continue;
                    target.Storage.ReadRange(data, MyStorageDataTypeFlags.ContentAndMaterial, lod, min, max);
                }

                var worldMatrix = target.WorldMatrix;
                for (var i = 0; i < data.SizeLinear; i++)
                {
                    if (data.Content(i) < VoxelContentMin || !hasOre[data.Material(i)])
                        continue;
                    Vector3I p;
                    data.ComputePosition(i, out p);
                    var localCenter = ((p + min) << lod) - size / 2;
                    var halfSize = (1 << lod) * 0.5;
                    var boundingBoxD = new BoundingBoxD(localCenter * 1.0 - halfSize, localCenter * 1.0 + halfSize);
                    points.Add(new ValueTuple<BoundingBoxD, MatrixD>(boundingBoxD, worldMatrix));
                }
            }

            return points.ToArray();
        }

        public override void Draw()
        {
            if (_drawLayers == null) return;
            for (var i = 0; i < _drawLayers.Length; i++)
                DrawPoints(_drawLayers[i], Palette[i % Palette.Length]);
        }

        static void DrawPoints(ValueTuple<BoundingBoxD, MatrixD>[] points, Color color)
        {
            if (points == null) return;

            var cameraPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            var sorted = new List<KeyValuePair<double, int>>(points.Length);
            sorted.AddRange(points.Select((pos, index) =>
                new KeyValuePair<double, int>(
                    Vector3D.DistanceSquared(cameraPos, pos.Item1.TransformFast(pos.Item2).Center), index)));

            sorted.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var pair in sorted.Take(100))
            {
                var pos = points[pair.Value];
                MySimpleObjectDraw.DrawTransparentBox(ref pos.Item2, ref pos.Item1, ref color,
                    MySimpleObjectRasterizer.Wireframe, 1, 0.02f, null, MyStringId.Get("Square"),
                    blendType: MyBillboard.BlendTypeEnum.PostPP);
            }
        }
    }
}