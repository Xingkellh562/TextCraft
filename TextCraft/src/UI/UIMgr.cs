using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>UI 根节点：尺寸等于窗口尺寸，其余控件都挂在它下面。</summary>
    public sealed class UIRoot : UIComponent
    {
        public UIRoot()
        {
            Name = "root";
        }
    }

    /// <summary>
    /// UI 管理器：持有控件树、输入、渲染器与统计信息。
    /// 生命周期：构造 → <see cref="Load"/>（需要 GL 上下文）→ 每帧 Update/Render。
    /// </summary>
    internal sealed class UIMgr
    {
        private Vector2 _lastPointer;

        public UIRoot Root { get; } = new UIRoot();

        public UITable uITable { get; }

        public IRenderer? UiRender { get; }

        public UIInput Input { get; } = new UIInput();

        public Vector2i ScreenSize { get; private set; }

        public UIRenderStats Stats => (UiRender as UIRenderer)?.Batch.Stats ?? default;

        public bool IsLoaded { get; private set; }

        public UIMgr()
        {
            uITable = new UITable(this);
            UiRender = new UIRenderer(this);
        }

        public void Load()
        {
            UIAssets.Ins.UploadGpu();
            UIContext.Theme = UIAssets.Ins.Theme;

            UiRender?.Load();

            Root.ClearChildren();
            uITable.LoadUI(Root);
            Root.Layout(default);
            IsLoaded = true;
        }

        public void Render()
        {
            if (!IsLoaded) return;
            Root.Layout(default);
            UiRender?.Draw();
            uITable.AfterRender(Stats);
        }

        public void Update(float updateTime)
        {
            if (!IsLoaded) return;
            Root.Tick(updateTime);
            Input.Update(Root, _lastPointer);
            uITable.OnUpdate(updateTime);
        }

        public void UnLoad()
        {
            Input.Reset();
            Root.ClearChildren();
            IsLoaded = false;
        }

        public void OnSizeChange(Vector2i size)
        {
            ScreenSize = size;
            Root.Size = new Vector2(MathF.Max(1f, size.X), MathF.Max(1f, size.Y));
            Root.Layout(default);
            uITable.OnSizeChange(size);
            UiRender?.OnSizeChange(size);
        }

        // ---------------- 输入转发 ----------------

        public bool HandleMouseMove(float x, float y)
        {
            if (!IsLoaded) return false;
            _lastPointer = new Vector2(x, y);
            return Input.PointerMove(Root, _lastPointer);
        }

        public bool HandleMouseDown(float x, float y)
        {
            if (!IsLoaded) return false;
            _lastPointer = new Vector2(x, y);
            return Input.PointerPress(Root, _lastPointer);
        }

        public bool HandleMouseUp(float x, float y)
        {
            if (!IsLoaded) return false;
            _lastPointer = new Vector2(x, y);
            return Input.PointerRelease(Root, _lastPointer);
        }

        /// <summary>每帧刷新悬停状态（不依赖鼠标移动事件）。</summary>
        public void RefreshPointer()
        {
            if (!IsLoaded) return;
            Input.Update(Root, _lastPointer);
        }

        public void ReleasePointer() => Input.Reset();

        // ---------------- 键盘 / 文本输入 ----------------

        /// <summary>是否有输入框正在编辑（此时按键/字符都应该给 UI）。</summary>
        public bool HasTextFocus => Input.HasTextFocus;

        /// <summary>当前希望的指针形状。</summary>
        public UICursorShape CursorShape => Input.CursorShape;

        /// <summary>把按键交给输入框；返回是否消费。</summary>
        public bool HandleKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys key)
        {
            if (!IsLoaded) return false;
            return Input.HandleKeyDown(key);
        }

        /// <summary>把输入字符交给输入框；返回是否消费。</summary>
        public bool HandleTextInput(int unicode)
        {
            if (!IsLoaded) return false;
            return Input.HandleTextInput(unicode);
        }

        /// <summary>ESC：先让输入框失焦 / 收起下拉 / 关闭设置窗口；返回是否已消费。</summary>
        public bool HandleEscape()
        {
            if (!IsLoaded) return false;
            return uITable.HandleEscape();
        }
    }
}
