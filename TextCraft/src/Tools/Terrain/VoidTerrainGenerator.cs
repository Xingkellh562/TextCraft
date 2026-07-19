using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Table;

namespace TextCraft.src.Tools.Terrain
{
    internal class VoidTerrainGenerator : BaseTerrainGenerator
    {
        public VoidTerrainGenerator(int seed) : base(seed)
        {
        }

        public override Chunk BuildChunk(Chunk chunk, Vector3i chunkPos)
        {
            int sizeX = ConfigMgr.Ins.gameConfig.ChunkSizeX;
            int sizeY = ConfigMgr.Ins.gameConfig.ChunkSizeY;
            int sizeZ = ConfigMgr.Ins.gameConfig.ChunkSizeZ;

            for (int x = 0; x < sizeX; x++)
                for (int z = 0; z < sizeZ; z++)
                    for (int y = 0; y < sizeY; y++)
                    {
                        if (chunkPos == Vector3i.Zero && x == sizeX/2 && y == sizeY/2 && z == sizeZ / 2)
                            chunk[x, y, z] = 16;
                        else
                            chunk[x, y, z] = 0;
                    }

            return chunk;
        }
    }
}
