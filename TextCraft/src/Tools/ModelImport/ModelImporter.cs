using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Mathematics;

namespace TextCraft.src.Tools.ModelImport
{
    /// <summary>导入外部模型时可用的转换选项（由 imported_models.json 提供）。</summary>
    public sealed class ModelImportOptions
    {
        /// <summary>统一缩放系数，默认 1。</summary>
        public float Scale { get; init; } = 1f;

        /// <summary>导入后应用的偏移（世界单位）。</summary>
        public Vector3 Offset { get; init; } = Vector3.Zero;

        /// <summary>是否翻转 V 坐标。标准 OBJ 的 V 原点在图片底部，而引擎纹理上传按图片顶部为 V=0，默认翻转为 true。</summary>
        public bool FlipV { get; init; } = true;

        /// <summary>可选的纹理覆盖路径（已解析为绝对路径）；为空时使用模型自带的材质纹理。</summary>
        public string? TextureOverride { get; init; }
    }

    /// <summary>
    /// 导入后的网格数据。顶点格式与内置 <see cref="TextCraft.src.Rendering.Grid"/> 完全一致：
    /// 每个顶点 6 个 float（x, y, z, u, v, ao），按三角形顺序展开；因此可以直接构造 Grid 渲染。
    /// </summary>
    public sealed class ImportedMesh
    {
        public float[] Vertices { get; init; } = Array.Empty<float>();

        public Vector3 Min { get; init; }

        public Vector3 Max { get; init; }

        /// <summary>模型自带的纹理绝对路径（来自 MTL 的 map_Kd 或配置覆盖），可能为空。</summary>
        public string? TexturePath { get; init; }

        /// <summary>
        /// 没有纹理时使用的基础材质颜色（OBJ MTL 的 Kd / FBX 材质的 DiffuseColor）。
        /// 有纹理时忽略；多材质模型只取第一个材质的颜色。
        /// </summary>
        public Vector4? BaseColor { get; init; }

        /// <summary>程序生成的 RGBA 纹理像素（行优先）；例如纯色 MTL 烘焙出的 1×N 调色板。</summary>
        public byte[]? GeneratedTexturePixels { get; init; }

        public int GeneratedTextureWidth { get; init; }

        public int GeneratedTextureHeight { get; init; }

        /// <summary>导入过程中收集的非致命警告。</summary>
        public List<string> Warnings { get; init; } = new();

        public int VertexCount => Vertices.Length / 6;

        public int TriangleCount => Vertices.Length / 18;
    }

    /// <summary>
    /// 外部模型格式导入器。内置实现为 Wavefront OBJ（<see cref="ObjModelImporter"/>），
    /// 其它格式（如 glTF）可以注册新的实现，见 <see cref="ModelImporterRegistry"/>。
    /// </summary>
    public interface IModelImporter
    {
        /// <summary>格式名称（用于日志）。</summary>
        string Name { get; }

        /// <summary>支持的文件扩展名（含点，不区分大小写）。</summary>
        IReadOnlyList<string> Extensions { get; }

        /// <summary>把文件转换为内置渲染顶点格式。</summary>
        ImportedMesh Import(string filePath, ModelImportOptions options);
    }

    /// <summary>导入器注册表：按文件扩展名查找导入器，便于后续增加格式。</summary>
    public static class ModelImporterRegistry
    {
        private static readonly List<IModelImporter> _importers = new()
        {
            new ObjModelImporter(),
            new FbxModelImporter(),
        };

        public static IReadOnlyList<IModelImporter> Importers => _importers;

        /// <summary>注册一个自定义导入器。</summary>
        public static void Register(IModelImporter importer)
        {
            ArgumentNullException.ThrowIfNull(importer);
            _importers.Add(importer);
        }

        /// <summary>按扩展名查找导入器，找不到返回 null。</summary>
        public static IModelImporter? FindFor(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            foreach (var importer in _importers)
            {
                foreach (var supported in importer.Extensions)
                {
                    if (string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase))
                        return importer;
                }
            }

            return null;
        }

        /// <summary>当前所有导入器支持的扩展名。</summary>
        public static IEnumerable<string> SupportedExtensions()
        {
            foreach (var importer in _importers)
            {
                foreach (var extension in importer.Extensions)
                    yield return extension;
            }
        }
    }
}
