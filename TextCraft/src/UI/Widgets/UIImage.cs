using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 精灵图片控件。支持九宫格（自动识别精灵的 border）、等比缩放与染色。
    /// </summary>
    public class UIImage : UIComponent
    {
        public UIImage()
        {
        }

        public UIImage(UISprite sprite)
        {
            Sprite = sprite;
        }

        public UISprite? Sprite { get; set; }

        /// <summary>保持原始宽高比（在控件矩形内做 letterbox）。</summary>
        public bool PreserveAspect { get; set; }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var sprite = Sprite;
            if (sprite == null || !sprite.IsValid) return;

            var rect = ScreenRect;
            if (PreserveAspect && sprite.Width > 0 && sprite.Height > 0 && !rect.IsEmpty)
            {
                float spriteAspect = sprite.Width / (float)sprite.Height;
                float rectAspect = rect.Width / rect.Height;
                if (rectAspect > spriteAspect)
                {
                    float w = rect.Height * spriteAspect;
                    rect = new UIRect(rect.X + (rect.Width - w) * 0.5f, rect.Y, w, rect.Height);
                }
                else
                {
                    float h = rect.Width / spriteAspect;
                    rect = new UIRect(rect.X, rect.Y + (rect.Height - h) * 0.5f, rect.Width, h);
                }
            }

            DrawSprite(list, sprite, rect, clip, tint);
        }
    }
}
