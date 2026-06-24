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

        public float GameTime = 0;
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
            //for(int i = 0; i < 24; i++)
            //{
            //    chunkUpdateMgr.CommitGridUpdateRequest(Vector3i.Zero);
            //}
        }
        public void Update(float updateTime)
        {
            CreateRequest();

            player.Update(chunkDataMgr, updateTime);

            playerPos = player.pos;

            player.velocity += inputMgr.MoveUpdate(updateTime, 36, playerDir);
            if(GameTime > 5)
                player.AddForce(-Vector3.UnitY * 980 * 4);

            GameTime += updateTime;
        }

        void CreateRequest()
        {

            List<List<Vector3i>> list = ChunkRangeHelper.GetChunksInRanges(playerPos, new Vector3i(
                ConfigMgr.Ins.worldConfig.ChunkSizeX,
                ConfigMgr.Ins.worldConfig.ChunkSizeY,
                ConfigMgr.Ins.worldConfig.ChunkSizeZ
                ), 0, 5*32, 7*32);

            foreach (var chunkPos in list[0])
            {
                chunkUpdateMgr.CommitChunkUpdateRequest(chunkPos);

                chunkUpdateMgr.CommitGridCommitRequest(chunkPos);
            }
            foreach (var chunkPos in list[1])
            {
                chunkUpdateMgr.CommitChunkUpdateRequest(chunkPos);
            }
            Vector3i chunkSize = new Vector3i(ConfigMgr.Ins.worldConfig.ChunkSizeX,
                                ConfigMgr.Ins.worldConfig.ChunkSizeY,
                                ConfigMgr.Ins.worldConfig.ChunkSizeZ);
            foreach (var chunkPos in chunkDataMgr.Chunks.Keys)
            {
                Vector3 Pos = new Vector3(chunkPos.X * chunkSize.X, chunkPos.Y * chunkSize.Y, chunkPos.Z * chunkSize.Z);
                if ((Pos - playerPos).Length > 8 * 32)
                    chunkUpdateMgr.CommitChunkDeleteRequest(chunkPos);
                if((Pos - playerPos).Length > 6 * 32)
                    chunkUpdateMgr.CommitGridDeleteRequest(chunkPos);
            }
            while (chunkUpdateMgr.gridUpdateFinishQueue.TryDequeue(out Vector3i result))
                chunkUpdateMgr.RemoveGridUpdateRequest(result);


        }
    }
}
