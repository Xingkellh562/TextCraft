using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModel;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.Input;
using TextCraft.src.Rendering;
using TextCraft.src.Tools;
using TextCraft.src.Core.Physic;

namespace TextCraft.src.Core
{
    internal class World
    {
        public GridMgr gridMgr = new GridMgr();
        public ChunkDataMgr chunkDataMgr = new ChunkDataMgr();
        public ChunkUpdateMgr chunkUpdateMgr;

        public Vector3 playerPos = new Vector3(0,120,0);
        public Vector3 playerDir = new Vector3(0, 0, 1);

        public RigidBody player = new RigidBody(50);

        public InputMgr inputMgr = new InputMgr();
        public World() 
        {
            chunkUpdateMgr = new ChunkUpdateMgr(chunkDataMgr ,gridMgr);
            player.pos = playerPos;
        }

        public void Load()
        {
            gridMgr.AddNewLayers("default");
            gridMgr.AddNewLayers("lucency");

            chunkUpdateMgr.StartChunkUpdate();
        }
        public void Update(float updateTime)
        {
            CreateRequest();

            player.Update(chunkDataMgr, updateTime);

            playerPos = player.pos;

            player.AddForce(inputMgr.MoveUpdate(updateTime, 12, playerDir)*10000);
        }

        void CreateRequest()
        {

            List<List<Vector3i>> list = ChunkRangeHelper.GetChunksInRanges(playerPos, new Vector3i(
                ConfigMgr.Ins.worldConfig.ChunkSizeX,
                ConfigMgr.Ins.worldConfig.ChunkSizeY,
                ConfigMgr.Ins.worldConfig.ChunkSizeZ
                ), 0, 5*32, 6*32, 8*32);

            foreach (var chunkPos in list[0])
            {
                chunkUpdateMgr.CommitChunkUpdateRequest(chunkPos);

                chunkUpdateMgr.CommitGridCommitRequest(chunkPos);
            }
            foreach (var chunkPos in list[1])
            {
                chunkUpdateMgr.CommitChunkUpdateRequest(chunkPos);
                
                chunkUpdateMgr.CommitGridDeleteRequest(chunkPos);
            }
            foreach (var chunkPos in list[2])
            {
                chunkUpdateMgr.CommitChunkDeleteRequest(chunkPos);
            }
        }
    }
}
