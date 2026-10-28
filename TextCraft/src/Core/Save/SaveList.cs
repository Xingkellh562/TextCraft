using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TextCraft.src.Core.Save
{
    /// <summary>
    /// 一个存档槽位的信息（由目录 + worldData.xml 推导，不改动存档格式）。
    /// </summary>
    public sealed class SaveSlotInfo
    {
        /// <summary>世界名 = 存档目录名。</summary>
        public string Name = "";

        public string DirectoryPath = "";

        /// <summary>世界种子（worldData.xml 里的 seed；缺失时为 0）。</summary>
        public int Seed;

        public DateTime LastWriteTime;

        /// <summary>是否有 worldData.xml（没有的话是残缺存档，仍可列出）。</summary>
        public bool HasWorldData;

        /// <summary>下拉列表里显示的一行文字。</summary>
        public string Display
        {
            get
            {
                string seed = Seed == 0 ? "随机种子" : Seed.ToString();
                return $"{Name}    种子 {seed}    {LastWriteTime:MM-dd HH:mm}";
            }
        }

        public override string ToString() => Display;
    }

    /// <summary>
    /// 存档目录扫描与名字校验。
    ///
    /// 存档布局（沿用旧实现，未做任何格式改动）：
    /// <code>
    /// Save/
    /// └─ 世界名/
    ///    ├─ worldData.xml   // 相机位置/朝向 + seed
    ///    └─ chunkData/      // 每个区块一个 .bin（仅保存被修改过的区块）
    /// </code>
    /// </summary>
    public static class SaveList
    {
        /// <summary>世界名最大长度（要能被下拉框完整显示）。</summary>
        public const int MaxNameLength = 24;

        private static readonly char[] InvalidNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        /// <summary>存档根目录（程序输出目录下的 Save）。</summary>
        public static string Root => Path.Combine(AppContext.BaseDirectory, "Save");

        /// <summary>枚举所有存档，按最后游玩时间倒序。</summary>
        public static List<SaveSlotInfo> Enumerate()
        {
            var result = new List<SaveSlotInfo>();
            if (!Directory.Exists(Root)) return result;

            foreach (string directory in Directory.GetDirectories(Root))
            {
                var slot = ReadSlot(directory);
                if (slot != null) result.Add(slot);
            }

            return result.OrderByDescending(s => s.LastWriteTime).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static SaveSlotInfo? ReadSlot(string directory)
        {
            string name = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(name)) return null;

            string dataFile = Path.Combine(directory, "worldData.xml");
            bool hasChunkData = Directory.Exists(Path.Combine(directory, "chunkData"));
            bool hasWorldData = File.Exists(dataFile);

            // 既没有世界数据也没有区块的目录不是存档（例如空目录），忽略
            if (!hasWorldData && !hasChunkData) return null;

            var slot = new SaveSlotInfo
            {
                Name = name,
                DirectoryPath = directory,
                HasWorldData = hasWorldData,
            };

            if (hasWorldData)
            {
                try
                {
                    var document = XDocument.Load(dataFile);
                    string? seedText = document.Root?.Element("seed")?.Value;
                    slot.Seed = int.TryParse(seedText, out int seed) ? seed : 0;
                }
                catch
                {
                    // 存档文件损坏时仍然列出，种子按 0 处理
                }
                slot.LastWriteTime = File.GetLastWriteTime(dataFile);
            }
            else
            {
                slot.LastWriteTime = Directory.GetLastWriteTime(directory);
            }

            return slot;
        }

        /// <summary>是否存在同名存档。</summary>
        public static bool Exists(string name)
            => !string.IsNullOrWhiteSpace(name) && Directory.Exists(Path.Combine(Root, name));

        /// <summary>
        /// 校验世界名是否可以安全地作为目录名。返回 null 表示合法，否则返回提示文字。
        /// </summary>
        public static string? ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "请输入世界名称";

            name = name.Trim();
            if (name.Length > MaxNameLength)
                return $"名称最多 {MaxNameLength} 个字符";

            if (name == "." || name == "..")
                return "名称不合法";

            if (name.IndexOfAny(InvalidNameChars) >= 0)
                return "名称不能包含 \\ / : * ? \" < > |";

            foreach (char c in name)
            {
                if (char.IsControl(c)) return "名称不能包含控制字符";
            }

            return null;
        }
    }
}
