using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;

namespace TextCraft.src.Table
{
    public class BiomeTable : BaseSingleton<BiomeTable>
    {
        private ConcurrentDictionary<int, Biome> biomes = new();

        public Biome this[int id]
        {
            get
            {
                if (biomes.TryGetValue(id, out var biome))
                    return biome;
                return biomes[0]; // 默认返回 Void
            }
        }

        public BiomeTable()
        {
            CreateBiome(new Biome()); // id=0, default
            CreateBiome(new Biome(1, "plain", 2 * 16, 4 * 16, 1 * 16));
            CreateBiome(new Biome(2, "desert", 3 * 16, 3 * 16, 1 * 16));
            CreateBiome(new Biome(3, "wasteland", 4 * 16, 4 * 16, 1 * 16));
        }

        private void CreateBiome(Biome biome)
        {
            biomes[biome.Id] = biome;
            // 如果你需要保留原来的 biomeIndex 字典，可继续维护，这里略
        }

        public Biome FindBiome(float humidness)
        {
            if (humidness > 0.1)
                return biomes[1];
            else if(humidness > -0.1)
                return biomes[3];
            else
                return biomes[2];
        }
    }

    public class Biome
    {
        int _id = 0;
        string _name = "Void";

        int _surfaceBlock = 0;
        int _soilBlock = 0;
        int _stoneBlock = 0;
        int _range = 0;

        public Biome(){ }

        public Biome(int id,string name, int sur , int soil,int stone)
        {
            _id = id;
            _name = name;
            _surfaceBlock = sur;
            _soilBlock= soil;
            _stoneBlock = stone;
        }

        public int Id => _id;
        public string Name => _name;
        /// <summary>
        /// 地表方块Type
        /// </summary>
        public int SurfaceBlock => _surfaceBlock;
        /// <summary>
        /// 基岩层方块Type
        /// </summary>
        public int StoneBlock => _stoneBlock;
        /// <summary>
        /// 土质层方块Type
        /// </summary>
        public int SoilBlock => _soilBlock;
    }
}
