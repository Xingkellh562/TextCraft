using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.Save;

//using System.Threading;
using TextCraft.src.Rendering;
using TextCraft.src.Tools;
using TextCraft.src.Tools.Terrain;

namespace TextCraft.src.Core.ChunkModule
{

    internal class ChunkUpdateMgr
    {
        ChunkDataMgr _chunkMgr;
        GridMgr _gridMgr;
        ChunkStorage _chunkStorage;

        BaseTerrainGenerator terrainGenerator;

        bool _chunkUpdateRuning = false;

        object _lock = new object();

        public Thread updateThread;

        public ConcurrentQueue<Vector3i> chunkCreateQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> chunkDeleteQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridCommitQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridDeleteQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridUpdateQueue = new ConcurrentQueue<Vector3i>();

        public ConcurrentQueue<Vector3i> chunkCreateFinishQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> chunkDeleteFinishQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridCreateFinishQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridDeleteFinishQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridUpdateFinishQueue = new ConcurrentQueue<Vector3i>();

        private HashSet<Vector3i> chunkCreateRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> chunkDeleteRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> gridCommitRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> gridDeleteRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> gridUpdateRequest = new HashSet<Vector3i>();

        public ChunkUpdateMgr(ChunkDataMgr chunkMgr, GridMgr gridMgr,ChunkStorage chunkStorage ,int seed = 91919191)
        {
            _chunkMgr = chunkMgr;
            _gridMgr = gridMgr;
            _chunkStorage = chunkStorage;
            if(seed == 6767)
                terrainGenerator = new VoidTerrainGenerator(seed);
            else
                terrainGenerator = new MainTerrainGenerator(seed);
            updateThread = new Thread(Update);
        }


        public void StartChunkUpdate()
        {
            updateThread.IsBackground = true;
            _chunkUpdateRuning = true;
            updateThread.Start();
        }

        public void StopChunkUpdate()
        {
            lock (_lock)
            {
                _chunkUpdateRuning = false;
            }
            chunkCreateQueue.Clear();
            gridCommitQueue.Clear();
            gridDeleteQueue.Clear();
            gridUpdateQueue.Clear();

            chunkCreateFinishQueue.Clear();
            gridCreateFinishQueue.Clear();
            gridDeleteFinishQueue.Clear();
            gridUpdateFinishQueue.Clear();

            chunkCreateRequest.Clear();
            gridCommitRequest.Clear();
            gridDeleteRequest.Clear();
            gridUpdateRequest.Clear();
        }
        public void Update()
        {
            bool chunkUpdateRuning;
            lock (_lock)
            {
                chunkUpdateRuning = _chunkUpdateRuning;
            }
            while (chunkUpdateRuning || chunkDeleteQueue.Count > 0)
            {
                if (chunkCreateQueue.TryDequeue(out Vector3i result))
                    CreateChunk(result);
                if (chunkDeleteQueue.TryDequeue(out result))
                    DeleteChunk(result);
                if (gridCommitQueue.TryDequeue(out result))
                    CommitRanderChunk(result);
                if (gridDeleteQueue.TryDequeue(out result))
                    DeleteRanderChunk(result);
                if (gridUpdateQueue.TryDequeue(out result))
                    UpdateRanderChunk(result);
            }
        }

        private void CreateChunk(Vector3i chunkPos)
        {
            bool success = Pools.Ins.chunkPool.TryTake(out Chunk? chunk);

            var size = new Vector3i(
            ConfigMgr.Ins.gameConfig.ChunkSizeX,
            ConfigMgr.Ins.gameConfig.ChunkSizeY,
            ConfigMgr.Ins.gameConfig.ChunkSizeZ
            );

            if (!success) chunk = new Chunk(size);

            chunk ??= new(size);

            if(_chunkStorage.TryLoadChunk(chunkPos,out int[] chunkData))
                chunk.SetChunk(chunkData);
            else
                chunk = terrainGenerator.BuildChunk(chunk, chunkPos);

            _chunkMgr.AddChunk(chunkPos, chunk);

            chunkCreateFinishQueue.Enqueue(chunkPos);
        }

        private void DeleteChunk(Vector3i chunkPos)
        {
            if (_chunkStorage.storagingChunk.Contains(chunkPos) && _chunkMgr.Chunks.ContainsKey(chunkPos))
                _chunkStorage.SaveChunkWithPos(chunkPos, _chunkMgr.Chunks[chunkPos].GetChunk());
            //在函数内部入池
            _chunkMgr.TryRemoveChunk(chunkPos);

            chunkDeleteFinishQueue.Enqueue(chunkPos);

            _chunkStorage.storagingChunk.Remove(chunkPos);
        }

        private void CommitRanderChunk(Vector3i chunkPos)
        {
            if(GridGenerator.BuildGrid(_chunkMgr, chunkPos,2,out Grid[] grids))
            {
                _gridMgr.AddChunkGrids("default", chunkPos, grids[0]);
                if (grids[1].vertices?.Length > 0)
                    _gridMgr.AddChunkGrids("lucency", chunkPos, grids[1]);
                else
                    Pools.Ins.gridPool.Enter(grids[1]);
            }
            gridCreateFinishQueue.Enqueue(chunkPos);
        }
        private void UpdateRanderChunk(Vector3i chunkPos)
        {
            _chunkStorage.storagingChunk.Add(chunkPos);
            gridUpdateFinishQueue.Enqueue(chunkPos);

            if (!_gridMgr.grids["default"].TryGetValue(chunkPos,out var grid))
            {
                if (!Pools.Ins.gridPool.TryTake(out grid))
                    grid = new Grid();
                _gridMgr.AddChunkGrids("default", chunkPos, grid ?? new Grid());
            }
            if (!_gridMgr.grids["lucency"].TryGetValue(chunkPos , out var grid2))
            {
                if(!Pools.Ins.gridPool.TryTake(out grid2))
                    grid2 = new Grid();
                _gridMgr.AddChunkGrids("lucency",chunkPos, grid2 ?? new Grid());
            }
                
            Grid[] grids = { grid ?? new Grid(), grid2 ?? new Grid() };
            GridGenerator.BuildGrid(_chunkMgr, chunkPos, ref grids);

            if (!_gridMgr.grids["lucency"].ContainsKey(chunkPos) && grid2?.vertices?.Length == 0)
                grid2.Dispose();

        }

        private void DeleteRanderChunk(Vector3i chunkPos)
        {
            _gridMgr.RemoveChunkGrids("default", chunkPos);
            _gridMgr.RemoveChunkGrids("lucency", chunkPos);

            gridDeleteFinishQueue.Enqueue(chunkPos);
        }

        public void CommitChunkUpdateRequest(Vector3i chunkPos)
        {
            bool hasChunk = _chunkMgr.HasChunk(chunkPos);
            bool hasRequest = chunkCreateRequest.Contains(chunkPos);
            if (!hasChunk && !hasRequest)
            {
                chunkCreateRequest.Add(chunkPos);
                chunkCreateQueue.Enqueue(chunkPos);
            }
        }

        public void CommitChunkDeleteRequest(Vector3i chunkPos)
        {
            bool hasChunk = _chunkMgr.HasChunk(chunkPos);
            bool hasRequest = chunkDeleteRequest.Contains(chunkPos);
            if (hasChunk && !hasRequest)
            {
                chunkDeleteRequest.Add(chunkPos);
                chunkDeleteQueue.Enqueue(chunkPos);
            } 
        }

        public void CommitGridCommitRequest(Vector3i chunkPos)
        {
            bool hasGrid = _gridMgr.grids["default"].ContainsKey(chunkPos) || _gridMgr.grids["lucency"].ContainsKey(chunkPos);
            bool hasRequest = gridCommitRequest.Contains(chunkPos);
            if (!hasGrid && !hasRequest)
            {
                gridCommitRequest.Add(chunkPos);
                gridCommitQueue.Enqueue(chunkPos);
            }
        }

        public void CommitGridUpdateRequest(Vector3i chunkPos)
        {
            bool hasRequest = gridUpdateRequest.Contains(chunkPos);
            if (!hasRequest)
            {
                gridUpdateRequest.Add(chunkPos);
                gridUpdateQueue.Enqueue(chunkPos);
            }
        }

        public void CommitGridDeleteRequest(Vector3i chunkPos)
        {
            bool hasGrid = _gridMgr.grids["default"].ContainsKey(chunkPos) || _gridMgr.grids["lucency"].ContainsKey(chunkPos);
            bool hasRequest = gridDeleteRequest.Contains(chunkPos);
            if (hasGrid && !hasRequest)
            {
                gridDeleteRequest.Add(chunkPos);
                gridDeleteQueue.Enqueue(chunkPos);
            }
        }

        public void RemoveChunkCreateRequest(Vector3i chunkPos) => chunkCreateRequest.Remove(chunkPos);
        public void RemoveGridUpdateRequest(Vector3i chunkPos) => gridUpdateRequest.Remove(chunkPos);
        public void RemoveChunkDeleteRequest(Vector3i chunkPos) => chunkDeleteRequest.Remove(chunkPos);
        public void RemoveGridCreateRequest(Vector3i chunkPos) => gridCommitRequest.Remove(chunkPos);
        public void RemoveGridDeleteRequest(Vector3i chunkPos) => gridDeleteRequest.Remove(chunkPos);
    }    
}
