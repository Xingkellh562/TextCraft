using System;

namespace TextCraft.src.Table
{
    /// <summary>
    /// 实体模型兼容层。实体模型不再硬编码在代码里，而是与方块模型共用
    /// Config/Tables/models 下的模型资源（默认名字为 textEntity）。
    /// 保留 model1 入口是为了不大范围改动实体渲染代码。
    /// </summary>
    internal static class EntityModelTable
    {
        public const string ModelName = "textEntity";

        private static bool _missingWarned;

        public static Model model1
        {
            get
            {
                if (ModelTable.Ins.TryGet(ModelName, out var model))
                    return model;

                if (!_missingWarned)
                {
                    _missingWarned = true;
                    Console.WriteLine($"[EntityModelTable] 警告: 模型 '{ModelName}' 未加载，实体将不可见（请检查 Config/Tables/models）。");
                }

                return new Model();
            }
        }
    }
}
