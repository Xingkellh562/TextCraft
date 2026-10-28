using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 指针 / 键盘输入：命中最上层控件、维护悬停状态、处理按下/抬起/拖动捕获、
    /// 以及文本输入框的焦点与按键分发。
    ///
    /// 命中顺序与绘制顺序一致：先按层级（<see cref="UIComponent.Layer"/>）排序，
    /// 同层级按树中的提交顺序排序，因此"后画的在上"。
    /// 命中时只考虑 <see cref="UIComponent.IsInteractable"/> 或 <see cref="UIComponent.BlocksPointer"/> 的控件。
    /// </summary>
    public sealed class UIInput
    {
        private struct Hit
        {
            public UIComponent? Component;
            public bool Blocks;
            public int Layer;
            public int Sequence;
            public bool Valid;
        }

        private UIComponent? _hover;
        private UIComponent? _captured;
        private UITextField? _focusedText;
        private int _sequence;
        private Vector2 _lastPointer;

        public Vector2 Pointer { get; private set; }

        public bool PointerDown { get; private set; }

        /// <summary>指针当前是否被 UI 覆盖（含全屏遮罩）。</summary>
        public bool PointerOverUI { get; private set; }

        public UIComponent? Hover => _hover;
        public UIComponent? Captured => _captured;

        /// <summary>当前获得键盘焦点的输入框（null 表示没有）。</summary>
        public UITextField? FocusedText => _focusedText;

        public bool HasTextFocus => _focusedText != null;

        /// <summary>当前希望的指针形状。</summary>
        public UICursorShape CursorShape => _hover?.Cursor ?? UICursorShape.Default;

        /// <summary>每帧刷新悬停状态（即使没有移动事件，也能触发按钮的悬停动画）。</summary>
        public void Update(UIComponent root, Vector2 pointer)
        {
            Pointer = pointer;
            var delta = pointer - _lastPointer;
            _lastPointer = pointer;

            var target = FindTarget(root, pointer, out bool blocked);

            PointerOverUI = blocked || target != null;

            if (!ReferenceEquals(target, _hover))
            {
                _hover?.OnPointerExit();
                _hover = target;
                _hover?.OnPointerEnter();
            }

            if (_captured != null)
            {
                _captured.OnPointerMove(pointer);
                if (delta != Vector2.Zero) _captured.OnPointerDrag(delta);
            }
            else
            {
                _hover?.OnPointerMove(pointer);
            }
        }

        /// <summary>指针移动。返回 UI 是否消费了这次移动。</summary>
        public bool PointerMove(UIComponent root, Vector2 pointer)
        {
            Update(root, pointer);
            return _captured != null || _hover != null;
        }

        /// <summary>按下。返回 UI 是否消费了这次按下（用于屏蔽游戏内操作）。</summary>
        public bool PointerPress(UIComponent root, Vector2 pointer)
        {
            Update(root, pointer);

            // 点到下拉框/行之外的地方时先收起候选列表
            UIDropdown.NotifyPointerPressed(_hover);

            if (_hover != null)
            {
                _captured = _hover;
                PointerDown = true;

                // 点击输入框获得焦点，点到别处则失焦
                SetTextFocus(_hover as UITextField);

                _hover.OnPointerDown(pointer);
                return true;
            }

            SetTextFocus(null);
            return PointerOverUI;
        }

        /// <summary>抬起。返回 UI 是否消费了这次抬起。</summary>
        public bool PointerRelease(UIComponent root, Vector2 pointer)
        {
            var target = _captured;
            _captured = null;
            PointerDown = false;

            Update(root, pointer);

            if (target == null) return PointerOverUI;

            bool inside = target.ActiveInHierarchy && target.ContainsPoint(pointer);
            target.OnPointerUp(pointer, inside);
            if (inside) target.OnClick();
            return true;
        }

        // ---------------- 文本焦点 / 键盘 ----------------

        /// <summary>设置/清除文本焦点。</summary>
        public void SetTextFocus(UITextField? field)
        {
            if (ReferenceEquals(_focusedText, field)) return;
            _focusedText?.Blur();
            _focusedText = field;
            field?.Focus();
        }

        /// <summary>把按键交给当前输入框；返回是否消费。</summary>
        public bool HandleKeyDown(Keys key)
        {
            if (_focusedText == null) return false;

            if (key == Keys.Escape)
            {
                SetTextFocus(null);
                return true;
            }

            return _focusedText.HandleKey(key);
        }

        /// <summary>把输入的字符交给当前输入框；返回是否消费。</summary>
        public bool HandleTextInput(int unicode)
        {
            return _focusedText != null && _focusedText.HandleChar(unicode);
        }

        /// <summary>清除所有交互状态（例如光标被窗口捕获时）。</summary>
        public void Reset()
        {
            if (_hover != null)
            {
                _hover.OnPointerExit();
                _hover = null;
            }
            SetTextFocus(null);
            UIDropdown.CloseAllOpen();
            _captured = null;
            PointerDown = false;
            PointerOverUI = false;
        }

        private UIComponent? FindTarget(UIComponent root, Vector2 point, out bool blocked)
        {
            _sequence = 0;
            var best = default(Hit);
            Visit(root, UIRect.Infinite, point, ref best);

            blocked = best.Valid && best.Blocks;
            return best.Valid && !best.Blocks ? best.Component : null;
        }

        private void Visit(UIComponent component, in UIRect clip, Vector2 point, ref Hit best)
        {
            if (!component.ActiveSelf) return;

            int sequence = _sequence++;
            bool inside = !component.ScreenRect.IsEmpty
                && (clip.IsInfinite || component.ScreenRect.Intersects(clip))
                && component.ScreenRect.Contains(point);

            if (inside && (component.IsInteractable || component.BlocksPointer))
            {
                int layer = component.Layer;
                if (!best.Valid || layer > best.Layer || (layer == best.Layer && sequence > best.Sequence))
                {
                    best = new Hit
                    {
                        Component = component,
                        Blocks = component.BlocksPointer,
                        Layer = layer,
                        Sequence = sequence,
                        Valid = true,
                    };
                }
            }

            if (component.ChildCount == 0) return;

            var childClip = component.ClipChildren ? UIRect.Intersect(clip, component.ScreenRect) : clip;
            if (childClip.IsEmpty) return;

            var children = component.Children;
            for (int i = 0; i < children.Count; i++)
                Visit(children[i], childClip, point, ref best);
        }
    }
}
