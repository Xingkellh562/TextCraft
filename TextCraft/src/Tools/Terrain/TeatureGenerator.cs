using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.Config;
using TextCraft.src.Table;

namespace TextCraft.src.Tools.Terrain
{
    internal class TeatureGenerator
    {
        int _seed = 0;
        int _surfaceHeight;
        private readonly PerlinNoise _featureNoise;
        public TeatureGenerator(int seed) 
        { 
            _seed = seed;
            _featureNoise = new PerlinNoise(seed);

        }

        public void SetArgs(int surfaceHeight)
        {
            _surfaceHeight = surfaceHeight;
        }
        public int GetTeatureBlock(int x,int y,int z,List<Vector2i> teatureNodes)
        {
            if (_surfaceHeight < ConfigMgr.Ins.gameConfig.SeaLevel + 4)
                return 0;
            int chunkX = (int)MathF.Floor((float)x / ConfigMgr.Ins.gameConfig.ChunkSizeX);
            int chunkZ = (int)MathF.Floor((float)z / ConfigMgr.Ins.gameConfig.ChunkSizeZ);
            foreach (var node in teatureNodes)
            {

                if (x == chunkX * ConfigMgr.Ins.gameConfig.ChunkSizeX + node.X && z == chunkZ * ConfigMgr.Ins.gameConfig.ChunkSizeZ + node.Y)
                {
                    
                    if (y - _surfaceHeight > 0 && y - _surfaceHeight < 7)
                        return 96;
                    else if (y - _surfaceHeight == 0)
                        return 64;
                }
            }
            return 0;
        }

        public List<Vector2i> Get2DTeature(int chunkX,int chunkZ)
        {
            List<Vector2i> treeNodes = new List<Vector2i>();
            Random random = new Random(_seed * chunkX * chunkZ);

            for (int i = 0; i < 10; i++)
            {
                int x = random.Next(0, 32);
                int y = random.Next(0, 32);
                Vector2i node = new Vector2i(x, y);
                bool legal = true;
                foreach (var n in treeNodes)
                {
                    if (Vector2.Distance(n, node) <= 5)
                    {
                        legal = false;
                        break;
                    }
                }
                if (legal)
                {
                    treeNodes.Add(node);
                }
            }
            return treeNodes;
        }
    }
}
