using OpenTK.Graphics.Egl;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Table;

namespace TextCraft.src.Tools.Terrain
{
    internal class MainTerrainGenerator:BaseTerrainGenerator
    {
        private readonly PerlinNoise _heightNoise;
        private readonly PerlinNoise _detailNoise;
        private readonly PerlinNoise _rangeNoise;
        private readonly PerlinNoise _humidityNoise;

        private readonly TeatureGenerator _teatureGenerator;

        private Random _random;

        int _offsetX = 0;
        int _offsetZ = 0;

        public MainTerrainGenerator(int seed):base(seed)
        {
            _heightNoise = new PerlinNoise(seed);
            _teatureGenerator = new TeatureGenerator(seed);
            _detailNoise = new PerlinNoise(seed + 1);
            _rangeNoise = new PerlinNoise(seed + 2);
            _humidityNoise = new PerlinNoise(seed + 3);
            _random = new Random(seed+4);
            _offsetX = _random.Next(-10000,10001);
            _offsetZ = _random.Next(-10000, 10001);
        }

        /// <summary>
        /// 获取世界坐标 (x, z) 处的地面高度（整数）
        /// </summary>
        private int GetHeight(int worldX, int worldZ, Vector3 range)
        {
            // 低频主地形
            float main = (float)_heightNoise.Noise((worldX + _offsetX) * 0.01f, (worldZ + _offsetZ) * 0.01f);
            // 叠加中频起伏
            float mid = (float)_heightNoise.Noise((worldX + _offsetX) * 0.04f, (worldZ + _offsetZ) * 0.04f);
            // 叠加高频细节
            float detail = (float)_detailNoise.Noise((worldX + _offsetX) * 0.2f, (worldZ + _offsetZ) * 0.2f) * 0.2f;

            float height = main * range.X + mid * range.Y + detail * range.Z;
            // 增加海平面基线（比如 Y=0 为海平面）
            return (int)(height + 32f);
        }
        private float[] GetHumidity(int worldX, int worldZ)
        {
            return new float[] { (float)_humidityNoise.Noise((worldX+_offsetX-16) * 0.001, (worldZ+_offsetZ-16) * 0.001),
                                 (float)_humidityNoise.Noise((worldX+_offsetX+16) * 0.001, (worldZ+_offsetZ-16) * 0.001),
                                 (float)_humidityNoise.Noise((worldX+_offsetX+16) * 0.001, (worldZ+_offsetZ+16) * 0.001),
                                 (float)_humidityNoise.Noise((worldX+_offsetX-16) * 0.001, (worldZ+_offsetZ+16) * 0.001)};
        }
        private float GetRange(int worldX, int worldZ)
        {
            float value = ((float)_rangeNoise.Noise((worldX + _offsetX) * 0.002f, (worldZ + _offsetZ) * 0.002f) + 1);
            return value * value * 32;
        }
        private int GetBlockType(int worldY, int surfaceHeight, int stoneHeight,Biome biome)
        {
            if (worldY > stoneHeight && worldY > surfaceHeight)
            {
                if(worldY > ConfigMgr.Ins.gameConfig.SeaLevel)
                    return 0;
                else
                    return 144;
            }
                
            if (worldY > surfaceHeight && worldY < stoneHeight) return biome.SoilBlock;
            if (worldY == surfaceHeight && worldY > stoneHeight)
            {
                if (worldY > ConfigMgr.Ins.gameConfig.SeaLevel)
                    return biome.SurfaceBlock;
                else
                    return 48;
            } 
            if (worldY > stoneHeight) return biome.SoilBlock;
            return biome.StoneBlock;
        }
        public override Chunk BuildChunk(Chunk chunk,Vector3i chunkPos)
        {
            int sizeX = ConfigMgr.Ins.gameConfig.ChunkSizeX;
            int sizeY = ConfigMgr.Ins.gameConfig.ChunkSizeY;
            int sizeZ = ConfigMgr.Ins.gameConfig.ChunkSizeZ;

            var teatureNodes = _teatureGenerator.Get2DTeature(chunkPos.X,chunkPos.Z);
            
            for (int x = 0; x < sizeX; x++)
            {
                int trueX = x + chunkPos.X * sizeX;
                for (int z = 0; z < sizeZ; z++)
                {
                    int trueZ = z + chunkPos.Z * sizeZ;

                    int index = _random.Next(0,4);
                    float[] humiditys = GetHumidity(trueX, trueZ);
                    Biome biome = BiomeTable.Ins.FindBiome(humiditys[index]);

                    float height = GetRange(trueX,trueZ);

                    Vector3 range = new Vector3(height, height / 4, height / 16);

                    int surface = GetHeight(trueX, trueZ, range);
                    _teatureGenerator.SetArgs(surface);
                    for (int y = 0; y < sizeY; y++)
                    {
                        int trueY = y + chunkPos.Y * sizeY;

                        chunk[x,y,z] = GetBlockType(trueY, surface, surface - 5,biome);
                        int block = _teatureGenerator.GetTeatureBlock(trueX, trueY, trueZ, teatureNodes);
                        if (block != 0)
                            chunk[x, y, z] = block;

                    }
                }
            }
                      
            return chunk;
        }
    }
}
