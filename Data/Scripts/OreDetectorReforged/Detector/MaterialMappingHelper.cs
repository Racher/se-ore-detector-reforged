using Sandbox.Definitions;
using System;
using VRage.Game.Components;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace OreDetectorReforged.Detector
{
    class MaterialMappingHelper
    {
        public static MaterialMappingHelper Static
        {
            get
            {
                if (loaded == null)
                    loaded = new MaterialMappingHelper();
                return loaded;
            }
        }
        static MaterialMappingHelper loaded;

        public string[] naturalOres;
        public byte[] matIdxToOreIdx;
        public Dictionary<MyPlanetGeneratorDefinition, BitArray> planetWhitelists;
        public BitArray asteroidWhitelist;

        MaterialMappingHelper()
        {
            LoadNaturalOres();
            LoadMatIdx();
            LoadPlanetWhitelist();
            LoadAsteroidWhitelist();
        }

        void LoadNaturalOres()
        {
            var ores = new HashSet<string>();
            foreach (var planet in MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions())
            {
                foreach (var oreChannel in planet.OreMappings)
                    ores.Add(oreChannel.Type);
                foreach (var biome in planet.MaterialGroups)
                    foreach (var rule in biome.MaterialRules)
                        foreach (var layer in rule.Layers)
                            ores.Add(layer.Material);
            }
            ores = new HashSet<string>(ores.Select(s => MyDefinitionManager.Static.GetVoxelMaterialDefinition(s)?.MinedOre));
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                if (mat.SpawnsInAsteroids)
                    ores.Add(mat.MinedOre);
            ores.Remove("Stone");
            MyDefinitionManager.Static.GetOreTypeNames(out naturalOres);
            naturalOres = naturalOres.Where(s => ores.Contains(s)).Take(128).ToArray();
        }

        void LoadMatIdx()
        {
            matIdxToOreIdx = new byte[256];
            for (var i = 0; i < matIdxToOreIdx.Length; ++i)
                matIdxToOreIdx[i] = 255;
            foreach (var def in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                matIdxToOreIdx[def.Index] = (byte)Array.IndexOf(naturalOres, def.MinedOre);
        }

        void LoadPlanetWhitelist()
        {
            planetWhitelists = new Dictionary<MyPlanetGeneratorDefinition, BitArray>();
            foreach (var planet in MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions())
            {
                var whitelist = new BitArray(128);
                Action<string> Add = (material) =>
                {
                    var d = MyDefinitionManager.Static.GetVoxelMaterialDefinition(material);
                    var i = d == null ? 255 : matIdxToOreIdx[d.Index];
                    if (i < 128)
                        whitelist[i] = true;
                };
                foreach (var oreChannel in planet.OreMappings)
                    Add(oreChannel.Type);
                foreach (var biome in planet.MaterialGroups)
                    foreach (var rule in biome.MaterialRules)
                        foreach (var layer in rule.Layers)
                            Add(layer.Material);
                planetWhitelists[planet] = whitelist;
            }
        }

        void LoadAsteroidWhitelist()
        {
            asteroidWhitelist = new BitArray(128);
            int i;
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
                if (mat.SpawnsInAsteroids && (i = matIdxToOreIdx[mat.Index]) < 128)
                    asteroidWhitelist[i] = true;
        }
    }
}
