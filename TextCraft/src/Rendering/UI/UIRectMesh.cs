using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TextCraft.src.Rendering.UI
{
    public class UIRectMesh
    {
        public float[] vertices;

        public int vao;
        public int vbo;

        public bool isLoad = false;

        public UIRectMesh(float[] vertices)
        {
            this.vertices = vertices;
        }

        public void GetVertexObject()
        {
            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            if (vertices == null)
                vertices = new float[] { };
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), Vector2.SizeInBytes);
            GL.EnableVertexAttribArray(1);

            isLoad = true;
        }
    }
}
