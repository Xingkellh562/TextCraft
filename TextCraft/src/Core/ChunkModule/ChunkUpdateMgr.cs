using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using TextCraft.src.Core.Config;
//using System.Threading;
using TextCraft.src.Rendering;
using TextCraft.src.Tools;

namespace TextCraft.src.Core.ChunkModule
{

    internal class ChunkUpdateMgr
    {
        ChunkDataMgr _chunkMgr;
        GridMgr _gridMgr;
        TerrainGenerator terrainGenerator = new TerrainGenerator(91919191);

        Thread updateThread;

        public ConcurrentQueue<Vector3i> chunkCreateQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> chunkDeleteQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridCommitQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridDeleteQueue = new ConcurrentQueue<Vector3i>();
        public ConcurrentQueue<Vector3i> gridUpdateQueue = new ConcurrentQueue<Vector3i>();

        public ConcurrentQueue<Vector3i> gridUpdateFinishQueue = new ConcurrentQueue<Vector3i>();

        private HashSet<Vector3i> chunkCreateRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> chunkDeleteRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> gridCommitRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> gridDeleteRequest = new HashSet<Vector3i>();
        private HashSet<Vector3i> gridUpdateRequest = new HashSet<Vector3i>();

        public ChunkUpdateMgr(ChunkDataMgr chunkMgr, GridMgr gridMgr)
        {
            _chunkMgr = chunkMgr;
            _gridMgr = gridMgr;
            updateThread = new Thread(Update);
        }


        public void StartChunkUpdate()
        {
            updateThread.Start();
        }

        public void StopChunkUpdate()
        {
            
        }
        public void Update()
        {
            while (true)
            {
                while (chunkCreateQueue.TryDequeue(out Vector3i result))
                    CreateChunk(result);
                while (chunkDeleteQueue.TryDequeue(out Vector3i result))
                    DeleteChunk(result);
                while (gridCommitQueue.TryDequeue(out Vector3i result))
                    CommitRanderChunk(result);
                while (gridDeleteQueue.TryDequeue(out Vector3i result))
                    DeleteRanderChunk(result);
                while (gridUpdateQueue.TryDequeue(out Vector3i result))
                    UpdateRanderChunk(result);

            }
        }

        private void CreateChunk(Vector3i chunkPos)
        {
            bool success = Pools.Ins.chunkPool.TryTake(out Chunk chunk);
            if(!success) chunk = new Chunk(new Vector3i(
            ConfigMgr.Ins.worldConfig.ChunkSizeX,
            ConfigMgr.Ins.worldConfig.ChunkSizeY,
            ConfigMgr.Ins.worldConfig.ChunkSizeZ
            ));

            chunk = terrainGenerator.BuildChunk(chunk, chunkPos);

            _chunkMgr.AddChunk(chunkPos, chunk);

            
        }

        private void DeleteChunk(Vector3i chunkPos)
        {
            //在函数内部入池
            _chunkMgr.TryRemoveChunk(chunkPos);
            
        }

        private void CommitRanderChunk(Vector3i chunkPos)
        {
            if(GridGenerator.BuildGrid(_chunkMgr, chunkPos,2,out Grid[] grids))
            {
                _gridMgr.AddChunkGrids("default", chunkPos, grids[0]);
                if (grids[1].vertices.Length > 0)
                    _gridMgr.AddChunkGrids("lucency", chunkPos, grids[1]);
                else
                    Pools.Ins.gridPool.Enter(grids[1]);
                grids = null;
            }
                
        }
        private void UpdateRanderChunk(Vector3i chunkPos)
        {
            gridUpdateFinishQueue.Enqueue(chunkPos);
            Grid grid = _gridMgr.grids["default"][chunkPos];

            if(!_gridMgr.grids["lucency"].TryGetValue(chunkPos , out var grid2))
            {
                if(!Pools.Ins.gridPool.TryTake(out grid2))
                    grid2 = new Grid();
                _gridMgr.AddChunkGrids("lucency",chunkPos, grid2);
            }
                
            Grid[] grids = { grid, grid2 };
            GridGenerator.BuildGrid(_chunkMgr, chunkPos, ref grids);

            if (!_gridMgr.grids["lucency"].ContainsKey(chunkPos) && grid2.vertices.Length == 0)
                grid2.Dispose();

        }

        private void DeleteRanderChunk(Vector3i chunkPos)
        {
            _gridMgr.RemoveChunkGrids("default", chunkPos);
            _gridMgr.RemoveChunkGrids("lucency", chunkPos);
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
            if(hasChunk)
                chunkCreateRequest.Remove(chunkPos);
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
            if(!hasChunk)
                chunkDeleteRequest.Remove(chunkPos);
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
            if(hasGrid)
                gridCommitRequest.Remove(chunkPos);
        }

        public void CommitGridUpdateRequest(Vector3i chunkPos)
        {
            bool hasGrid = _gridMgr.grids["default"].ContainsKey(chunkPos) || _gridMgr.grids["lucency"].ContainsKey(chunkPos);
            bool hasRequest = gridUpdateRequest.Contains(chunkPos);
            if (hasGrid && !hasRequest)
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
            if(!hasGrid)
                gridDeleteRequest.Remove(chunkPos);
        }

        public void RemoveGridUpdateRequest(Vector3i chunkPos)
        {
            gridUpdateRequest.Remove(chunkPos);
        }
    
    }    
}
