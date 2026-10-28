using System;
using OpenTK.Mathematics;
using TextCraft.src.UI;

namespace TextCraft.src.Rendering.UI
{
    /// <summary>
    /// <see cref="IRenderer"/> 适配层：UI 的实际绘制委托给 <see cref="UIBatchRenderer"/>，
    /// 这样 UI 与 3D 渲染在 GameSession 之外保持一致的接口形状。
    /// </summary>
    internal sealed class UIRenderer : IRenderer
    {
        private readonly UIMgr _uIMgr;
        private Vector2i _size;

        public UIBatchRenderer Batch { get; } = new UIBatchRenderer();

        public UIRenderer(UIMgr uIMgr)
        {
            _uIMgr = uIMgr;
        }

        public void Load() => Batch.Load();

        public void Draw()
        {
            if (!UIAssets.Ins.IsLoaded) return;
            Batch.Render(_uIMgr.Root, _size);
        }

        public void GetCamera(Vector3d pos, Vector3d dir)
        {
        }

        public void OnSizeChange(Vector2i size) => _size = size;
    }
}
