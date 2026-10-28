using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 开关 / 复选框：左侧方块精灵 + 右侧文案，点击整行切换。
    /// </summary>
    public class UIToggle : UIComponent
    {
        private readonly UILabel _label;
        private bool _isOn;
        private bool _hover;

        public UIToggle()
        {
            Name = "toggle";
            Size = new Vector2(260f, 32f);

            _label = new UILabel
            {
                Name = "label",
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Stretch(_label, UIAnchorPreset.StretchAll, 32f, 0f, 0f, 0f);
            Add(_label);
        }

        public string Text
        {
            get => _label.Text;
            set => _label.Text = value ?? "";
        }

        public UIFont? Font
        {
            get => _label.Font;
            set => _label.Font = value;
        }

        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                Changed?.Invoke(this, _isOn);
            }
        }

        public bool Interactable { get; set; } = true;

        public event Action<UIToggle, bool>? Changed;

        public override bool IsInteractable => Interactable;

        public override void OnPointerEnter() => _hover = true;

        public override void OnPointerExit() => _hover = false;

        /// <summary>点击整行即切换。</summary>
        public override void OnClick()
        {
            if (!Interactable) return;
            IsOn = !IsOn;
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            var box = UIRect.FromMinMax(
                new Vector2(ScreenRect.X, ScreenRect.Center.Y - 11f),
                new Vector2(ScreenRect.X + 22f, ScreenRect.Center.Y + 11f));

            var off = theme.Sprite(UISkin.CheckboxOff);
            var on = theme.Sprite(UISkin.CheckboxOn);

            var color = Interactable ? tint : tint * 0.55f;
            if (_isOn && on.IsValid)
                DrawSprite(list, on, box, clip, color);
            else
                DrawSprite(list, off.IsValid ? off : on, box, clip, color);

            if (_hover && Interactable)
            {
                var highlight = theme.Sprite(UISkin.White);
                DrawSolid(list, highlight, box, clip, new Vector4(0.45f, 0.7f, 1f, 0.16f));
            }
        }
    }
}
