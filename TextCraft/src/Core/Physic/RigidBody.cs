using System;
using System.Collections;
using TextCraft.src.Table;
using OpenTK.Mathematics;
using System.Text;
using TextCraft.src.Core.ChunkModel;
using System.Data;
using System.Diagnostics;

namespace TextCraft.src.Core.Physic
{
    internal class RigidBody
    {
        public Vector3 pos;
        float _mass;
        public float Mass => _mass;
        public Vector3 velocity;
        public Vector3 acceleration;
        public float damp = 0.05f;

        public bool onGround;

        Queue<Vector3> _forces = new Queue<Vector3> ();

        Box box = new Box(new Vector3(-0.16f, -1.7f, -0.16f), new Vector3(0.16f, 0.2f, 0.16f));

        public RigidBody(float mass)
        {
            if (mass <= 0)
                mass = 0.01f;
            _mass = mass;
        }

        public void AddForce(Vector3 force)
        {
            _forces.Enqueue(force);
        }

        public void Update(ChunkDataMgr chunkMgr,float time)
        {
            for(int i = 0;i<_forces.Count;i++)
            {
                Vector3 force = _forces.Dequeue();
                acceleration += force/_mass;
                _forces.Enqueue(force);
            }

            CollisionDetection(chunkMgr, time);

            velocity += acceleration * time;
            velocity -= damp * velocity;

            while (_forces.TryDequeue(out var force))
                acceleration -= force / _mass;
        }

        public void CollisionDetection(ChunkDataMgr chunkMgr, float time)
        {
            onGround = false;

            // 1. 子步迭代：防止速度过快时直接穿过薄方块（体素大小为1，单步移动不超过0.3格比较安全）
            float maxStepDistance = 0.3f;
            float totalMovement = velocity.Length * time;
            int steps = Math.Max(1, (int)Math.Ceiling(totalMovement / maxStepDistance));
            float subTime = time / steps;

            for (int step = 0; step < steps; step++)
            {
                // --- X轴：先移动，再修正，最后归零速度 ---
                Vector3 oldPosX = pos;
                pos.X += velocity.X * subTime;
                if (IsInterSectInAxis(chunkMgr, pos))
                {
                    pos = oldPosX;
                    if (velocity.X > 0) // 向右撞墙
                        pos.X = (float)Math.Round(pos.X + box.MaxPos.X) - box.MaxPos.X - 1e-4f;
                    else if (velocity.X < 0) // 向左撞墙
                        pos.X = (float)Math.Round(pos.X + box.MinPos.X) - box.MinPos.X + 1e-4f;

                    velocity.X = 0; // 只有修正完位置后，才能放心归零速度
                }

                // --- Y轴：同上（注意地面的处理）---
                Vector3 oldPosY = pos;
                pos.Y += velocity.Y * subTime;
                if (IsInterSectInAxis(chunkMgr, pos))
                {
                    pos = oldPosY;
                    if (velocity.Y > 0) // 头顶天花板
                        pos.Y = (float)Math.Round(pos.Y + box.MaxPos.Y) - box.MaxPos.Y - 1e-4f;
                    
                    else if (velocity.Y < 0) // 脚踩地板
                    {
                        pos.Y = (float)Math.Round(pos.Y + box.MinPos.Y) - box.MinPos.Y + 1e-4f;
                        onGround = true; // 只有确定着地才标记
                    }
                    velocity.Y = 0;
                }

                // --- Z轴：同X轴逻辑 ---
                Vector3 oldPosZ = pos;
                pos.Z += velocity.Z * subTime;
                if (IsInterSectInAxis(chunkMgr,pos))
                {
                    pos = oldPosZ;
                    if (velocity.Z > 0)
                        pos.Z = (float)Math.Round(pos.Z + box.MaxPos.Z) - box.MaxPos.Z - 1e-4f;
                    else if (velocity.Z < 0)
                        pos.Z = (float)Math.Round(pos.Z + box.MinPos.Z) - box.MinPos.Z + 1e-4f;

                    velocity.Z = 0;
                }
            }
        }

        public bool IsInterSectInAxis(ChunkDataMgr chunkMgr,Vector3 position)
        {
            List<Vector3i> voxels = box.GetDetermin(position);

            foreach(var voxel in voxels)
            {
                if(BlockTable.Ins[chunkMgr.GetBlock(voxel.X, voxel.Y, voxel.Z)].BlockType==BlockType.Solid|| BlockTable.Ins[chunkMgr.GetBlock(voxel.X, voxel.Y, voxel.Z)].BlockType == BlockType.IncompleteBlock)
                    return true;
            }
            return false;
        }
    }
}
