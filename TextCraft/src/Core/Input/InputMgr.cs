using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.ChunkModel;
using TextCraft.src.Core.Config;
using TextCraft.src.Table;
using TextCraft.src.Tools;


namespace TextCraft.src.Core.Input
{
    internal class InputMgr
    {
        float _axisX = 0;
        float _axisY = 0;
        float _axisZ = 0;

        const int Scale = 4;

        public float pastMouseX = 400;
        public float pastMouseY = 300;

        public bool forward = false;
        public bool back = false;
        public bool left = false;
        public bool right = false;
        public bool up = false;
        public bool down = false;

        public int nowBlock = 1;

        private float UpdateAxis(float time,float speed,ref float axis,bool positive,bool negative)
        {
            if(axis != 0) axis += axis > 0 ? -2 * time:2 * time;
            if(MathF.Abs(axis) < time) axis = 0;

            if (positive) axis += time * Scale;
            if (negative) axis -= time * Scale;

            axis = Math.Clamp(axis, -1, 1);

            return time * speed * axis;
        }

        public Vector3 MoveUpdate(float time,float speed,Vector3 playerDir) =>
            (playerDir * new Vector3(1,0,1)).Normalized() * UpdateAxis(time, speed,ref _axisZ,forward,back) 
          +Vector3.Cross(playerDir, Vector3.UnitY).Normalized() * UpdateAxis(time,speed, ref _axisX,right,left)
          + Vector3.UnitY * UpdateAxis(time, speed, ref _axisY,up,down);

        public Matrix4 MouseMoveX(float sensitivity, float mouseX)
        {
            Vector3 arbitraryAxis = Vector3.UnitY;
            float angleRad = MathHelper.DegreesToRadians(sensitivity * (pastMouseX - mouseX));
            pastMouseX = mouseX;
            return Matrix4.CreateFromAxisAngle(arbitraryAxis, angleRad);
        }
        public Matrix4 MouseMoveY(float sensitivity, float mouseY, Vector3 cameraDir)
        {
            Vector3 arbitraryAxis = Vector3.Cross(cameraDir, Vector3.UnitY).Normalized();
            float angleRad = MathHelper.DegreesToRadians(sensitivity * (pastMouseY - mouseY));
            if (cameraDir.Y >= 0.99f && pastMouseY - mouseY > 0) angleRad = 0;
            if (cameraDir.Y <= -0.99f && pastMouseY - mouseY < 0) angleRad = 0;
            pastMouseY = mouseY;
            return Matrix4.CreateFromAxisAngle(arbitraryAxis, angleRad);
        }

        public void LeftMouseButton(World world)
        {
            if(Radial.RaycastVoxel(world.chunkDataMgr,world.playerPos,world.playerDir,5,out Vector3i hitPos,out float hitDist,out Vector3i normal))
            {
                world.chunkDataMgr.SetBlock(hitPos.X,hitPos.Y,hitPos.Z,0);

                //函数内部会去除重复请求
                Vector3i chunkPos = new Vector3i(
                    (int)Math.Floor((float)hitPos.X / ConfigMgr.Ins.worldConfig.ChunkSizeX),
                    (int)Math.Floor((float)hitPos.Y / ConfigMgr.Ins.worldConfig.ChunkSizeY),
                    (int)Math.Floor((float)hitPos.Z / ConfigMgr.Ins.worldConfig.ChunkSizeZ)
                    );

                world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos);

                if(hitPos.X - chunkPos.X * ConfigMgr.Ins.worldConfig.ChunkSizeX == 0)
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

            }
        }

        public void RightMouseButton(World world)
        {
            if (Radial.RaycastVoxel(world.chunkDataMgr, world.playerPos, world.playerDir, 5, out Vector3i hitPos, out float hitDist, out Vector3i normal))
            {
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

                world.chunkDataMgr.SetBlock(hitPos.X, hitPos.Y, hitPos.Z, nowBlock * 16 + dir);

                if (world.player.IsInterSectInAxis(world.chunkDataMgr,world.playerPos))
                {
                    world.chunkDataMgr.SetBlock(hitPos.X, hitPos.Y, hitPos.Z, 0);
                    return;
                }

                Vector3i chunkPos = new Vector3i(
                    (int)Math.Floor((float)hitPos.X / ConfigMgr.Ins.worldConfig.ChunkSizeX),
                    (int)Math.Floor((float)hitPos.Y / ConfigMgr.Ins.worldConfig.ChunkSizeY),
                    (int)Math.Floor((float)hitPos.Z / ConfigMgr.Ins.worldConfig.ChunkSizeZ)
                    );

                
                world.chunkUpdateMgr.CommitGridUpdateRequest(chunkPos);

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

            }
        }
    }
}
