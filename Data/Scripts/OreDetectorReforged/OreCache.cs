using VRageMath;
using System.Collections.Generic;
using System;

namespace OreDetectorReforged
{
    class OreCache
    {

        int next;
        CachePage[] pages = new CachePage[0];
        readonly Dictionary<ulong, int> cache = new Dictionary<ulong, int>(40000);
        public int OreCount { get; private set; }

        public OreCache()
        {
            EnsureCapacity(200);
        }

        public void Clear()
        {
            next = 0;
            OreCount = 0;
            cache.Clear();
            foreach (var page in pages)
                page.Clear();
        }

        public void EnsureCapacity(int capacity)
        {
            if (pages.Length >= capacity)
                return;
            next = pages.Length;
            Array.Resize(ref pages, capacity);
            for (var i = next; i < capacity; ++i)
                pages[i] = new CachePage(new List<ulong>(250), new List<V31Byte>(500), new List<V31Byte>(100));
        }

        public CachePage GetTopPage(ChunkInfo chunk) => GetTopPage(ref chunk);
        public CachePage GetTopPage(ref ChunkInfo chunk)
        {
            var key = HashChunk(ref chunk, 8);
            int pagei;
            CachePage page;
            if (cache.TryGetValue(key, out pagei))
                page = pages[pagei];
            else
            {
                pagei = next;
                next = (next + 1) % pages.Length;
                page = pages[pagei];
                foreach (var k in page.keys)
                    cache.Remove(k);
                OreCount -= page.ores.Count;
                page.Clear();
                var w = 6;
                while (w > 4 && chunk.vbi.size1.AbsMin() <= 2 << w)
                    --w;
                var max = Vector3I.Min(Vector3I.One << 8, chunk.vbi.size1) - 1 >> w;
                for (var vit = new Vector3I_RangeIterator(ref Vector3I.Zero, ref max); vit.IsValid(); vit.MoveNext())
                    page.searchs.Add(new V31Byte(vit.Current << w, w));
                page.keys.Add(key);
                cache.Add(key, pagei);
            }
            return page;
        }

        internal void AddOre(ChunkInfo chunk, V31Byte r)
        {
            var key = HashChunk(ref chunk, 8);
            int pagei;
            if (!cache.TryGetValue(key, out pagei))
                return;
            pages[pagei].ores.Add(r);
            ++OreCount;
        }

        internal void UpdateOre(ChunkInfo chunk, V31Byte r)
        {
            var old = new V31Byte(chunk.v, -chunk.w);
            if (old.Equals(r))
                return;
            var key = HashChunk(ref chunk, 8);
            int pagei;
            if (!cache.TryGetValue(key, out pagei))
                return;
            var ores = pages[pagei].ores;
            var i = ores.IndexOf(old);
            if (i == -1 || ores[i].W > r.W)
                return;
            ores[i] = r;
        }

        internal bool AddPending(ChunkInfo chunk)
        {
            var key = HashChunk(ref chunk, chunk.w);
            if (cache.ContainsKey(key))
                return false;
            var w = chunk.w;
            var page = GetTopPage(ref chunk);
            page.keys.Add(key);
            cache.Add(key, -1);
            page.searchs.Add(new V31Byte(chunk.v, chunk.w));
            return true;
        }
        static ulong HashChunk(ref ChunkInfo chunk, int w)
        {
            var v = chunk.v >> w;
            unchecked
            {
                var a = (ulong)v.X << 0 | (ulong)v.Y << 18 | (ulong)v.Z << 36 | (ulong)w << 54;
                a *= 0xc6a4a7935bd1e995UL;
                a ^= (ulong)chunk.vbi.entityId;
                a *= 0xc6a4a7935bd1e995UL;
                a ^= a >> 47;
                a *= 0xc6a4a7935bd1e995UL;
                return a;
            }
        }
    }
}
