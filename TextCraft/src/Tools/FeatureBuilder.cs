using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.Event;

namespace TextCraft.src.Tools
{
    internal class FeatureBuilder
    {
        Chunk _chunk;

        public FeatureBuilder(Chunk chunk)
        {
            _chunk = chunk;
            EventMgr.Ins.Subscribe(typeof(TeatureBulidEventArg), (arg) => Build(arg as TeatureBulidEventArg ?? new()));
        }
        public void Build(TeatureBulidEventArg e) 
        {
            int minX = -1, maxX = -1;
            int minY = -1, maxY = -1;
            int minZ = -1, maxZ = -1;
            for (int x = 0;x<ConfigMgr.Ins.gameConfig.ChunkSizeX;x++)
                for (int y = 0; y < ConfigMgr.Ins.gameConfig.ChunkSizeY; y++)
                    for (int z = 0; z < ConfigMgr.Ins.gameConfig.ChunkSizeZ; z++)
                    {
                        int block = _chunk.GetBlock(x,y,z);
                        if(block != 0)
                        {
                            if (minX == -1 || (minX != -1 && x < minX))
                                minX = x;
                            if (minY == -1 || (minY != -1 && y < minY))
                                minY = y;
                            if (minZ == -1 || (minZ != -1 && z < minZ))
                                minZ = z;
                            if (maxX == -1 || (minX != -1 && x > maxX))
                                maxX = x;
                            if (maxY == -1 || (minY != -1 && y > maxY))
                                maxY = y;
                            if (maxZ == -1 || (minZ != -1 && z > maxZ))
                                maxZ = z;
                        }
                    }
            maxX++;maxY++;maxZ++;
            Console.WriteLine($"地物大小:({maxX - minX},{maxY - minY},{maxZ - minZ})");
        }
    }
}
