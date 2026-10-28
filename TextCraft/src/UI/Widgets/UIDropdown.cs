using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 下拉选择框。
    ///
    /// 展开的候选列表是本控件的子控件，但 `Layer` 被提升到 <see cref="UILayers.Popup"/>，
    /// 因此永远画在普通面板/窗口之上；它随下拉框一起布局，不需要把控件搬到别处。
    /// 点击候选之外的地方由 <see cref="NotifyPointerPressed"/> 统一收起。
    /// </summary>
    public class UIDropdown : UIComponent
    {
        private static readonly List<UIDropdown> OpenDropdowns = new();

        private readonly UILabel _label;
        private readonly UIImage _arrow;
        private readonly UIPanel _popup;
        private readonly List<UIDropdownItem> _rows = new();
        private readonly List<string> _items = new();

        private int _selectedIndex = -1;
        private float _hoverAmount;
        private float _hoverTarget;

        public UIDropdown()
        {
            Name = "dropdown";
            Size = new Vector2(300f, 36f);
            Cursor = UICursorShape.Hand;

            _label = new UILabel
            {
                Name = "label",
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Stretch(_label, UIAnchorPreset.StretchAll, 10f, 0f, 30f, 0f);
            Add(_label);

            _arrow = new UIImage { Name = "arrow" };
            UILayout.Place(_arrow, UIAnchorPreset.Right, -10f, 0f, 14f, 14f);
            Add(_arrow);

            _popup = new UIPanel
            {
                Name = "popup",
                Layer = UILayers.Popup,
                BlocksPointer = true,
                SpriteKey = UISkin.Window,   // 需要不透明，避免透出下层文字
            };
            UILayout.Place(_popup, UIAnchorPreset.TopLeft, 0f, 0f, 300f, 0f);
            _popup.SetActive(false);
            Add(_popup);
        }

        /// <summary>候选行高度。</summary>
        public float RowHeight { get; set; } = 28f;

        /// <summary>候选列表最多同时显示多少行（存档按最近优先排序，超出部分不列出）。</summary>
        public int MaxVisibleRows { get; set; } = 10;

        public string Placeholder { get; set; } = "请选择";

        /// <summary>下拉框与候选行的字体；为空时使用主题默认字体。</summary>
        public UIFont? Font
        {
            get => _label.Font;
            set
            {
                _label.Font = value;
                foreach (var row in _rows) row.SetFont(value);
            }
        }

        public IReadOnlyList<string> Items => _items;

        public bool IsOpen => _popup.ActiveSelf;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                int clamped = _items.Count == 0 ? -1 : Math.Clamp(value, 0, _items.Count - 1);
                if (clamped == _selectedIndex) return;
                _selectedIndex = clamped;
                _label.Text = SelectedItem ?? Placeholder;
                SelectionChanged?.Invoke(clamped);
            }
        }

        public string? SelectedItem
            => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        /// <summary>候选列表里实际显示的行数。</summary>
        public int VisibleRowCount => _rows.Count;

        public event Action<int>? SelectionChanged;

        /// <summary>替换候选列表。</summary>
        public void SetItems(IEnumerable<string> items, int selected = 0)
        {
            Close();
            _items.Clear();
            _items.AddRange(items);
            RebuildRows();

            _selectedIndex = -1;
            SelectedIndex = _items.Count == 0 ? -1 : Math.Clamp(selected, 0, _items.Count - 1);
            _label.Text = SelectedItem ?? Placeholder;
        }

        private void RebuildRows()
        {
            foreach (var row in _rows) _popup.Remove(row);
            _rows.Clear();

            int count = Math.Min(_items.Count, MaxVisibleRows);
            for (int i = 0; i < count; i++)
            {
                var row = new UIDropdownItem(this, i) { Name = "row" + i };
                row.SetFont(Font);
                row.SetText(_items[i]);
                UILayout.Place(row, UIAnchorPreset.TopLeft, 4f, 4f + i * RowHeight, 0f, RowHeight - 2f);
                _popup.Add(row);
                _rows.Add(row);
            }
        }

        public override void OnPointerEnter() => _hoverTarget = 1f;

        public override void OnPointerExit() => _hoverTarget = 0f;

        public override void OnPointerDown(Vector2 point)
        {
            if (IsOpen) Close();
            else Open();
        }

        public override void OnClick()
        {
            // 点击处理在 OnPointerDown 里完成（展开/收起），这里刻意留空
        }

        public void Open()
        {
            if (_items.Count == 0) return;
            if (_rows.Count == 0) RebuildRows();

            foreach (var other in OpenDropdowns.ToArray())
            {
                if (!ReferenceEquals(other, this)) other.Close();
            }

            _popup.SetActive(true);
            _popup.BringToFront();
            if (!OpenDropdowns.Contains(this)) OpenDropdowns.Add(this);

            // 立刻重算一次布局，避免第一帧候选列表还在旧位置
            RequestLayout();
        }

        public void Close()
        {
            if (!_popup.ActiveSelf) return;
            _popup.SetActive(false);
            OpenDropdowns.Remove(this);
        }

        /// <summary>关闭所有展开的下拉框，返回是否有被关闭的。</summary>
        public static bool CloseAllOpen()
        {
            if (OpenDropdowns.Count == 0) return false;
            var copy = OpenDropdowns.ToArray();
            OpenDropdowns.Clear();
            foreach (var dropdown in copy) dropdown._popup.SetActive(false);
            return true;
        }

        /// <summary>指针按下时通知所有展开的下拉框：点到别处就收起。</summary>
        internal static void NotifyPointerPressed(UIComponent? target)
        {
            if (OpenDropdowns.Count == 0) return;

            for (int i = OpenDropdowns.Count - 1; i >= 0; i--)
            {
                var dropdown = OpenDropdowns[i];
                if (IsSelfOrDescendant(dropdown, target)) continue;
                dropdown.Close();
            }
        }

        private static bool IsSelfOrDescendant(UIComponent ancestor, UIComponent? node)
        {
            while (node != null)
            {
                if (ReferenceEquals(node, ancestor)) return true;
                node = node.Parent;
            }
            return false;
        }

        internal void SelectFromRow(int index)
        {
            SelectedIndex = index;
            Close();
        }

        // ---------------- 布局 / 更新 / 绘制 ----------------

        protected override void OnBeforeLayout()
        {
            int count = _rows.Count;
            _popup.Size = new Vector2(Size.X, 8f + count * RowHeight);
            for (int i = 0; i < count; i++)
            {
                _rows[i].Size = new Vector2(MathF.Max(1f, Size.X - 8f), RowHeight - 2f);
                _rows[i].Position = new Vector2(4f, 4f + i * RowHeight);
            }
        }

        protected override void OnLayout()
        {
            // 优先向下展开；空间不够则向上翻转，保证列表留在窗口内
            float height = _popup.Size.Y;
            var root = Root;
            float limit = root.ScreenRect.Height > 0f ? root.ScreenRect.Height : 100000f;

            float belowY = ScreenRect.Height + 2f;
            bool overflowBelow = ScreenRect.Bottom + 2f + height > limit;
            float aboveY = -(height + 2f);

            _popup.Position = new Vector2(0f, overflowBelow && ScreenRect.Y + aboveY > 0f ? aboveY : belowY);
        }

        protected override void OnUpdate(float deltaTime)
        {
            _hoverAmount += (_hoverTarget - _hoverAmount) * MathF.Min(1f, deltaTime * 12f);
        }

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            if (_arrow.Sprite == null || !_arrow.Sprite.IsValid)
                _arrow.Sprite = theme.Sprite(UISkin.ArrowDown);

            bool open = IsOpen;
            var background = theme.Sprite(open ? UISkin.ButtonHover : UISkin.Button);
            if (!background.IsValid) background = theme.Sprite(UISkin.Panel);

            DrawSprite(list, background, ScreenRect, clip, Interactable ? tint : tint * 0.55f);

            if (_hoverAmount > 0.01f && !open)
            {
                var hover = theme.Sprite(UISkin.ButtonHover);
                DrawSprite(list, hover, ScreenRect, clip, tint * new Vector4(1f, 1f, 1f, _hoverAmount * 0.75f));
            }
        }

        public bool Interactable { get; set; } = true;

        public override bool IsInteractable => Interactable;
    }

    /// <summary>下拉候选中的一行。</summary>
    internal sealed class UIDropdownItem : UIComponent
    {
        private readonly UIDropdown _owner;
        private readonly int _index;
        private readonly UILabel _label;
        private bool _hover;

        public UIDropdownItem(UIDropdown owner, int index)
        {
            _owner = owner;
            _index = index;
            Cursor = UICursorShape.Hand;

            _label = new UILabel
            {
                Name = "label",
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                Wrap = false,
            };
            UILayout.Stretch(_label, UIAnchorPreset.StretchAll, 8f, 0f, 4f, 0f);
            Add(_label);
        }

        public override bool IsInteractable => true;

        public void SetText(string text) => _label.Text = text;

        public void SetFont(UIFont? font) => _label.Font = font;

        public override void OnPointerEnter() => _hover = true;

        public override void OnPointerExit() => _hover = false;

        public override void OnClick() => _owner.SelectFromRow(_index);

        protected override void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            var theme = UIContext.Theme;
            if (theme == null) return;

            var sprite = theme.Sprite(_hover ? UISkin.ListItemHover : UISkin.ListItem);
            if (sprite.IsValid)
                DrawSprite(list, sprite, ScreenRect, clip, tint);
        }
    }
}
