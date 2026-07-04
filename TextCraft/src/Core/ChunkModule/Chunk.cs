using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.ChunkModule
{
    public class Chunk(Vector3i size) : IDisposable
    {
        private readonly Vector3i _size = size;
        private int[]? _blocks = new int[size.X * size.Y * size.Z];
        private bool _disposed = false;

        public int this[int x, int y, int z]
        {
            get => GetBlock(x, y, z);
            set => SetBlock(x, y, z, value);
        }

        public void SetChunk(int[]? datas)
        {
            if(datas?.Length == _blocks?.Length)
                _blocks = datas;
        }
        public int[] GetChunk()
        {
            return (int[]?)_blocks?.Clone() ?? new int[0];
        }

        public int GetBlock(int x, int y, int z)
            => _blocks != null ? _blocks[x * _size.Y * _size.Z + y * _size.Z + z] : 0;

        public int GetBlock(Vector3i pos)
            => GetBlock(pos.X, pos.Y, pos.Z);

        public void SetBlock(int x, int y, int z, int value)
        {
            if (_blocks != null) 
                _blocks[x * _size.Y * _size.Z + y * _size.Z + z] = value;
        }


        public void SetBlock(Vector3i pos, int value)
            => SetBlock(pos.X, pos.Y, pos.Z, value);

        // IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);   // 阻止析构函数调用（如果定义了）
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源（此处为数组，设为 null 便于 GC 回收）
                    _blocks = null;
                    // 若还有其他托管资源（如 Stream、Event 等），在此释放
                }

                // 释放非托管资源（如果有的话），例如：
                // if (handle != IntPtr.Zero) { NativeMethods.Release(handle); handle = IntPtr.Zero; }

                _disposed = true;
            }
        }

        // 若没有非托管资源，可省略析构函数；若保留，则需取消注释
        // ~Chunk() => Dispose(false);
    }
}
