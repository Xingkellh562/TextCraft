using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core.Config;

namespace TextCraft.src.Core.Save
{
    public class ChunkStorage
    {
        private string _path;
        private bool _useCompression;

        private int _chunkCount;

        public ConcurrentQueue<Vector3i> chunkStorageQueue = new();
        public HashSet<Vector3i> storagingChunk = new();
        public ConcurrentQueue<Vector3i> chunkStorageFinishQueue = new();

        private const uint MAGIC = 0x4D43564F; // "MCVO" 的十六进制
        private const byte VERSION = 1;
        public ChunkStorage(string path,bool useCompression = true) 
        {
            _path = Path.Combine(path , "chunkData");
            if (!File.Exists(_path)) Directory.CreateDirectory(_path);
            _useCompression = useCompression;
            _chunkCount = ConfigMgr.Ins.gameConfig.ChunkSizeX *
                ConfigMgr.Ins.gameConfig.ChunkSizeY *
                ConfigMgr.Ins.gameConfig.ChunkSizeZ;
        }

        private string GetChunkPath(int x,int y,int z) 
        {
            int bigChunkX = x >> 3;
            int bigChunkY = y >> 3;
            int bigChunkZ = z >> 3;
            string path = Path.Combine(_path, $"_{bigChunkX}_{bigChunkY}_{bigChunkZ}");
            if (!File.Exists(path)) Directory.CreateDirectory(path);

            return Path.Combine(path, $"_{x}_{y}_{z}.bin");
        }

        private bool TryGetChunkPath(int x, int y, int z ,out string path)
        {
            int bigChunkX = x >> 3;
            int bigChunkY = y >> 3;
            int bigChunkZ = z >> 3;
            path = Path.Combine(_path, $"_{bigChunkX}_{bigChunkY}_{bigChunkZ}");
            if (!Directory.Exists(path)) return false;

            path = Path.Combine(path, $"_{x}_{y}_{z}.bin");
            return true;
        }

        public void SaveChunkWithPos(Vector3i pos, int[] chunkData)
        {
            string path = GetChunkPath(pos.X,pos.Y,pos.Z);
            using var fileStream = new FileStream(path,FileMode.Create,FileAccess.Write);

            if (_useCompression)
            {
                using var gzip = new GZipStream(fileStream,CompressionLevel.Fastest,leaveOpen:false);
                using var bw = new BinaryWriter(gzip);
                WriteHeaderAndData(bw,pos.X,pos.Y,pos.Z,chunkData);
            }
            else
            {
                using var bw = new BinaryWriter(fileStream);
                WriteHeaderAndData(bw, pos.X, pos.Y, pos.Z, chunkData);
            }
        }

        private void WriteHeaderAndData(BinaryWriter bw,int x,int y,int z, int[] blocks)
        {
            bw.Write(MAGIC);
            bw.Write(VERSION);
            bw.Write(x);
            bw.Write(y);
            bw.Write(z);
            bw.Write(blocks.Length);

            //以字节流的形式读取int数组
            Span<byte> byteSpan = MemoryMarshal.AsBytes(blocks.AsSpan());

            bw.Write(byteSpan);
        }

        public bool TryLoadChunk(Vector3i pos, out int[] blockData)
        {
            blockData = new int[] { };
            if(!TryGetChunkPath(pos.X, pos.Y, pos.Z ,out string path)) return false;
            if (!File.Exists(path)) return false;

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            Stream readStream = fileStream;

            if (_useCompression)
                readStream = new GZipStream(fileStream,CompressionMode.Decompress,leaveOpen:false);

            using var br = new BinaryReader(readStream);
            return TryReadHeaderAndData(br, pos.X, pos.Y, pos.Z, ref blockData);
        }

        private bool TryReadHeaderAndData(BinaryReader br, int x, int y, int z,ref int[] blocks)
        {
            uint magic = br.ReadUInt32();
            if (magic != MAGIC) return false;
            byte version = br.ReadByte();

            int fileX = br.ReadInt32();
            int fileY = br.ReadInt32();
            int fileZ = br.ReadInt32();
            if (fileX != x || fileY != y || fileZ != z) return false;

            int count = br.ReadInt32();
            if(count != _chunkCount) return false;

            byte[] rawBytes = br.ReadBytes(_chunkCount * sizeof(int));
            if (rawBytes.Length != _chunkCount * sizeof(int)) return false;

            blocks = new int[_chunkCount];
            Buffer.BlockCopy(rawBytes,0,blocks,0,rawBytes.Length);
            return true;
        }

        public void Delete(int x, int y,int z)
        {
            TryGetChunkPath(x, y,z,out string path);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
