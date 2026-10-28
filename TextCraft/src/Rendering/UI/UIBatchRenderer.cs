using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using TextCraft.src.UI;

namespace TextCraft.src.Rendering.UI
{
    /// <summary>
    /// UI 合批渲染器。
    ///
    /// 每帧流程：
    /// <list type="number">
    /// <item>遍历控件树，把所有四边形写入 <see cref="UIDrawList"/>（同一份暂存顶点数组）；</item>
    /// <item>按 (UI 层级 → 提交顺序) 排序，再按 (纹理, 采样方式, 裁剪矩形) 合并成批次；</item>
    /// <item>把合并后的顶点一次性上传到同一个 VBO；</item>
    /// <item>逐批次设置纹理/采样/裁剪并 <c>glDrawArrays</c>。</item>
    /// </list>
    ///
    /// <see cref="UIRenderMode.Legacy"/> 下 <see cref="UIDrawList.SplitAll"/> 为 true，
    /// 每个四边形都独占一次 DrawCall，用于和旧实现对照（F4 可实时切换）。
    /// </summary>
    internal sealed class UIBatchRenderer
    {
        private readonly UIShader _shader = new();
        private readonly UIMeshBuffer _mesh = new();
        private readonly UIDrawList _drawList = new();

        private Texture? _boundTexture;
        private bool _boundAlphaMask;
        private bool _scissorEnabled;

        public UIRenderStats Stats { get; private set; }

        public UIDrawList DrawList => _drawList;

        public void Load()
        {
            _shader.Create();
            _mesh.Create();
        }

        public void Render(UIComponent root, in Vector2i size)
        {
            if (size.X <= 0 || size.Y <= 0) return;

            var assets = UIAssets.Ins;
            _drawList.SplitAll = assets.Mode == UIRenderMode.Legacy;
            _drawList.CoalesceBatches = assets.Coalesce;
            _drawList.CoalesceWindow = assets.CoalesceWindow;

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Multisample);
            GL.Disable(EnableCap.ScissorTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _shader.Use();
            _shader.SetProjection(size);
            _shader.ResetMaterialState();

            _drawList.Begin();
            root.CollectDraw(_drawList, UIRect.Infinite, Vector4.One);
            Stats = _drawList.End();

            if (_drawList.VertexCount == 0) return;

            _mesh.Upload(_drawList.FinalArray, _drawList.VertexCount);
            _mesh.Bind();

            _boundTexture = null;
            _boundAlphaMask = false;
            _scissorEnabled = false;
            _shader.ResetMaterialState();

            var batches = _drawList.Batches;
            for (int i = 0; i < batches.Length; i++)
            {
                ref readonly var batch = ref batches[i];
                if (batch.Count <= 0) continue;

                var texture = batch.Key.Texture;
                if (texture == null) continue;

                if (!ReferenceEquals(texture, _boundTexture))
                {
                    _shader.BindTexture(texture, TextureUnit.Texture0);
                    _boundTexture = texture;
                }

                bool alphaMask = batch.Key.Material == UIMaterial.AlphaMask;
                if (alphaMask != _boundAlphaMask)
                {
                    _shader.SetAlphaMask(alphaMask);
                    _boundAlphaMask = alphaMask;
                }

                ApplyScissor(batch.Key.Clip, size);

                GL.DrawArrays(PrimitiveType.Triangles, batch.Start, batch.Count);
            }

            if (_scissorEnabled) GL.Disable(EnableCap.ScissorTest);
            GL.BindVertexArray(0);
        }

        private void ApplyScissor(in UIRect clip, in Vector2i size)
        {
            if (clip.IsInfinite)
            {
                if (_scissorEnabled)
                {
                    GL.Disable(EnableCap.ScissorTest);
                    _scissorEnabled = false;
                }
                return;
            }

            int x = (int)MathF.Floor(clip.X);
            int y = (int)MathF.Floor(size.Y - clip.Bottom);
            int width = (int)MathF.Ceiling(clip.Width);
            int height = (int)MathF.Ceiling(clip.Height);
            if (width <= 0 || height <= 0) return;

            if (!_scissorEnabled)
            {
                GL.Enable(EnableCap.ScissorTest);
                _scissorEnabled = true;
            }

            GL.Scissor(x, y, width, height);
        }
    }
}