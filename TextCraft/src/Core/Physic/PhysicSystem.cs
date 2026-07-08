using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.EntityModule;
using TextCraft.src.Table;

namespace TextCraft.src.Core.Physic
{
    internal class PhysicSystem
    {
        public void Update(World world, float time)
        {

            //CollisionDetection(chunkMgr, time, body, box);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[]{ typeof(Transform), typeof(RigidBody), typeof(Box) }))
            {
                var body = world.ecsMgr.GetComponent<RigidBody>(entity);
                var trans = world.ecsMgr.GetComponent<Transform>(entity);
                var box = world.ecsMgr.GetComponent<Box>(entity);

                body.velocity += body.acceleration * time;

                if (body.useGravity)
                {
                    body.acceleration.Y = -9.8f * 4;
                }
                else
                {
                    body.damp.Y *= 5;
                    body.acceleration.Y = 0;
                }

                body.velocity -= new Vector3(body.damp.X * body.velocity.X, body.damp.Y * body.velocity.Y, body.damp.Z * body.velocity.Z);

                if(!body.useGravity)  body.damp.Y /= 5;

                CollisionDetection(world.chunkDataMgr,time,ref body,box,ref trans);

                world.ecsMgr.AddComponent<RigidBody>(entity,body);
                world.ecsMgr.AddComponent<Transform>(entity,trans);
                world.ecsMgr.AddComponent<Box>(entity,box);
            }
        }

        public void CollisionDetection(ChunkDataMgr chunkMgr, float time,ref RigidBody body,Box box,ref Transform trans)
        {
            body.onGround = false;

            // 1. 子步迭代：防止速度过快时直接穿过薄方块（体素大小为1，单步移动不超过0.3格比较安全）
            float maxStepDistance = 0.3f;
            float totalMovement = body.velocity.Length * time;
            int steps = Math.Max(1, (int)Math.Ceiling(totalMovement / maxStepDistance));
            float subTime = time / steps;

            for (int step = 0; step < steps; step++)
            {
                // --- X轴：先移动，再修正，最后归零速度 ---
                Vector3 oldPosX = trans.position;
                trans.position.X += body.velocity.X * subTime;
                if (IsInterSectInAxis(chunkMgr, trans.position, box))
                {
                    trans.position = oldPosX;
                    if (body.velocity.X > 0) // 向右撞墙
                        trans.position.X = (float)Math.Round(trans.position.X + box.MaxPos.X) - box.MaxPos.X - 1e-4f;
                    else if (body.velocity.X < 0) // 向左撞墙
                        trans.position.X = (float)Math.Round(trans.position.X + box.MinPos.X) - box.MinPos.X + 1e-4f;

                    body.velocity.X = 0; // 只有修正完位置后，才能放心归零速度
                }

                // --- Y轴：同上（注意地面的处理）---
                Vector3 oldPosY = trans.position;
                trans.position.Y += body.velocity.Y * subTime;
                if (IsInterSectInAxis(chunkMgr, trans.position, box))
                {
                    trans.position = oldPosY;
                    if (body.velocity.Y > 0) // 头顶天花板
                        trans.position.Y = (float)Math.Round(trans.position.Y + box.MaxPos.Y) - box.MaxPos.Y - 1e-4f;

                    else if (body.velocity.Y < 0) // 脚踩地板
                    {
                        trans.position.Y = (float)Math.Round(trans.position.Y + box.MinPos.Y) - box.MinPos.Y + 1e-4f;
                        body.onGround = true; // 只有确定着地才标记
                    }
                    body.velocity.Y = 0;
                }

                // --- Z轴：同X轴逻辑 ---
                Vector3 oldPosZ = trans.position;
                trans.position.Z += body.velocity.Z * subTime;
                if (IsInterSectInAxis(chunkMgr, trans.position, box))
                {
                    trans.position = oldPosZ;
                    if (body.velocity.Z > 0)
                        trans.position.Z = (float)Math.Round(trans.position.Z + box.MaxPos.Z) - box.MaxPos.Z - 1e-4f;
                    else if (body.velocity.Z < 0)
                        trans.position.Z = (float)Math.Round(trans.position.Z + box.MinPos.Z) - box.MinPos.Z + 1e-4f;

                    body.velocity.Z = 0;
                }
            }
            
        }

        public bool IsInterSectInAxis(ChunkDataMgr chunkMgr, Vector3 position,Box box)
        {
            List<Vector3i> voxels = GetDetermin(position, box);

            foreach (var voxel in voxels)
            {
                if (BlockTable.Ins[chunkMgr.GetBlock(voxel.X, voxel.Y, voxel.Z)].BlockType == BlockType.Solid || BlockTable.Ins[chunkMgr.GetBlock(voxel.X, voxel.Y, voxel.Z)].BlockType == BlockType.IncompleteBlock)
                    return true;
            }
            return false;
        }

        public List<Vector3i> GetDetermin(Vector3 pos,Box box)
        {
            List<Vector3i> result = new List<Vector3i>();
            Vector3 worldPosA = box.MinPos + pos;
            Vector3 worldPosB = box.MaxPos + pos;

            int ax = (int)MathF.Floor(worldPosA.X);
            int ay = (int)MathF.Floor(worldPosA.Y);
            int az = (int)MathF.Floor(worldPosA.Z);

            int bx = (int)MathF.Floor(worldPosB.X);
            int by = (int)MathF.Floor(worldPosB.Y);
            int bz = (int)MathF.Floor(worldPosB.Z);

            for (int x = ax; x <= bx; x++)
                for (int y = ay; y <= by; y++)
                    for (int z = az; z <= bz; z++)
                        result.Add(new Vector3i(x, y, z));

            return result;

        }
    }
}
