using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Table;

namespace TextCraft.src.Tools
{
    internal class Radial
    {
        public static bool RaycastVoxel(ChunkDataMgr world, Vector3d origin, Vector3d dir, float maxDist, out Vector3i hitPos, out float hitDist, out Vector3i normal)
        {
            hitPos = Vector3i.Zero;
            hitDist = maxDist;
            normal = Vector3i.Zero;
            // 1. 当前格子
            int ix = (int)Math.Floor(origin.X);
            int iy = (int)Math.Floor(origin.Y);
            int iz = (int)Math.Floor(origin.Z);

            // 2. 步进方向
            int stepX = (dir.X > 0) ? 1 : (dir.X < 0) ? -1 : 0;
            int stepY = (dir.Y > 0) ? 1 : (dir.Y < 0) ? -1 : 0;
            int stepZ = (dir.Z > 0) ? 1 : (dir.Z < 0) ? -1 : 0;

            // 3. tMax 初始化
            double tMaxX, tMaxY, tMaxZ;

            if (dir.X != 0)
            {
                if (stepX > 0)
                    tMaxX = (ix + 1 - origin.X) / dir.X;
                else
                    tMaxX = (ix - origin.X) / dir.X;
            }
            else tMaxX = double.PositiveInfinity;

            if (dir.Y != 0)
            {
                if (stepY > 0)
                    tMaxY = (iy + 1 - origin.Y) / dir.Y;
                else
                    tMaxY = (iy - origin.Y) / dir.Y;
            }
            else tMaxY = double.PositiveInfinity;

            if (dir.Z != 0)
            {
                if (stepZ > 0)
                    tMaxZ = (iz + 1 - origin.Z) / dir.Z;
                else
                    tMaxZ = (iz - origin.Z) / dir.Z;
            }
            else tMaxZ = double.PositiveInfinity;

            // 4. tDelta
            double tDeltaX = (dir.X != 0) ? 1.0 / Math.Abs(dir.X) : double.PositiveInfinity;
            double tDeltaY = (dir.Y != 0) ? 1.0 / Math.Abs(dir.Y) : double.PositiveInfinity;
            double tDeltaZ = (dir.Z != 0) ? 1.0 / Math.Abs(dir.Z) : double.PositiveInfinity;

            // 5. 迭代
            double dist = 0;
            const double epsilon = 1e-8;

            while (dist < maxDist)
            {
                int lastStepX = 0, lastStepY = 0, lastStepZ = 0;
                // 选择最小 tMax 的轴
                if (tMaxX < tMaxY - epsilon && tMaxX < tMaxZ - epsilon)
                {
                    ix += stepX;
                    dist = tMaxX;
                    tMaxX += tDeltaX;
                    lastStepX = stepX;
                }
                else if (tMaxY < tMaxZ - epsilon)
                {
                    iy += stepY;
                    dist = tMaxY;
                    tMaxY += tDeltaY;
                    lastStepY = stepY;
                }
                else
                {
                    iz += stepZ;
                    dist = tMaxZ;
                    tMaxZ += tDeltaZ;
                    lastStepZ = stepZ;
                }

                // 检查方块
                if (BlockTable.Ins[world.GetBlock(ix, iy, iz)].BlockType == BlockType.Solid || BlockTable.Ins[world.GetBlock(ix, iy, iz)].BlockType == BlockType.IncompleteBlock)
                {
                    hitPos = new Vector3i(ix, iy, iz);
                    hitDist = (float)dist;
                    if (lastStepX != 0) normal = Vector3i.UnitX * lastStepX;
                    if (lastStepY != 0) normal = Vector3i.UnitY * lastStepY;
                    if (lastStepZ != 0) normal = Vector3i.UnitZ * lastStepZ;
                    return true;
                }
            }
            return false;
        }
    }
}
