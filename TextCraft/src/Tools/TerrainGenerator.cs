using OpenTK.Graphics.Egl;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
using TextCraft.src.Core.ChunkModel;
using TextCraft.src.Core.Config;
using TextCraft.src.Table;

namespace TextCraft.src.Tools
{
    internal class TerrainGenerator
    {
        private readonly PerlinNoise _heightNoise;
        private readonly PerlinNoise _detailNoise;
        private readonly PerlinNoise _rangeNoise;
        private readonly int _seed;

        private Random _random;

        public TerrainGenerator(int seed)
        {
            _seed = seed;
            _heightNoise = new PerlinNoise(seed);
            _detailNoise = new PerlinNoise(seed + 1);
            _rangeNoise = new PerlinNoise(seed + 2);
            _random = new Random(seed+3);
        }

        /// <summary>
        /// 获取世界坐标 (x, z) 处的地面高度（整数）
        /// </summary>
        public int GetHeight(int worldX, int worldZ, Vector3 range)
        {
            // 低频主地形（幅度 64，频率 0.005）
            float main = (float)_heightNoise.Noise(worldX * 0.01f, worldZ * 0.01f);
            // 叠加中频起伏（幅度 16，频率 0.02）
            float mid = (float)_heightNoise.Noise(worldX * 0.04f, worldZ * 0.04f) * 0.5f;
            // 叠加高频细节（幅度 4，频率 0.1）
            float detail = (float)_detailNoise.Noise(worldX * 0.2f, worldZ * 0.2f) * 0.2f;

            float height = main * range.X + mid * range.Y + detail * range.Z;
            // 增加海平面基线（比如 Y=0 为海平面）
            return (int)(height + 32f);
        }

        public float[] GetHumidity(int worldX, int worldZ)
        {
            return new float[] { (float)_heightNoise.Noise((worldX-16) * 0.001f, (worldZ-16) * 0.001f),
                                 (float)_heightNoise.Noise((worldX+16) * 0.001f, (worldZ-16) * 0.001f),
                                 (float)_heightNoise.Noise((worldX+16) * 0.001f, (worldZ+16) * 0.001f),
                                 (float)_heightNoise.Noise((worldX-16) * 0.001f, (worldZ+16) * 0.001f)};
        }

        public float GetRange(int worldX, int worldZ)
        {
            float value = ((float)_rangeNoise.Noise(worldX * 0.002f, worldZ * 0.002f) + 1);
            return value * value * 32;
        }

        public int GetBlockType(int worldY, int surfaceHeight, int stoneHeight,Biome biome)
        {
            if (worldY > stoneHeight && worldY > surfaceHeight)
            {
                if(worldY > ConfigMgr.Ins.worldConfig.SeaLevel)
                    return 0;
                else
                    return 144;
            }
                
            if (worldY > surfaceHeight && worldY < stoneHeight) return biome.SoilBlock;
            if (worldY == surfaceHeight && worldY > stoneHeight)
            {
                if (worldY > ConfigMgr.Ins.worldConfig.SeaLevel)
                    return biome.SurfaceBlock;
                else
                    return 48;
            } 
            if (worldY > stoneHeight) return biome.SoilBlock;
            return biome.StoneBlock;
        }
        public Chunk BuildChunk(Chunk chunk,Vector3i chunkPos)
        {
            int sizeX = ConfigMgr.Ins.worldConfig.ChunkSizeX;
            int sizeY = ConfigMgr.Ins.worldConfig.ChunkSizeY;
            int sizeZ = ConfigMgr.Ins.worldConfig.ChunkSizeZ;

            
            for (int x = 0; x < sizeX; x++)
            {
                int trueX = x + chunkPos.X * sizeX;
                for (int z = 0; z < sizeZ; z++)
                {
                    int trueZ = z + chunkPos.Z * sizeZ;

                    int index = _random.Next(0,4);
                    Biome biome = BiomeTable.Ins.FindBiome(GetHumidity(trueX, trueZ)[index]);

                    float height = GetRange(trueX,trueZ);

                    Vector3 range = new Vector3(height, height / 4, height / 16);

                    int surface = GetHeight(trueX, trueZ, range);
                    for (int y = 0; y < sizeY; y++)
                    {
                        int trueY = y + chunkPos.Y * sizeY;

                        chunk[x,y,z] = GetBlockType(trueY, surface, surface - 5,biome);

                        //if (trueX == 1 && trueZ == 1 && trueY == 118)
                        //    chunk[x, y, z] = 80;
                        //if (trueX == 1 && trueZ == 3 && trueY == 118)
                        //    chunk[x, y, z] = 96;
                    }
                }
            }
                      
            return chunk;
        }
    }
}
