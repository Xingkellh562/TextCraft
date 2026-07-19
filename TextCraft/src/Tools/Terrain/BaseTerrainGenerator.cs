using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModule;

namespace TextCraft.src.Tools.Terrain
{
    internal abstract class BaseTerrainGenerator
    {
        protected readonly int _seed;
        public BaseTerrainGenerator(int seed)
        {
            _seed = seed;
        }
        public abstract Chunk BuildChunk(Chunk chunk, Vector3i chunkPos);
    }
}
