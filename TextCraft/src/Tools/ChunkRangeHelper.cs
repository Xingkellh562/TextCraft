using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TextCraft.src.Tools
{
    public static class ChunkRangeHelper
    {
        // ----- 原有的单区间函数（保留，内部调用通用函数）-----
        public static List<Vector3i> GetChunksInRange(
            int r1,
            int r2,
            Vector3 playerPos,
            Vector3i chunkSize)
        {
            var result = GetChunksInRanges(new[] { r1, r2 }, playerPos, chunkSize);
            return result.Count > 0 ? result[0] : new List<Vector3i>();
        }

        // ----- 新增通用函数（变长半径参数）-----
        /// <summary>
        /// 根据一组半径（至少两个），返回多个区块列表。
        /// 假设 radii = [R0, R1, R2, ..., Rn-1]，则返回 n-1 个列表：
        /// 列表 i 包含满足 Ri ≤ d < Ri+1 的区块（d 为区块中心到玩家的距离），
        /// 每个列表内部按距离升序排列。
        /// 半径按传入顺序解释，函数假定它们已递增（不强制，但若递减可能导致某些区间无效）。
        /// </summary>
        /// <param name="radii">半径数组，长度至少为2</param>
        /// <param name="playerPos">玩家世界坐标</param>
        /// <param name="chunkSize">区块尺寸</param>
        /// <returns>包含 count-1 个 List&lt;Vector3i&gt; 的列表</returns>
        public static List<List<Vector3i>> GetChunksInRanges(
            int[] radii,
            Vector3 playerPos,
            Vector3i chunkSize)
        {
            if (radii == null || radii.Length < 2)
                throw new ArgumentException("至少需要两个半径值", nameof(radii));

            int count = radii.Length;
            float[] radiiSq = new float[count];
            for (int i = 0; i < count; i++)
                radiiSq[i] = (float)radii[i] * radii[i];

            // 使用最大半径作为遍历边界（确保覆盖所有可能区块）
            int rMax = radii.Max();
            float rMaxSq = (float)rMax * rMax;

            // 计算各轴索引边界（基于区块中心与玩家的距离 ≤ rMax）
            int minX = (int)Math.Ceiling((playerPos.X - rMax) / chunkSize.X - 0.5f);
            int maxX = (int)Math.Floor((playerPos.X + rMax) / chunkSize.X - 0.5f);
            int minY = (int)Math.Ceiling((playerPos.Y - rMax) / chunkSize.Y - 0.5f);
            int maxY = (int)Math.Floor((playerPos.Y + rMax) / chunkSize.Y - 0.5f);
            int minZ = (int)Math.Ceiling((playerPos.Z - rMax) / chunkSize.Z - 0.5f);
            int maxZ = (int)Math.Floor((playerPos.Z + rMax) / chunkSize.Z - 0.5f);

            var candidates = new List<(Vector3i chunk, float distSq)>();

            for (int x = minX; x <= maxX; x++)
            {
                float centerX = (x + 0.5f) * chunkSize.X;
                float dx = centerX - playerPos.X;
                float dxSq = dx * dx;
                if (dxSq > rMaxSq) continue;

                for (int y = minY; y <= maxY; y++)
                {
                    float centerY = (y + 0.5f) * chunkSize.Y;
                    float dy = centerY - playerPos.Y;
                    float dySq = dy * dy;
                    float xyDistSq = dxSq + dySq;
                    if (xyDistSq > rMaxSq) continue;

                    for (int z = minZ; z <= maxZ; z++)
                    {
                        float centerZ = (z + 0.5f) * chunkSize.Z;
                        float dz = centerZ - playerPos.Z;
                        float distSq = xyDistSq + dz * dz;

                        // 只收集严格小于最大半径平方的区块（因为最大区间是 < rMax）
                        if (distSq < rMaxSq)
                        {
                            candidates.Add((new Vector3i(x, y, z), distSq));
                        }
                    }
                }
            }

            // 按距离平方升序排序
            candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));

            // 初始化结果列表（count-1 个）
            var result = new List<List<Vector3i>>(count - 1);
            for (int i = 0; i < count - 1; i++)
                result.Add(new List<Vector3i>());

            // 遍历排序后的候选，根据距离平方分配至对应区间
            foreach (var (chunk, distSq) in candidates)
            {
                // 查找该距离属于哪个区间（左闭右开）
                for (int i = 0; i < count - 1; i++)
                {
                    if (distSq >= radiiSq[i] && distSq < radiiSq[i + 1])
                    {
                        result[i].Add(chunk);
                        break; // 每个距离只可能属于一个区间（假定半径递增）
                    }
                }
            }

            return result;
        }

        // ----- 可选：params 重载方便调用 -----
        public static List<List<Vector3i>> GetChunksInRanges(
            Vector3 playerPos,
            Vector3i chunkSize,
            params int[] radii)
        {
            return GetChunksInRanges(radii, playerPos, chunkSize);
        }
    }
}
