using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace TextCraft.src.Tools.ModelImport.Fbx
{
    /// <summary>
    /// Binary FBX（7.x）解析器。只解析节点树与属性，不解释具体业务含义；
    /// 支持 32 位（&lt; 7500）与 64 位（&gt;= 7500）节点头，以及 deflate 压缩的数组属性。
    /// </summary>
    internal static class FbxBinaryParser
    {
        // "Kaydara FBX Binary  \0\x1a\0"，共 23 字节
        private static readonly byte[] Magic =
        {
            (byte)'K', (byte)'a', (byte)'y', (byte)'d', (byte)'a', (byte)'r', (byte)'a', (byte)' ',
            (byte)'F', (byte)'B', (byte)'X', (byte)' ', (byte)'B', (byte)'i', (byte)'n', (byte)'a',
            (byte)'r', (byte)'y', (byte)' ', (byte)' ', 0x00, 0x1A, 0x00,
        };

        private const int MaxStringLength = 64 * 1024 * 1024;
        private const long MaxArrayBytes = 512L * 1024 * 1024;

        public static bool HasMagic(byte[] header)
            => header.Length >= Magic.Length && header.AsSpan(0, Magic.Length).SequenceEqual(Magic);

        public static FbxNode Parse(Stream stream)
        {
            var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var header = reader.ReadBytes(Magic.Length);
            if (header.Length != Magic.Length || !header.SequenceEqual(Magic))
                throw new InvalidDataException("不是有效的 Binary FBX 文件（文件头不匹配）");

            var version = reader.ReadUInt32();
            var is64Bit = version >= 7500;
            var root = new FbxNode { Name = "<root>" };

            while (true)
            {
                var node = ReadNode(reader, is64Bit, out var isNullRecord);
                if (isNullRecord || node == null)
                    break;

                root.Children.Add(node);
            }

            return root;
        }

        private static FbxNode? ReadNode(BinaryReader reader, bool is64Bit, out bool isNullRecord)
        {
            var start = reader.BaseStream.Position;

            var endOffset = is64Bit ? reader.ReadUInt64() : reader.ReadUInt32();
            var propertyCount = is64Bit ? reader.ReadUInt64() : reader.ReadUInt32();
            var propertyListLength = is64Bit ? reader.ReadUInt64() : reader.ReadUInt32();
            var nameLength = reader.ReadByte();

            isNullRecord = endOffset == 0 && propertyCount == 0 && propertyListLength == 0 && nameLength == 0;
            if (isNullRecord)
                return null;

            if (endOffset < (ulong)(reader.BaseStream.Position - start))
                throw new InvalidDataException($"FBX 节点偏移非法（pos={start}）");

            var nameBytes = reader.ReadBytes(nameLength);
            if (nameBytes.Length != nameLength)
                throw new EndOfStreamException("读取 FBX 节点名时文件提前结束");

            var node = new FbxNode { Name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0') };
            var propertiesEnd = reader.BaseStream.Position + (long)propertyListLength;

            for (ulong i = 0; i < propertyCount; i++)
                node.Values.Add(ReadProperty(reader));

            if (reader.BaseStream.Position != propertiesEnd)
            {
                if (reader.BaseStream.Position > propertiesEnd)
                    throw new InvalidDataException($"FBX 节点 '{node.Name}' 的属性长度与声明不一致");
                reader.BaseStream.Position = propertiesEnd;
            }

            var end = (long)endOffset;
            while (end > 0 && reader.BaseStream.Position < end)
            {
                var child = ReadNode(reader, is64Bit, out var childIsNull);
                if (childIsNull || child == null)
                    break;

                node.Children.Add(child);
            }

            return node;
        }

        private static object ReadProperty(BinaryReader reader)
        {
            var type = (char)reader.ReadByte();
            return type switch
            {
                'Y' => reader.ReadInt16(),
                'C' => reader.ReadByte(),
                'I' => reader.ReadInt32(),
                'F' => reader.ReadSingle(),
                'D' => reader.ReadDouble(),
                'L' => reader.ReadInt64(),
                'S' => ReadString(reader),
                'R' => ReadRaw(reader),
                'f' => ReadArray<float>(reader, 4, static data => ToFloatArray(data)),
                'd' => ReadArray<double>(reader, 8, static data => ToDoubleArray(data)),
                'i' => ReadArray<int>(reader, 4, static data => ToIntArray(data)),
                'l' => ReadArray<long>(reader, 8, static data => ToLongArray(data)),
                'b' => ReadArray<byte>(reader, 1, static data => data),
                'c' => ReadArray<byte>(reader, 1, static data => data),
                _ => throw new InvalidDataException($"不支持的 FBX 属性类型: '{type}'"),
            };
        }

        private static T[] ReadArray<T>(BinaryReader reader, int elementSize, Func<byte[], T[]> decode)
        {
            var length = reader.ReadUInt32();
            var encoding = reader.ReadUInt32();
            var compressedLength = reader.ReadUInt32();

            var totalBytes = (long)length * elementSize;
            if (totalBytes > MaxArrayBytes)
                throw new InvalidDataException($"FBX 数组过大: {length} 个元素");

            byte[] data;
            if ((encoding & 0x01) == 0)
            {
                data = reader.ReadBytes((int)totalBytes);
                if (data.Length != totalBytes)
                    throw new EndOfStreamException("读取 FBX 数组时文件提前结束");
            }
            else
            {
                var compressed = reader.ReadBytes((int)compressedLength);
                if (compressed.Length != compressedLength)
                    throw new EndOfStreamException("读取 FBX 压缩数组时文件提前结束");
                data = Inflate(compressed, (int)totalBytes);
            }

            return decode(data);
        }

        private static string ReadString(BinaryReader reader)
        {
            var length = reader.ReadUInt32();
            if (length > MaxStringLength)
                throw new InvalidDataException($"FBX 字符串过长: {length}");

            var bytes = reader.ReadBytes((int)length);
            if (bytes.Length != length)
                throw new EndOfStreamException("读取 FBX 字符串时文件提前结束");

            return Encoding.UTF8.GetString(bytes);
        }

        private static byte[] ReadRaw(BinaryReader reader)
        {
            var length = reader.ReadUInt32();
            if (length > MaxStringLength)
                throw new InvalidDataException($"FBX 原始数据过长: {length}");

            var bytes = reader.ReadBytes((int)length);
            if (bytes.Length != length)
                throw new EndOfStreamException("读取 FBX 原始数据时文件提前结束");

            return bytes;
        }

        private static byte[] Inflate(byte[] compressed, int expectedLength)
        {
            try
            {
                using var input = new MemoryStream(compressed);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                return ReadExactly(zlib, expectedLength);
            }
            catch (InvalidDataException)
            {
                // 少数导出器写的是裸 deflate 流，回退再试一次
                using var input = new MemoryStream(compressed);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                return ReadExactly(deflate, expectedLength);
            }
        }

        private static byte[] ReadExactly(Stream stream, int length)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = stream.Read(buffer, offset, length - offset);
                if (read <= 0)
                    throw new InvalidDataException("FBX 压缩数组解压结果长度不足");

                offset += read;
            }

            return buffer;
        }

        private static float[] ToFloatArray(byte[] data)
        {
            var result = new float[data.Length / 4];
            Buffer.BlockCopy(data, 0, result, 0, result.Length * 4);
            return result;
        }

        private static double[] ToDoubleArray(byte[] data)
        {
            var result = new double[data.Length / 8];
            Buffer.BlockCopy(data, 0, result, 0, result.Length * 8);
            return result;
        }

        private static int[] ToIntArray(byte[] data)
        {
            var result = new int[data.Length / 4];
            Buffer.BlockCopy(data, 0, result, 0, result.Length * 4);
            return result;
        }

        private static long[] ToLongArray(byte[] data)
        {
            var result = new long[data.Length / 8];
            Buffer.BlockCopy(data, 0, result, 0, result.Length * 8);
            return result;
        }
    }
}
