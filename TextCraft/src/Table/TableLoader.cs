using System;
using System.Diagnostics;
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

            stopwatch.Stop();
            IsLoaded = true;

            Console.WriteLine($"[资源] 已加载模型 {models.Count} 个、方块 {blocks.Count} 种，耗时 {stopwatch.ElapsedMilliseconds} ms。");
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
        }
    }
}
