using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TextCraft.src.UI;

namespace TextCraft.src.Tools
{
    /// <summary>
    /// 字体烘焙请求。由 ui.json 的 fonts 节点填充。
    /// </summary>
    public sealed class FontBakeRequest
    {
        public string Name = "default";
        public string FilePath = "";
        public string Family = "";
        public float Size = 20f;
        public string Style = "Regular";
        public string Charset = "ascii";
        public string ExtraChars = "";
        public IReadOnlyList<string> Ranges = Array.Empty<string>();
        public int AtlasWidth = 512;
        public float LineSpacing = 1f;
    }

    /// <summary>
    /// 字体图集烘焙器。
    ///
    /// 与旧 <c>FontLoader</c> 的差异：
    /// <list type="bullet">
    /// <item>字形尺寸来自真实度量（<see cref="TextMeasurer"/>），不再把每个字符塞进固定 32x32 方格再拉伸；</item>
    /// <item>支持任意字号、字符集（ASCII / 额外字符串 / Unicode 区段），因此可以显示中文；</item>
    /// <item>支持多套字体（标题 / 正文 / 小字），各自生成图集；</item>
    /// <item>图集尺寸由货架算法动态决定，不再固定 1024 宽并对空白像素做无用功；</item>
    /// <item>不再往运行目录丢 <c>font_atlas_fixed.png</c> 调试文件。</item>
    /// </list>
    /// </summary>
    public static class FontBaker
    {
        public static UIFont Bake(FontBakeRequest request)
        {
            if (!System.IO.File.Exists(request.FilePath))
                throw new System.IO.FileNotFoundException($"字体文件不存在: {request.FilePath}");

            var collection = new FontCollection();
            var families = collection.AddCollection(request.FilePath).ToList();
            if (families.Count == 0)
                throw new InvalidOperationException($"字体文件里没有可用字族: {request.FilePath}");

            FontFamily family;
            if (string.IsNullOrWhiteSpace(request.Family))
            {
                family = families[0];
            }
            else
            {
                var matched = families.FirstOrDefault(f => string.Equals(f.Name, request.Family, StringComparison.OrdinalIgnoreCase));
                if (matched.Name == null)
                    throw new InvalidOperationException(
                        $"字体文件 '{request.FilePath}' 中找不到字族 '{request.Family}'（可用: {string.Join(", ", families.Select(f => f.Name))}）");
                family = matched;
            }

            var style = ParseStyle(request.Style);
            Font font = family.CreateFont(MathF.Max(4f, request.Size), style);

            var codepoints = ParseCharset(request.Charset, request.ExtraChars, request.Ranges);
            var textOptions = new TextOptions(font);

            var items = new List<GlyphItem>(codepoints.Count);
            foreach (int codePoint in codepoints)
            {
                if (codePoint <= 0 || codePoint > 0x10FFFF) continue;
                // 代理项与不可打印控制字符跳过（换行/制表由排版逻辑处理）
                if (codePoint < 0x20) continue;
                if (codePoint >= 0xD800 && codePoint <= 0xDFFF) continue;

                string text = char.ConvertFromUtf32(codePoint);
                FontRectangle bounds;
                float advance;
                try
                {
                    bounds = TextMeasurer.MeasureBounds(text, textOptions);
                    advance = TextMeasurer.MeasureAdvance(text, textOptions).Width;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Font] 警告: 字符 U+{codePoint:X4} 度量失败（{ex.Message}），已跳过。");
                    continue;
                }

                var item = new GlyphItem
                {
                    CodePoint = codePoint,
                    Text = text,
                    Advance = advance,
                    BoundsX = bounds.X,
                    BoundsY = bounds.Y,
                    Width = MathF.Max(0f, bounds.Width),
                    Height = MathF.Max(0f, bounds.Height),
                };
                items.Add(item);
            }

            var result = new UIFont(request.Name, request.Size);
            var metrics = font.FontMetrics;
            float scale = request.Size / metrics.UnitsPerEm;
            float lineHeight = metrics.HorizontalMetrics.LineHeight * scale * MathF.Max(0.5f, request.LineSpacing);
            result.LineHeight = MathF.Max(request.Size, lineHeight);
            result.Ascent = metrics.HorizontalMetrics.Ascender * scale;
            result.Descent = MathF.Abs(metrics.HorizontalMetrics.Descender) * scale;
            var layout = PackAtlas(items, Math.Max(64, request.AtlasWidth), 1);
            int atlasWidth = layout.Width;
            int atlasHeight = layout.Height;

            using var atlas = new Image<Rgba32>(atlasWidth, atlasHeight);
            atlas.Mutate(ctx =>
            {
                ctx.Clear(Color.Transparent);
                foreach (var item in items)
                {
                    if (item.Width <= 0.5f || item.Height <= 0.5f) continue;   // 空格等无墨迹字符
                    var origin = new System.Numerics.Vector2(
                        item.PackedX - item.BoundsX,
                        item.PackedY - item.BoundsY);
                    var rich = new RichTextOptions(font) { Origin = origin };
                    ctx.DrawText(rich, item.Text, Color.White);
                }
            });

            var pixels = new byte[atlasWidth * atlasHeight * 4];
            atlas.CopyPixelDataTo(pixels);
            result.SetAtlas(pixels, atlasWidth, atlasHeight);

            float invW = 1f / atlasWidth;
            float invH = 1f / atlasHeight;
            foreach (var item in items)
            {
                var glyph = new UIGlyph
                {
                    Character = (char)item.CodePoint,
                    Advance = item.Advance,
                    Width = item.Width,
                    Height = item.Height,
                    OffsetX = item.BoundsX,
                    OffsetY = item.BoundsY,
                    U0 = 0f, V0 = 0f, U1 = 0f, V1 = 0f,
                };

                if (item.Width > 0.5f && item.Height > 0.5f)
                {
                    glyph.U0 = (item.PackedX + 0.5f) * invW;
                    glyph.V0 = (item.PackedY + 0.5f) * invH;
                    glyph.U1 = (item.PackedX + item.Width - 0.5f) * invW;
                    glyph.V1 = (item.PackedY + item.Height - 0.5f) * invH;
                }

                result.AddGlyph(glyph);
            }

            result.SetFallback('?');
            return result;
        }

        private static FontStyle ParseStyle(string? style)
        {
            if (string.IsNullOrWhiteSpace(style)) return FontStyle.Regular;
            if (Enum.TryParse<FontStyle>(style, true, out var parsed)) return parsed;
            return FontStyle.Regular;
        }

        /// <summary>
        /// 解析字符集：<paramref name="charset"/> 支持 <c>ascii</c> / <c>ascii+</c> / <c>none</c>，
        /// <paramref name="extraChars"/> 直接列字符，<paramref name="ranges"/> 支持 <c>"0x4E00-0x9FFF"</c>、
        /// <c>"19968-40959"</c> 以及单值。
        /// </summary>
        public static List<int> ParseCharset(string? charset, string? extraChars, IReadOnlyList<string>? ranges)
        {
            var set = new SortedSet<int>();

            string mode = (charset ?? "ascii").Trim().ToLowerInvariant();
            if (mode is "ascii" or "ascii+")
            {
                for (int c = 0x20; c <= 0x7E; c++) set.Add(c);
                if (mode == "ascii+")
                {
                    // 常用中日韩标点与符号，避免中文界面里出现方块
                    foreach (char c in "，。！？：；、（）【】《》“”‘’—…·　℃×÷") set.Add(c);
                }
            }

            if (!string.IsNullOrEmpty(extraChars))
                foreach (char c in extraChars)
                    set.Add(c);

            if (ranges != null)
            {
                foreach (string range in ranges)
                {
                    if (string.IsNullOrWhiteSpace(range)) continue;
                    string spec = range.Trim();
                    int dash = spec.IndexOf('-');
                    if (dash < 0)
                    {
                        if (TryParseCodePoint(spec, out int single)) set.Add(single);
                        continue;
                    }

                    string fromText = spec[..dash].Trim();
                    string toText = spec[(dash + 1)..].Trim();
                    if (!TryParseCodePoint(fromText, out int from) || !TryParseCodePoint(toText, out int to))
                        continue;
                    if (to < from) (from, to) = (to, from);
                    // 上限保护：单个区段最多 8192 个字符，避免一次烘焙出巨型图集
                    if (to - from > 8192) to = from + 8192;
                    for (int c = from; c <= to; c++)
                    {
                        if (c >= 0xD800 && c <= 0xDFFF) continue;
                        set.Add(c);
                    }
                }
            }

            return set.ToList();
        }

        private static bool TryParseCodePoint(string text, out int value)
        {
            text = text.Trim();
            if (text.StartsWith("U+", StringComparison.OrdinalIgnoreCase) || text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            return int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private sealed class GlyphItem
        {
            public int CodePoint;
            public string Text = "";
            public float Advance;
            public float BoundsX;
            public float BoundsY;
            public float Width;
            public float Height;
            public int PackedX;
            public int PackedY;
        }

        private sealed class AtlasLayout
        {
            public int Width;
            public int Height;
        }

        /// <summary>
        /// 货架装箱：按墨迹高度降序排列，逐行放置；图集宽度固定，高度按需增长（向上取整到 4 的倍数）。
        /// </summary>
        private static AtlasLayout PackAtlas(List<GlyphItem> items, int atlasWidth, int padding)
        {
            int top = 0, shelfHeight = 0, shelfX = 0;

            var drawable = items.Where(i => i.Width > 0.5f && i.Height > 0.5f)
                                .OrderByDescending(i => i.Height)
                                .ThenByDescending(i => i.Width)
                                .ToList();

            foreach (var item in drawable)
            {
                int w = (int)MathF.Ceiling(item.Width) + padding * 2;
                int h = (int)MathF.Ceiling(item.Height) + padding * 2;
                if (w > atlasWidth) w = atlasWidth;

                if (shelfX + w > atlasWidth)
                {
                    top += shelfHeight;
                    shelfHeight = 0;
                    shelfX = 0;
                }

                item.PackedX = shelfX + padding;
                item.PackedY = top + padding;

                shelfX += w;
                if (h > shelfHeight) shelfHeight = h;
            }

            int height = top + shelfHeight;
            if (height <= 0) height = atlasWidth;
            height = (height + 3) / 4 * 4;

            return new AtlasLayout { Width = atlasWidth, Height = height };
        }
    }
}
