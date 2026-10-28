using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    public enum UITextAlign
    {
        Left,
        Center,
        Right,
    }

    public enum UIVerticalAlign
    {
        Top,
        Middle,
        Bottom,
    }

    /// <summary>
    /// 文本控件。
    ///
    /// 排版要点：
    /// <list type="bullet">
    /// <item>字形位置来自 <see cref="UIFont"/> 的真实度量（墨迹偏移 + 推进宽度），不再假设等宽 32px 方格；</item>
    /// <item>支持 <c>\n</c> 换行与按宽度自动换行（拉丁按空格断行，中文按字断行）；</item>
    /// <item>水平/垂直对齐均按"墨迹包围盒"计算，因此单行文字在按钮里也是真正视觉居中；</item>
    /// <item>可选描边阴影（同样只增加四边形，不增加 DrawCall）。</item>
    /// </list>
    /// </summary>
    public sealed class UILabel : UIComponent
    {
        private struct TextLine
        {
            public int Start;
            public int Length;
            public float Width;
            public float InkTop;
            public float InkBottom;
            public bool HasInk;
        }

        private readonly List<TextLine> _lines = new();

        private string _cachedText = "\0";
        private UIFont? _cachedFont;
        private float _cachedWrapWidth = -1f;
        private float _cachedLineSpacing = -1f;
        private float _inkTop;
        private float _inkBottom;

        public UILabel()
        {
        }

        public UILabel(string text)
        {
            Text = text;
        }

        /// <summary>文本内容，支持 <c>\n</c>。</summary>
        public string Text { get; set; } = "";

        /// <summary>字体；为空时使用主题默认字体。</summary>
        public UIFont? Font { get; set; }

        public UITextAlign Align { get; set; } = UITextAlign.Left;

        public UIVerticalAlign VerticalAlign { get; set; } = UIVerticalAlign.Middle;

        /// <summary>按控件宽度自动换行。</summary>
        public bool Wrap { get; set; }

        /// <summary>根据文本自动调整 <see cref="UIComponent.Size"/>（启用 Wrap 时以当前宽度为换行宽度）。</summary>
        public bool AutoSize { get; set; }

        /// <summary>内边距（像素）。</summary>
        public float Padding { get; set; }

        /// <summary>行距倍率（1 = 字体自身行高）。</summary>
        public float LineSpacing { get; set; } = 1f;

        /// <summary>文字颜色；为空时使用主题 text 颜色。</summary>
        public Vector4? TextColor { get; set; }

        public bool Shadow { get; set; }

        public Vector2 ShadowOffset { get; set; } = new Vector2(1.5f, 1.5f);

        public UIFont? ResolvedFont => Font ?? UIContext.Theme?.Font;

        public float ResolvedLineSpacing
        {
            get
            {
                var font = ResolvedFont;
                float lineHeight = font?.LineHeight ?? 16f;
                return MathF.Max(1f, lineHeight * MathF.Max(0.5f, LineSpacing));
            }
        }

        /// <summary>排版后的行数（调试/布局用）。</summary>
        public int LineCount => _lines.Count;

        protected override void OnBeforeLayout()
        {
            if (!AutoSize) return;

            var font = ResolvedFont;
            if (font == null) return;

            float wrapWidth = Wrap ? MathF.Max(1f, Size.X - Padding * 2f) : 0f;
            EnsureLines(font, wrapWidth);

            float maxLineWidth = 0f;
            for (int i = 0; i < _lines.Count; i++)
                maxLineWidth = MathF.Max(maxLineWidth, _lines[i].Width);

            float height = MathF.Max(1, _lines.Count) * ResolvedLineSpacing;
            Size = new Vector2(maxLineWidth + Padding * 2f + 1f, height + Padding * 2f);
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var font = ResolvedFont;
            var theme = UIContext.Theme;
            if (font == null || theme == null) return;
            if (string.IsNullOrEmpty(Text)) return;

            var content = new UIRect(
                ScreenRect.X + Padding,
                ScreenRect.Y + Padding,
                ScreenRect.Width - Padding * 2f,
                ScreenRect.Height - Padding * 2f);
            if (content.IsEmpty) return;

            EnsureLines(font, Wrap ? content.Width : 0f);
            if (_lines.Count == 0) return;

            float spacing = ResolvedLineSpacing;
            float top;
            switch (VerticalAlign)
            {
                case UIVerticalAlign.Top:
                    top = content.Top;
                    break;
                case UIVerticalAlign.Bottom:
                    top = content.Bottom - _inkBottom;
                    break;
                default:
                    top = content.Center.Y - (_inkTop + _inkBottom) * 0.5f;
                    break;
            }

            var color = tint * (TextColor ?? theme.Color(UISkin.ColorText));

            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                float x;
                switch (Align)
                {
                    case UITextAlign.Center:
                        x = content.X + (content.Width - line.Width) * 0.5f;
                        break;
                    case UITextAlign.Right:
                        x = content.Right - line.Width;
                        break;
                    default:
                        x = content.X;
                        break;
                }

                float y = top + i * spacing;
                if (Shadow)
                {
                    var shadowColor = new Vector4(color.X, color.Y, color.Z, color.W) * theme.Color(UISkin.ColorShadow);
                    DrawLine(list, font, line, x + ShadowOffset.X, y + ShadowOffset.Y, clip, shadowColor);
                }
                DrawLine(list, font, line, x, y, clip, color);
            }
        }

        private void DrawLine(UIDrawList list, UIFont font, in TextLine line,
            float x, float y, in UIRect clip, in Vector4 color)
        {
            if (line.Length <= 0) return;

            var key = new UIBatchKey(font.Texture, UIMaterial.AlphaMask, clip);
            float penX = x;
            for (int k = 0; k < line.Length; k++)
            {
                char c = Text[line.Start + k];
                if (!font.TryGetGlyph(c, out var glyph))
                {
                    penX += font.Size * 0.5f;
                    continue;
                }

                if (glyph.HasInk)
                {
                    var dest = UIRect.FromMinMax(
                        new Vector2(penX + glyph.OffsetX, y + glyph.OffsetY),
                        new Vector2(penX + glyph.OffsetX + glyph.Width, y + glyph.OffsetY + glyph.Height));
                    list.AddQuad(Layer, key, dest,
                        new Vector2(glyph.U0, glyph.V0), new Vector2(glyph.U1, glyph.V1), color);
                }

                penX += glyph.Advance;
            }
        }

        /// <summary>
        /// 重新分行并计算每行的推进宽度与墨迹高度。
        /// 缓存键为（文本引用 / 字体对象 / 换行宽度 / 行距），命中时直接返回。
        /// </summary>
        private void EnsureLines(UIFont font, float wrapWidth)
        {
            float spacing = ResolvedLineSpacing;
            if (ReferenceEquals(font, _cachedFont)
                && string.Equals(Text, _cachedText, StringComparison.Ordinal)
                && MathF.Abs(wrapWidth - _cachedWrapWidth) < 0.01f
                && MathF.Abs(spacing - _cachedLineSpacing) < 0.01f)
            {
                return;
            }

            _cachedFont = font;
            _cachedText = Text;
            _cachedWrapWidth = wrapWidth;
            _cachedLineSpacing = spacing;

            _lines.Clear();
            _inkTop = 0f;
            _inkBottom = 0f;

            string text = Text;
            if (string.IsNullOrEmpty(text)) return;

            int lineStart = 0;
            int index = 0;
            float lineWidth = 0f;

            while (true)
            {
                bool atEnd = index >= text.Length;
                bool newline = !atEnd && text[index] == '\n';
                bool overflow = !atEnd && !newline && wrapWidth > 0f && index > lineStart
                    && lineWidth + font.GetAdvance(text[index]) > wrapWidth;

                if (atEnd || newline || overflow)
                {
                    if (overflow)
                    {
                        int breakAt = FindBreak(text, lineStart, index);
                        if (breakAt > lineStart)
                        {
                            EmitLine(font, text, lineStart, breakAt, spacing);
                            index = breakAt;
                            while (index < text.Length && text[index] == ' ') index++;
                            lineStart = index;
                            lineWidth = 0f;
                            continue;
                        }
                    }

                    EmitLine(font, text, lineStart, index, spacing);
                    if (atEnd) break;

                    index = newline ? index + 1 : index;
                    lineStart = index;
                    lineWidth = 0f;
                    continue;
                }

                lineWidth += font.GetAdvance(text[index]);
                index++;
            }
        }

        /// <summary>在 [start, end) 内从后向前找可断行的位置（空格/连字符）。</summary>
        private static int FindBreak(string text, int start, int end)
        {
            for (int i = end - 1; i > start; i--)
            {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '-' || c == '/') return i;
            }
            return start;
        }

        private void EmitLine(UIFont font, string text, int start, int end, float spacing)
        {
            int index = _lines.Count;
            float width = 0f;
            float inkTop = float.MaxValue;
            float inkBottom = float.MinValue;
            bool hasInk = false;

            for (int k = start; k < end; k++)
            {
                char c = text[k];
                float advance = font.GetAdvance(c);
                if (font.TryGetGlyph(c, out var glyph) && glyph.HasInk)
                {
                    inkTop = MathF.Min(inkTop, glyph.OffsetY);
                    inkBottom = MathF.Max(inkBottom, glyph.OffsetY + glyph.Height);
                    hasInk = true;
                }
                width += advance;
            }

            if (!hasInk)
            {
                inkTop = 0f;
                inkBottom = 0f;
            }

            _lines.Add(new TextLine
            {
                Start = start,
                Length = end - start,
                Width = width,
                InkTop = inkTop,
                InkBottom = inkBottom,
                HasInk = hasInk,
            });

            // 墨迹包围盒（相对第一行行顶），用于垂直对齐
            float lineOffset = index * spacing;
            if (hasInk)
            {
                _inkTop = MathF.Min(_inkTop, lineOffset + inkTop);
                _inkBottom = MathF.Max(_inkBottom, lineOffset + inkBottom);
            }
            else
            {
                _inkBottom = MathF.Max(_inkBottom, lineOffset + spacing * 0.5f);
            }
        }
    }
}
