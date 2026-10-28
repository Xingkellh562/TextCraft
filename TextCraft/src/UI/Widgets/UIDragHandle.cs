using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 可拖动区域：按住后移动 <see cref="Target"/>（通常是窗口）。
    /// 拖动不会越出父控件，因此窗口标题栏不会把窗口拖出屏幕。
    /// </summary>
    public class UIDragHandle : UIComponent
    {
        private bool _dragging;
        private Vector2 _lastPoint;

        public UIDragHandle()
        {
            Name = "dragHandle";
            Size = new Vector2(200f, 34f);
        }

        /// <summary>被拖动的控件。</summary>
        public UIComponent? Target { get; set; }

        public UISprite? Sprite { get; set; }

        public string? SpriteKey { get; set; }

        public override bool IsInteractable => Target != null;

        public override void OnPointerDown(Vector2 point)
        {
            if (Target == null) return;
            _dragging = true;
            _lastPoint = point;
            Target.BringToFront();
        }

        public override void OnPointerMove(Vector2 point)
        {
            if (!_dragging || Target == null) return;

            var delta = point - _lastPoint;
            _lastPoint = point;

            var rect = Target.ScreenRect;
            var parent = Target.Parent?.ScreenRect ?? default;
            if (!parent.IsEmpty && parent.Width > 0f && parent.Height > 0f)
            {
                float maxX = MathF.Max(parent.X, parent.Right - rect.Width);
                float maxY = MathF.Max(parent.Y, parent.Bottom - rect.Height);
                float x = MathHelper.Clamp(rect.X + delta.X, parent.X, maxX);
                float y = MathHelper.Clamp(rect.Y + delta.Y, parent.Y, maxY);
                delta = new Vector2(x - rect.X, y - rect.Y);
            }

            Target.Position += delta;
        }

        public override void OnPointerUp(Vector2 point, bool inside) => _dragging = false;

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var sprite = Sprite;
            if (sprite == null && SpriteKey != null)
                sprite = UIContext.Theme?.Sprite(SpriteKey);
            if (sprite is { IsValid: true })
                DrawSprite(list, sprite, ScreenRect, clip, tint);
        }
    }
}
