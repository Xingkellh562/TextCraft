using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TextCraft.src.Core.ChunkModule;
using TextCraft.src.Core.Config;
using TextCraft.src.Table;
using TextCraft.src.Tools;
using TextCraft.src.Core.Physic;


namespace TextCraft.src.Core.Input
{
    internal class InputMgr
    {
        private float UpdateAxis(float time,float speed,ref float axis,bool positive,bool negative,InputComponent input)
        {
            if(axis != 0) axis += axis > 0 ? -8 * time:8 * time;
            if(MathF.Abs(axis) < 0.1) axis = 0;

            if (positive) axis += time * input.Scale;
            if (negative) axis -= time * input.Scale;

            axis = Math.Clamp(axis, -1, 1);

            return time * speed * axis;
        }
        public Vector3 MoveUpdate(float time,float speed,Vector3 playerDir, bool useGravity, ref InputComponent input) =>
            (playerDir * new Vector3(1,0,1)).Normalized() * UpdateAxis(time, speed,ref input.axisZ, input.forward, input.back, input) 
          +Vector3.Cross(playerDir, Vector3.UnitY).Normalized() * UpdateAxis(time,speed, ref input.axisX, input.right, input.left, input)
          + (!useGravity ? Vector3.UnitY * UpdateAxis(time, speed, ref input.axisY, input.up, input.down, input) : Vector3.Zero);

        public Matrix4 MouseMoveX(float sensitivity, ref InputComponent input)
        {
            Vector3 arbitraryAxis = Vector3.UnitY;
            float angleRad = MathHelper.DegreesToRadians(sensitivity * (input.pastMouseX - input.mouseX));
            input.pastMouseX = input.mouseX;
            return Matrix4.CreateFromAxisAngle(arbitraryAxis, angleRad);
        }
        public Matrix4 MouseMoveY(float sensitivity, Vector3 cameraDir, ref InputComponent input)
        {
            Vector3 arbitraryAxis = Vector3.Cross(cameraDir, Vector3.UnitY).Normalized();
            float angleRad = MathHelper.DegreesToRadians(sensitivity * (input.pastMouseY - input.mouseY));
            if (cameraDir.Y >= 0.99f && input.pastMouseY - input.mouseY > 0) angleRad = 0;
            if (cameraDir.Y <= -0.99f && input.pastMouseY - input.mouseY < 0) angleRad = 0;
            input.pastMouseY = input.mouseY;
            return Matrix4.CreateFromAxisAngle(arbitraryAxis, angleRad);
        }

        public void LeftMouseButton(World world,float time,ref InputComponent input)
        {
            input.timer -= time/2;
            input.timer = Math.Clamp(input.timer,0,input.interval);
            if (input.destory && input.timer == 0 && Radial.RaycastVoxel(world.chunkDataMgr,world.playerPos,world.playerDir,5,out Vector3i hitPos,out float hitDist,out Vector3i normal))
            {
                world.chunkDataMgr.SetBlock(hitPos.X,hitPos.Y,hitPos.Z,0);

                input.timer = input.interval;

                //函数内部会去除重复请求
                Vector3i chunkPos = new Vector3i(
                    (int)Math.Floor((float)hitPos.X / ConfigMgr.Ins.worldConfig.ChunkSizeX),
                    (int)Math.Floor((float)hitPos.Y / ConfigMgr.Ins.worldConfig.ChunkSizeY),
                    (int)Math.Floor((float)hitPos.Z / ConfigMgr.Ins.worldConfig.ChunkSizeZ)
                    );

                if (hitPos.X - chunkPos.X * ConfigMgr.Ins.worldConfig.ChunkSizeX == 0)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos - Vector3i.UnitX);
                if (hitPos.Y - chunkPos.Y * ConfigMgr.Ins.worldConfig.ChunkSizeY == 0)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos - Vector3i.UnitY);
                if (hitPos.Z - chunkPos.Z * ConfigMgr.Ins.worldConfig.ChunkSizeZ == 0)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos - Vector3i.UnitZ);
                if (hitPos.X - chunkPos.X * ConfigMgr.Ins.worldConfig.ChunkSizeX == ConfigMgr.Ins.worldConfig.ChunkSizeX - 1)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos + Vector3i.UnitX);
                if (hitPos.Y - chunkPos.Y * ConfigMgr.Ins.worldConfig.ChunkSizeY == ConfigMgr.Ins.worldConfig.ChunkSizeY - 1)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos + Vector3i.UnitY);
                if (hitPos.Z - chunkPos.Z * ConfigMgr.Ins.worldConfig.ChunkSizeZ == ConfigMgr.Ins.worldConfig.ChunkSizeZ - 1)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos + Vector3i.UnitZ);

                world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos);
            }
        }

        public void RightMouseButton(World world, float time,Box box, ref InputComponent input)
        {
            input.timer -= time/2;
            input.timer = Math.Clamp(input.timer, 0, input.interval);
            if (input.build && input.timer == 0 && Radial.RaycastVoxel(world.chunkDataMgr, world.playerPos, world.playerDir, 5, out Vector3i hitPos, out float hitDist, out Vector3i normal))
            {
                input.timer = input.interval;

                hitPos -= normal;
                if (BlockTable.Ins[world.chunkDataMgr.GetBlock(hitPos.X, hitPos.Y, hitPos.Z)].BlockType == BlockType.Solid&& BlockTable.Ins[world.chunkDataMgr.GetBlock(hitPos.X, hitPos.Y, hitPos.Z)].BlockType == BlockType.IncompleteBlock)
                    return;

                int dir = 0;
                if (normal.X != 0)
                    dir = 1;
                else if (normal.Y != 0)
                    dir = 0;
                else if (normal.Z != 0)
                    dir = 2;

                int pastBlock = world.chunkDataMgr.GetBlock(hitPos.X, hitPos.Y, hitPos.Z);
                world.chunkDataMgr.SetBlock(hitPos.X, hitPos.Y, hitPos.Z, input.nowBlock * 16 + dir);

                if (world.physicSystem.IsInterSectInAxis(world.chunkDataMgr, world.playerPos,box))
                {
                    world.chunkDataMgr.SetBlock(hitPos.X, hitPos.Y, hitPos.Z, pastBlock);
                    return;
                }

                Vector3i chunkPos = new Vector3i(
                    (int)Math.Floor((float)hitPos.X / ConfigMgr.Ins.worldConfig.ChunkSizeX),
                    (int)Math.Floor((float)hitPos.Y / ConfigMgr.Ins.worldConfig.ChunkSizeY),
                    (int)Math.Floor((float)hitPos.Z / ConfigMgr.Ins.worldConfig.ChunkSizeZ)
                    );

                if (hitPos.X - chunkPos.X * ConfigMgr.Ins.worldConfig.ChunkSizeX == 0)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos - Vector3i.UnitX);
                if (hitPos.Y - chunkPos.Y * ConfigMgr.Ins.worldConfig.ChunkSizeY == 0)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos - Vector3i.UnitY);
                if (hitPos.Z - chunkPos.Z * ConfigMgr.Ins.worldConfig.ChunkSizeZ == 0)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos - Vector3i.UnitZ);
                if (hitPos.X - chunkPos.X * ConfigMgr.Ins.worldConfig.ChunkSizeX == ConfigMgr.Ins.worldConfig.ChunkSizeX - 1)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos + Vector3i.UnitX);
                if (hitPos.Y - chunkPos.Y * ConfigMgr.Ins.worldConfig.ChunkSizeY == ConfigMgr.Ins.worldConfig.ChunkSizeY - 1)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos + Vector3i.UnitY);
                if (hitPos.Z - chunkPos.Z * ConfigMgr.Ins.worldConfig.ChunkSizeZ == ConfigMgr.Ins.worldConfig.ChunkSizeZ - 1)
                    world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos + Vector3i.UnitZ);

                world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos);
            }
        }
    }
}
