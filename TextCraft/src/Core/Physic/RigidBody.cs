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

            bool[] makeZero = CollisionDetection(chunkMgr, time);

            pos += velocity * time;

            velocity += acceleration * time;
            velocity -= damp * velocity;

            if (makeZero[0]) velocity.X = 0;
            if (makeZero[1]) velocity.Y = 0;
            if (makeZero[2]) velocity.Z = 0;

            while (_forces.TryDequeue(out var force))
                acceleration -= force / _mass;
        }

        public bool[] CollisionDetection(ChunkDataMgr chunkMgr,float time)
        {
            bool[] result = { false, false, false };
            if (IsInterSectInAxis(chunkMgr, Vector3.UnitX, time))
            {
                //float xMove = velocity.X * time;
                //if (xMove > 0) // 向右
                //{
                //    float worldMaxX = pos.X + xMove + box.MaxPos.X;
                //    velocity.X = ((float)Math.Floor(worldMaxX) - box.MaxPos.X - pos.X) / time;

                //}
                //else if (xMove < 0) // 向左
                //{
                //    float worldMinX = pos.X + xMove + box.MinPos.X;
                //    velocity.X = ((float)Math.Floor(worldMinX) + 1 - box.MinPos.X - pos.X) / time;
                //}
                //else
                    velocity.X = 0;
                //result[0] = true;
            }
            if (IsInterSectInAxis(chunkMgr, Vector3.UnitY, time))
            {
                //float yMove = velocity.Y * time;
                //if (yMove > 0) // 向上
                //{
                //    // 顶部碰到方块底部：maxPos 对齐到 floor(worldMaxPos)
                //    float worldMaxY = pos.Y + yMove + box.MaxPos.Y;
                //    velocity.Y = ((float)Math.Floor(worldMaxY) - box.MaxPos.Y - pos.Y) / time;

                //}
                //else if (yMove < 0) // 向下
                //{
                //    //底部碰到方块顶部：minPos 对齐到 floor(worldMinY) + 1
                //    float worldMinY = pos.Y + yMove + box.MinPos.Y;
                //    velocity.Y = ((float)Math.Floor(worldMinY) + 1 - box.MinPos.Y - pos.Y) / time;
                //}
                //else
                    velocity.Y = 0;
                //result[1] = true;
            }
            if (IsInterSectInAxis(chunkMgr, Vector3.UnitZ, time))
            {
                //float zMove = velocity.Z * time;
                //if (zMove > 0)
                //{
                //    float worldMaxZ = pos.Z + zMove + box.MaxPos.Z;
                //    velocity.Z = ((float)Math.Floor(worldMaxZ) - box.MaxPos.Z - pos.Z) / time;

                //}
                //else if (zMove < 0)
                //{
                //    float worldMinZ = pos.Z + zMove + box.MinPos.Z;
                //    velocity.Z = ((float)Math.Floor(worldMinZ) + 1 - box.MinPos.Z - pos.Z) / time;
                //}
                //else
                    velocity.Z = 0;
                //result[2] = true;
            }
            return result;
        }

        public bool IsInterSectInAxis(ChunkDataMgr chunkMgr,Vector3 axis,float time)
        {
            Vector3 fv = pos + axis * velocity * time;
            List<Vector3i> voxels = box.GetDetermin(fv);

            foreach(var voxel in voxels)
            {
                if(BlockTable.Ins[chunkMgr.GetBlock(voxel.X, voxel.Y, voxel.Z)].BlockType==BlockType.Solid|| BlockTable.Ins[chunkMgr.GetBlock(voxel.X, voxel.Y, voxel.Z)].BlockType == BlockType.IncompleteBlock)
                    return true;
            }
            return false;
        }
    }
}
