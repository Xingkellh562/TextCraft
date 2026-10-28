using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using TextCraft.src.Core;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.Event;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Table;

namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 结构表：用新控件搭出主菜单 / 设置窗口 / 游戏 HUD / 加载遮罩 / 调试面板。
    ///
    /// 相比旧版（一个 Panel 一个 DrawCall、只有一个准星和一块标题图）：
    /// <list type="bullet">
    /// <item>控件按层级组织：Background → Panel（主菜单、窗口）→ Hud → Overlay → Debug；</item>
    /// <item>文本用真实字体度量排版，中文可显示；</item>
    /// <item>主菜单/设置窗口是带交互的复合控件（按钮、开关、滑块、可拖动窗口）。</item>
    /// </list>
    /// </summary>
    public sealed class UITable
    {
        private readonly Dictionary<string, UIComponent> _components = new(StringComparer.OrdinalIgnoreCase);
        private readonly UIMgr _uiMgr;

        private UIPanel _hud = null!;
        private UIImage _crosshair = null!;
        private UILabel _fpsLabel = null!;
        private UILabel _blockLabel = null!;
        private UIHotbar _hotbar = null!;

        private UIPanel _mainMenu = null!;
        private UILabel _titleLabel = null!;
        private UILabel _subtitleLabel = null!;
        private UIButton _playButton = null!;
        private UIButton _settingsButton = null!;
        private UIButton _quitButton = null!;

        private UIWindow _settingsWindow = null!;
        private UIToggle _fogToggle = null!;
        private UIToggle _vsyncToggle = null!;
        private UISlider _viewSlider = null!;
        private UILabel _viewValueLabel = null!;
        private UIToggle _batchToggle = null!;

        private UIWorldSelectPanel _worldSelect = null!;
        private UIPauseMenu _pauseMenu = null!;

        private UIPanel _loadingOverlay = null!;
        private UILabel _loadingLabel = null!;
        private UIProgressBar _loadingBar = null!;

        private UIPanel _debugPanel = null!;
        private UILabel _debugLabel = null!;

        private int _selectedBlockId = 1;
        private int _selectedSlot;
        private double _fps;
        private float _debugRefreshTimer;
        private bool _debugVisible;

        internal UITable(UIMgr uiMgr)
        {
            _uiMgr = uiMgr;
        }

        /// <summary>按名字取控件（供外部逻辑更新 FPS / 选中方块等）。</summary>
        public UIComponent? this[string name]
            => _components.TryGetValue(name, out var component) ? component : null;

        public UIComponent? Get(string name) => this[name];

        public bool DebugVisible => _debugVisible;

        // =====================================================================
        // 构建
        // =====================================================================

        public void LoadUI(UIComponent root)
        {
            _components.Clear();

            BuildHud(root);
            BuildMainMenu(root);
            BuildSettingsWindow(root);
            BuildWorldSelect(root);
            BuildPauseMenu(root);
            BuildLoadingOverlay(root);
            BuildDebugPanel(root);

            EnterMainMenu();

            SetSelectedBlock(1);
        }

        /// <summary>“开始游戏”界面：存档列表 + 新建世界。</summary>
        private void BuildWorldSelect(UIComponent root)
        {
            _worldSelect = new UIWorldSelectPanel { Name = "worldSelect" };
            root.Add(_worldSelect);
            Register("worldSelect", _worldSelect);

            _worldSelect.Back += EnterMainMenu;
            _worldSelect.LoadRequested += name =>
            {
                // 读取已有存档：种子以 worldData.xml 为准，这里传 0
                EventMgr.Ins.Publish(new LoadWorldEventArg { name = name, seed = 0 });
            };
            _worldSelect.CreateRequested += (name, seed) =>
                EventMgr.Ins.Publish(new LoadWorldEventArg { name = name, seed = seed });

            _worldSelect.SetActive(false);
        }

        /// <summary>游戏内暂停面板（ESC 打开）。</summary>
        private void BuildPauseMenu(UIComponent root)
        {
            _pauseMenu = new UIPauseMenu { Name = "pauseMenu" };
            root.Add(_pauseMenu);
            Register("pauseMenu", _pauseMenu);

            _pauseMenu.ResumeRequested += () => EventMgr.Ins.Publish(new ResumeGameEventArg());
            _pauseMenu.SaveAndQuitRequested += () => EventMgr.Ins.Publish(new SaveAndQuitWorldEventArg());
            _pauseMenu.SettingsRequested += () => _settingsWindow.Show();
            _pauseMenu.QuitRequested += () => EventMgr.Ins.Publish(new RequestExitEventArg());
        }

        private void BuildHud(UIComponent root)
        {
            _hud = new UIPanel { Name = "hud", Layer = UILayers.Hud };
            UILayout.Stretch(_hud, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);
            root.Add(_hud);
            Register("hud", _hud);
            Register("gamePanel", _hud);   // 兼容旧命名

            var theme = UIContext.Theme!;

            // 准星
            _crosshair = new UIImage(theme.Sprite("sight")) { Name = "crosshair" };
            UILayout.Place(_crosshair, UIAnchorPreset.Center, 0f, 0f, 20f, 20f);
            _crosshair.Color = new Vector4(1f, 1f, 1f, 0.85f);
            _hud.Add(_crosshair);
            Register("crosshair", _crosshair);

            // FPS
            _fpsLabel = new UILabel("FPS: --")
            {
                Name = "fps",
                Font = theme.FontSmall,
                VerticalAlign = UIVerticalAlign.Top,
                Align = UITextAlign.Left,
                Shadow = true,
            };
            UILayout.Place(_fpsLabel, UIAnchorPreset.TopLeft, 10f, 8f, 220f, 20f);
            _hud.Add(_fpsLabel);
            Register("fps", _fpsLabel);

            // 当前选中方块名
            _blockLabel = new UILabel("stone")
            {
                Name = "blockLabel",
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Bottom,
                Shadow = true,
                Color = new Vector4(1f, 1f, 1f, 0.92f),
            };
            UILayout.Place(_blockLabel, UIAnchorPreset.Bottom, 0f, -96f, 320f, 24f);
            _hud.Add(_blockLabel);
            Register("blockLabel", _blockLabel);

            // 快捷栏
            _hotbar = new UIHotbar { Name = "hotbar", SlotCount = 10 };
            UILayout.Place(_hotbar, UIAnchorPreset.Bottom, 0f, -18f, 0f, 0f);
            _hotbar.IconProvider = index => GetBlockIcon(index + 1);
            _hotbar.SlotSelected += index =>
            {
                SetSelectedSlot(index);
                EventMgr.Ins.Publish(new SelectBlockEventArg { blockId = index + 1 });
            };
            _hud.Add(_hotbar);
            Register("hotbar", _hotbar);
            Register("displayBlock", _hotbar);   // 兼容旧命名
        }

        private void BuildMainMenu(UIComponent root)
        {
            var theme = UIContext.Theme!;

            _mainMenu = new UIPanel { Name = "mainMenu", Layer = UILayers.Panel, SpriteKey = UISkin.White };
            UILayout.Stretch(_mainMenu, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);
            _mainMenu.Color = new Vector4(0.05f, 0.06f, 0.09f, 0.88f);
            root.Add(_mainMenu);
            Register("mainMenu", _mainMenu);
            Register("mainMenuPanel", _mainMenu);

            _titleLabel = new UILabel("TEXTCRAFT")
            {
                Name = "title",
                Font = theme.FontTitle,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                Shadow = true,
                TextColor = theme.Color(UISkin.ColorTitle),
                Color = Vector4.One,
            };
            UILayout.Place(_titleLabel, UIAnchorPreset.Top, 0f, 64f, 900f, 70f);
            _mainMenu.Add(_titleLabel);
            Register("mainMenuText", _titleLabel);   // 兼容旧命名

            _subtitleLabel = new UILabel("体素沙盒 · 全新 UI 渲染系统")
            {
                Name = "subtitle",
                Font = theme.FontSmall,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme.Color(UISkin.ColorTextDim),
            };
            UILayout.Place(_subtitleLabel, UIAnchorPreset.Top, 0f, 138f, 900f, 24f);
            _mainMenu.Add(_subtitleLabel);

            var buttons = new UIPanel { Name = "menuButtons" };
            UILayout.Place(buttons, UIAnchorPreset.Center, 0f, 30f, 260f, 190f);
            _mainMenu.Add(buttons);

            _playButton = CreateMenuButton(buttons, "开始游戏", 0f, OpenWorldSelect);
            _settingsButton = CreateMenuButton(buttons, "设置", 62f, () => _settingsWindow.Show());
            _quitButton = CreateMenuButton(buttons, "退出游戏", 124f, () =>
                EventMgr.Ins.Publish(new RequestExitEventArg()));

            _playButton.Font = theme.Font;
            _settingsButton.Font = theme.Font;
            _quitButton.Font = theme.Font;

            Register("playButton", _playButton);
            Register("settingsButton", _settingsButton);

            var hint = new UILabel("选择存档或创建新世界　|　F3 调试面板　F4 切换渲染模式")
            {
                Name = "hint",
                Font = theme.FontSmall,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = new Vector4(0.62f, 0.66f, 0.78f, 1f),
            };
            UILayout.Place(hint, UIAnchorPreset.Bottom, 0f, -22f, 900f, 22f);
            _mainMenu.Add(hint);

            var version = new UILabel("TextCraft · UI v2")
            {
                Name = "version",
                Font = theme.FontSmall,
                Align = UITextAlign.Right,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = new Vector4(0.5f, 0.54f, 0.64f, 1f),
            };
            UILayout.Place(version, UIAnchorPreset.BottomRight, -14f, -10f, 260f, 20f);
            _mainMenu.Add(version);
        }

        private UIButton CreateMenuButton(UIComponent parent, string text, float y, Action onClick)
        {
            var button = new UIButton { Name = "button_" + text, Text = text };
            UILayout.Place(button, UIAnchorPreset.TopLeft, 0f, y, 260f, 46f);
            button.Clicked += _ => onClick();
            parent.Add(button);
            return button;
        }

        private void BuildSettingsWindow(UIComponent root)
        {
            var theme = UIContext.Theme!;

            _settingsWindow = new UIWindow("设置") { Name = "settingsWindow" };
            // 设置窗口需要盖在暂停面板之上，因此放到 Window 层之后
            _settingsWindow.Layer = UILayers.Window + 500;
            UILayout.Place(_settingsWindow, UIAnchorPreset.Center, 0f, 0f, 440f, 320f);
            root.Add(_settingsWindow);
            Register("settingsWindow", _settingsWindow);

            var content = _settingsWindow.Content;

            _fogToggle = new UIToggle { Name = "fogToggle", Text = "雾效", Font = theme.Font };
            UILayout.Place(_fogToggle, UIAnchorPreset.TopLeft, 0f, 4f, 380f, 30f);
            _fogToggle.IsOn = ConfigMgr.Ins.graphicConfig.fog;
            _fogToggle.Changed += (_, value) => ConfigMgr.Ins.graphicConfig.fog = value;
            content.Add(_fogToggle);
            Register("fogToggle", _fogToggle);

            _vsyncToggle = new UIToggle { Name = "vsyncToggle", Text = "垂直同步", Font = theme.Font };
            UILayout.Place(_vsyncToggle, UIAnchorPreset.TopLeft, 0f, 42f, 380f, 30f);
            _vsyncToggle.IsOn = false;
            _vsyncToggle.Changed += (_, value) => EventMgr.Ins.Publish(new ToggleVSyncEventArg { enabled = value });
            content.Add(_vsyncToggle);
            Register("vsyncToggle", _vsyncToggle);

            var viewTitle = new UILabel("视野距离")
            {
                Name = "viewTitle",
                Font = theme.Font,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Place(viewTitle, UIAnchorPreset.TopLeft, 0f, 88f, 200f, 26f);
            content.Add(viewTitle);

            _viewValueLabel = new UILabel
            {
                Name = "viewValue",
                Font = theme.Font,
                Align = UITextAlign.Right,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme.Color(UISkin.ColorAccent),
            };
            UILayout.Place(_viewValueLabel, UIAnchorPreset.TopRight, -2f, 88f, 120f, 26f);
            content.Add(_viewValueLabel);

            _viewSlider = new UISlider { Name = "viewSlider" };
            UILayout.Place(_viewSlider, UIAnchorPreset.TopLeft, 0f, 120f, 380f, 24f);
            _viewSlider.Value = MathHelper.Clamp((ConfigMgr.Ins.graphicConfig.ViewRange - 64f) / (384f - 64f), 0f, 1f);
            _viewSlider.ValueChanged += _ => ApplyViewRange();
            content.Add(_viewSlider);
            Register("viewSlider", _viewSlider);
            ApplyViewRange();

            // 运行时切换"合批 / 逐控件"渲染，直接观察 DrawCall 变化
            _batchToggle = new UIToggle { Name = "batchToggle", Text = "UI 合批渲染", Font = theme.Font };
            UILayout.Place(_batchToggle, UIAnchorPreset.TopLeft, 0f, 162f, 380f, 30f);
            _batchToggle.IsOn = UIAssets.Ins.Mode == UIRenderMode.Batched;
            _batchToggle.Changed += (_, value) =>
                UIAssets.Ins.SetMode(value ? UIRenderMode.Batched : UIRenderMode.Legacy);
            content.Add(_batchToggle);
            Register("batchToggle", _batchToggle);

            var close = new UIButton { Name = "settingsClose", Text = "关闭", Font = theme.Font };
            UILayout.Place(close, UIAnchorPreset.BottomRight, -2f, -2f, 120f, 40f);
            close.Clicked += _ => _settingsWindow.Hide();
            content.Add(close);

            _settingsWindow.Hide();
        }

        private void BuildLoadingOverlay(UIComponent root)
        {
            var theme = UIContext.Theme!;

            _loadingOverlay = new UIPanel { Name = "loadingOverlay", Layer = UILayers.Overlay, BlocksPointer = true, SpriteKey = UISkin.White };
            UILayout.Stretch(_loadingOverlay, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);
            _loadingOverlay.Color = new Vector4(0.02f, 0.03f, 0.05f, 0.92f);
            root.Add(_loadingOverlay);
            Register("loadingPanel", _loadingOverlay);

            _loadingLabel = new UILabel("正在生成世界…")
            {
                Name = "loadingLabel",
                Font = theme.Font,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Place(_loadingLabel, UIAnchorPreset.Center, 0f, -18f, 600f, 30f);
            _loadingOverlay.Add(_loadingLabel);
            Register("loadingText", _loadingLabel);

            _loadingBar = new UIProgressBar { Name = "loadingBar" };
            UILayout.Place(_loadingBar, UIAnchorPreset.Center, 0f, 24f, 360f, 22f);
            _loadingBar.Value = 0.35f;
            _loadingBar.Text = "";
            _loadingOverlay.Add(_loadingBar);

            _loadingOverlay.SetActive(false);
        }

        private void BuildDebugPanel(UIComponent root)
        {
            var theme = UIContext.Theme!;

            _debugPanel = new UIPanel { Name = "debugPanel", Layer = UILayers.Debug, BlocksPointer = false };
            _debugPanel.SpriteKey = UISkin.Tooltip;
            UILayout.Place(_debugPanel, UIAnchorPreset.TopRight, -10f, 10f, 300f, 150f);
            _debugPanel.Color = new Vector4(1f, 1f, 1f, 0.95f);
            root.Add(_debugPanel);
            Register("debugPanel", _debugPanel);

            _debugLabel = new UILabel
            {
                Name = "debugLabel",
                Font = theme.FontSmall,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Top,
                LineSpacing = 1f,
            };
            UILayout.Stretch(_debugLabel, UIAnchorPreset.StretchAll, 10f, 8f, 8f, 8f);
            _debugPanel.Add(_debugLabel);

            _debugPanel.SetActive(false);
        }

        // =====================================================================
        // 运行时
        // =====================================================================

        public void OnSizeChange(Vector2i size)
        {
        }

        public void OnUpdate(float updateTime)
        {
            if (!_debugVisible) return;

            _debugRefreshTimer += updateTime;
            if (_debugRefreshTimer < 0.25f) return;
            _debugRefreshTimer = 0f;
            RefreshDebugText();
        }

        /// <summary>渲染结束后刷新调试文本（此时统计信息是"本帧"的）。</summary>
        public void AfterRender(in UIRenderStats stats)
        {
            if (!_debugVisible) return;
            _lastStats = stats;
            RefreshDebugText();
        }

        private UIRenderStats _lastStats;

        private void RefreshDebugText()
        {
            var assets = UIAssets.Ins;
            var sb = new StringBuilder(256);
            sb.Append("UI 渲染 (")
              .Append(assets.Mode == UIRenderMode.Legacy ? "逐控件" : "层级合批")
              .Append(")\n");
            sb.Append("FPS     ").Append(_fps.ToString("0")).Append("   控件 ").Append(_lastStats.Components).Append('\n');
            sb.Append("图元    ").Append(_lastStats.Primitives).Append("   顶点 ").Append(_lastStats.Vertices).Append('\n');
            sb.Append("批次    ").Append(_lastStats.Batches).Append("   DrawCall ").Append(_lastStats.DrawCalls).Append('\n');
            sb.Append("层级    ").Append(_lastStats.Layers).Append('\n');
            sb.Append("F3 关闭   F4 切换模式");
            _debugLabel.Text = sb.ToString();
        }

        public void UpdateFps(double fps)
        {
            _fps = fps;
            if (_fpsLabel != null)
                _fpsLabel.Text = "FPS: " + (int)fps;
        }

        public void SetDebugVisible(bool visible)
        {
            _debugVisible = visible;
            _debugPanel.SetActive(visible);
            if (visible)
            {
                _debugPanel.BringToFront();
                RefreshDebugText();
            }
        }

        public void ToggleDebug() => SetDebugVisible(!_debugVisible);

        /// <summary>打开设置窗口（主菜单按钮 / 控制台命令用）。</summary>
        public void OpenSettings() => _settingsWindow.Show();

        public void CloseSettings() => _settingsWindow.Hide();

        /// <summary>切换合批/逐控件渲染，用于直观对比 DrawCall 数量。</summary>
        public void ToggleRenderMode() => UIAssets.Ins.SetMode(
            UIAssets.Ins.Mode == UIRenderMode.Legacy ? UIRenderMode.Batched : UIRenderMode.Legacy);

        public void SetSelectedSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            _hotbar.SelectedIndex = slotIndex;
            SetSelectedBlock(slotIndex + 1);
        }

        public void SetSelectedBlock(int blockId)
        {
            _selectedBlockId = blockId;
            _hotbar.SelectedIndex = Math.Clamp(blockId - 1, 0, _hotbar.SlotCount - 1);
            var block = BlockTable.Ins[blockId * 16];
            _blockLabel.Text = string.IsNullOrEmpty(block.Name) ? blockId.ToString() : $"{block.Name}  ({blockId})";
        }

        public int SelectedBlockId => _selectedBlockId;

        /// <summary>主菜单：「开始游戏」按钮进入存档/新建界面。</summary>
        public void OpenWorldSelect()
        {
            _mainMenu.SetActive(false);
            _hud.SetActive(false);
            _loadingOverlay.SetActive(false);
            _settingsWindow.Hide();
            ClosePause();

            _worldSelect.RefreshSaves();
            _worldSelect.SetActive(true);
            _worldSelect.BringToFront();
            _uiMgr.ReleasePointer();
        }

        /// <summary>关闭“开始游戏”界面回到主菜单。</summary>
        public void CloseWorldSelect()
        {
            _worldSelect.SetActive(false);
            EnterMainMenu();
        }

        /// <summary>显示暂停面板（ESC）。</summary>
        public void OpenPause(string worldName, int seed)
        {
            _pauseMenu.Show(worldName, seed);
            _uiMgr.ReleasePointer();
        }

        public void ClosePause()
        {
            if (_pauseMenu.IsOpen) _pauseMenu.Hide();
            _uiMgr.ReleasePointer();
        }

        public bool IsPauseOpen => _pauseMenu.IsOpen;

        /// <summary>进入世界后刷新暂停面板里的世界信息。</summary>
        public void SetWorldInfo(string worldName, int seed) => _pauseMenu.SetWorldInfo(worldName, seed);

        /// <summary>
        /// ESC 的统一处理：依次尝试"让输入框失焦 → 收起下拉 → 关闭设置窗口"。
        /// 返回 true 表示这次 ESC 已被 UI 消费。
        /// </summary>
        public bool HandleEscape()
        {
            if (_uiMgr.Input.HasTextFocus)
            {
                _uiMgr.Input.SetTextFocus(null);
                return true;
            }

            if (UIDropdown.CloseAllOpen()) return true;

            if (_settingsWindow.ActiveSelf)
            {
                _settingsWindow.Hide();
                return true;
            }

            return false;
        }

        public void EnterMainMenu()
        {
            _hud.SetActive(false);
            _mainMenu.SetActive(true);
            _loadingOverlay.SetActive(false);
            _worldSelect.SetActive(false);
            _settingsWindow.Hide();
            _pauseMenu.Hide();
            _uiMgr.ReleasePointer();
        }

        public void EnterGame()
        {
            _mainMenu.SetActive(false);
            _hud.SetActive(true);
            _loadingOverlay.SetActive(false);
            _worldSelect.SetActive(false);
            _settingsWindow.Hide();
            _pauseMenu.Hide();
            _uiMgr.ReleasePointer();
        }

        public void ShowLoading(bool visible)
        {
            _loadingOverlay.SetActive(visible);
            if (visible)
            {
                _worldSelect.SetActive(false);
                _pauseMenu.Hide();
            }
            if (visible)
            {
                _loadingOverlay.BringToFront();
                _uiMgr.ReleasePointer();
            }
        }

        private void ApplyViewRange()
        {
            int range = (int)MathF.Round(64f + _viewSlider.Value * (384f - 64f));
            ConfigMgr.Ins.graphicConfig.ViewRange = range;
            _viewValueLabel.Text = range.ToString();
        }

        /// <summary>方块图标来自 UI 图集里名为 "1".."10" 的精灵。</summary>
        private static UISprite GetBlockIcon(int blockId)
        {
            var atlas = UIAssets.Ins.GetAtlas("ui");
            if (atlas != null && atlas.TryGetSprite(blockId.ToString(), out var sprite))
                return sprite;
            return UISprite.Empty;
        }

        private void Register(string name, UIComponent component) => _components[name] = component;
    }
}
