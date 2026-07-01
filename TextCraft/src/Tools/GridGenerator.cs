using OpenTK.Graphics.ES20;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Rendering;
using TextCraft.src.Table;

namespace TextCraft.src.Tools
{
    public static class GridGenerator
    {
        public static int[,] neighboursTable = new int[,]
        {
            {1,0,0 }, //右
            {-1,0,0 },//左
            {0,1,0 }, //上
            {0,-1,0 },//下
            {0,0,1 }, //前
            {0,0,-1 },//后
        };

        private static readonly int[,,,] AOOffsets = new int[6, 4, 3, 3]
{
    // 面 0: 右面 (+X)
    {
        // 角0: 下前 (y=0, z=1)
        { { 1, -1,  1 }, { 1,  0,  1 }, { 1, -1,  0 } },
        // 角1: 下后 (y=0, z=0)
        { { 1, -1, -1 }, { 1,  0, -1 }, { 1, -1,  0 } },
        // 角2: 上后 (y=1, z=0)
        { { 1,  1, -1 }, { 1,  0, -1 }, { 1,  1,  0 } },
        // 角3: 上前 (y=1, z=1)
        { { 1,  1,  1 }, { 1,  0,  1 }, { 1,  1,  0 } }
    },
    // 面 1: 左面 (-X)
    {
        // 角0: 下后 (y=0, z=0)
        { { -1, -1, -1 }, {-1,  0, -1 }, {-1, -1,  0 } },
        // 角1: 下前 (y=0, z=1)
        { { -1, -1,  1 }, {-1,  0,  1 }, {-1, -1,  0 } },
        // 角2: 上前 (y=1, z=1)
        { { -1,  1,  1 }, {-1,  0,  1 }, {-1,  1,  0 } },
        // 角3: 上后 (y=1, z=0)
        { { -1,  1, -1 }, {-1,  0, -1 }, {-1,  1,  0 } }
    },
    // 面 2: 上面 (+Y)
    {
        // 角0: 前左 (x=0, z=1)
        { { -1,  1,  1 }, { 0,  1,  1 }, { -1,  1,  0 } },
        // 角1: 前右 (x=1, z=1)
        { {  1,  1,  1 }, { 0,  1,  1 }, {  1,  1,  0 } },
        // 角2: 后右 (x=1, z=0)
        { {  1,  1, -1 }, { 0,  1, -1 }, {  1,  1,  0 } },
        // 角3: 后左 (x=0, z=0)
        { { -1,  1, -1 }, { 0,  1, -1 }, { -1,  1,  0 } }
    },
    // 面 3: 下面 (-Y)
    {
        // 角0: 后左 (x=0, z=0)
        { { -1, -1, -1 }, {-1, -1,  0 }, {  0, -1, -1 } },
        // 角1: 后右 (x=1, z=0)
        { {  1, -1, -1 }, { 1, -1,  0 }, {  0, -1, -1 } },
        // 角2: 前右 (x=1, z=1)
        { {  1, -1,  1 }, { 1, -1,  0 }, {  0, -1,  1 } },
        // 角3: 前左 (x=0, z=1)
        { { -1, -1,  1 }, {-1, -1,  0 }, {  0, -1,  1 } }
    },
    // 面 4: 前面 (+Z)
    {
        // 角0: 左下 (x=0, y=0)
        { { -1,  0,  1 }, { 0, -1,  1 }, { -1, -1,  1 } },
        // 角1: 右下 (x=1, y=0)
        { {  1,  0,  1 }, { 0, -1,  1 }, {  1, -1,  1 } },
        // 角2: 右上 (x=1, y=1)
        { {  1,  0,  1 }, { 0,  1,  1 }, {  1,  1,  1 } },
        // 角3: 左上 (x=0, y=1)
        { { -1,  0,  1 }, { 0,  1,  1 }, { -1,  1,  1 } }
    },
    // 面 5: 后面 (-Z)
    {
        // 角0: 右下 (x=1, y=0)
        { {  1, -1, -1 }, { 0, -1, -1 }, {  1,  0, -1 } },
        // 角1: 左下 (x=0, y=0)
        { { -1, -1, -1 }, { 0, -1, -1 }, { -1,  0, -1 } },
        // 角2: 左上 (x=0, y=1)
        { { -1,  1, -1 }, { 0,  1, -1 }, { -1,  0, -1 } },
        // 角3: 右上 (x=1, y=1)
        { {  1,  1, -1 }, { 0,  1, -1 }, {  1,  0, -1 } }
    }
};

        private static readonly int[] vertexIndex = { 0,1,2,0,2,3};
        public static bool BuildGrid(ChunkDataMgr chunkMgr, Vector3i pos,int length,out Grid[] grids)
        {
            grids = new Grid[length];

            for(int i = 0; i < grids.Length; i++) 
            {
                bool success = Pools.Ins.gridPool.TryTake(out var grid);
                if (!success) grid = new Grid();

                if(grid != null)
                    grids[i] = grid;
            }
            if(!GetVertices(chunkMgr,  pos, grids.Length, out var vertices))
                return false;

            Vector3i chunkSize = new Vector3i(ConfigMgr.Ins.worldConfig.ChunkSizeX,
                                ConfigMgr.Ins.worldConfig.ChunkSizeY,
                                ConfigMgr.Ins.worldConfig.ChunkSizeZ);

            for (int i = 0; i < grids.Length; i++)
            {
                grids[i].vertices = vertices[i].Count > 0 ? vertices[i].ToArray() : new float[]{ };
                grids[i].Pos = new Vector3(pos.X * chunkSize.X, pos.Y * chunkSize.Y, pos.Z * chunkSize.Z);
                grids[i].isUpdate = false;
            }

            return true;
        }

        public static bool BuildGrid(ChunkDataMgr chunkMgr, Vector3 pos, ref Grid[] grids)
        {
            if (!GetVertices(chunkMgr, (Vector3i)pos, grids.Length, out var vertices))
                return false;

            Vector3i chunkSize = new Vector3i(ConfigMgr.Ins.worldConfig.ChunkSizeX,
                                ConfigMgr.Ins.worldConfig.ChunkSizeY,
                                ConfigMgr.Ins.worldConfig.ChunkSizeZ);

            for(int i = 0; i < grids.Length; i++)
            {
                grids[i].vertices = vertices[i].Count > 0 ? vertices[i].ToArray() : new float[] { };
                grids[i].Pos = new Vector3(pos.X * chunkSize.X, pos.Y * chunkSize.Y, pos.Z * chunkSize.Z);
                grids[i].isUpdate = false;
            }

            return true;
        }

        private static bool GetVertices(ChunkDataMgr chunkMgr, Vector3i pos , int length,out List<float>[] vertices)
        {
            Vector3i chunkSize = new Vector3i(ConfigMgr.Ins.worldConfig.ChunkSizeX,
                                ConfigMgr.Ins.worldConfig.ChunkSizeY,
                                ConfigMgr.Ins.worldConfig.ChunkSizeZ);

            vertices = new List<float>[length];
            for(int i = 0;i< vertices.Length;i++)
                vertices[i] = new List<float> ();

            if (!chunkMgr.TryGetChunk(pos, out Chunk? chunk))
                return false;

            if(chunk == null) return false;

            for (int x = 0; x < chunkSize.X; x++)
                for (int y = 0; y < chunkSize.Y; y++)
                    for (int z = 0; z < chunkSize.Z; z++)
                    {
                        int block = chunk[x, y, z];
                        if (BlockTable.Ins[block].BlockType != BlockType.Air)
                        {
                            int g = BlockTable.Ins[block].IsLucency&&length > 1 ? 1 : 0;
                            for (int w = 0; w < 6; w++)
                            {
                                int dx = x + neighboursTable[w, 0];
                                int dy = y + neighboursTable[w, 1];
                                int dz = z + neighboursTable[w, 2];

                                int nei = dx >= 0 && dx < chunkSize.X && dy >= 0 && dy < chunkSize.Y && dz >= 0 && dz < chunkSize.Z ?
                                    chunk[dx, dy, dz] :
                                    chunkMgr.GetBlock(dx + chunkSize.X * pos.X, dy + chunkSize.Y * pos.Y, dz + chunkSize.Z * pos.Z); ;

                                bool ifCreateFace = false;
                                if(BlockTable.Ins[nei].BlockType == BlockType.Air || BlockTable.Ins[nei].BlockType == BlockType.IncompleteBlock) ifCreateFace = true;
                                else if (BlockTable.Ins[nei].BlockType == BlockType.Liquid && BlockTable.Ins[block].BlockType == BlockType.Solid) ifCreateFace = true;
                                else if (BlockTable.Ins[nei].BlockType == BlockType.Liquid && BlockTable.Ins[block].BlockType == BlockType.IncompleteBlock) ifCreateFace = true;
                                if (BlockTable.Ins[nei].BlockType == BlockTable.Ins[block].BlockType) ifCreateFace = false;
                                if (ifCreateFace)
                                {
                                    for (int i = 0; i < BlockTable.Ins[block].Model[w].vertices.Length; i += 6)
                                    {

                                        float ao = BlockTable.Ins[block].BlockType == BlockType.Solid ? GenerateAO(
                                            x + chunkSize.X * pos.X,
                                            y + chunkSize.Y * pos.Y,
                                            z + chunkSize.Z * pos.Z,
                                            w, vertexIndex[(i / 6) % 6], chunkMgr) : 1;
                                        vertices[g].Add(BlockTable.Ins[block].Model[w].vertices[i] + x);
                                        vertices[g].Add(BlockTable.Ins[block].Model[w].vertices[i + 1] + y);
                                        vertices[g].Add(BlockTable.Ins[block].Model[w].vertices[i + 2] + z);
                                        vertices[g].Add(BlockTable.Ins[block].Model[w].vertices[i + 3]);
                                        vertices[g].Add(BlockTable.Ins[block].Model[w].vertices[i + 4]);
                                        vertices[g].Add(BlockTable.Ins[block].Model[w].vertices[i + 5] + ao);
                                    }
                                }
                            }
                            if (BlockTable.Ins[block].Model.Length > 6)
                            {
                                for (int i = 0; i < BlockTable.Ins[block].Model[6].vertices.Length; i += 6)
                                {
                                    vertices[g].Add(BlockTable.Ins[block].Model[6].vertices[i] + x);
                                    vertices[g].Add(BlockTable.Ins[block].Model[6].vertices[i + 1] + y);
                                    vertices[g].Add(BlockTable.Ins[block].Model[6].vertices[i + 2] + z);
                                    vertices[g].Add(BlockTable.Ins[block].Model[6].vertices[i + 3]);
                                    vertices[g].Add(BlockTable.Ins[block].Model[6].vertices[i + 4]);
                                    vertices[g].Add(BlockTable.Ins[block].Model[6].vertices[i + 5]);
                                }
                            }
                        }
                    }
            return true;
        }

        public static float GenerateAO(int blockX, int blockY, int blockZ, int normal, int corner,ChunkDataMgr chunkMgr)
        {
            int solidCount = 0;

            // 获取三个需要检查的邻居偏移
            for (int i = 0; i < 3; i++) // i=0: 轴向1, i=1: 轴向2, i=2: 对角
            {
                int dx = AOOffsets[normal, corner, i, 0];
                int dy = AOOffsets[normal, corner, i, 1];
                int dz = AOOffsets[normal, corner, i, 2];

                int nx = blockX + dx;
                int ny = blockY + dy;
                int nz = blockZ + dz;

                if (BlockTable.Ins[chunkMgr.GetBlock(nx, ny, nz)].BlockType == BlockType.Solid)
                    solidCount++;
            }

            // solidCount 范围 0~3，映射到亮度系数 0.5~1.0
            float aoValue = 1.0f - (solidCount / 3.0f) * 0.9f;
            return aoValue;
        }
    }
}
