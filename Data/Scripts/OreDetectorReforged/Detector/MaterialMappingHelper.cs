using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using VRage.Game.Components;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global

namespace OreDetectorReforged.Detector
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    class MaterialMappingHelper : MySessionComponentBase
    {
        static bool _loaded;

        public static string[] NaturalOres;
        public static Dictionary<string, int> NaturalOresToIdx;
        public static byte[] MatIdxToOreIdx;
        public static BitArray AsteroidWhitelist;
        public static int NaturalOreCount => NaturalOres.Length;

        public override void UpdateBeforeSimulation()
        {
            if (_loaded) return;
            LoadNaturalOres();
            LoadMatIdx();
            LoadAsteroidWhitelist();
            _loaded = true;
        }

        protected override void UnloadData()
        {
            _loaded = false;
            NaturalOres = null;
            NaturalOresToIdx = null;
            MatIdxToOreIdx = null;
            AsteroidWhitelist = null;
        }

        static void LoadNaturalOres()
        {
            var ores = new HashSet<string>();
            foreach (var planet in MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions())
                foreach (var oreChannel in planet.OreMappings)
                {
                    var ore = MyDefinitionManager.Static.GetVoxelMaterialDefinition(oreChannel.Type)?.MinedOre;
                    if (ore != null)
                        ores.Add(ore);
                }

            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                if (mat.SpawnsInAsteroids)
                    ores.Add(mat.MinedOre);
            ores.Remove("Stone");
            string[] oreNames;
            MyDefinitionManager.Static.GetOreTypeNames(out oreNames);
            NaturalOres = oreNames.Where(ores.Contains).Take(254).ToArray();
            NaturalOresToIdx = new Dictionary<string, int>(NaturalOres.Length);
            for (var i = 0; i < NaturalOres.Length; i++)
                NaturalOresToIdx[NaturalOres[i]] = i;
        }

        static void LoadMatIdx()
        {
            MatIdxToOreIdx = new byte[256];
            for (var i = 0; i < MatIdxToOreIdx.Length; ++i)
                MatIdxToOreIdx[i] = 255;
            foreach (var def in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {
                int idx;
                if (def.MinedOre != null && NaturalOresToIdx.TryGetValue(def.MinedOre, out idx))
                    MatIdxToOreIdx[def.Index] = (byte)idx;
            }
        }

        static void LoadAsteroidWhitelist()
        {
            AsteroidWhitelist = new BitArray(256);
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                if (mat.SpawnsInAsteroids)
                    AsteroidWhitelist[MatIdxToOreIdx[mat.Index]] = true;
        }
    }
}