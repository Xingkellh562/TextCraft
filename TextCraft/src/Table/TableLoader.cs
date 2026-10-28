using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TextCraft.src.Core.Config;

namespace TextCraft.src.Table
{
    /// <summary>
    /// 数据表加载入口。启动时调用一次：
    /// 先加载模型表（Config/Tables/models），再加载方块表（Config/Tables/blocks.json，
    /// 方块按名字引用模型）。加载失败会抛出 <see cref="TableLoadException"/>，
    /// 由调用方决定如何提示与退出。
    /// </summary>
    public static class TableLoader
    {
        /// <summary>是否已完成加载。</summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>加载全部数据表（幂等）。</summary>
        public static void LoadAll()
        {
            if (IsLoaded)
                return;

            var stopwatch = Stopwatch.StartNew();

            var models = ModelTable.Ins;
            models.Load(ResourcePaths.ModelsDirectory);

            var blocks = BlockTable.Ins;
            blocks.Load(ResourcePaths.BlocksFile, models);

            var importedModels = ImportedModelTable.Ins;
            importedModels.Load(ResourcePaths.ImportedModelsFile);

            stopwatch.Stop();
            IsLoaded = true;

            Console.WriteLine(
                $"[资源] 已加载模型 {models.Count} 个、方块 {blocks.Count} 种、外部模型 {importedModels.Count} 个，" +
                $"耗时 {stopwatch.ElapsedMilliseconds} ms。");
        }

        /// <summary>打印数据表摘要，供 --validate-tables 使用。</summary>
        public static void PrintSummary()
        {
            var models = ModelTable.Ins;
            var blocks = BlockTable.Ins;

            Console.WriteLine("模型列表：");
            foreach (var name in models.Models.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var model = models.Models[name];
                Console.WriteLine($"  {name,-16} 面数={model.faces.Length}");
            }

            Console.WriteLine("方块列表：");
            foreach (var definition in blocks.Definitions.OrderBy(d => d.Id).ThenBy(d => d.Type))
            {
                var modelName = string.IsNullOrWhiteSpace(definition.ModelName) ? "-" : definition.ModelName;
                Console.WriteLine(
                    $"  {definition.Id,3}:{definition.Type}  {definition.Name,-12} {definition.BlockTypeName,-16} model={modelName}");
            }

            Console.WriteLine("外部模型列表：");
            var importedModels = ImportedModelTable.Ins;
            if (importedModels.Count == 0)
            {
                Console.WriteLine("  （无，可在 Config/Tables/imported_models.json 中配置）");
                return;
            }

            foreach (var model in importedModels.Models.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                var texture = model.Mesh.GeneratedTexturePixels is { Length: > 0 } &&
                              model.Mesh.GeneratedTextureWidth > 0
                    ? $"调色板({model.Mesh.GeneratedTextureWidth}x{model.Mesh.GeneratedTextureHeight})"
                    : string.IsNullOrWhiteSpace(model.TexturePath)
                        ? (model.Mesh.BaseColor is { } color
                            ? $"基础色({color.X:0.##},{color.Y:0.##},{color.Z:0.##},{color.W:0.##})"
                            : "无（白色占位）")
                        : (File.Exists(model.TexturePath)
                            ? Path.GetFileName(model.TexturePath)
                            : Path.GetFileName(model.TexturePath) + "（缺失）");
                Console.WriteLine(
                    $"  {model.Name,-16} 顶点={model.Mesh.VertexCount,5} 三角={model.Mesh.TriangleCount,5} " +
                    $"纹理={texture} 来源={Path.GetFileName(model.SourceFile)}");
                Console.WriteLine(
                    $"                  包围盒 min=({model.Mesh.Min.X:F3}, {model.Mesh.Min.Y:F3}, {model.Mesh.Min.Z:F3}) " +
                    $"max=({model.Mesh.Max.X:F3}, {model.Mesh.Max.Y:F3}, {model.Mesh.Max.Z:F3})");
            }
        }
    }
}
