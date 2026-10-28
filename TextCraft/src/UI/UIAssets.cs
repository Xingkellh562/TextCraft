using System;
using System.Collections.Generic;
using System.Diagnostics;
using TextCraft.src.Core;
using TextCraft.src.Rendering;
using TextCraft.src.Table;
using TextCraft.src.Tools;

namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 资源库：ui.json 里声明的图集、字体、主题与渲染选项。
    ///
    /// 加载分两个阶段：
    /// <list type="number">
    /// <item><see cref="Load"/>：只做 CPU 侧工作（解 PNG、读精灵表、烘焙字体图集），因此可以在
    /// 没有窗口的情况下跑 <c>--validate-ui</c> 自检；</item>
    /// <item><see cref="UploadGpu"/>：在 OpenGL 上下文里创建纹理。</item>
    /// </list>
    /// </summary>
    public sealed class UIAssets : BaseSingleton<UIAssets>
    {
        private readonly Dictionary<string, Atlas> _atlases = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Atlas> _atlasOrder = new();
        private readonly Dictionary<string, UIFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<UIFont> _fontOrder = new();

        public UIConfigFile Config { get; private set; } = new UIConfigFile();
        public UITheme Theme { get; private set; } = null!;

        public IReadOnlyDictionary<string, Atlas> Atlases => _atlases;
        public IReadOnlyList<Atlas> AtlasOrder => _atlasOrder;
        public IReadOnlyDictionary<string, UIFont> Fonts => _fonts;
        public IReadOnlyList<UIFont> FontOrder => _fontOrder;

        public UIRenderMode Mode { get; private set; } = UIRenderMode.Batched;
        public bool Coalesce { get; private set; } = true;
        public int CoalesceWindow { get; private set; } = 64;

        /// <summary>运行时切换渲染模式（F4 / 控制台），便于对比 DrawCall 数量。</summary>
        public void SetMode(UIRenderMode mode) => Mode = mode;

        public bool IsLoaded { get; private set; }
        public long LoadMilliseconds { get; private set; }
        public int UnresolvedSprites { get; private set; }

        public UIFont DefaultFont => Theme.Font;

        /// <summary>
        /// 读取 ui.json 并完成 CPU 侧加载（图集解码 + 字体烘焙）。
        /// 失败抛 <see cref="TableLoadException"/>，与方块/模型表保持一致的错误风格。
        /// </summary>
        public void Load(string configPath)
        {
            if (IsLoaded) return;

            var stopwatch = Stopwatch.StartNew();

            Config = UIConfigLoader.Load(configPath);

            _atlases.Clear();
            _atlasOrder.Clear();
            foreach (var atlasConfig in Config.Atlases)
            {
                var atlas = new Atlas();
                var options = BuildTextureOptions(atlasConfig);
                try
                {
                    atlas.Load(atlasConfig.Name,
                        UIConfigLoader.ResolvePath(atlasConfig.Texture),
                        UIConfigLoader.ResolvePath(atlasConfig.Spirits),
                        options);
                }
                catch (Exception ex)
                {
                    throw new TableLoadException($"UI 图集 '{atlasConfig.Name}' 加载失败: {ex.Message}", ex);
                }
                _atlases[atlasConfig.Name] = atlas;
                _atlasOrder.Add(atlas);
            }

            _fonts.Clear();
            _fontOrder.Clear();

            // 字体烘焙是纯 CPU 工作（字形度量 + 光栅化），多套字体并行烘焙可以显著缩短启动时间
            var requests = new FontBakeRequest[Config.Fonts.Count];
            for (int i = 0; i < Config.Fonts.Count; i++)
            {
                var fontConfig = Config.Fonts[i];
                requests[i] = new FontBakeRequest
                {
                    Name = fontConfig.Name,
                    FilePath = UIConfigLoader.ResolvePath(fontConfig.File),
                    Family = fontConfig.Family,
                    Size = fontConfig.Size,
                    Style = fontConfig.Style,
                    Charset = fontConfig.Charset,
                    ExtraChars = fontConfig.ExtraChars,
                    Ranges = fontConfig.Ranges,
                    AtlasWidth = fontConfig.AtlasWidth,
                    LineSpacing = fontConfig.LineSpacing,
                };
            }

            var baked = new UIFont?[requests.Length];
            var failures = new Exception?[requests.Length];
            var bakeWatch = Stopwatch.StartNew();
            System.Threading.Tasks.Parallel.For(0, requests.Length, i =>
            {
                try
                {
                    baked[i] = FontBaker.Bake(requests[i]);
                }
                catch (Exception ex)
                {
                    failures[i] = ex;
                }
            });
            bakeWatch.Stop();

            for (int i = 0; i < requests.Length; i++)
            {
                if (failures[i] != null)
                    throw new TableLoadException($"字体 '{requests[i].Name}' 烘焙失败: {failures[i]!.Message}", failures[i]);
                _fonts[requests[i].Name] = baked[i]!;
                _fontOrder.Add(baked[i]!);
            }

            Theme = UITheme.Build(Config.Theme ?? new UIThemeConfig(), _fonts, _fontOrder, ResolveSprite);

            Mode = string.Equals(Config.Render?.Mode, "legacy", StringComparison.OrdinalIgnoreCase)
                ? UIRenderMode.Legacy
                : UIRenderMode.Batched;
            Coalesce = Config.Render?.Coalesce ?? true;
            CoalesceWindow = Math.Max(1, Config.Render?.CoalesceWindow ?? 64);

            stopwatch.Stop();
            LoadMilliseconds = stopwatch.ElapsedMilliseconds;
            IsLoaded = true;

            Console.WriteLine(
                $"[UI] 已加载图集 {_atlases.Count} 张、字体 {_fonts.Count} 套（烘焙 {bakeWatch.ElapsedMilliseconds} ms）、" +
                $"主题精灵 {Config.Theme?.Sprites.Count ?? 0} 项，总耗时 {LoadMilliseconds} ms" +
                $"（渲染模式: {(Mode == UIRenderMode.Legacy ? "legacy" : "batched")}）。");
        }

        /// <summary>在 OpenGL 上下文里创建所有图集与字体纹理。</summary>
        public void UploadGpu()
        {
            if (!IsLoaded)
                throw new InvalidOperationException("UIAssets 尚未加载，无法上传纹理");

            foreach (var atlas in _atlasOrder)
                atlas.UploadGpu();

            foreach (var font in _fontOrder)
                font.UploadGpu();
        }

        private static TextureOptions BuildTextureOptions(UIAtlasConfig config)
        {
            bool linear = string.Equals(config.Filter, "linear", StringComparison.OrdinalIgnoreCase);
            if (linear)
            {
                return config.Mipmap
                    ? new TextureOptions(OpenTK.Graphics.OpenGL4.TextureMinFilter.LinearMipmapLinear,
                        OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                        OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge, true)
                    : TextureOptions.Smooth;
            }

            return config.Mipmap
                ? TextureOptions.PixelArt
                : new TextureOptions(OpenTK.Graphics.OpenGL4.TextureMinFilter.Nearest,
                    OpenTK.Graphics.OpenGL4.TextureMagFilter.Nearest,
                    OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge, false);
        }

        /// <summary>把 "图集名:精灵名" 解析成运行时精灵。</summary>
        public UISprite ResolveSprite(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                UnresolvedSprites++;
                return UISprite.Empty;
            }

            int colon = reference.IndexOf(':');
            string atlasName;
            string spriteName;
            if (colon > 0 && colon < reference.Length - 1)
            {
                atlasName = reference[..colon];
                spriteName = reference[(colon + 1)..];
            }
            else
            {
                atlasName = _atlasOrder.Count > 0 ? _atlasOrder[^1].Name : "";
                spriteName = reference;
            }

            if (_atlases.TryGetValue(atlasName, out var atlas) && atlas.TryGetSprite(spriteName, out var sprite))
                return sprite;

            UnresolvedSprites++;
            Console.WriteLine($"[UI] 警告: 无法解析精灵引用 '{reference}'（图集 '{atlasName}' / 精灵 '{spriteName}'）。");
            return UISprite.Empty;
        }

        public Atlas? GetAtlas(string name)
            => _atlases.TryGetValue(name, out var atlas) ? atlas : null;

        public UIFont GetFont(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name) && _fonts.TryGetValue(name, out var font))
                return font;
            return Theme.Font;
        }

        /// <summary>供 --validate-ui 打印摘要。</summary>
        public void PrintSummary()
        {
            Console.WriteLine("UI 图集：");
            foreach (var atlas in _atlasOrder)
            {
                Console.WriteLine(
                    $"  {atlas.Name,-10} 精灵={atlas.Count,3} 纹理={atlas.texture.Width}x{atlas.texture.Height} " +
                    $"filter={(atlas.Options.Mag == OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear ? "linear" : "nearest")} " +
                    $"mipmap={(atlas.Options.Mipmap ? "yes" : "no")}");
            }

            Console.WriteLine("UI 字体：");
            foreach (var font in _fontOrder)
            {
                Console.WriteLine(
                    $"  {font.Name,-10} 字号={font.Size,5:0.#} 字形={font.GlyphCount,4} " +
                    $"图集={font.AtlasWidth}x{font.AtlasHeight} 行高={font.LineHeight:0.##}");
            }

            Console.WriteLine(
                $"UI 主题：字体={Theme.Font.Name} 小字={Theme.FontSmall.Name} 标题={Theme.FontTitle.Name} " +
                $"精灵引用={Config.Theme?.Sprites.Count ?? 0} 未解析={UnresolvedSprites}");
        }
    }
}
