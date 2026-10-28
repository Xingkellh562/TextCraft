using System;
using System.Collections.Generic;
using System.IO;
using TextCraft.src.Tools.ModelImport.Fbx;

namespace TextCraft.src.Tools.ModelImport
{
    /// <summary>
    /// Autodesk FBX 导入器：支持二进制 FBX（7.x，含 deflate 压缩数组）与常见 ASCII FBX。
    /// 只提取 Mesh 的基础数据：顶点、多边形、第一套 UV、材质槽与漫反射纹理；
    /// 骨骼、蒙皮、动画、变形目标、PBR 材质参数等一律忽略。
    /// </summary>
    public sealed class FbxModelImporter : IModelImporter
    {
        private const int HeaderLength = 23;

        public string Name => "Autodesk FBX";

        public IReadOnlyList<string> Extensions { get; } = new[] { ".fbx" };

        public ImportedMesh Import(string filePath, ModelImportOptions options)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"FBX 文件不存在: {filePath}", filePath);

            var header = new byte[HeaderLength];
            using (var stream = File.OpenRead(filePath))
            {
                var read = stream.Read(header, 0, header.Length);
                if (read < header.Length)
                    throw new InvalidDataException($"FBX 文件过短，无法识别: {filePath}");
            }

            FbxNode root;
            if (FbxBinaryParser.HasMagic(header))
            {
                using var stream = File.OpenRead(filePath);
                root = FbxBinaryParser.Parse(stream);
            }
            else
            {
                root = FbxAsciiParser.Parse(File.ReadAllText(filePath));
            }

            return FbxSceneImporter.Import(root, filePath, options);
        }
    }
}
