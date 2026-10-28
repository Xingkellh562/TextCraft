using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 进度条：底板 + 填充（两者都支持九宫格）+ 可选居中文本。
    /// 用于加载界面与世界生成提示。
    /// </summary>
    public class UIProgressBar : UIComponent
    {
        private readonly UILabel _label;
        private float _value;

        public UIProgressBar()
        {
            Name = "progressBar";
            Size = new Vector2(320f, 22f);

            _label = new UILabel
            {
                Name = "label",
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Stretch(_label, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);
            Add(_label);
        }

        /// <summary>0~1 的进度。</summary>
        public float Value
        {
            get => _value;
            set => _value = MathHelper.Clamp(value, 0f, 1f);
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

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            var background = theme.Sprite(UISkin.ProgressBg);
            DrawSprite(list, background.IsValid ? background : theme.Sprite(UISkin.Panel), ScreenRect, clip, tint);

            if (_value > 0f)
            {
                var inner = ScreenRect.Inset(2f, 2f, 2f, 2f);
                if (inner.Width > 0.5f && inner.Height > 0.5f)
                {
                    var fillRect = new UIRect(inner.X, inner.Y, inner.Width * _value, inner.Height);
                    var fill = theme.Sprite(UISkin.ProgressFill);
                    if (fill.IsValid)
                        DrawSprite(list, fill, fillRect, clip, tint);
                    else
                        DrawSolid(list, theme.Sprite(UISkin.White), fillRect, clip, theme.Color(UISkin.ColorAccent));
                }
            }
        }
    }
}
