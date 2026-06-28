using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Rendering.UI
{
    internal class UIShader : BaseShader
    {
        //顶点着色器
        protected override string VertexShaderSource => @"
            #version 450 core
            layout(location = 0) in vec2 aPos;
            layout(location = 1) in vec2 aTexCoord;

            out vec2 TexCoord;

            uniform mat4 projection;

            void main()
            {
                gl_Position = projection *  vec4(aPos,-0.1 ,1.0);
                TexCoord = aTexCoord;
            }";
        //片段着色器
        protected override string FragmentShaderSource => @"
            #version 450 core
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D uiTexture;
            uniform vec4 uColor;

            void main()
            {
                FragColor = texture(uiTexture,TexCoord);
            }";

        public void GetMatrix(Vector2i size)
        {
            GL.UseProgram(_program);
            Matrix4.CreateOrthographicOffCenter(0,size.X, size.Y, 0,-1,1,out var _projectionMatrix);
            int location = GL.GetUniformLocation(_program, "projection");
            GL.UniformMatrix4(location, false, ref _projectionMatrix);
        }

        public void GetVertices()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray( _vao );
            GL.BindBuffer(BufferTarget.ArrayBuffer,_vbo);

            float[] vertices =
            {
                //0,0,0,0,
                //0,0.5f,0,0,
                //0.5f,0.5f,0,0
                0,0,0,0,
                400,0,0.0625f,0,
                400,400,0.0625f,0.0625f,
                0,0,0,0,
                400,400,0.0625f,0.0625f,
                0,400,0,0.0625f,
            };

            _vertexCount = vertices.Length;

            GL.BufferData(BufferTarget.ArrayBuffer,vertices.Length * sizeof(float),IntPtr.Zero,BufferUsageHint.DynamicDraw);
            GL.BufferSubData(BufferTarget.ArrayBuffer,IntPtr.Zero,vertices.Length * sizeof(float),vertices);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), Vector2.SizeInBytes);
            GL.EnableVertexAttribArray(1);
        }

        public void GetRectMesh(UIRectMesh mesh) 
        {
            if (!mesh.isLoad)
                mesh.GetVertexObject();

            GetVertices(mesh.vao, mesh.vbo, mesh.vertices.Length);
        }

        public override void Draw()
        {
            GL.UseProgram(_program);

            Bind(TextureUnit.Texture1);

            int textureLocation = GL.GetUniformLocation(_program, "uiTexture");
            GL.Uniform1(textureLocation, 1);

            Vector4 color = new Vector4(1, 0, 0, 1);
            int colorLoc = GL.GetUniformLocation(_program, "uColor");
            GL.Uniform4(colorLoc, color);

            GL.Disable(EnableCap.CullFace);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount / 4);
        }
    }
}
