using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Voxels;
using VRageMath;

namespace OreDetectorReforged.Detector
{
    class DetectorPagePlanet : MyComponentBase, IDetectorPage
    {
        // levelOffsets[z] = (4^z - 1) / 3 for 32x32 grid (topZ=5); nodes encode (z<<10|cy<<5|cx)
        static readonly int[] LevelOffsets = { 0, 1, 5, 21, 85, 341, 1365 };
        static readonly MyStorageData StorageData = new MyStorageData();
        readonly float _averageRadius;
        readonly byte[] _blueToDepthIndex;
        readonly byte[] _blueToOreIndex;

        readonly OreCaches[] _byOre = new OreCaches[MaterialMappingHelper.NaturalOreCount];
        readonly bool[] _faceLoaded = new bool[6];
        readonly string _folderName;
        readonly float _maximumRadius;
        readonly MyPlanet _planet;
        readonly Vector3I _storageSize;
        readonly int _subGrid;
        MatrixD _viewMatrix;
        MatrixD _worldMatrix;

        DetectorPagePlanet(MyPlanet planet)
        {
            _planet = planet;
            StorageData.Resize(Vector3I.One * 3);
            _viewMatrix = planet.GetViewMatrix();
            _worldMatrix = planet.WorldMatrix;
            _storageSize = planet.Size;
            _maximumRadius = planet.MaximumRadius;
            _averageRadius = planet.AverageRadius;
            _subGrid = Math.Max(1, Math.Min(4, (int)Math.Round(_averageRadius / 15000f)));
            _folderName = planet.Generator.FolderName;

            _blueToOreIndex = new byte[256];
            _blueToDepthIndex = new byte[256];
            for (var i = 0; i < 256; ++i)
                _blueToOreIndex[i] = 255;
            var p = new List<float>[256];
            foreach (var oreChannel in planet.Generator.OreMappings)
            {
                if (oreChannel.Depth < 1.5f) continue; // Not accurate enough to be reliably found
                var ore = MaterialMappingHelper.MatIdxToOreIdx[MyDefinitionManager.Static.GetVoxelMaterialDefinition(oreChannel.Type).Index];
                if (ore == 255) continue;
                _blueToOreIndex[oreChannel.Value] = ore;
                var d = oreChannel.Start + oreChannel.Depth - 1f;
                if (p[ore] == null)
                    p[ore] = new List<float>();
                var g = p[ore].IndexOf(d);
                if (g == -1)
                {
                    g = p[ore].Count;
                    if (g == 15)
                        continue;

                    p[ore].Add(d);
                }

                _blueToDepthIndex[oreChannel.Value] = (byte)g;
            }

            for (var i = 0; i < p.Length; ++i)
                if (p[i] != null)
                    _byOre[i] = new OreCaches(p[i].ToArray());
        }

        public void PushRoot(float radius, PriorityQueue<Node, Node.Comparer> pq, int currOre, int page, Vector3 centerLocal)
        {
            if (_byOre[currOre] == null) return;
            for (var face = 0; face < 6; face++)
            {
                var d = GetDistance(0, 0, 1, face, centerLocal);
                if (d > radius) continue;
                var oc = _byOre[currOre].Faces[face];
                if (oc != null && !oc.Pyramid[0])
                    continue;
                pq.Push(new Node(d, page, 0, face));
            }
        }

        public void Process(ref Node cell, SearchTask task, PriorityQueue<Node, Node.Comparer> pq, Vector3 centerLocal)
        {
            var face = cell.Face;
            if (!_faceLoaded[face])
                using (_planet.Pin())
                {
                    if (_planet.Closed) return;
                    LoadFace(face);
                }

            var oc = _byOre[task.CurrOre]?.Faces[face];
            if (oc == null) return;

            // Decode quadtree node: i = (z<<10) | (cy<<5) | cx
            var z = (int)(cell.I >> 10);
            var ny = (int)((cell.I >> 5) & 31);
            var nx = (int)(cell.I & 31);

            // Leaf: search this grid cell directly
            if (z == 5)
            {
                var tryGrid = TryGrid(task, oc, face, nx, ny, centerLocal);
                if (!tryGrid && oc.GridCounts[nx + ny * 32] == 0)
                    oc.Pyramid[LevelOffsets[z] + ny * (1 << z) + nx] = false;
                return;
            }

            // Intermediate: push children whose pyramid bit is set; clear parent if none remain
            var nextZ = z + 1;
            var childOffset = LevelOffsets[nextZ];
            var childDim = 1 << nextZ;
            var pushedAny = false;
            for (var dy = 0; dy < 2; dy++)
                for (var dx = 0; dx < 2; dx++)
                {
                    var cx = nx * 2 + dx;
                    var cy = ny * 2 + dy;
                    if (!oc.Pyramid[childOffset + cy * childDim + cx])
                        continue;
                    pushedAny = true;
                    var d = GetDistance(cx, cy, 1 << nextZ, face, centerLocal);
                    if (d < task.Distance)
                        pq.Push(new Node(d, cell.P, (nextZ << 10) | (cy << 5) | cx, face));
                }

            if (!pushedAny)
                oc.Pyramid[LevelOffsets[z] + ny * (1 << z) + nx] = false;
        }

        public Vector3 WorldToLocal(Vector3D v)
        {
            return Vector3D.Transform(v, ref _viewMatrix);
        }

        internal static IDetectorPage GetOrCreate(MyPlanet planet)
        {
            var o = planet.Components.Get<DetectorPagePlanet>();
            if (o == null)
                planet.Components.Add(o = new DetectorPagePlanet(planet));
            return o;
        }

        internal long EstimateMemoryBytes()
        {
            long total = 0;
            foreach (var oc in _byOre)
            {
                if (oc == null) continue;
                total += oc.DepthPalette.Length * 4;
                for (var f = 0; f < 6; f++)
                {
                    var c = oc.Faces[f];
                    if (c == null) continue;
                    total += c.Pixels.Length * 2;
                    total += c.GridStarts.Length * 2;
                    total += c.GridCounts.Length;
                    total += (c.Pyramid.Length + 7) / 8;
                    if (c.LocalPixels != null)
                        total += c.LocalPixels.Capacity * 4;
                }
            }

            return total;
        }

        void LoadFace(int face)
        {
            if (_faceLoaded[face]) return;
            var loadSw = Stopwatch.StartNew();

            var png = PlanetMatHelper.LoadPlanetFacePng(_folderName, face);

            var tmpOreLists = new List<ushort>[MaterialMappingHelper.NaturalOreCount][];
            if (png.Width == 2048 && png.Height == 2048)
                for (var y = 0; y < 2048; ++y)
                    for (var x = 0; x < 2048; ++x)
                    {
                        var px = png.GetPixel(x, y);
                        var ore = _blueToOreIndex[px.B];
                        if (ore == 255) continue;
                        var ol = tmpOreLists[ore] ?? (tmpOreLists[ore] = new List<ushort>[32 * 32]);
                        var ci = (x >> 6) | ((y >> 6) << 5);
                        var ocl = ol[ci] ?? (ol[ci] = new List<ushort>());
                        if (ocl.Count < 255)
                            ocl.Add((ushort)(((y & 63) << 10) | ((x & 63) << 4) | _blueToDepthIndex[px.B]));
                    }

            var orePixelsByOre = new ushort[256][];
            var gridStartsByOre = new ushort[256][];
            var gridCountsByOre = new byte[256][];
            for (var o = 0; o < tmpOreLists.Length; ++o)
            {
                if (tmpOreLists[o] == null) continue;
                var orePixelsList = new List<ushort>();
                var dedupMap = new Dictionary<int, List<int>>();
                var gs = gridStartsByOre[o] = new ushort[32 * 32];
                var gc = gridCountsByOre[o] = new byte[32 * 32];
                for (var ci = 0; ci < 32 * 32; ++ci)
                {
                    var list = tmpOreLists[o][ci];
                    if (list == null) continue;
                    list.Sort();
                    var h = ContentHash(list);
                    List<int> existingStarts;
                    dedupMap.TryGetValue(h, out existingStarts);
                    var matchStart = -1;
                    if (existingStarts != null)
                        foreach (var s in existingStarts)
                            if (ContentMatches(orePixelsList, s, list))
                            {
                                matchStart = s;
                                break;
                            }

                    if (matchStart >= 0)
                    {
                        gs[ci] = (ushort)matchStart;
                    }
                    else
                    {
                        var start = orePixelsList.Count;
                        if (start + list.Count > 65535) continue;

                        if (existingStarts == null)
                            dedupMap[h] = existingStarts = new List<int>();
                        existingStarts.Add(start);
                        gs[ci] = (ushort)start;
                        orePixelsList.AddRange(list);
                    }

                    gc[ci] = (byte)list.Count;
                }

                orePixelsByOre[o] = orePixelsList.ToArray();
            }

            for (var o = 0; o < 256; ++o)
            {
                if (gridCountsByOre[o] == null || _byOre[o] == null) continue;
                var gc = gridCountsByOre[o];
                var p = new BitArray(LevelOffsets[LevelOffsets.Length - 1]);
                var leafOffset = LevelOffsets[5];
                for (var ci = 0; ci < gc.Length; ci++)
                    if (gc[ci] > 0)
                        p[leafOffset + ci] = true;
                for (var z = 4; z >= 0; z--)
                {
                    var offset = LevelOffsets[z];
                    var childOffset = LevelOffsets[z + 1];
                    var dim = 1 << z;
                    var childDim = dim * 2;
                    for (var cy = 0; cy < dim; cy++)
                        for (var cx = 0; cx < dim; cx++)
                            if (p[childOffset + cy * 2 * childDim + cx * 2]
                                || p[childOffset + cy * 2 * childDim + cx * 2 + 1]
                                || p[childOffset + (cy * 2 + 1) * childDim + cx * 2]
                                || p[childOffset + (cy * 2 + 1) * childDim + cx * 2 + 1])
                                p[offset + cy * dim + cx] = true;
                }

                _byOre[o].Faces[face] = new OreCache(orePixelsByOre[o] ?? Array.Empty<ushort>(), gridStartsByOre[o], gc, p);
            }

            _faceLoaded[face] = true;
            DetectorServer.PlanetLoadTicks += loadSw.ElapsedTicks;
        }

        bool TryGrid(SearchTask res, OreCache oc, int face, int x, int y, Vector3 centerLocal)
        {
            var gridIdx = x + y * 32;
            int count = oc.GridCounts[gridIdx];
            int start = oc.GridStarts[gridIdx];
            if (count == 0)
                return false;

            var originFace = (PlanetCubemapHelper.LocalToFace(centerLocal, face) + 1) * 1024;
            var pixels = oc.Pixels;
            if (oc.LocalPixels == null)
                oc.LocalPixels = new List<uint>();
            var localPixels = oc.LocalPixels;
            var inLocal = start >= pixels.Length;

            while (count > 0)
            {
                var bestDist = float.MaxValue;
                for (var i = 0; i < count; i++)
                {
                    var vi = inLocal ? localPixels[start - pixels.Length + i] : pixels[start + i];
                    var xi = (x << 6) | (int)((vi >> 4) & 63);
                    var yi = (y << 6) | (int)((vi >> 10) & 63);
                    var d = Vector2.DistanceSquared(originFace, new Vector2(xi + 0.5f, yi + 0.5f));
                    if (d < bestDist)
                        bestDist = d;
                }

                var bdUpper = (float)Math.Sqrt(bestDist) + 0.5f * (float)Math.Sqrt(2.0) * (1f - 1f / _subGrid);
                bdUpper = Math.Max(bdUpper * bdUpper, bestDist);
                var bdj = float.MaxValue;
                var bx = 0;
                var by = 0;
                var bc = 0;
                var bMask = 0;
                var bj = 0;
                var bi = 0;
                for (var i = 0; i < count; i++)
                {
                    var vi = inLocal ? localPixels[start - pixels.Length + i] : pixels[start + i];
                    var xi = (x << 6) | (int)((vi >> 4) & 63);
                    var yi = (y << 6) | (int)((vi >> 10) & 63);
                    var d = Vector2.DistanceSquared(originFace, new Vector2(xi + 0.5f, yi + 0.5f));
                    if (d > bdUpper) continue;
                    var mask = (int)(vi >> 16);
                    for (var j = 0; j < _subGrid * _subGrid; ++j)
                    {
                        if ((mask & (1 << j)) > 0) continue;
                        var xj = xi * _subGrid + j % _subGrid;
                        var yj = yi * _subGrid + j / _subGrid;
                        var dj = Vector2.DistanceSquared(originFace, new Vector2(xj + 0.5f, yj + 0.5f) / _subGrid);
                        if (dj < bdj)
                        {
                            bi = i;
                            bdj = dj;
                            bx = xj;
                            by = yj;
                            bc = (int)(vi & 15);
                            bMask = mask;
                            bj = j;
                        }
                    }
                }

                if (TrySubPixel(bx, by, bc, face, res, centerLocal, _subGrid))
                    return true;

                if (!inLocal)
                {
                    var newStart = pixels.Length + localPixels.Count;
                    if (newStart > ushort.MaxValue)
                    {
                        oc.GridCounts[gridIdx] = 0;
                        return false;
                    }

                    for (var i = start; i < start + count; i++)
                        localPixels.Add(pixels[i]);
                    oc.GridStarts[gridIdx] = (ushort)newStart;
                    oc.GridCounts[gridIdx] = (byte)count;
                    start = newStart;
                    inLocal = true;
                }

                var localStart = start - pixels.Length;
                bMask ^= 1 << bj;
                localPixels[localStart + bi] |= (uint)bMask << 16;
                if (bMask == (1 << (_subGrid * _subGrid)) - 1)
                {
                    count--;
                    oc.GridCounts[gridIdx]--;
                    localPixels[localStart + bi] = localPixels[localStart + count];
                }
            }

            return false;
        }

        static int ContentHash(List<ushort> list)
        {
            var h = list.Count;
            foreach (var v in list)
                h = unchecked(h * 1000003 + v);
            return h;
        }

        static bool ContentMatches(List<ushort> pixels, int start, List<ushort> list)
        {
            for (var i = 0; i < list.Count; i++)
                if (pixels[start + i] != list[i])
                    return false;
            return true;
        }

        bool TrySubPixel(int x, int y, int w, int face, SearchTask task, Vector3 centerLocal, int sg)
        {
            Vector3 pL;
            Vector3I pV1;
            var depth = _byOre[task.CurrOre].DepthPalette[w];
            var up = Vector3.Normalize(PlanetCubemapHelper.TexToCube(new Vector2(x + 0.5f, y + 0.5f), sg << 11, face));
            using (_planet.Pin())
            {
                if (_planet.Closed)
                    return false;
                var surf = _planet.GetClosestSurfacePointLocal(ref up);
                var surfRad = (float)surf.Length();
                var cosSlope = (float)PlanetMatHelper.ShapeNormalZ(_planet, surf);
                var oreRad = surfRad - depth / cosSlope;
                pL = up * oreRad;
                pV1 = Vector3I.Clamp(Vector3I.Round(pL * 0.5f - (StorageData.Size3D - 1) * 0.5f) + _storageSize / 4, Vector3I.Zero, _storageSize - StorageData.Size3D);
                _planet.Storage.ReadRange(StorageData, MyStorageDataTypeFlags.ContentAndMaterial, 1, pV1, pV1 + StorageData.Size3D - 1);
            }

            var bestD = float.MaxValue;
            var bestV = default(Vector3I);
            for (var i = 0; i < StorageData.SizeLinear; ++i)
                if (StorageData.Content(i) >= 96 && MaterialMappingHelper.MatIdxToOreIdx[StorageData.Material(i)] == task.CurrOre)
                {
                    Vector3I match;
                    StorageData.ComputePosition(i, out match);
                    match += pV1;
                    var d = Vector3.DistanceSquared(pL * 0.5f, match);
                    if (!(d < bestD)) continue;
                    bestD = d;
                    bestV = match;
                }

            if (bestD < float.MaxValue)
            {
                var resLocal = bestV * 2f - _storageSize / 2f;
                var d = GetDistance(x, y, sg * 2048f, face, centerLocal) * task.QuasiNearest;
                if (d < task.Distance)
                {
                    task.Distance = d;
                    task.Vb = _planet;
                    task.LocalPos = resLocal;
                }

                return true;
            }

            if (DetectorServer.ShowMiss)
            {
                var missGps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.Transform(pL, ref _worldMatrix), true);
                missGps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(60);
                DetectorServer.Misses.Enqueue(missGps);
            }

            return false;
        }

        float GetDistance(int x, int y, float res, int face, Vector3 centerLocal)
        {
            var txScale = 2f / res;
            var tMin = new Vector2(x, y) * txScale - 1;
            var tMax = new Vector2(x + 1, y + 1) * txScale - 1;
            var texCorner = Vector2.Clamp(PlanetCubemapHelper.LocalToFace(centerLocal, face), tMin, tMax);
            var cornerUp = PlanetCubemapHelper.FaceToCube(texCorner, face).Normalized();
            var r0 = centerLocal.Length();
            var cosAngle = Vector3D.Dot(cornerUp, centerLocal) / r0;
            var dHori = cosAngle < 0.99
                ? (float)Math.Acos(Math.Max(-1, cosAngle)) * _averageRadius
                : Vector3.Distance(cornerUp, centerLocal / r0) * _averageRadius;
            return dHori + Math.Max(0, r0 - _maximumRadius);
        }

        class OreCaches
        {
            internal readonly float[] DepthPalette;
            internal readonly OreCache[] Faces = new OreCache[6];

            internal OreCaches(float[] depthPalette)
            {
                DepthPalette = depthPalette;
            }
        }

        class OreCache
        {
            internal readonly byte[] GridCounts;

            internal readonly ushort[] GridStarts;

            // pixels[i] = 16bit (x:6, y:6, depth:4) per ore pixel in dedup pool
            // gridStarts[ci] = start index into pixels for this cell
            // gridCounts[ci] = pixel count for this cell (<=255)
            // Identical cell lists share the same pixels slice
            internal readonly ushort[] Pixels;

            // Quadtree pyramid: pyramid[LevelOffsets[z] + cy*(1<<z) + cx] = true if subtree has ore
            // levelOffsets[z] = (4^z - 1) / 3; nodes encode (z<<10|cy<<5|cx)
            internal readonly BitArray Pyramid;

            // localPixels holds mutable copies of cells when sub-pixel exhaustion tracking is needed
            // upper 16 bits = exhausted sub-pixel mask; indices offset by pixels.Length
            internal List<uint> LocalPixels;

            internal OreCache(ushort[] pixels, ushort[] gridStarts, byte[] gridCounts, BitArray pyramid)
            {
                Pixels = pixels;
                GridStarts = gridStarts;
                GridCounts = gridCounts;
                Pyramid = pyramid;
            }
        }
    }
}