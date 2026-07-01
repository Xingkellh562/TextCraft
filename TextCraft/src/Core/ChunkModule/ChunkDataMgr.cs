using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.Config;

namespace TextCraft.src.Core.ChunkModule
{
    public class ChunkDataMgr
    {
        ConcurrentDictionary<Vector3i, Chunk> chunks = new();

        public ConcurrentDictionary<Vector3i, Chunk> Chunks => chunks;

        public readonly Vector3i size;

        public ChunkDataMgr()
        {
            size = new Vector3i(ConfigMgr.Ins.worldConfig.ChunkSizeX, ConfigMgr.Ins.worldConfig.ChunkSizeY, ConfigMgr.Ins.worldConfig.ChunkSizeZ);
        }
        
        public void AddChunk(Vector3i chunkPos,Chunk chunk)
        {
            chunks[chunkPos] = chunk;
        }

        public bool HasChunk(Vector3i chunkPos)
        {
            return chunks.ContainsKey(chunkPos);
        }

        public bool TryGetChunk(Vector3i chunkPos,out Chunk? chunk)
        {
            return chunks.TryGetValue(chunkPos, out chunk);
        }

        public bool TryRemoveChunk(Vector3i chunkPos)
        {
            if(chunks.TryRemove(chunkPos, out var chunk))
            {
                Pools.Ins.chunkPool.Enter(chunk);
                return true;
            }
            return false;
        }

        public int GetBlock(int x,int y,int z)
        {
            int chunkX = (int)MathF.Floor((float)x / size.X);
            int chunkY = (int)MathF.Floor((float)y / size.Y);
            int chunkZ = (int)MathF.Floor((float)z / size.Z);
            Vector3i chunkPos = new Vector3i(chunkX,chunkY,chunkZ);
            int blockX = x - chunkX * size.X;
            int blockY = y - chunkY * size.Y;
            int blockZ = z - chunkZ * size.Z;
            if(chunks.TryGetValue(chunkPos, out var chunk))
                return chunk.GetBlock(blockX, blockY, blockZ);
            return 0;
        }

        public void SetBlock(int x, int y, int z,int value)
        {
            int chunkX = (int)MathF.Floor((float)x / size.X);
            int chunkY = (int)MathF.Floor((float)y / size.Y);
            int chunkZ = (int)MathF.Floor((float)z / size.Z);
            Vector3i chunkPos = new Vector3i(chunkX, chunkY, chunkZ);
            int blockX = x - chunkX * size.X;
            int blockY = y - chunkY * size.Y;
            int blockZ = z - chunkZ * size.Z;
            if (chunks.TryGetValue(chunkPos, out var chunk))
                chunk.SetBlock(blockX, blockY, blockZ,value);
        }
    }
}
