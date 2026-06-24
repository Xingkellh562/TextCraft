using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.Config
{
    internal class ConfigMgr:BaseSingleton<ConfigMgr>
    {
        public readonly WorldConfig worldConfig = new WorldConfig();
        public readonly GraphicConfig graphicConfig = new GraphicConfig();
    }

    public class BaseConfig { }


    internal class WorldConfig: BaseConfig
    {
        int _chunkSizeX = 32;
        int _chunkSizeY = 32;
        int _chunkSizeZ = 32;

        int _seaLevel = 0;

        public int SeaLevel => _seaLevel;

        public int ChunkSizeX => _chunkSizeX;
        public int ChunkSizeY => _chunkSizeY;
        public int ChunkSizeZ => _chunkSizeZ;
    }

    internal class GraphicConfig: BaseConfig
    {
        int _viewRange = 128;
        public int ViewRange => _viewRange;
        public bool fog = true;
    }
}
