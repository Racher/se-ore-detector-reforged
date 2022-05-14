using VRage.Game;
using System.Diagnostics;
using System;
using VRageMath;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using System.Collections;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Voxels;

namespace OreDetectorReforged
{
    class DetectorPagePlanet : IDetectorPage
    {
        byte voxelContentMin;
        int biomeRepeatDivisor;
        readonly MyPlanet planet;
        readonly int face;
        readonly int div;
        readonly BitArray[] orePyramids;
        readonly string[] oreTypeNames;
        readonly LinearCompressor heightCompressor;
        readonly MyStorageData storageData = new MyStorageData();
        int topz = 12;
        byte[] orePalette;
        byte[] matPalette;
        float[] depthPalette;
        byte[,] bitmap;
        byte[] heightMin;
        byte[] heightMax;
        Vector3I lod1BoundMin;
        ushort page;
        int currOre;
        PriorityQueue<Node> pq;
        Vector3 originLocal;

        public DetectorPagePlanet(MyPlanet planet, int face)
        {
            this.planet = planet;
            this.face = face;
            MyDefinitionManager.Static.GetOreTypeNames(out oreTypeNames);
            oreTypeNames[Array.IndexOf(oreTypeNames, "Stone")] = "a";
            var whitelist = PlanetMatHelper.planetGeneratedOres.Get(planet);
            orePyramids = new BitArray[oreTypeNames.Length];
            heightCompressor = LinearCompressor.FromMinMax(planet.MinimumRadius - planet.Generator.MaterialsMaxDepth.Max, planet.MaximumRadius);
            heightMin = new byte[] { 0 };
            heightMax = new byte[] { 255 };
            for (var o = 0; o < oreTypeNames.Length; ++o)
                if (whitelist[o])
                    orePyramids[o] = new BitArray(1, true);
            div = Math.Max(1, Math.Min(6, (int)Math.Round(planet.AverageRadius / 10000)));
        }

        public void Setup(PriorityQueue<Node> pq, Vector3D center, int page, int ore)
        {
            if (orePyramids[ore] == null)
                return;
            this.pq = pq;
            this.page = (ushort)page;
            currOre = ore;
            originLocal = Vector3D.Transform(center, planet.GetViewMatrix());
            var regioncenter = PlanetCubemapHelper.FaceToCube(new Vector2(), face) * planet.AverageRadius / 3;
            lod1BoundMin = Vector3I.Round(regioncenter - short.MaxValue);
            TryPush(0, 0, 0);
        }

        void Load()
        {
            voxelContentMin = ConfigLoader.Static.voxelContentMinPlanet;
            biomeRepeatDivisor = ConfigLoader.Static.biomeRepeatDivisor;
            storageData.Resize(Vector3I.One * 2);
            var t0 = Stopwatch.GetTimestamp();
            var png = PlanetMatHelper.LoadPlanetFacePng(planet.Generator, face);
            while (1 << topz - 1 >= png.Width)
                --topz;
            var width = Math.Min(Math.Min(png.Height, png.Width), 1 << topz);
            bitmap = new byte[width, width];
            var lastId = 0;
            matPalette = new byte[256];
            orePalette = new byte[256];
            depthPalette = new float[256];
            var palette = new PermaCache<byte, float, int>((mat, depth) =>
            {
                ++lastId;
                matPalette[lastId] = mat;
                orePalette[lastId] = (byte)Array.IndexOf(oreTypeNames, MyDefinitionManager.Static.GetVoxelMaterialDefinition(mat).MinedOre);
                depthPalette[lastId] = depth;
                return lastId;
            });
            var blueToGray = new byte[256];
            foreach (var oreChannel in planet.Generator.OreMappings)
            {
                var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(oreChannel.Type);
                if (Array.IndexOf(oreTypeNames, def.MinedOre) == -1)
                    continue;
                var depth = oreChannel.Start + oreChannel.Depth - Math.Min(2, oreChannel.Depth / 2);
                var gray = palette.Get(def.Index, depth);
                if (gray >= 256)
                    continue;
                blueToGray[oreChannel.Value] = (byte)gray;
            }
            var redToRules = new MyPlanetMaterialPlacementRule[256][];
            foreach (var biome in planet.Generator.MaterialGroups)
            {
                var rules = new MyPlanetMaterialPlacementRule[biome.MaterialRules.Length];
                for (var i = 0; i < rules.Length; ++i)
                {
                    var rule = rules[i] = new MyPlanetMaterialPlacementRule(biome.MaterialRules[i])
                    {
                        Value = 0
                    };
                    MyPlanetMaterialLayer orelayer = rule.Layers.LastOrDefault(layer => GetOreIdx(layer.Material) != 255);
                    if (orelayer.Equals(default(MyPlanetMaterialLayer)))
                        continue;
                    var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(orelayer.Material);
                    if (Array.IndexOf(oreTypeNames, def.MinedOre) == -1)
                        continue;
                    var depth = orelayer.Depth - 2;
                    var gray = palette.Get(def.Index, depth);
                    if (gray >= 256)
                        continue;
                    rule.Value = (byte)gray;
                    redToRules[biome.Value] = rules;
                }
            }
            for (var o = 0; o < oreTypeNames.Length; ++o)
            {
                if (orePyramids[o] == null)
                    continue;
                orePyramids[o].SetAll(false);
                orePyramids[o].Length = IndexToPyarmidLinear(0, 0, topz);
            }
            heightMax = new byte[IndexToPyarmidLinear(0, 0, topz)];
            heightMin = new byte[IndexToPyarmidLinear(0, 0, topz)];
            for (var i = 0; i < heightMin.Length; ++i)
                heightMin[i] = 255;
            var pxcounter = biomeRepeatDivisor * 10;
            var redcounter = new int[256];
            for (var y = 0; y < width; ++y)
                for (var x = 0; x < width; ++x)
                {
                    ++pxcounter;
                    var px = png.GetPixel(x, y);
                    var gray = blueToGray[px.B];
                    var biomeRules = redToRules[px.R];
                    if (gray == 0)
                    {
                        if (biomeRules == null || redcounter[px.R] * biomeRepeatDivisor > pxcounter)
                            continue;
                        ++redcounter[px.R];
                        var cpos = PlanetCubemapHelper.TexToCube(new Vector2(x + 0.5f, y + 0.5f), width, face);
                        var rule = PlanetMatHelper.GetRule(planet, biomeRules, planet.GetClosestSurfacePointLocal(ref cpos));
                        if (rule == null || rule.Value == 0)
                            continue;
                        gray = rule.Value;
                    }
                    bitmap[x, y] = gray;
                    var ore = orePalette[gray];
                    float hmin, hmax;
                    CalcPixelCornerHeights(x, y, out hmin, out hmax);
                    var l = heightCompressor.CompressLower(hmin);
                    var h = heightCompressor.CompressUpper(hmax);
                    for (var z = 0; z <= topz; ++z)
                    {
                        var i = IndexToPyarmidLinear(x << z >> topz, y << z >> topz, z);
                        if (i >= heightMin.Length)
                            continue;
                        orePyramids[ore][i] = true;
                        Min(ref heightMin[i], l);
                        Max(ref heightMax[i], h);
                    }
                }
            //MyAPIGateway.Utilities.ShowMessage("", planet.Generator.FolderName + " " + oreTypeNames[currOre] + " " + TimeSpan.FromTicks(Stopwatch.GetTimestamp() - t0));
        }

        public Vector3D Pop()
        {
            var cell = pq.Top;
            pq.Pop();
            if (bitmap == null)
            {
                try
                {
                    Load();
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage("OreDetectorReforged", "" + e);
                    Array.Clear(orePyramids, 0, orePyramids.Length);
                    return new Vector3D();
                }
            }
            var x = cell.x;
            var y = cell.y;
            var z = cell.z;
            if (z == topz)
            {
                var any = false;
                for (var cy = 0.5f / div; cy < 1; cy += 1f / div)
                    for (var cx = 0.5f / div; cx < 1; cx += 1f / div)
                    {
                        var gray = bitmap[x, y];
                        var depth = depthPalette[gray];
                        var mat = matPalette[gray];
                        var up = Vector3.Normalize(PlanetCubemapHelper.TexToCube(new Vector2(x + cx, y + cy), 1 << topz, face));
                        var surf = planet.GetClosestSurfacePointLocal(ref up);
                        var height = surf.Length() - depth / PlanetMatHelper.ShapeNormalZ(planet, surf);
                        var lpos = up * (float)height;
                        var plod1 = Vector3I.Floor(lpos / 2);
                        if (!HasMat(plod1, mat))
                            continue;
                        any = true;
                        var dist = Vector3.Distance(2 * plod1 + 1, originLocal);
                        plod1 -= lod1BoundMin;
                        pq.Push(new Node(dist, page, Convert.ToUInt16(plod1.X), Convert.ToUInt16(plod1.Y), Convert.ToUInt16(plod1.Z)));
                    }
                if (!any)
                    bitmap[x, y] = 0;
            }
            else if (z > topz)
                return Vector3D.Transform((lod1BoundMin + new Vector3I(x, y, z)) * 2.0 + 1, planet.WorldMatrix);
            else
            {
                var any = false;
                for (var dy = 0; dy < 2; ++dy)
                    for (var dx = 0; dx < 2; ++dx)
                        any = TryPush(2 * x + dx, 2 * y + dy, z + 1) | any;
                if (!any)
                    orePyramids[currOre][IndexToPyarmidLinear(x, y, z)] = false;
            }
            return Vector3D.Zero;
        }

        bool HasMat(Vector3I pos, byte mat)
        {
            var min = pos + (planet.Storage.Size >> 2);
            planet.Storage.ReadRange(storageData, MyStorageDataTypeFlags.ContentAndMaterial, 1, min, min + 1);
            for (var i = 0; i < storageData.SizeLinear; ++i)
                if (storageData.Content(i) >= voxelContentMin && storageData.Material(i) == mat)
                    return true;
            return false;
        }

        byte GetOreIdx(string material) => (byte)(material == null ? -1 : Array.IndexOf(oreTypeNames, MyDefinitionManager.Static.GetVoxelMaterialDefinition(material).MinedOre));

        static int IndexToPyarmidLinear(int x, int y, int z) => (((1 << (2 * z)) - 1) / 3) + (y << z) + x;

        static void Max(ref byte l, byte r)
        {
            if (r > l)
                l = r;
        }

        static void Min(ref byte l, byte r)
        {
            if (r < l)
                l = r;
        }
        static void Max(ref float l, float r)
        {
            if (r > l)
                l = r;
        }

        static void Min(ref float l, float r)
        {
            if (r < l)
                l = r;
        }

        bool TryPush(int x, int y, int z)
        {
            var i = IndexToPyarmidLinear(x, y, z);
            var pyramid = orePyramids[currOre];
            if (i >= pyramid.Length)
            {
                if (orePalette[bitmap[x, y]] != currOre)
                    return false;
            }
            else if (!pyramid[i])
                return false;
            pq.Push(new Node(GetDistance(x, y, z), page, (ushort)x, (ushort)y, (ushort)z));
            return true;
        }

        float GetDistance(int x, int y, int z)
        {
            var txscale = 2f / (1 << z);
            var tmin = new Vector2(x, y) * txscale - 1;
            var tmax = new Vector2(x + 1, y + 1) * txscale - 1;
            var fpos = Vector2.Clamp(PlanetCubemapHelper.LocalToFace(originLocal, face), tmin, tmax);
            var up = Vector3.Normalize(PlanetCubemapHelper.FaceToCube(fpos, face));
            var i = IndexToPyarmidLinear(x, y, z);
            float hmin, hmax;
            if (i < heightMin.Length)
            {
                hmin = heightCompressor.Decompress(heightMin[i]);
                hmax = heightCompressor.Decompress(heightMax[i]);
            }
            else
                CalcPixelCornerHeights(x, y, out hmin, out hmax);
            var height = Math.Min(hmax, Math.Max(hmin, Vector3.Dot(up, originLocal)));
            return Vector3.Distance(originLocal, height * up);
        }

        void CalcPixelCornerHeights(int x, int y, out float hmin, out float hmax)
        {
            var width = 1 << topz;
            var depth = depthPalette[bitmap[x, y]];
            hmin = float.MaxValue;
            hmax = 0;
            for (var dy = 0; dy < 2; ++dy)
                for (var dx = 0; dx < 2; ++dx)
                {
                    var cpos = PlanetCubemapHelper.TexToCube(new Vector2(x + dx, y + dy), width, face);
                    var height = PlanetMatHelper.GetHeight(planet, cpos, depth);
                    Min(ref hmin, height);
                    Max(ref hmax, height);
                }
        }
    }
}
