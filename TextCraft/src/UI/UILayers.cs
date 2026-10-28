namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 层级（数值越小越先绘制，即越靠"后面"）。
    /// 同一层级内的元素按提交顺序绘制，不同层级之间整体排序，
    /// 因此"背包窗口"不会因为添加顺序而被 HUD 遮住。
    /// </summary>
    public static class UILayers
    {
        /// <summary>背景（全屏遮罩、主菜单底图）。</summary>
        public const int Background = 0;

        /// <summary>世界层叠加（不属于任何面板的提示）。</summary>
        public const int World = 1000;

        /// <summary>游戏内 HUD（准星、快捷栏、FPS）。</summary>
        public const int Hud = 2000;

        /// <summary>面板 / 主菜单。</summary>
        public const int Panel = 3000;

        /// <summary>窗口（设置、背包等可拖动窗口）。</summary>
        public const int Window = 4000;

        /// <summary>弹出层（下拉框、悬浮提示）。</summary>
        public const int Popup = 5000;

        /// <summary>全屏遮罩（加载中）。</summary>
        public const int Overlay = 6000;

        /// <summary>调试信息（F3）。</summary>
        public const int Debug = 7000;

        /// <summary>未指定层级时的默认值。</summary>
        public const int Default = Panel;

        public static string ToName(int layer) => layer switch
        {
            Background => nameof(Background),
            World => nameof(World),
            Hud => nameof(Hud),
            Panel => nameof(Panel),
            Window => nameof(Window),
            Popup => nameof(Popup),
            Overlay => nameof(Overlay),
            Debug => nameof(Debug),
            _ => layer.ToString(),
        };
    }

    /// <summary>UI 渲染模式。</summary>
    public enum UIRenderMode
    {
        /// <summary>层级 + 合批（默认）。</summary>
        Batched = 0,

        /// <summary>旧行为：每个 UI 元素一次 DrawCall，用于对比/回滚验证。</summary>
        Legacy = 1,
    }

    /// <summary>鼠标悬停在控件上时希望的指针形状（由 Game 应用到窗口）。</summary>
    public enum UICursorShape
    {
        Default = 0,
        Text = 1,
        Hand = 2,
    }

    /// <summary>
    /// 主题里可用的精灵键与颜色键常量。
    /// ui.json 的 theme.sprites / theme.colors 用这些名字登记资源。
    /// </summary>
    public static class UISkin
    {
        public const string White = "white";
        public const string Panel = "panel";
        public const string Tooltip = "tooltip";
        public const string Divider = "divider";

        public const string Button = "button";
        public const string ButtonHover = "buttonHover";
        public const string ButtonPressed = "buttonPressed";
        public const string ButtonDisabled = "buttonDisabled";

        public const string Window = "window";
        public const string WindowTitle = "windowTitle";
        public const string WindowShadow = "windowShadow";
        public const string Close = "close";

        public const string CheckboxOff = "checkboxOff";
        public const string CheckboxOn = "checkboxOn";

        public const string ProgressBg = "progressBg";
        public const string ProgressFill = "progressFill";

        public const string SliderTrack = "sliderTrack";
        public const string SliderFill = "sliderFill";
        public const string SliderHandle = "sliderHandle";

        public const string Slot = "slot";
        public const string SlotSelected = "slotSelected";

        public const string TextField = "textField";
        public const string TextFieldFocused = "textFieldFocused";
        public const string ArrowDown = "arrowDown";
        public const string ListItem = "listItem";
        public const string ListItemHover = "listItemHover";

        public const string ColorText = "text";
        public const string ColorTextDim = "textDim";
        public const string ColorTextDisabled = "textDisabled";
        public const string ColorAccent = "accent";
        public const string ColorTitle = "title";
        public const string ColorShadow = "shadow";
        public const string ColorTint = "tint";
        public const string ColorDanger = "danger";
        public const string ColorSuccess = "success";
    }
}
