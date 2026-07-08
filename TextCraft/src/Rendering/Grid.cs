using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Rendering
{
    public struct Vertex(Vector3 p,Vector3 uva)
    {
        public Vector3 pos = p;
        public Vector3 uva = uva;
    }
    public class Grid:IDisposable
    {
        public float[]? vertices;
        Vector3 _position;

        public int vao;
        public int vbo;

        public bool isLoad = false;
        public bool isUpdate = false;

        public Vector3 Pos
        {
            get => _position; set => _position = value;
        }
        public Grid(float[] vertices,Vector3 pos)
        {
            this.vertices = vertices;
            _position = pos;
        }

        public Grid()
        {
            this.vertices = new float[] { };
            _position = Vector3.Zero;
        }


        public void GetVertexObject()
        {
            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            if (vertices == null)
                vertices = new float[] { };
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float) , vertices, BufferUsageHint.DynamicDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), Vector3.SizeInBytes);
            GL.EnableVertexAttribArray(1);

            isLoad = true;
        }

        public void ResetForPool(float[]? newVertices = null, Vector3? newPos = null)
        {
            // 释放对旧托管数组的引用（帮助 GC，但数组不一定立即回收）
            this.vertices = newVertices ?? Array.Empty<float>();
            if (newPos.HasValue) _position = newPos.Value;
            // 注意：VAO/VBO 保持不变！isLoad 依然为 true
            isUpdate = false;
        }      

        // 更新顶点数据（池化复用时不重新生成 VAO/VBO，只更新 Buffer）
        public void UpdateVertices()
        {
            if (!isLoad) GetVertexObject();
            if (vertices == null) vertices = new float[]{ };
            ;
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float),
                          vertices, BufferUsageHint.DynamicDraw);
            isUpdate = true;
        }

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);   // 如果实现了析构函数，阻止其执行
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源（如将数组置 null 帮助 GC）
                    vertices = null;
                }

                // 释放非托管 OpenGL 资源（即使 disposing == false 也要执行）
                if (vao != 0)
                {
                    GL.DeleteVertexArray(vao);
                    vao = 0;
                }
                if (vbo != 0)
                {
                    GL.DeleteBuffer(vbo);
                    vbo = 0;
                }
                isLoad = false;

                _disposed = true;
            }
        }

        // 可选：析构函数作为安全网（防止忘记调用 Dispose）
        ~Grid()
        {
            Dispose(false);
        }
    }
}
