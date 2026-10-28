using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 快捷栏：一排方块图标 + 选中高亮 + 序号。
    ///
    /// 这个控件刻意"自己画所有格子"：10 个格子 + 10 个图标 + 10 个序号全部写进同一个
    /// 绘制列表，在合批模式下只产生 2~3 个 DrawCall（底板/图标/文字）。
    /// </summary>
    public class UIHotbar : UIComponent
    {
        public UIHotbar()
        {
            Name = "hotbar";
            Layer = UILayers.Hud;
        }

        public int SlotCount { get; set; } = 10;

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => _selectedIndex = Math.Clamp(value, 0, Math.Max(0, SlotCount - 1));
        }

        public float SlotSize { get; set; } = 46f;

        public float SlotGap { get; set; } = 4f;

        public float Padding { get; set; } = 6f;

        /// <summary>索引 → 图标精灵。</summary>
        public Func<int, UISprite?>? IconProvider { get; set; }

        /// <summary>是否绘制 1..N 序号。</summary>
        public bool ShowIndex { get; set; } = true;

        public override bool IsInteractable => true;

        /// <summary>点击格子时触发（返回索引）。</summary>
        public event Action<int>? SlotSelected;

        private Vector2 _lastPointerDown;

        public override void OnPointerDown(Vector2 point) => _lastPointerDown = point;

        public override void OnClick()
        {
            int index = IndexAt(_lastPointerDown);
            if (index < 0) return;
            SelectedIndex = index;
            SlotSelected?.Invoke(index);
        }

        private int IndexAt(Vector2 point)
        {
            if (!ScreenRect.Contains(point)) return -1;
            float startX = ScreenRect.X + Padding;
            float startY = ScreenRect.Y + Padding;
            if (point.Y < startY || point.Y > startY + SlotSize) return -1;

            for (int i = 0; i < SlotCount; i++)
            {
                float x = startX + i * (SlotSize + SlotGap);
                if (point.X >= x && point.X <= x + SlotSize) return i;
            }
            return -1;
        }

        protected override void OnBeforeLayout()
        {
            float width = Padding * 2f + SlotCount * SlotSize + MathF.Max(0, SlotCount - 1) * SlotGap;
            float height = Padding * 2f + SlotSize;
            Size = new Vector2(width, height);
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            // 底板
            var panel = theme.Sprite(UISkin.Panel);
            if (panel.IsValid)
                DrawSprite(list, panel, ScreenRect, clip, tint * new Vector4(1f, 1f, 1f, 0.85f));

            var slotSprite = theme.Sprite(UISkin.Slot);
            var selectedSprite = theme.Sprite(UISkin.SlotSelected);
            var font = theme.FontSmall;
            var textColor = tint * theme.Color(UISkin.ColorTextDim);

            float startX = ScreenRect.X + Padding;
            float startY = ScreenRect.Y + Padding;

            for (int i = 0; i < SlotCount; i++)
            {
                float x = startX + i * (SlotSize + SlotGap);
                var slotRect = new UIRect(x, startY, SlotSize, SlotSize);

                if (slotSprite.IsValid)
                    DrawSprite(list, slotSprite, slotRect, clip, tint);

                if (i == SelectedIndex && selectedSprite.IsValid)
                    DrawSprite(list, selectedSprite, slotRect.Inflate(2f), clip, tint);

                var icon = IconProvider?.Invoke(i);
                if (icon is { IsValid: true })
                {
                    float inset = MathF.Max(4f, SlotSize * 0.16f);
                    DrawSprite(list, icon, slotRect.Inset(inset, inset, inset, inset), clip, tint);
                }

                if (ShowIndex && font != null)
                {
                    string label = (i + 1).ToString();
                    var key = new UIBatchKey(font.Texture, UIMaterial.AlphaMask, clip);
                    float penX = x + 3f;
                    float penY = startY + 1f;
                    foreach (char c in label)
                    {
                        if (!font.TryGetGlyph(c, out var glyph) || !glyph.HasInk) continue;
                        var dest = UIRect.FromMinMax(
                            new Vector2(penX + glyph.OffsetX, penY + glyph.OffsetY),
                            new Vector2(penX + glyph.OffsetX + glyph.Width, penY + glyph.OffsetY + glyph.Height));
                        list.AddQuad(Layer, key, dest,
                            new Vector2(glyph.U0, glyph.V0), new Vector2(glyph.U1, glyph.V1),
                            textColor);
                        penX += glyph.Advance;
                    }
                }
            }
        }
    }
}
