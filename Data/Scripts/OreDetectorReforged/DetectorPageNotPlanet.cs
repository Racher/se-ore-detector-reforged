using VRageMath;
using Sandbox.Game.Entities;
using System.Collections;
using System;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using VRage.Voxels;

namespace OreDetectorReforged
{
    class DetectorPageNotPlanet : IDetectorPage
    {
        readonly MyVoxelBase vb;
        readonly BitArray[] pyramids;
        readonly byte[] orePalette = new byte[256];
        readonly MyStorageData storageData = new MyStorageData();
        readonly BitArray currMat = new BitArray(256);
        byte voxelContentMin;
        bool loaded;
        int topw;
        ushort page;
        BitArray pyramid;
        PriorityQueue<Node> pq;
        Vector3 storageLocal;

        public DetectorPageNotPlanet(MyVoxelBase vb)
        {
            storageData.Resize(Vector3I.One * 4);
            var whitelist = vb.BoulderInfo != null
                ? MaterialMappingHelper.Static.planetWhitelists[(MyAPIGateway.Entities.GetEntityById(vb.BoulderInfo.Value.PlanetId) as MyPlanet).Generator]
                : MaterialMappingHelper.Static.asteroidWhitelist;
            pyramids = new BitArray[MaterialMappingHelper.Static.naturalOres.Length];
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {
                var ore = MaterialMappingHelper.Static.matIdxToOreIdx[mat.Index];
                if (ore == 255 || !whitelist[ore])
                    continue;
                orePalette[mat.Index] = (byte)(ore + 1);
                if (pyramids[ore] == null)
                    pyramids[ore] = new BitArray(10, true);
            }
            this.vb = vb;
        }

        public void Setup(PriorityQueue<Node> pq, Vector3D center, int page, int ore)
        {
            using (var pin = vb.Pin())
            {
                if (vb.Closed)
                    return;
                pyramid = pyramids[ore];
                if (pyramid == null)
                    return;
                currMat.SetAll(false);
                foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                    currMat[mat.Index] = MaterialMappingHelper.Static.matIdxToOreIdx[mat.Index] == ore;
                this.pq = pq;
                this.page = (ushort)page;
                storageLocal = Vector3D.Transform(center, vb.GetViewMatrix()) + vb.Storage.Size / 2;
                TryPush(0, 0, 0, 0);
            }
        }

        void Load()
        {
            voxelContentMin = ConfigLoader.Static.voxelContentMinNotPlanet;
            loaded = true;
            var itmax = (vb.Size - 1) / 16;
            var resolution = Math.Max(2, itmax.AbsMax() + 1);
            topw = 6;
            while (1 << topw - 1 >= resolution)
                --topw;
            foreach (var pyramid in pyramids)
                if (pyramid != null)
                    pyramid.SetAll(false);
            var count = 0;
            for (var vit = new Vector3I_RangeIterator(ref Vector3I.Zero, ref itmax); vit.IsValid(); vit.MoveNext())
            {
                var min = vit.Current * 4;
                vb.Storage.ReadRange(storageData, MyStorageDataTypeFlags.ContentAndMaterial, 2, min, min + 3);
                for (var i = 0; i < storageData.SizeLinear; ++i)
                {
                    byte m, o;
                    if (storageData.Content(i) < voxelContentMin || (m = storageData.Material(i)) == 255 || (o = orePalette[m]) == 0)
                        continue;
                    --o;
                    var pyramid = pyramids[o];
                    if (pyramid.Length == 10)
                        pyramid.Length = IndexToPyarmidLinear(0, 0, 0, topw + 1);
                    var c = vit.Current;
                    for (var z = topw; z >= 0; --z, c >>= 1)
                        pyramid[IndexToPyarmidLinear(c.X, c.Y, c.Z, z)] = true;
                    ++count;
                    break;
                }
            }
        }

        float GetDistance(int x, int y, int z, int w)
        {
            var step = 16 << (topw - w);
            var min = new Vector3(x, y, z) * step;
            var max = min + step;
            return new BoundingBox(min, max).Distance(storageLocal);
        }

        bool GetPosition(int x, int y, int z, out Vector3 pos)
        {
            var min = new Vector3I(x, y, z) * 4;
            var mind2 = float.MaxValue;
            pos = new Vector3();
            vb.Storage.ReadRange(storageData, MyStorageDataTypeFlags.ContentAndMaterial, 2, min, min + 3);
            for (var i = 0; i < storageData.SizeLinear; ++i)
            {
                if (storageData.Content(i) < voxelContentMin || !currMat[storageData.Material(i)])
                    continue;
                Vector3I p;
                storageData.ComputePosition(i, out p);
                var q = ((p + min) << 2) + 1f;
                var d2 = Vector3.DistanceSquared(q, storageLocal);
                if (d2 >= mind2)
                    continue;
                mind2 = d2;
                pos = q;
            }
            return mind2 != float.MaxValue;
        }

        public Vector3D Pop()
        {
            var i = pq.Top.y << 16 | pq.Top.x;
            pq.Pop();
            using (var pin = vb.Pin())
            {
                if (vb.Closed)
                    return Vector3D.Zero;
                if (!loaded)
                    Load();
                int x, y, z, w;
                DecompressPyramidIndex(i, out x, out y, out z, out w);
                if (w == topw)
                {
                    Vector3 pos;
                    if (GetPosition(x, y, z, out pos))
                        return Vector3D.Transform(pos - vb.Storage.Size / 2, vb.WorldMatrix);
                }
                else
                {
                    var any = false;
                    for (var dz = 0; dz < 2; ++dz)
                        for (var dy = 0; dy < 2; ++dy)
                            for (var dx = 0; dx < 2; ++dx)
                                any = TryPush(2 * x + dx, 2 * y + dy, 2 * z + dz, w + 1) | any;
                    if (any)
                        return Vector3D.Zero;
                }
                pyramid[i] = false;
            }
            return Vector3D.Zero;
        }

        bool TryPush(int x, int y, int z, int w)
        {
            var i = IndexToPyarmidLinear(x, y, z, w);
            if (!pyramid[i])
                return false;
            pq.Push(new Node(GetDistance(x, y, z, w), page, (ushort)i, (ushort)(i >> 16), 0));
            return true;
        }

        static int IndexToPyarmidLinear(int x, int y, int z, int w) => (((1 << (3 * w)) - 1) / 7) + (z << w << w) + (y << w) + x;

        static void DecompressPyramidIndex(int i, out int x, out int y, out int z, out int w)
        {
            var j = i;
            for (w = 0; i >= 1 << 3 * w; ++w)
                i -= 1 << 3 * w;
            var mask = (1 << w) - 1;
            z = i >> 2 * w & mask;
            y = i >> 1 * w & mask;
            x = i >> 0 * w & mask;
            if (IndexToPyarmidLinear(x, y, z, w) != j)
                throw new Exception();
        }
    }
}
