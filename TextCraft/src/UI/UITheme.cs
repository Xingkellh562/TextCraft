using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Table;

namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 主题：把 ui.json 里的精灵引用（"图集:精灵"）和颜色解析成运行时可用的对象。
    /// 控件通过常量键取值，缺失时回退到安全默认值并只警告一次，避免资源少一项就崩溃。
    /// </summary>
    public sealed class UITheme
    {
        private static readonly Dictionary<string, Vector4> DefaultColors = new(StringComparer.OrdinalIgnoreCase)
        {
            [UISkin.ColorText] = new Vector4(0.94f, 0.95f, 0.99f, 1f),
            [UISkin.ColorTextDim] = new Vector4(0.68f, 0.71f, 0.80f, 1f),
            [UISkin.ColorTextDisabled] = new Vector4(0.46f, 0.48f, 0.56f, 1f),
            [UISkin.ColorAccent] = new Vector4(0.40f, 0.66f, 1.00f, 1f),
            [UISkin.ColorTitle] = new Vector4(0.98f, 0.99f, 1f, 1f),
            [UISkin.ColorShadow] = new Vector4(0f, 0f, 0f, 0.55f),
            [UISkin.ColorTint] = new Vector4(1f, 1f, 1f, 1f),
            [UISkin.ColorDanger] = new Vector4(0.95f, 0.42f, 0.40f, 1f),
            [UISkin.ColorSuccess] = new Vector4(0.45f, 0.85f, 0.50f, 1f),
        };

        private readonly Dictionary<string, UISprite> _sprites = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector4> _colors = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _warnedSprites = new();
        private readonly HashSet<string> _warnedColors = new();

        public UIFont Font { get; private set; }
        public UIFont FontSmall { get; private set; }
        public UIFont FontTitle { get; private set; }

        public UITheme(UIFont font)
        {
            Font = font;
            FontSmall = font;
            FontTitle = font;
        }

        /// <summary>取精灵；未登记或解析失败时返回空精灵。</summary>
        public UISprite Sprite(string key)
        {
            if (_sprites.TryGetValue(key, out var sprite) && sprite.IsValid)
                return sprite;

            if (_warnedSprites.Add(key))
                Console.WriteLine($"[UI] 警告: 主题精灵 '{key}' 未配置或无效（检查 ui.json 的 theme.sprites 与图集）。");
            return UISprite.Empty;
        }

        public bool TryGetSprite(string key, out UISprite sprite)
        {
            if (_sprites.TryGetValue(key, out var found) && found != null && found.IsValid)
            {
                sprite = found;
                return true;
            }
            sprite = UISprite.Empty;
            return false;
        }

        public Vector4 Color(string key)
        {
            if (_colors.TryGetValue(key, out var color)) return color;
            if (DefaultColors.TryGetValue(key, out var fallback)) return fallback;

            if (_warnedColors.Add(key))
                Console.WriteLine($"[UI] 警告: 主题颜色 '{key}' 未配置，已使用白色。");
            return Vector4.One;
        }

        public Vector4 ColorRgb(string key)
        {
            var c = Color(key);
            return new Vector4(c.X, c.Y, c.Z, 1f);
        }

        internal void AddSprite(string key, UISprite sprite) => _sprites[key] = sprite;

        internal void AddColor(string key, Vector4 color) => _colors[key] = color;

        /// <summary>
        /// 依据配置构建主题：先解析颜色，再解析精灵引用，最后绑定字体。
        /// </summary>
        public static UITheme Build(UIThemeConfig config, IReadOnlyDictionary<string, UIFont> fonts,
            IReadOnlyList<UIFont> fontOrder, Func<string, UISprite> resolveSprite)
        {
            var defaultFont = PickFont(fonts, config.Font, fontOrder);
            var theme = new UITheme(defaultFont)
            {
                FontSmall = PickFont(fonts, config.FontSmall, fontOrder, defaultFont),
                FontTitle = PickFont(fonts, config.FontTitle, fontOrder, defaultFont),
            };

            if (config.Colors != null)
            {
                foreach (var pair in config.Colors)
                {
                    var value = pair.Value;
                    if (value == null || value.Length < 3) continue;
                    theme.AddColor(pair.Key, new Vector4(
                        value[0],
                        value[1],
                        value[2],
                        value.Length >= 4 ? value[3] : 1f));
                }
            }

            if (config.Sprites != null)
            {
                foreach (var pair in config.Sprites)
                    theme.AddSprite(pair.Key, resolveSprite(pair.Value ?? ""));
            }

            return theme;
        }

        private static UIFont PickFont(IReadOnlyDictionary<string, UIFont> fonts, string? name,
            IReadOnlyList<UIFont> order, UIFont? fallback = null)
        {
            if (!string.IsNullOrWhiteSpace(name) && fonts.TryGetValue(name, out var font) && font != null)
                return font;
            if (fallback != null) return fallback;
            if (order.Count > 0) return order[0];
            throw new InvalidOperationException("UI 主题没有任何可用字体");
        }
    }
}
