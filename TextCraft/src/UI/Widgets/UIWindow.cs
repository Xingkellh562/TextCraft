using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 窗口：背景 + 标题栏（可拖动）+ 标题 + 关闭按钮 + 内容容器。
    /// 演示"复杂控件由基础控件组合"的写法：窗口本身几乎不含绘制代码。
    /// </summary>
    public class UIWindow : UIComponent
    {
        private readonly UIPanel _background;
        private readonly UIDragHandle _titleBar;
        private readonly UILabel _titleLabel;
        private readonly UIButton _closeButton;
        private readonly UIPanel _content;

        public UIWindow()
            : this("")
        {
        }

        public UIWindow(string title)
        {
            Name = "window";
            Layer = UILayers.Window;
            Size = new Vector2(460f, 340f);
            ClipChildren = true;

            _background = new UIPanel { Name = "background", SpriteKey = UISkin.Window };
            UILayout.Stretch(_background, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);
            Add(_background);

            _titleBar = new UIDragHandle
            {
                Name = "titleBar",
                Target = this,
                SpriteKey = UISkin.WindowTitle,
            };
            UILayout.Stretch(_titleBar, UIAnchorPreset.StretchTop, 0f, 0f, 0f, 0f);
            UILayout.Sized(_titleBar, 0f, 36f);
            Add(_titleBar);

            _titleLabel = new UILabel
            {
                Name = "title",
                Text = title,
                VerticalAlign = UIVerticalAlign.Middle,
                Align = UITextAlign.Left,
                Padding = 0f,
            };
            UILayout.Place(_titleLabel, UIAnchorPreset.TopLeft, 14f, 0f, 320f, 36f);
            Add(_titleLabel);

            _closeButton = new UIButton { Name = "close", Text = "×" };
            UILayout.Place(_closeButton, UIAnchorPreset.TopRight, -8f, 3f, 30f, 30f);
            _closeButton.Clicked += _ => Close();
            Add(_closeButton);

            _content = new UIPanel { Name = "content" };
            UILayout.Stretch(_content, UIAnchorPreset.StretchAll, 14f, 48f, 14f, 14f);
            Add(_content);
        }

        public string Title
        {
            get => _titleLabel.Text;
            set => _titleLabel.Text = value ?? "";
        }

        /// <summary>内容容器：业务 UI 都挂在这里。</summary>
        public UIComponent Content => _content;

        public UIDragHandle TitleBar => _titleBar;

        public bool Closable
        {
            get => _closeButton.ActiveSelf;
            set => _closeButton.SetActive(value);
        }

        public event Action<UIWindow>? Closed;

        public void Show()
        {
            SetActive(true);
            BringToFront();
            RequestLayout();
        }

        public void Hide() => SetActive(false);

        public void Close()
        {
            Hide();
            Closed?.Invoke(this);
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            // 窗口下方补一层柔和投影，让窗口从游戏画面上"浮"起来
            var theme = UIContext.Theme;
            if (theme == null) return;

            var shadowRect = ScreenRect.Inflate(10f);
            var shadow = theme.Sprite(UISkin.WindowShadow);
            if (shadow.IsValid)
                DrawSprite(list, shadow, shadowRect, clip, tint * new Vector4(1f, 1f, 1f, 0.9f));
            else
                DrawSolid(list, theme.Sprite(UISkin.White), shadowRect, clip, new Vector4(0f, 0f, 0f, 0.35f * tint.W));
        }
    }
}
