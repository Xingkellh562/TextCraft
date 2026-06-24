using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;

namespace TextCraft.src.Rendering
{
    internal class GridMgr
    {
        public ConcurrentDictionary<string, ConcurrentDictionary<Vector3,Grid>> grids = new();

        public List<string> layers = new List<string>();
        public void AddNewLayers(string name)
        {
            grids.TryAdd(name, new ConcurrentDictionary<Vector3, Grid>());
            layers.Add(name);
        }

        public void DeleteLayers(string name)
        {
            grids.TryRemove(name,out var grid);
            layers.Remove(name);
        }

        public bool AddChunkGrids(string layer,Vector3 pos,Grid grid)
        {
            return grids[layer].TryAdd(pos, grid);
        }

        public void RemoveChunkGrids(string layer, Vector3 pos)
        {
            if(grids[layer].TryRemove(pos,out var grid))
            {
                grid.ResetForPool();
                Pools.Ins.gridPool.Enter(grid);
            }
        }
    }
}
