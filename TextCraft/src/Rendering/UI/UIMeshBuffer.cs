using System;
using OpenTK.Graphics.OpenGL4;

namespace TextCraft.src.Rendering.UI
{
    /// <summary>
    /// 所有 UI 顶点共用的一个动态 VBO/VAO。
    /// 每帧只做一次上传，之后按 <see cref="UIBatch"/> 的区间连续 draw，避免"一个控件一次 draw"。
    /// </summary>
    internal sealed class UIMeshBuffer
    {
        private int _vao;
        private int _vbo;
        private int _capacity;

        public int Vao => _vao;

        public void Create()
        {
            if (_vao != 0) return;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, UIVertex.Stride, 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, UIVertex.Stride, 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, UIVertex.Stride, 4 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        /// <summary>上传整帧合并后的顶点（<paramref name="count"/> 之前的元素有效）。</summary>
        public void Upload(UIVertex[] vertices, int count)
        {
            Create();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            int bytes = count * UIVertex.Stride;
            if (bytes > _capacity)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, bytes, vertices, BufferUsageHint.StreamDraw);
                _capacity = bytes;
            }
            else
            {
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, vertices);
            }
        }

        public void Bind() => GL.BindVertexArray(_vao);

        public void Dispose()
        {
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            _vbo = 0;
            _vao = 0;
            _capacity = 0;
        }
    }
}
