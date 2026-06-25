using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.EntityModule;
using TextCraft.src.Core.Input;
using TextCraft.src.Core.Physic;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.EntityRender;
using TextCraft.src.Table;
using TextCraft.src.Tools;

namespace TextCraft.src.Core
{
    internal class World
    {
        public GridMgr gridMgr = new GridMgr();
        public ChunkDataMgr chunkDataMgr = new ChunkDataMgr();
        public ChunkUpdateMgr chunkUpdateMgr;

        public Vector3 playerPos = new Vector3(0, 90, 0);
        public Vector3 playerDir = new Vector3(0, 0, 1);

        public InputMgr inputMgr = new InputMgr();

        public ECSManager ecsMgr = new ECSManager();

        public PhysicSystem physicSystem = new PhysicSystem();

        public ModelRenderMgr modelRenderMgr = new ModelRenderMgr();

        public float GameTime = 0;
        public World() 
        {
            chunkUpdateMgr = new ChunkUpdateMgr(chunkDataMgr ,gridMgr);

        }

        public void Load()
        {
            gridMgr.AddNewLayers("default");
            gridMgr.AddNewLayers("lucency");
            gridMgr.AddNewLayers("Entity");

            chunkUpdateMgr.StartChunkUpdate();
            LoadPlayer();

            AddEntity(new Vector3(0, 90, 5), 1);
            AddEntity(new Vector3(0, 90, 6), 2);
        }
        public void Update(float updateTime)
        {
            CreateRequest();

            foreach (var entity in ecsMgr.GetEntitiesWith(new Type[] { typeof(Transform), typeof(InputComponent), typeof(RigidBody), typeof(Box) }))
            {
                var trans = ecsMgr.GetComponent<Transform>(entity);
                var input = ecsMgr.GetComponent<InputComponent>(entity);
                var body = ecsMgr.GetComponent<RigidBody>(entity);
                var box = ecsMgr.GetComponent<Box>(entity);

                body.velocity += inputMgr.MoveUpdate(updateTime, 36, trans.rotation, ref input);

                Vector4 move = new Vector4(trans.rotation, 0) * inputMgr.MouseMoveX(0.5f,ref input);
                move *= inputMgr.MouseMoveY(0.5f, trans.rotation, ref input);
                trans.rotation = new Vector3(move.X, move.Y, move.Z);

                inputMgr.LeftMouseButton(this,updateTime ,ref input);
                inputMgr.RightMouseButton(this,updateTime,box,ref input);

                if (chunkDataMgr.GetBlock((int)trans.position.X, (int)trans.position.Y - 1, (int)trans.position.Z) == 144)
                {
                    body.damp = 0.25f;
                    body.onGround = true;
                }
                else body.damp = 0.05f;

                ecsMgr.AddComponent(entity, trans);
                ecsMgr.AddComponent(entity, input);
                ecsMgr.AddComponent(entity, body);
            }

            if(GameTime > 5)
                physicSystem.Update(this, updateTime);

            foreach (var entity in ecsMgr.GetEntitiesWith(new Type[] { typeof(Transform), typeof(Camera) }))
            {
                var trans = ecsMgr.GetComponent<Transform>(entity);
                playerPos = trans.position;
                playerDir = trans.rotation;
            }

            modelRenderMgr.Update(this);

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

        void LoadPlayer()
        {
            EntityObject entityObject = new EntityObject() { name = "player", type = EntityType.living };
            Transform playerTrans = new Transform() { position = new Vector3(0, 90, 0), rotation = new Vector3(0, 0, 1) };
            RigidBody body = new RigidBody(50);
            Box box = new Box(new Vector3(-0.16f, -1.7f, -0.16f), new Vector3(0.16f, 0.2f, 0.16f));
            Camera camera = new Camera();
            InputComponent input = new();

            ecsMgr.AddComponent(0, entityObject);
            ecsMgr.AddComponent(0, playerTrans);
            ecsMgr.AddComponent(0, body);
            ecsMgr.AddComponent(0, box);
            ecsMgr.AddComponent(0, camera);
            ecsMgr.AddComponent(0, input);
        }

        Grid LoadModel()
        {
            List<float> vertices = new List<float>();
            for(int i = 0;i< EntityModelTable.model1.faces.Length; i++)
                for (int j = 0; j < EntityModelTable.model1.faces[i].Length; j++)
                {
                    if ((j+1) % 6 == 0)
                        vertices.Add(1);
                    else
                        vertices.Add(EntityModelTable.model1.faces[i][j]);
                }
                    
            return new Grid() { vertices = vertices.ToArray()};
        }

        void AddEntity(Vector3 pos,int id)
        {
            EntityObject entityObject = new EntityObject() { name = "entity", type = EntityType.living };
            Transform trans = new Transform() { position = pos, rotation = new Vector3(0, 0, 1) };
            RigidBody body = new RigidBody(50);
            Box box = new Box(new Vector3(-0.16f, -1.7f, -0.16f), new Vector3(0.16f, 0.2f, 0.16f));
            ModelCompenent model = new ModelCompenent() { posIndex = pos };

            Grid grid = LoadModel();
            grid.Pos = trans.position;
            gridMgr.AddChunkGrids("Entity", model.posIndex, grid);

            body.useGravity = true;

            ecsMgr.AddComponent(id, entityObject);
            ecsMgr.AddComponent(id, trans);
            ecsMgr.AddComponent(id, body);
            ecsMgr.AddComponent(id, box);
            ecsMgr.AddComponent(id, model);
        }
    }
}
