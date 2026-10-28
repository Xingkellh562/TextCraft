using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 字形在字体图集里的位置与排版度量。
    /// <c>OffsetX/OffsetY</c> 是字形墨迹左上角相对"文字原点（行框左上角）"的偏移，
    /// 由六边形字体度量直接测得，因此不再需要旧实现那种"固定 32x32 方格 + 拉伸"。
    /// </summary>
    public struct UIGlyph
    {
        public char Character;
        public float U0, V0, U1, V1;
        public float Width;
        public float Height;
        public float OffsetX;
        public float OffsetY;
        public float Advance;

        public bool HasInk => Width > 0.5f && Height > 0.5f;
    }

    /// <summary>
    /// 运行时字体：字体图集纹理 + 字形表 + 行高等度量。
    /// 烘焙（CPU）与上传（GPU）分离，便于无窗口校验。
    /// </summary>
    public sealed class UIFont
    {
        public string Name { get; }
        public float Size { get; }
        public float LineHeight { get; internal set; }
        public float Ascent { get; internal set; }
        public float Descent { get; internal set; }

        public Texture Texture { get; } = new Texture();
        public int AtlasWidth { get; private set; }
        public int AtlasHeight { get; private set; }

        private readonly Dictionary<char, UIGlyph> _glyphs = new();
        private readonly HashSet<char> _missingWarned = new();
        private byte[]? _pixels;

        /// <summary>缺失字形时的回退字符（通常是 '?'）。</summary>
        public char FallbackCharacter { get; private set; } = '?';

        public int GlyphCount => _glyphs.Count;
        public bool IsUploaded => Texture.IsUploaded;
        public bool HasPendingPixels => _pixels != null;

        public UIFont(string name, float size)
        {
            Name = name;
            Size = size;
            LineHeight = MathF.Max(1f, size * 1.25f);
        }

        /// <summary>所有已烘焙的字形（调试/统计用）。</summary>
        public IReadOnlyDictionary<char, UIGlyph> Glyphs => _glyphs;

        internal void SetAtlas(byte[] pixels, int width, int height)
        {
            _pixels = pixels;
            AtlasWidth = width;
            AtlasHeight = height;
        }

        internal void AddGlyph(in UIGlyph glyph) => _glyphs[glyph.Character] = glyph;

        internal void SetFallback(char c)
        {
            if (_glyphs.ContainsKey(c)) FallbackCharacter = c;
        }

        /// <summary>在 OpenGL 上下文中创建字体图集纹理。</summary>
        public void UploadGpu()
        {
            if (Texture.IsUploaded) return;
            if (_pixels == null || AtlasWidth <= 0 || AtlasHeight <= 0)
                throw new InvalidOperationException($"字体 '{Name}' 尚未烘焙，无法上传纹理");

            Texture.UploadPixels(_pixels, AtlasWidth, AtlasHeight, TextureOptions.FontMask);
        }

        /// <summary>取字形；字符缺失时回退到 <see cref="FallbackCharacter"/>。</summary>
        public bool TryGetGlyph(char c, out UIGlyph glyph)
        {
            if (_glyphs.TryGetValue(c, out glyph)) return true;

            if (FallbackCharacter != c && _glyphs.TryGetValue(FallbackCharacter, out glyph))
            {
                WarnMissing(c);
                return true;
            }

            WarnMissing(c);
            glyph = default;
            return false;
        }

        private void WarnMissing(char c)
        {
            if (!_missingWarned.Add(c)) return;
            if (_missingWarned.Count > 8) return;
            Console.WriteLine($"[Font] 警告: 字体 '{Name}' 缺字符 '{c}'(U+{(int)c:X4})，请在 ui.json 的 extraChars/ranges 中补上。");
        }

        /// <summary>字符推进距离（像素）。</summary>
        public float GetAdvance(char c) => TryGetGlyph(c, out var g) ? g.Advance : Size * 0.5f;

        /// <summary>测量一行文本的宽度（像素，不含行末多余空白裁剪）。</summary>
        public float MeasureLineWidth(ReadOnlySpan<char> text)
        {
            float width = 0f;
            foreach (char c in text)
                width += GetAdvance(c);
            return width;
        }

        /// <summary>测量多行文本的尺寸（宽 = 最长行，高 = 行数 * 行高）。</summary>
        public Vector2 MeasureText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.Zero;

            float maxWidth = 0f;
            float lineWidth = 0f;
            int lines = 1;
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    lines++;
                    if (lineWidth > maxWidth) maxWidth = lineWidth;
                    lineWidth = 0f;
                    continue;
                }
                lineWidth += GetAdvance(c);
            }
            if (lineWidth > maxWidth) maxWidth = lineWidth;
            return new Vector2(maxWidth, lines * LineHeight);
        }

        public override string ToString() => $"font:{Name}({Size:0.#}px, {_glyphs.Count} glyphs, {AtlasWidth}x{AtlasHeight})";
    }
}
