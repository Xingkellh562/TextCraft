using System;
using System.Text;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>输入框允许的内容。</summary>
    public enum UITextContentType
    {
        /// <summary>任意可打印字符。</summary>
        Any = 0,

        /// <summary>整数（允许开头一个负号）。</summary>
        Integer = 1,

        /// <summary>非负整数（只允许数字）。</summary>
        PositiveInteger = 2,
    }

    /// <summary>
    /// 文本输入框：点击获得焦点，支持光标移动/删除/水平滚动/占位提示/内容过滤。
    ///
    /// 实现方式：真正的字形排版交给内部的 <see cref="UILabel"/>（它已经有度量、行缓存），
    /// 输入框自己负责编辑状态、水平滚动、光标绘制与焦点框；<see cref="UIComponent.ClipChildren"/>
    /// 保证长文本不会溢出边框。
    /// </summary>
    public class UITextField : UIComponent
    {
        private readonly UILabel _label;
        private readonly StringBuilder _buffer = new();

        private int _caret;
        private float _scrollX;
        private float _blinkTime;
        private bool _focused;

        public UITextField()
        {
            Name = "textfield";
            Size = new Vector2(240f, 34f);
            ClipChildren = true;
            Cursor = UICursorShape.Text;

            _label = new UILabel
            {
                Name = "text",
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                Wrap = false,
            };
            _label.Position = new Vector2(PaddingX, 0f);
            Add(_label);

            // 初始为空：此时应该显示占位文字（否则占位符要等到第一次编辑才出现）
            ApplyText();
        }

        /// <summary>左右内边距（像素）。</summary>
        public float PaddingX { get; set; } = 9f;

        public string Text
        {
            get => _buffer.ToString();
            set
            {
                _buffer.Clear();
                _buffer.Append(value ?? "");
                _caret = Math.Clamp(_caret, 0, _buffer.Length);
                _scrollX = 0f;
                ApplyText();
            }
        }
        private string _placeholder = "";

        /// <summary>为空时显示的灰色提示文字（后设置也会立即刷新）。</summary>
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? "";
                if (_buffer.Length == 0) ApplyText();
            }
        }

        public UIFont? Font
        {
            get => _label.Font;
            set => _label.Font = value;
        }

        public int MaxLength { get; set; } = 32;

        public UITextContentType ContentType { get; set; } = UITextContentType.Any;

        public bool Interactable { get; set; } = true;

        public override bool IsInteractable => Interactable;

        public bool IsFocused => _focused;

        /// <summary>回车提交。</summary>
        public event Action<UITextField>? Submitted;

        /// <summary>内容变化（任何编辑）。</summary>
        public event Action<UITextField>? Changed;

        /// <summary>主题精灵键：普通/聚焦。</summary>
        public string NormalSpriteKey { get; set; } = UISkin.TextField;

        public string FocusedSpriteKey { get; set; } = UISkin.TextFieldFocused;

        private UIFont? ActiveFont => Font ?? UIContext.Theme?.Font;

        // ---------------- 焦点 ----------------

        public void Focus()
        {
            if (_focused) return;
            _focused = true;
            _blinkTime = 0f;
            _caret = _buffer.Length;
            EnsureCaretVisible();
        }

        public void Blur()
        {
            if (!_focused) return;
            _focused = false;
        }

        /// <summary>按当前内容重新计算文本控件。</summary>
        private void ApplyText()
        {
            bool empty = _buffer.Length == 0;
            _label.Text = empty ? Placeholder : Text;
            var theme = UIContext.Theme;
            if (empty)
                _label.TextColor = theme != null
                    ? theme.Color(UISkin.ColorTextDisabled)
                    : new Vector4(0.5f, 0.5f, 0.5f, 1f);
            else
                _label.TextColor = null;
            Changed?.Invoke(this);
        }

        // ---------------- 编辑 ----------------

        /// <summary>处理编辑按键；返回是否消费。</summary>
        public bool HandleKey(Keys key)
        {
            switch (key)
            {
                case Keys.Backspace:
                    if (_caret > 0)
                    {
                        _buffer.Remove(_caret - 1, 1);
                        _caret--;
                        AfterEdit();
                    }
                    return true;

                case Keys.Delete:
                    if (_caret < _buffer.Length)
                    {
                        _buffer.Remove(_caret, 1);
                        AfterEdit();
                    }
                    return true;

                case Keys.Left:
                    _caret = Math.Max(0, _caret - 1);
                    EnsureCaretVisible();
                    return true;

                case Keys.Right:
                    _caret = Math.Min(_buffer.Length, _caret + 1);
                    EnsureCaretVisible();
                    return true;

                case Keys.Home:
                case Keys.PageUp:
                    _caret = 0;
                    EnsureCaretVisible();
                    return true;

                case Keys.End:
                case Keys.PageDown:
                    _caret = _buffer.Length;
                    EnsureCaretVisible();
                    return true;

                case Keys.Enter:
                case Keys.KeyPadEnter:
                    Submitted?.Invoke(this);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>插入一个输入字符；返回是否消费（即使因为过滤被拒绝也算消费，避免落到游戏输入）。</summary>
        public bool HandleChar(int unicode)
        {
            if (unicode < 0x20 || unicode == 0x7F) return false;
            if (unicode > 0xFFFF) return true;   // 暂不支持 BMP 之外的字符

            char c = (char)unicode;

            switch (ContentType)
            {
                case UITextContentType.PositiveInteger:
                    if (!char.IsDigit(c)) return true;
                    break;

                case UITextContentType.Integer:
                    if (char.IsDigit(c)) break;
                    // 只允许在开头输入一个负号
                    if (c == '-' && _caret == 0 && !_buffer.ToString().Contains('-')) break;
                    return true;
            }

            if (_buffer.Length >= MaxLength) return true;

            _buffer.Insert(_caret, c);
            _caret++;
            AfterEdit();
            return true;
        }

        private void AfterEdit()
        {
            ApplyText();
            EnsureCaretVisible();
            _blinkTime = 0f;
        }

        /// <summary>点击时把光标放到点击位置附近。</summary>
        public void SetCaretFromPoint(Vector2 point)
        {
            var font = ActiveFont;
            if (font == null) return;

            float target = point.X - (ScreenRect.X + PaddingX) + _scrollX;
            int best = 0;
            float bestDistance = float.MaxValue;
            float x = 0f;
            for (int i = 0; i <= _buffer.Length; i++)
            {
                float distance = MathF.Abs(x - target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
                if (i < _buffer.Length) x += font.GetAdvance(_buffer[i]);
            }

            _caret = best;
            EnsureCaretVisible();
        }

        /// <summary>文本宽度（像素）。</summary>
        private float MeasureWidth(int count)
        {
            var font = ActiveFont;
            if (font == null) return 0f;

            float width = 0f;
            for (int i = 0; i < count && i < _buffer.Length; i++)
                width += font.GetAdvance(_buffer[i]);
            return width;
        }

        /// <summary>让光标保持在可视区域内（水平滚动）。</summary>
        private void EnsureCaretVisible()
        {
            float innerWidth = MathF.Max(1f, ScreenRect.Width - PaddingX * 2f);
            float caretX = MeasureWidth(_caret);

            if (caretX - _scrollX > innerWidth) _scrollX = caretX - innerWidth;
            if (caretX - _scrollX < 0f) _scrollX = caretX;
            if (_scrollX < 0f) _scrollX = 0f;
        }

        // ---------------- 布局 / 更新 / 绘制 ----------------

        protected override void OnLayout()
        {
            float innerWidth = MathF.Max(1f, ScreenRect.Width - PaddingX * 2f);
            float innerHeight = MathF.Max(1f, ScreenRect.Height);

            EnsureCaretVisible();

            var font = ActiveFont;
            float textWidth = font == null ? innerWidth : MathF.Max(innerWidth, font.MeasureLineWidth(_label.Text.AsSpan()));

            _label.Position = new Vector2(PaddingX - _scrollX, 0f);
            _label.Size = new Vector2(textWidth + 2f, innerHeight);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (_focused) _blinkTime += deltaTime;
        }
        public override void OnPointerDown(Vector2 point)
        {
            if (!Interactable) return;
            SetCaretFromPoint(point);
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            // 背景：聚焦时用高亮边框
            string key = _focused ? FocusedSpriteKey : NormalSpriteKey;
            var background = theme.Sprite(key);
            if (!background.IsValid) background = theme.Sprite(UISkin.Panel);
            if (background.IsValid)
            {
                var color = Interactable ? tint : tint * 0.6f;
                DrawSprite(list, background, ScreenRect, clip, color);
            }

            if (!_focused || !Interactable) return;

            // 光标：闪烁，超出可视区域时不画
            if (_blinkTime % 1.06f > 0.62f) return;

            float caretX = ScreenRect.X + PaddingX + MeasureWidth(_caret) - _scrollX;
            float innerLeft = ScreenRect.X + PaddingX - 1f;
            float innerRight = ScreenRect.Right - PaddingX + 1f;
            if (caretX < innerLeft || caretX > innerRight) return;

            var caretRect = new UIRect(caretX, ScreenRect.Y + 6f, 2f, MathF.Max(2f, ScreenRect.Height - 12f));
            DrawSolid(list, theme.Sprite(UISkin.White), caretRect, clip, theme.Color(UISkin.ColorAccent));

        }
    }
}
