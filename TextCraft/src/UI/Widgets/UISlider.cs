using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 滑块：点击/拖动改变 0~1 的值，用于设置项（视野距离等）。
    /// 拖动过程中由 <see cref="UIInput"/> 把指针"捕获"到本控件，指针移出也不会中断拖动。
    /// </summary>
    public class UISlider : UIComponent
    {
        private float _value = 0.5f;
        private bool _dragging;
        private bool _hover;

        public UISlider()
        {
            Name = "slider";
            Size = new Vector2(260f, 24f);
        }

        public float Value
        {
            get => _value;
            set
            {
                float clamped = MathHelper.Clamp(value, 0f, 1f);
                if (MathF.Abs(clamped - _value) < 1e-5f) return;
                _value = clamped;
                ValueChanged?.Invoke(_value);
            }
        }

        public bool Interactable { get; set; } = true;

        /// <summary>手柄直径（像素）。</summary>
        public float HandleSize { get; set; } = 18f;

        public event Action<float>? ValueChanged;

        public override bool IsInteractable => Interactable;

        public override void OnPointerEnter() => _hover = true;

        public override void OnPointerExit() => _hover = false;

        public override void OnPointerDown(Vector2 point)
        {
            if (!Interactable) return;
            _dragging = true;
            UpdateFromPoint(point);
        }

        public override void OnPointerMove(Vector2 point)
        {
            if (_dragging) UpdateFromPoint(point);
        }

        public override void OnPointerUp(Vector2 point, bool inside)
        {
            if (_dragging && inside) UpdateFromPoint(point);
            _dragging = false;
        }

        private void UpdateFromPoint(Vector2 point)
        {
            float left = ScreenRect.X + HandleSize * 0.5f;
            float right = ScreenRect.Right - HandleSize * 0.5f;
            float span = MathF.Max(1f, right - left);
            Value = (point.X - left) / span;
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            float centerY = ScreenRect.Center.Y;
            float trackHeight = MathF.Min(10f, ScreenRect.Height);

            var track = theme.Sprite(UISkin.SliderTrack);
            var trackRect = new UIRect(ScreenRect.X, centerY - trackHeight * 0.5f, ScreenRect.Width, trackHeight);
            DrawSprite(list, track.IsValid ? track : theme.Sprite(UISkin.ProgressBg), trackRect, clip, tint);

            float half = HandleSize * 0.5f;
            float usable = MathF.Max(1f, ScreenRect.Width - HandleSize);
            float handleX = ScreenRect.X + half + usable * _value;

            var fill = theme.Sprite(UISkin.SliderFill);
            var fillRect = new UIRect(ScreenRect.X, trackRect.Y, MathF.Max(0f, handleX - ScreenRect.X), trackHeight);
            if (fill.IsValid)
                DrawSprite(list, fill, fillRect, clip, tint);
            else
                DrawSolid(list, theme.Sprite(UISkin.White), fillRect, clip, theme.Color(UISkin.ColorAccent));

            var handle = theme.Sprite(UISkin.SliderHandle);
            var handleRect = new UIRect(handleX - half, centerY - half, HandleSize, HandleSize);
            if (handle.IsValid)
            {
                var handleTint = _hover || _dragging ? tint : tint * 0.92f;
                DrawSprite(list, handle, handleRect, clip, handleTint);
            }
            else
            {
                DrawSolid(list, theme.Sprite(UISkin.White), handleRect, clip, theme.Color(UISkin.ColorText));
            }
        }
    }
}
