using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Collections;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Rendering;

namespace TextCraft.src.Core
{
    internal class Pools:BaseSingleton<Pools>
    {
        public Pool<Chunk> chunkPool = new Pool<Chunk>(ConfigMgr.Ins.gameConfig.PoolMaxCapacity, () => new Chunk(new OpenTK.Mathematics.Vector3i(
            ConfigMgr.Ins.gameConfig.ChunkSizeX,
            ConfigMgr.Ins.gameConfig.ChunkSizeY,
            ConfigMgr.Ins.gameConfig.ChunkSizeZ
            )));

        public Pool<Grid> gridPool = new Pool<Grid>(ConfigMgr.Ins.gameConfig.PoolMaxCapacity, () => new Grid());
    }
}
