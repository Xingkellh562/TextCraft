using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// 所有 UI 控件的基类：层级树、锚点布局、绘制采集、指针事件。
    ///
    /// 与旧版 <c>UIComponent</c> 的关键差异：
    /// <list type="bullet">
    /// <item>布局改为 Unity 风格锚点（AnchorMin/AnchorMax/Pivot/Position/Size），每帧重新解算，
    /// 不再把绝对坐标写回 <c>rect</c>（旧实现重复调用会累加偏移）；</item>
    /// <item>控件不再自己持有 VAO/VBO，而是把四边形写进全局绘制列表，由渲染器统一合批；</item>
    /// <item>新增层级（<see cref="Layer"/>）、裁剪（<see cref="ClipChildren"/>）、顶点色（<see cref="Color"/>）；</item>
    /// <item>新增指针事件回调，供按钮/滑块等交互控件使用。</item>
    /// </list>
    /// </summary>
    public abstract class UIComponent
    {
        private readonly List<UIComponent> _children = new();

        // ---------------- 标识与层级树 ----------------

        public string Name { get; set; } = "";

        public UIComponent? Parent { get; private set; }

        /// <summary>子控件（只读视图，顺序即绘制顺序）。</summary>
        public IReadOnlyList<UIComponent> Children => _children;

        /// <summary>兼容旧代码：旧版字段名。</summary>
        public IReadOnlyList<UIComponent> subObjects => _children;

        /// <summary>兼容旧代码：旧版字段名。</summary>
        public UIComponent? parent => Parent;

        public int ChildCount => _children.Count;

        public T? Find<T>(string name) where T : UIComponent
        {
            foreach (var child in _children)
            {
                if (child is T typed && string.Equals(typed.Name, name, StringComparison.OrdinalIgnoreCase))
                    return typed;
                var nested = child.Find<T>(name);
                if (nested != null) return nested;
            }
            return null;
        }

        public void Add(UIComponent child) => AddSubComponent(child);

        public void AddSubComponent(UIComponent child)
        {
            if (child == null || child == this) return;
            child.Parent = this;
            _children.Add(child);
        }

        public void Insert(int index, UIComponent child)
        {
            if (child == null || child == this) return;
            child.Parent = this;
            _children.Insert(Math.Clamp(index, 0, _children.Count), child);
        }

        public bool Remove(UIComponent child)
        {
            if (!_children.Remove(child)) return false;
            child.Parent = null;
            return true;
        }

        public void RemoveFromParent() => Parent?.Remove(this);

        public void ClearChildren()
        {
            foreach (var child in _children) child.Parent = null;
            _children.Clear();
        }

        /// <summary>把自身移到同级最后（最后绘制，最上层）。</summary>
        public void BringToFront()
        {
            if (Parent == null) return;
            Parent._children.Remove(this);
            Parent._children.Add(this);
        }

        /// <summary>深度优先遍历（先父后子），兼容旧接口。</summary>
        public List<UIComponent> Traverse(bool allComponents = true)
        {
            var result = new List<UIComponent>();
            TraverseInto(result, allComponents);
            return result;
        }

        public void Traverse(List<UIComponent> result, bool allComponents = false)
        {
            result.Clear();
            TraverseInto(result, allComponents);
        }

        private void TraverseInto(List<UIComponent> result, bool allComponents)
        {
            if (!ActiveSelf && !allComponents) return;
            result.Add(this);
            for (int i = 0; i < _children.Count; i++)
                _children[i].TraverseInto(result, allComponents);
        }

        // ---------------- 可见性 ----------------

        public bool ActiveSelf { get; private set; } = true;

        /// <summary>兼容旧接口。</summary>
        public bool Awake => ActiveSelf;

        public bool ActiveInHierarchy => ActiveSelf && (Parent?.ActiveInHierarchy ?? true);

        public void SetActive(bool active) => ActiveSelf = active;

        public void Wake() => SetActive(true);

        public void Sleep() => SetActive(false);

        // ---------------- 层级 ----------------

        private int? _layerOverride;

        /// <summary>绘制层级，见 <see cref="UILayers"/>。未显式设置时继承父控件。</summary>
        public int Layer
        {
            get => _layerOverride ?? Parent?.Layer ?? UILayers.Default;
            set => _layerOverride = value;
        }

        public void ClearLayerOverride() => _layerOverride = null;

        public bool HasLayerOverride => _layerOverride.HasValue;

        // ---------------- 渲染状态 ----------------

        /// <summary>自身绘制颜色乘算（含透明度），不向下继承。</summary>
        public Vector4 Color { get; set; } = Vector4.One;

        /// <summary>
        /// 整组颜色乘算：同时作用于自身与所有子控件（类似 Unity 的 CanvasGroup）。
        /// 与 <see cref="Color"/> 分开，避免"给容器设置背景色"时把子控件一起染黑。
        /// </summary>
        public Vector4 GroupTint { get; set; } = Vector4.One;

        /// <summary>是否裁剪超出自身矩形的子控件。</summary>
        public bool ClipChildren { get; set; }

        /// <summary>是否吞掉指针事件（例如全屏遮罩）。</summary>
        public bool BlocksPointer { get; set; }

        /// <summary>是否可接收指针事件（按钮、滑块等重写）。</summary>
        public virtual bool IsInteractable => false;


        /// <summary>悬停在本控件上时的指针形状。</summary>
        public UICursorShape Cursor { get; set; } = UICursorShape.Default;

        // ---------------- 布局 ----------------

        /// <summary>锚点左下角（父矩形内的归一化坐标，0~1）。</summary>
        public Vector2 AnchorMin { get; set; } = Vector2.Zero;

        /// <summary>锚点右上角（父矩形内的归一化坐标，0~1）。与 AnchorMin 相等时为"点锚点"。</summary>
        public Vector2 AnchorMax { get; set; } = Vector2.Zero;

        /// <summary>自身轴心（0~1），决定 <see cref="Position"/> 参考的是哪个点。</summary>
        public Vector2 Pivot { get; set; } = Vector2.Zero;

        /// <summary>点锚点下相对锚点的像素偏移。</summary>
        public Vector2 Position { get; set; } = Vector2.Zero;

        /// <summary>点锚点下的尺寸（像素）。</summary>
        public Vector2 Size { get; set; } = new Vector2(160f, 32f);

        /// <summary>拉伸锚点下相对锚点区域左上角的偏移。</summary>
        public Vector2 OffsetMin { get; set; } = Vector2.Zero;

        /// <summary>拉伸锚点下相对锚点区域右下角的偏移。</summary>
        public Vector2 OffsetMax { get; set; } = Vector2.Zero;

        /// <summary>本帧解算出的屏幕矩形（像素，左上角原点）。</summary>
        public UIRect ScreenRect { get; private set; }

        /// <summary>兼容旧代码：旧版字段名。</summary>
        public UIRect Rect => ScreenRect;

        /// <summary>是否有横向/纵向拉伸锚点。</summary>
        public bool StretchHorizontal => MathF.Abs(AnchorMax.X - AnchorMin.X) > 1e-4f;
        public bool StretchVertical => MathF.Abs(AnchorMax.Y - AnchorMin.Y) > 1e-4f;

        /// <summary>解算整棵子树（每帧调用，开销与控件数线性相关）。</summary>
        public void Layout(in UIRect parentRect)
        {
            if (!ActiveSelf) return;

            OnBeforeLayout();

            ScreenRect = Parent == null
                ? new UIRect(0f, 0f, MathF.Max(0f, Size.X), MathF.Max(0f, Size.Y))
                : ResolveRect(parentRect);

            OnLayout();

            for (int i = 0; i < _children.Count; i++)
                _children[i].Layout(ScreenRect);
        }

        /// <summary>布局前回调：需要依据内容调整尺寸的控件（如自动宽度的文本）在这里改 <see cref="Size"/>。</summary>
        protected virtual void OnBeforeLayout()
        {
        }

        /// <summary>布局后回调：可在此根据最终矩形重新排版内容（如文本换行缓存）。</summary>
        protected virtual void OnLayout()
        {
        }

        protected UIRect ResolveRect(in UIRect parentRect)
        {
            var anchorRefMin = new Vector2(
                parentRect.X + parentRect.Width * AnchorMin.X,
                parentRect.Y + parentRect.Height * AnchorMin.Y);
            var anchorRefMax = new Vector2(
                parentRect.X + parentRect.Width * AnchorMax.X,
                parentRect.Y + parentRect.Height * AnchorMax.Y);

            float minX, maxX, minY, maxY;

            if (StretchHorizontal)
            {
                minX = anchorRefMin.X + OffsetMin.X;
                maxX = anchorRefMax.X + OffsetMax.X;
            }
            else
            {
                float width = Size.X;
                float left = anchorRefMin.X + Position.X - Pivot.X * width;
                minX = left;
                maxX = left + width;
            }

            if (StretchVertical)
            {
                minY = anchorRefMin.Y + OffsetMin.Y;
                maxY = anchorRefMax.Y + OffsetMax.Y;
            }
            else
            {
                float height = Size.Y;
                float top = anchorRefMin.Y + Position.Y - Pivot.Y * height;
                minY = top;
                maxY = top + height;
            }

            return UIRect.FromMinMax(new Vector2(minX, minY), new Vector2(maxX, maxY));
        }

        // ---------------- 绘制采集 ----------------

        /// <summary>
        /// 把本控件与子控件的几何写进绘制列表。
        /// </summary>
        /// <param name="clip">父级传下来的裁剪矩形（<see cref="UIRect.Infinite"/> 表示不裁剪）。</param>
        /// <param name="tint">父级传下来的颜色乘算值（不包含自身 <see cref="Color"/>）。</param>
        public void CollectDraw(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
            if (!ActiveSelf) return;
            list.CountComponent();

            bool selfVisible = !ScreenRect.IsEmpty && (clip.IsInfinite || ScreenRect.Intersects(clip));
            if (selfVisible)
                OnCollect(list, clip, tint * Color);

            if (_children.Count == 0) return;

            var childClip = ClipChildren ? UIRect.Intersect(clip, ScreenRect) : clip;
            if (childClip.IsEmpty) return;

            var childTint = tint * GroupTint;
            for (int i = 0; i < _children.Count; i++)
                _children[i].CollectDraw(list, childClip, childTint);
        }

        /// <summary>子类在这里绘制自身内容（不包含子控件）。</summary>
        protected virtual void OnCollect(UIDrawList list, in UIRect clip, in Vector4 tint)
        {
        }

        /// <summary>绘制一个精灵（自动处理九宫格与裁剪键）。</summary>
        protected void DrawSprite(UIDrawList list, UISprite sprite, in UIRect rect, in UIRect clip, in Vector4 tint)
        {
            if (!sprite.IsValid || sprite.Texture == null || rect.IsEmpty) return;

            var key = new UIBatchKey(sprite.Texture, UIMaterial.Rgba, clip);
            if (sprite.IsNineSlice)
            {
                list.AddNineSlice(Layer, key, rect, sprite.UVRect, sprite.Width, sprite.Height,
                    sprite.BorderLeft, sprite.BorderTop, sprite.BorderRight, sprite.BorderBottom, tint);
            }
            else
            {
                list.AddQuad(Layer, key, rect, sprite.UVMin, sprite.UVMax, tint);
            }
        }

        /// <summary>绘制纯色矩形（需要主题里有 <c>white</c> 精灵）。</summary>
        protected void DrawSolid(UIDrawList list, UISprite white, in UIRect rect, in UIRect clip, in Vector4 tint)
        {
            if (!white.IsValid || white.Texture == null || rect.IsEmpty) return;
            var key = new UIBatchKey(white.Texture, UIMaterial.Rgba, clip);
            list.AddQuad(Layer, key, rect, white.UVMin, white.UVMax, tint);
        }

        // ---------------- 指针事件 ----------------

        public virtual void OnPointerEnter()
        {
        }

        public virtual void OnPointerExit()
        {
        }

        public virtual void OnPointerDown(Vector2 point)
        {
        }

        public virtual void OnPointerUp(Vector2 point, bool inside)
        {
        }

        public virtual void OnClick()
        {
        }

        public virtual void OnPointerMove(Vector2 point)
        {
        }

        public virtual void OnPointerDrag(Vector2 delta)
        {
        }

        /// <summary>控件树的根（最顶层控件）。</summary>
        public UIComponent Root => Parent == null ? this : Parent.Root;

        /// <summary>请求立即重新布局（例如窗口刚被显示，需要马上参与命中检测）。</summary>
        public void RequestLayout()
        {
            var root = this;
            while (root.Parent != null) root = root.Parent;
            if (root.ActiveSelf) root.Layout(default);
        }

        /// <summary>控件是否在指定点上（已考虑自身矩形；裁剪由遍历方处理）。</summary>
        public bool ContainsPoint(Vector2 point) => ScreenRect.Contains(point);

        // ---------------- 每帧更新 ----------------

        /// <summary>递归更新（悬停动画、进度条插值等）。</summary>
        public void Tick(float deltaTime)
        {
            if (!ActiveSelf) return;
            OnUpdate(deltaTime);
            for (int i = 0; i < _children.Count; i++)
                _children[i].Tick(deltaTime);
        }

        protected virtual void OnUpdate(float deltaTime)
        {
        }

        public override string ToString() => $"{GetType().Name}({Name}) {ScreenRect}";
    }
}
