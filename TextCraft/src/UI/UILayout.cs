using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 锚点预设。<see cref="Min"/>/<see cref="Max"/> 相同表示点锚点（用 Position+Size），
    /// 不同表示拉伸锚点（用 OffsetMin/OffsetMax）。
    /// </summary>
    public readonly struct UIAnchorPreset
    {
        public readonly Vector2 Min;
        public readonly Vector2 Max;
        public readonly Vector2 Pivot;

        public UIAnchorPreset(Vector2 min, Vector2 max, Vector2 pivot)
        {
            Min = min;
            Max = max;
            Pivot = pivot;
        }

        public static readonly UIAnchorPreset TopLeft = new(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        public static readonly UIAnchorPreset Top = new(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        public static readonly UIAnchorPreset TopRight = new(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        public static readonly UIAnchorPreset Left = new(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        public static readonly UIAnchorPreset Center = new(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        public static readonly UIAnchorPreset Right = new(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        public static readonly UIAnchorPreset BottomLeft = new(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        public static readonly UIAnchorPreset Bottom = new(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        public static readonly UIAnchorPreset BottomRight = new(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));

        public static readonly UIAnchorPreset StretchAll = new(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        public static readonly UIAnchorPreset StretchTop = new(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        public static readonly UIAnchorPreset StretchBottom = new(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        public static readonly UIAnchorPreset StretchLeft = new(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
        public static readonly UIAnchorPreset StretchRight = new(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));

        public void Apply(UIComponent component)
        {
            component.AnchorMin = Min;
            component.AnchorMax = Max;
            component.Pivot = Pivot;
        }

        public bool IsStretch => !Min.Equals(Max);
    }

    /// <summary>
    /// 布局辅助：一行代码完成"锚点 + 位置 + 尺寸"，让 UI 构建代码接近声明式。
    /// </summary>
    public static class UILayout
    {
        /// <summary>点锚点：把控件放在锚点 + (x, y) 处，并给定尺寸。</summary>
        public static T Place<T>(T component, UIAnchorPreset preset, float x, float y, float width, float height)
            where T : UIComponent
        {
            preset.Apply(component);
            component.Position = new Vector2(x, y);
            component.Size = new Vector2(width, height);
            return component;
        }

        /// <summary>拉伸锚点：四边相对锚点区域的边距。</summary>
        public static T Stretch<T>(T component, UIAnchorPreset preset, float left, float top, float right, float bottom)
            where T : UIComponent
        {
            preset.Apply(component);
            component.OffsetMin = new Vector2(left, top);
            component.OffsetMax = new Vector2(-right, -bottom);
            return component;
        }

        /// <summary>仅设置位置（保持锚点与尺寸）。</summary>
        public static T At<T>(T component, float x, float y) where T : UIComponent
        {
            component.Position = new Vector2(x, y);
            return component;
        }

        /// <summary>仅设置尺寸。</summary>
        public static T Sized<T>(T component, float width, float height) where T : UIComponent
        {
            component.Size = new Vector2(width, height);
            return component;
        }
    }

    /// <summary>
    /// UI 全局上下文：当前主题。控件在绘制时读取，便于热替换主题/做离线校验。
    /// </summary>
    public static class UIContext
    {
        public static UITheme? Theme { get; set; }

        public static UISprite White
        {
            get
            {
                var theme = Theme;
                return theme?.Sprite(UISkin.White) ?? UISprite.Empty;
            }
        }

        public static UIFont? DefaultFont => Theme?.Font;
    }
}
