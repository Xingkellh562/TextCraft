using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 按钮：普通/悬停/按下/禁用四种视觉状态 + 文案 + 可选图标 + 点击回调。
    ///
    /// 实现上用组合而不是"自己画所有东西"：
    /// 背景由按钮绘制，文案/图标是子控件（<see cref="UILabel"/> / <see cref="UIImage"/>），
    /// 因此鼠标事件只会落在按钮本身（子控件不可交互），行为与原生按钮一致。
    /// </summary>
    public class UIButton : UIComponent
    {
        private readonly UILabel _label;
        private readonly UIImage _icon;

        private string _text = "";
        private bool _hover;
        private bool _pressed;
        private float _hoverAmount;

        public UIButton()
        {
            Name = "button";
            Size = new Vector2(200f, 44f);

            _label = new UILabel
            {
                Name = "label",
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                Padding = 0f,
            };
            UILayout.Stretch(_label, UIAnchorPreset.StretchAll, 12f, 0f, 12f, 0f);
            Add(_label);

            _icon = new UIImage { Name = "icon" };
            UILayout.Place(_icon, UIAnchorPreset.Left, 10f, 0f, 22f, 22f);
            _icon.SetActive(false);
            Add(_icon);
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                _label.Text = _text;
            }
        }

        public UIFont? Font
        {
            get => _label.Font;
            set => _label.Font = value;
        }

        public Vector4? TextColor
        {
            get => _label.TextColor;
            set => _label.TextColor = value;
        }

        /// <summary>左侧图标（22x22）。设为空精灵即隐藏。</summary>
        public UISprite? Icon
        {
            get => _icon.Sprite;
            set
            {
                _icon.Sprite = value;
                _icon.SetActive(value is { IsValid: true });
            }
        }

        /// <summary>是否可交互；false 时使用"禁用"外观且不响应点击。</summary>
        public bool Interactable { get; set; } = true;

        /// <summary>自定义背景精灵（空则使用主题 button 系列）。</summary>
        public UISprite? BackgroundOverride { get; set; }

        public event Action<UIButton>? Clicked;

        public override bool IsInteractable => Interactable;

        public override void OnPointerEnter() => _hover = true;

        public override void OnPointerExit()
        {
            _hover = false;
            _pressed = false;
        }

        public override void OnPointerDown(Vector2 point)
        {
            if (Interactable) _pressed = true;
        }

        public override void OnPointerUp(Vector2 point, bool inside) => _pressed = false;

        /// <summary>指针在按钮上按下并抬起时由 <see cref="UIInput"/> 调用。</summary>
        public override void OnClick() => PerformClick();

        /// <summary>以代码方式触发点击（键盘/自动化测试用）。</summary>
        public void PerformClick()
        {
            if (!Interactable) return;
            Clicked?.Invoke(this);
        }

        protected override void OnUpdate(float deltaTime)
        {
            float target = _hover && Interactable ? 1f : 0f;
            float speed = MathF.Min(1f, deltaTime * 12f);
            _hoverAmount += (target - _hoverAmount) * speed;
            if (MathF.Abs(_hoverAmount - target) < 0.002f) _hoverAmount = target;
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            var normal = Pick(theme, UISkin.Button);
            if (!Interactable)
            {
                var disabled = Pick(theme, UISkin.ButtonDisabled);
                DrawSprite(list, disabled, ScreenRect, clip, tint * 0.9f);
                return;
            }

            DrawSprite(list, normal, ScreenRect, clip, tint);

            if (_pressed)
            {
                DrawSprite(list, Pick(theme, UISkin.ButtonPressed), ScreenRect, clip, tint);
                return;
            }

            if (_hoverAmount > 0.01f)
            {
                var hover = Pick(theme, UISkin.ButtonHover);
                var hoverTint = tint * new Vector4(1f, 1f, 1f, _hoverAmount);
                DrawSprite(list, hover, ScreenRect, clip, hoverTint);
            }
        }

        private UISprite Pick(UITheme theme, string key)
        {
            if (BackgroundOverride is { IsValid: true } custom) return custom;
            if (theme.TryGetSprite(key, out var sprite) && sprite.IsValid) return sprite;
            return theme.Sprite(UISkin.Panel);
        }
    }
}
