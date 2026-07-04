using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.EntityModule;
using TextCraft.src.Core.Input;
using TextCraft.src.Core.Physic;
using TextCraft.src.Core.Save;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.EntityRender;
using TextCraft.src.Table;
using TextCraft.src.Tools;

namespace TextCraft.src.Core
{
    internal class World : IDisposable
    {
        public string Name { get; set; } = "";

        public GridMgr gridMgr = new GridMgr();
        public ChunkDataMgr chunkDataMgr = new ChunkDataMgr();
        public ChunkStorage chunkStorage;

        public ChunkUpdateMgr chunkUpdateMgr;

        public Vector3 playerPos = new Vector3(0,90,0);
        public Vector3 playerDir = new Vector3(0,0,1);

        public InputMgr inputMgr = new InputMgr();

        public ECSManager ecsMgr = new ECSManager();

        public PhysicSystem physicSystem = new PhysicSystem();

        public ModelRenderMgr modelRenderMgr = new ModelRenderMgr();

        private bool _disposed = false;

        private int _seed = 0;
        public int Seed => _seed;

        public float GameTime = 0;
        public World(string name,int seed = 0) 
        {
            Name = name;
            _seed = seed;
            chunkStorage = new(AppContext.BaseDirectory + $"Save//{Name}");

            LoadData(Path.Combine(AppContext.BaseDirectory + $"Save//{Name}//worldData.xml"));

            chunkUpdateMgr = new ChunkUpdateMgr(chunkDataMgr ,gridMgr, chunkStorage, _seed);
        }

        public void Save(string path,WorldLoadData data)
        {
            var xs = new XmlSerializer(typeof(WorldLoadData));
            using var fs = new FileStream(path, FileMode.Create);
            xs.Serialize(fs, data);
        }
        public void LoadData(string path)
        {
            if(!File.Exists(path)) return;
            var xs = new XmlSerializer(typeof(WorldLoadData));
            using var fs = new FileStream(path, FileMode.Open);
            var data = (WorldLoadData?)xs.Deserialize(fs) ?? new WorldLoadData();
            Vector3 cameraPos = new Vector3(data.cameraPosX,data.cameraPosY,data.cameraPosZ);
            Vector3 cameraDir = new Vector3(data.cameraDirX, data.cameraDirY, data.cameraDirZ);
            playerPos = cameraPos; playerDir = cameraDir;_seed = data.seed;
        }

        public void Load()
        {
            gridMgr.AddNewLayers("default");
            gridMgr.AddNewLayers("lucency");
            gridMgr.AddNewLayers("Entity");

            chunkUpdateMgr.StartChunkUpdate();
            
            LoadPlayer();

            //AddEntity(new Vector3(0, 90, 5), 1);
            //AddEntity(new Vector3(0, 90, 6), 2);
        }

        public void UnLoad()
        {
            WorldLoadData data = new() {cameraPosX = playerPos.X,
                                        cameraPosY = playerPos.Y,
                                        cameraPosZ = playerPos.Z,
                                        cameraDirX = playerDir.X,
                                        cameraDirY = playerDir.Y,
                                        cameraDirZ = playerDir.Z,
                                        seed = _seed };
            Save(Path.Combine(AppContext.BaseDirectory + $"Save//{Name}//worldData.xml"), data);
            foreach(var chunkPos in chunkDataMgr.Chunks.Keys)
                chunkUpdateMgr.CommitChunkDeleteRequest(chunkPos);
            chunkUpdateMgr.StopChunkUpdate();
            Dispose(true);
        }
        public void Update(float updateTime)
        {
            CreateRequest();

            foreach (var entity in ecsMgr.GetEntitiesWith(new Type[] { typeof(Transform), typeof(Moving), typeof(InputComponent), typeof(RigidBody), typeof(Box) }))
            {
                var trans = ecsMgr.GetComponent<Transform>(entity);
                var input = ecsMgr.GetComponent<InputComponent>(entity);
                var body = ecsMgr.GetComponent<RigidBody>(entity);
                var box = ecsMgr.GetComponent<Box>(entity);
                var moving = ecsMgr.GetComponent<Moving>(entity);

                if (body.useGravity)
                    body.velocity += inputMgr.MoveUpdate(updateTime, moving.moveSpeed, trans.rotation,body.useGravity, ref input);
                else
                    body.velocity += inputMgr.MoveUpdate(updateTime, moving.flyingSpeed, trans.rotation, body.useGravity, ref input);

                if (body.onGround && body.useGravity && input.up)
                    body.velocity.Y += moving.jumpForce;

                input.spaceTimer -= updateTime;
                input.spaceTimer = Math.Clamp(input.spaceTimer,0,input.spaceInterval);

                if (input.spaceTimer == 0)
                    input.spacePress = 0;
                if (input.spacePress >= 2)
                {
                    body.useGravity = !body.useGravity;
                    input.spaceTimer = 0;
                }

                Vector4 move = new Vector4(trans.rotation, 0) * inputMgr.MouseMoveX(0.5f,ref input);
                move *= inputMgr.MouseMoveY(0.5f, trans.rotation, ref input);
                trans.rotation = new Vector3(move.X, move.Y, move.Z);

                inputMgr.LeftMouseButton(this,updateTime ,ref input);
                inputMgr.RightMouseButton(this,updateTime,box,ref input);

                body.damp = BlockTable.Ins[chunkDataMgr.GetBlock((int)trans.position.X, (int)trans.position.Y - 1, (int)trans.position.Z)].damp;

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
                ConfigMgr.Ins.gameConfig.ChunkSizeX,
                ConfigMgr.Ins.gameConfig.ChunkSizeY,
                ConfigMgr.Ins.gameConfig.ChunkSizeZ
                ), 0, 7*32 + 16);

            foreach (var chunkPos in list[0])
            {
                chunkUpdateMgr.CommitChunkUpdateRequest(chunkPos);
            }

            Vector3i chunkSize = new Vector3i(ConfigMgr.Ins.gameConfig.ChunkSizeX,
                                ConfigMgr.Ins.gameConfig.ChunkSizeY,
                                ConfigMgr.Ins.gameConfig.ChunkSizeZ);

            Vector3i[] nei = {
                    Vector3i.UnitX,
                    Vector3i.UnitY,
                    Vector3i.UnitZ,
                    -Vector3i.UnitX,
                    -Vector3i.UnitY,
                    -Vector3i.UnitZ,
                };

            foreach (var chunkPos in chunkDataMgr.Chunks.Keys)
            {

                Vector3 Pos = new Vector3(chunkPos.X * chunkSize.X + chunkSize.X / 2,
                                          chunkPos.Y * chunkSize.Y + chunkSize.Y / 2,
                                          chunkPos.Z * chunkSize.Z + chunkSize.Z / 2);
                bool commitRequest = true;
                for (int i = 0;i<nei.Length;i++)
                    if (!chunkDataMgr.Chunks.ContainsKey(chunkPos + nei[i]))
                    {
                        commitRequest = false;
                        break;
                    }

                if (commitRequest) chunkUpdateMgr.CommitGridCommitRequest(chunkPos);
                else chunkUpdateMgr.CommitGridDeleteRequest(chunkPos);

                if ((Pos - playerPos).Length > 8 * 32)
                    chunkUpdateMgr.CommitChunkDeleteRequest(chunkPos);
                    
            }


            while (chunkUpdateMgr.gridUpdateFinishQueue.TryDequeue(out Vector3i result))
                chunkUpdateMgr.RemoveGridUpdateRequest(result);
            while (chunkUpdateMgr.chunkCreateFinishQueue.TryDequeue(out Vector3i result))
                chunkUpdateMgr.RemoveChunkCreateRequest(result);
            while (chunkUpdateMgr.chunkDeleteFinishQueue.TryDequeue(out Vector3i result))
                chunkUpdateMgr.RemoveChunkDeleteRequest(result);
            while (chunkUpdateMgr.gridCreateFinishQueue.TryDequeue(out Vector3i result))
                chunkUpdateMgr.RemoveGridCreateRequest(result);
            while (chunkUpdateMgr.gridDeleteFinishQueue.TryDequeue(out Vector3i result))
                chunkUpdateMgr.RemoveGridDeleteRequest(result);
        }     

        void LoadPlayer()
        {
            EntityObject entityObject = new EntityObject() { name = "Xingkellh",typeName = "Player", type = EntityType.living };
            Moving moving = new Moving() {moveSpeed = 128,flyingSpeed = 256,jumpForce = 16};
            Transform playerTrans = new Transform() { position = playerPos, rotation = playerDir };
            RigidBody body = new RigidBody(50);
            Box box = new Box(new Vector3(-0.16f, -1.7f, -0.16f), new Vector3(0.16f, 0.2f, 0.16f));
            Camera camera = new Camera();
            InputComponent input = new();

            ecsMgr.AddComponent(0, entityObject);
            ecsMgr.AddComponent(0, moving);
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

       
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); 
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {

                chunkUpdateMgr?.StopChunkUpdate();
                if (gridMgr != null)
                {
                    foreach (var grids in gridMgr.grids.Values)
                    {
                        foreach (var grid in grids.Values)
                        {
                            grid?.Dispose();
                        }
                    }
                    gridMgr.grids.Clear();
                }
                if (chunkDataMgr != null)
                {
                    foreach (var chunk in chunkDataMgr.Chunks.Values)
                    {
                        chunk?.Dispose();
                    }
                    chunkDataMgr?.Chunks.Clear();
                }
                
                Pools.Ins.chunkPool.Clear();
                Pools.Ins.gridPool.Clear();
            }

            GC.Collect();

            _disposed = true;
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

    public class WorldLoadData()
    {
        public float cameraPosX, cameraPosY, cameraPosZ;
        public float cameraDirX, cameraDirY, cameraDirZ;
        public int seed;


    }
}
