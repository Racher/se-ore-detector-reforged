using VRageMath;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.ModAPI;

namespace OreDetectorReforged
{
    class ChunkCollector
    {
        readonly List<MyVoxelBase> vbs = new List<MyVoxelBase>();
        readonly List<VoxelBaseInfo> vbis = new List<VoxelBaseInfo>();
        readonly List<BoundingBoxI> bbs = new List<BoundingBoxI>();
        readonly List<long> sortkeys = new List<long>();

        public void CollectVoxelBases(Vector3D position)
        {
            vbis.Clear();
            vbs.Clear();
            if (position == default(Vector3D))
                return;
            var sphere = new BoundingSphereD(position, 50000);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, vbs);
            foreach (var vb in vbs)
            {
                if (vb.RootVoxel != vb) continue;
                if (vb.VoxelSize != 1) continue;
                if (vb.Size.AbsMin() < 2) continue;
                if (vb.Size.AbsMin() < 2) continue;
                if ((vb as MyPlanet) != null && (position - (vb as IMyEntity).GetPosition()).Length() > (vb as MyPlanet).MaximumRadius + 5000) continue;
                vbis.Add(new VoxelBaseInfo(vb, MyTransforms.WorldToLod(vb, position, 1)));
            }
        }

        public void ListTopChunks(List<ChunkInfo> topChunks)
        {
            topChunks.Clear();
            if (vbis.Count == 0)
                return;
            var count = SessionComponent.config.seachVolumeLimit512MChunks;
            var doubleCount = 2 * count;
            var radMin = 1;
            var radMax = 100;
            for (var j = 0; j < 8; ++j)
            {
                var radi = radMin / 2 + radMax / 2;
                bbs.Clear();
                bbs.EnsureCapacity(vbis.Count);
                foreach (var vbi in vbis)
                {
                    var max = ((vbi.size1 - 1) >> 8) + 1;
                    var center = Vector3I.Round(vbi.center1 * (1f / 256));
                    bbs.Add(new BoundingBoxI(center - radi, center + radi).Intersect(new BoundingBoxI(Vector3I.Zero, max)));
                }
                var vol = 0f;
                for (var i = 0; i < bbs.Count; ++i)
                    if (bbs[i].IsValid)
                        vol += bbs[i].Volume();
                if (vol > doubleCount)
                    radMax = radi;
                else
                    radMin = radi;
            }
            var d2Max = radMin * radMin << 16;
            topChunks.EnsureCapacity(doubleCount);
            var sorter = new ListSorter<ChunkInfo>(sortkeys);
            sortkeys.EnsureCapacity(doubleCount);
            for (var i = 0; i < bbs.Count; ++i)
            {
                if (!bbs[i].IsValid)
                    continue;
                var vbi = vbis[i];
                var bb = bbs[i];
                var max = bb.Max - 1;
                var center = vbi.center1 - 128;
                for (var vit = new Vector3I_RangeIterator(ref bb.Min, ref max); vit.IsValid(); vit.MoveNext())
                {
                    var d = (vit.Current << 8) - center;
                    var d2 = (d * d).RectangularLength();
                    if (d2 > d2Max)
                        continue;
                    topChunks.Add(new ChunkInfo(vbi, vit.Current << 8, 8));
                    sorter.AddKey(d2);
                }
            }
            sorter.Sort(topChunks);
            if (topChunks.Count > count)
                topChunks.RemoveRange(count, topChunks.Count - count);
        }
    }
}
