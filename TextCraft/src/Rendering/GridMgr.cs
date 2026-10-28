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
            if(!grids.ContainsKey(layer)) return false;
            return grids[layer].TryAdd(pos, grid);
        }

        /// <summary>
        /// 覆盖式放置网格：目标位置已有网格时先回收旧的，再放入新的。
        /// 用于把实体网格替换成外部导入模型等场景（AddChunkGrids 在键冲突时会失败）。
        /// </summary>
        public bool SetChunkGrids(string layer, Vector3 pos, Grid grid)
        {
            if (!grids.ContainsKey(layer))
                return false;

            if (grids[layer].TryRemove(pos, out var oldGrid) && oldGrid != null)
            {
                oldGrid.ResetForPool();
                Pools.Ins.gridPool.Enter(oldGrid);
            }

            return grids[layer].TryAdd(pos, grid);
        }

        public void RemoveChunkGrids(string layer, Vector3 pos)
        {
            if (!grids.ContainsKey(layer)) return;
            if (grids[layer].TryRemove(pos,out var grid))
            {
                grid.ResetForPool();
                Pools.Ins.gridPool.Enter(grid);
            }
        }
    }
}
