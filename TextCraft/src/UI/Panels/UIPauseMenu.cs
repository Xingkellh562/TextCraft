using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 游戏内暂停面板：ESC 打开，半透明遮罩 + 居中卡片。
    /// 提供「继续游戏」「保存并退回主菜单」「设置」「退出游戏」。
    /// </summary>
    public class UIPauseMenu : UIPanel
    {
        private readonly UILabel _worldInfo;

        /// <summary>「继续游戏」按钮（可用于默认焦点）。</summary>
        public UIButton ResumeButton { get; }

        public UIPauseMenu()
        {
            Name = "pauseMenu";
            Layer = UILayers.Window;
            SpriteKey = UISkin.White;
            Color = new Vector4(0.02f, 0.03f, 0.05f, 0.62f);
            // 全屏铺满（遮罩需要盖住整个画面）
            UILayout.Stretch(this, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);
            BlocksPointer = true;

            var theme = UIContext.Theme;
            var font = theme?.Font;
            var fontSmall = theme?.FontSmall;

            var card = new UIPanel { Name = "card", SpriteKey = UISkin.Window };
            UILayout.Place(card, UIAnchorPreset.Center, 0f, 0f, 420f, 372f);
            Add(card);

            var title = new UILabel("已暂停")
            {
                Name = "title",
                Font = theme?.FontTitle,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                Shadow = true,
            };
            UILayout.Place(title, UIAnchorPreset.Top, 0f, 22f, 400f, 60f);
            card.Add(title);

            _worldInfo = new UILabel
            {
                Name = "worldInfo",
                Font = fontSmall,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorTextDim),
            };
            UILayout.Place(_worldInfo, UIAnchorPreset.Top, 0f, 88f, 400f, 22f);
            card.Add(_worldInfo);

            ResumeButton = CreateButton(card, "继续游戏", 128f, () => ResumeRequested?.Invoke(), font, accent: true);
            CreateButton(card, "保存并退回主菜单", 182f, () => SaveAndQuitRequested?.Invoke(), font);
            CreateButton(card, "设置", 236f, () => SettingsRequested?.Invoke(), font);
            CreateButton(card, "退出游戏", 290f, () => QuitRequested?.Invoke(), font);

            var hint = new UILabel("按 Esc 继续游戏")
            {
                Name = "hint",
                Font = fontSmall,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorTextDim),
            };
            UILayout.Place(hint, UIAnchorPreset.Bottom, 0f, -14f, 400f, 20f);
            card.Add(hint);

            SetActive(false);
        }

        private UIButton CreateButton(UIComponent parent, string text, float y, Action onClick, UIFont? font, bool accent = false)
        {
            var button = new UIButton
            {
                Name = "pause_" + text,
                Text = text,
                Font = font,
                TextColor = accent ? UIContext.Theme?.Color(UISkin.ColorTitle) : null,
            };
            UILayout.Place(button, UIAnchorPreset.Top, 0f, y, 320f, 44f);
            button.Clicked += _ => onClick();
            parent.Add(button);
            return button;
        }

        public event Action? ResumeRequested;

        public event Action? SaveAndQuitRequested;

        public event Action? SettingsRequested;

        public event Action? QuitRequested;

        public bool IsOpen => ActiveSelf;

        /// <summary>显示暂停面板并刷新世界信息。</summary>
        public void Show(string worldName, int seed)
        {
            string seedText = seed == 0 ? "随机种子" : seed.ToString();
            _worldInfo.Text = $"世界：{worldName}    种子 {seedText}";
            SetActive(true);
            BringToFront();
            RequestLayout();
        }

        public void Hide() => SetActive(false);

        /// <summary>不改变可见性，只刷新世界信息（进入世界时调用）。</summary>
        public void SetWorldInfo(string worldName, int seed)
        {
            string seedText = seed == 0 ? "随机种子" : seed.ToString();
            _worldInfo.Text = $"世界：{worldName}    种子 {seedText}";
        }
    }
}
