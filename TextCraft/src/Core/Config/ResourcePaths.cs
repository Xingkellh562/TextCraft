using System;
using System.IO;

namespace TextCraft.src.Core.Config
{
    /// <summary>
    /// 运行时数据文件路径的统一入口。
    /// 所有路径均相对于程序输出目录（<see cref="AppContext.BaseDirectory"/>），
    /// 目录结构与仓库中的 RequiredResources 保持一致，构建时会自动同步。
    /// 新增资源时请优先在这里登记路径，避免在业务代码中拼接字符串。
    /// </summary>
    public static class ResourcePaths
    {
        /// <summary>程序输出目录。</summary>
        public static string BaseDirectory => AppContext.BaseDirectory;

        /// <summary>Config 目录。</summary>
        public static string ConfigDirectory => Path.Combine(BaseDirectory, "Config");

        /// <summary>游戏配置（config.xml）。</summary>
        public static string GameConfigFile => Path.Combine(ConfigDirectory, "config.xml");

        /// <summary>数据表目录（方块、模型等 JSON 配置）。</summary>
        public static string TablesDirectory => Path.Combine(ConfigDirectory, "Tables");

        /// <summary>方块定义文件（blocks.json）。</summary>
        public static string BlocksFile => Path.Combine(TablesDirectory, "blocks.json");

        /// <summary>模型定义目录（每个 *.json 可包含单个模型或模型数组）。</summary>
        public static string ModelsDirectory => Path.Combine(TablesDirectory, "models");
    }
}
