using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TextCraft.src.Table
{
    /// <summary>
    /// UI 全局配置（Config/Tables/ui/ui.json）。
    /// 结构：图集 → 字体 → 主题（精灵引用 / 颜色）→ 渲染选项。
    /// </summary>
    public sealed class UIConfigFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("atlases")]
        public List<UIAtlasConfig> Atlases { get; set; } = new();

        [JsonPropertyName("fonts")]
        public List<UIFontConfig> Fonts { get; set; } = new();

        [JsonPropertyName("theme")]
        public UIThemeConfig Theme { get; set; } = new();

        [JsonPropertyName("render")]
        public UIRenderConfig Render { get; set; } = new();
    }

    public sealed class UIAtlasConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>纹理 PNG 路径（相对程序输出目录）。</summary>
        [JsonPropertyName("texture")]
        public string Texture { get; set; } = "";

        /// <summary>精灵表 XML 路径（相对程序输出目录）。</summary>
        [JsonPropertyName("spirits")]
        public string Spirits { get; set; } = "";

        /// <summary><c>nearest</c> 或 <c>linear</c>。</summary>
        [JsonPropertyName("filter")]
        public string Filter { get; set; } = "nearest";

        [JsonPropertyName("mipmap")]
        public bool Mipmap { get; set; } = true;
    }

    public sealed class UIFontConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>字体文件路径（ttf/ttc/otf，相对程序输出目录）。</summary>
        [JsonPropertyName("file")]
        public string File { get; set; } = "";

        /// <summary>ttc 里选择字族；留空用第一个。</summary>
        [JsonPropertyName("family")]
        public string Family { get; set; } = "";

        [JsonPropertyName("size")]
        public float Size { get; set; } = 20f;

        [JsonPropertyName("style")]
        public string Style { get; set; } = "Regular";

        /// <summary><c>ascii</c> / <c>ascii+</c> / <c>none</c>。</summary>
        [JsonPropertyName("charset")]
        public string Charset { get; set; } = "ascii";

        /// <summary>额外需要的字符（中文界面文案直接写在这里）。</summary>
        [JsonPropertyName("extraChars")]
        public string ExtraChars { get; set; } = "";

        /// <summary>Unicode 区段，例如 <c>0x4E00-0x4EFF</c>。</summary>
        [JsonPropertyName("ranges")]
        public List<string> Ranges { get; set; } = new();

        [JsonPropertyName("atlasWidth")]
        public int AtlasWidth { get; set; } = 512;

        /// <summary>行距倍率，1 表示字体自身行高。</summary>
        [JsonPropertyName("lineSpacing")]
        public float LineSpacing { get; set; } = 1f;
    }

    public sealed class UIThemeConfig
    {
        [JsonPropertyName("font")]
        public string Font { get; set; } = "";

        [JsonPropertyName("fontSmall")]
        public string FontSmall { get; set; } = "";

        [JsonPropertyName("fontTitle")]
        public string FontTitle { get; set; } = "";

        /// <summary>键 → "图集名:精灵名"。</summary>
        [JsonPropertyName("sprites")]
        public Dictionary<string, string> Sprites { get; set; } = new();

        /// <summary>键 → [r, g, b, a]（0~1）。</summary>
        [JsonPropertyName("colors")]
        public Dictionary<string, float[]> Colors { get; set; } = new();
    }

    public sealed class UIRenderConfig
    {
        /// <summary><c>batched</c>（默认，多层级合批）或 <c>legacy</c>（每个控件一次 DrawCall）。</summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "batched";

        /// <summary>是否允许跨越"中间无重叠"的批次做进一步合并。</summary>
        [JsonPropertyName("coalesce")]
        public bool Coalesce { get; set; } = true;

        [JsonPropertyName("coalesceWindow")]
        public int CoalesceWindow { get; set; } = 64;
    }

    /// <summary>
    /// ui.json 加载器：解析 + 校验 + 一次性抛出所有错误。
    /// </summary>
    public static class UIConfigLoader
    {
        public const int SupportedVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static UIConfigFile Load(string path)
        {
            if (!File.Exists(path))
                throw new TableLoadException($"UI 配置文件不存在: {path}");

            UIConfigFile? config;
            try
            {
                string json = File.ReadAllText(path);
                config = JsonSerializer.Deserialize<UIConfigFile>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new TableLoadException($"UI 配置解析失败: {path}\n  - {ex.Message}", ex);
            }

            if (config == null)
                throw new TableLoadException($"UI 配置为空: {path}");

            var errors = new List<string>();
            Validate(config, path, errors);

            if (errors.Count > 0)
                throw new TableLoadException($"UI 配置校验失败: {path}\n  - " + string.Join("\n  - ", errors));

            return config;
        }

        private static void Validate(UIConfigFile config, string path, List<string> errors)
        {
            if (config.Version != SupportedVersion)
                errors.Add($"version={config.Version} 不受支持（当前支持 {SupportedVersion}）");

            var atlasNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config.Atlases.Count == 0)
                errors.Add("atlases 为空，至少需要一张 UI 图集");

            for (int i = 0; i < config.Atlases.Count; i++)
            {
                var atlas = config.Atlases[i];
                if (string.IsNullOrWhiteSpace(atlas.Name))
                    errors.Add($"atlases[{i}] 缺少 name");
                else if (!atlasNames.Add(atlas.Name))
                    errors.Add($"atlases[{i}] 名字重复: {atlas.Name}");

                if (string.IsNullOrWhiteSpace(atlas.Texture))
                    errors.Add($"图集 '{atlas.Name}' 缺少 texture");
                else if (!File.Exists(ResolvePath(atlas.Texture)))
                    errors.Add($"图集 '{atlas.Name}' 的纹理不存在: {ResolvePath(atlas.Texture)}");

                if (!string.IsNullOrWhiteSpace(atlas.Spirits) && !File.Exists(ResolvePath(atlas.Spirits)))
                    errors.Add($"图集 '{atlas.Name}' 的精灵表不存在: {ResolvePath(atlas.Spirits)}");

                string filter = (atlas.Filter ?? "").Trim().ToLowerInvariant();
                if (filter is not ("nearest" or "linear" or ""))
                    errors.Add($"图集 '{atlas.Name}' 的 filter 只能是 nearest 或 linear，当前为 '{atlas.Filter}'");
            }

            if (config.Fonts.Count == 0)
                errors.Add("fonts 为空，至少需要一套字体");

            var fontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < config.Fonts.Count; i++)
            {
                var font = config.Fonts[i];
                if (string.IsNullOrWhiteSpace(font.Name))
                    errors.Add($"fonts[{i}] 缺少 name");
                else if (!fontNames.Add(font.Name))
                    errors.Add($"fonts[{i}] 名字重复: {font.Name}");

                if (string.IsNullOrWhiteSpace(font.File))
                    errors.Add($"字体 '{font.Name}' 缺少 file");
                else if (!File.Exists(ResolvePath(font.File)))
                    errors.Add($"字体 '{font.Name}' 的文件不存在: {ResolvePath(font.File)}");

                if (font.Size < 4f || font.Size > 512f)
                    errors.Add($"字体 '{font.Name}' 的 size 应在 4~512 之间，当前 {font.Size}");

                if (font.AtlasWidth < 64 || font.AtlasWidth > 8192)
                    errors.Add($"字体 '{font.Name}' 的 atlasWidth 应在 64~8192 之间，当前 {font.AtlasWidth}");
            }

            var theme = config.Theme ?? new UIThemeConfig();
            if (!string.IsNullOrWhiteSpace(theme.Font) && !fontNames.Contains(theme.Font))
                errors.Add($"theme.font 引用了不存在的字体 '{theme.Font}'");

            foreach (var pair in theme.Sprites)
            {
                string reference = pair.Value ?? "";
                int colon = reference.IndexOf(':');
                if (colon <= 0 || colon >= reference.Length - 1)
                {
                    errors.Add($"theme.sprites['{pair.Key}'] 的格式应为 \"图集名:精灵名\"，当前为 \"{reference}\"");
                    continue;
                }

                string atlasName = reference[..colon];
                if (!atlasNames.Contains(atlasName))
                    errors.Add($"theme.sprites['{pair.Key}'] 引用了不存在的图集 '{atlasName}'");
            }

            foreach (var pair in theme.Colors)
            {
                if (pair.Value == null || pair.Value.Length < 3)
                    errors.Add($"theme.colors['{pair.Key}'] 需要至少 3 个分量（rgba）");
            }

            string mode = (config.Render?.Mode ?? "batched").Trim().ToLowerInvariant();
            if (mode is not ("batched" or "legacy"))
                errors.Add($"render.mode 只能是 batched 或 legacy，当前为 '{config.Render?.Mode}'");

            if (config.Render != null && config.Render.CoalesceWindow < 1)
                errors.Add($"render.coalesceWindow 必须 >= 1，当前为 {config.Render.CoalesceWindow}");
        }

        /// <summary>把配置里的相对路径解析成基于程序输出目录的绝对路径。</summary>
        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (Path.IsPathRooted(path)) return path;
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
