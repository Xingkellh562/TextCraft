using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Collections;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.ChunkModel;
using TextCraft.src.Rendering;

namespace TextCraft.src.Core
{
    internal class Pools:BaseSingleton<Pools>
    {
        public Pool<Chunk> chunkPool = new Pool<Chunk>(3000,() => new Chunk(new OpenTK.Mathematics.Vector3i(
            ConfigMgr.Ins.worldConfig.ChunkSizeX,
            ConfigMgr.Ins.worldConfig.ChunkSizeY,
            ConfigMgr.Ins.worldConfig.ChunkSizeZ
            )));

        public Pool<Grid> gridPool = new Pool<Grid>(3000, () => new Grid());
    }
}
