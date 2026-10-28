using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 基础面板：九宫格精灵或纯色矩形，通常用作其他控件的容器。
    /// </summary>
    public class UIPanel : UIComponent
    {
        public UIPanel()
        {
        }

        public UIPanel(UISprite sprite)
        {
            Sprite = sprite;
        }

        /// <summary>背景精灵；为空时按 <see cref="SpriteKey"/> 从主题解析，再退回纯色矩形。</summary>
        public UISprite? Sprite { get; set; }

        /// <summary>主题精灵键（延迟解析，便于切换主题）。</summary>
        public string? SpriteKey { get; set; }

        /// <summary>
        /// 没有任何精灵时是否退回"纯色矩形"。默认 false：
        /// 容器面板（按钮容器、窗口内容层）不应该凭空画出一块白色。
        /// </summary>
        public bool DrawSolidFallback { get; set; }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var sprite = Sprite;
            if (sprite == null && SpriteKey != null)
                sprite = UIContext.Theme?.Sprite(SpriteKey);

            if (sprite != null && sprite.IsValid)
            {
                DrawSprite(list, sprite, ScreenRect, clip, tint);
                return;
            }

            if (!DrawSolidFallback) return;

            var theme = UIContext.Theme;
            if (theme == null) return;
            DrawSolid(list, theme.Sprite(UISkin.White), ScreenRect, clip, tint);
        }
    }
}
