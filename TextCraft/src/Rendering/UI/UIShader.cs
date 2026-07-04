using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Table;

namespace TextCraft.src.Rendering.UI
{
    internal class UIShader : BaseShader
    {
        public bool isFont = false;
        //顶点着色器
        protected override string VertexShaderSource => @"
            #version 450 core
            layout(location = 0) in vec2 aPos;
            layout(location = 1) in vec2 aTexCoord;

            out vec2 TexCoord;

            uniform mat4 projection;

            void main()
            {
                gl_Position = projection *  vec4(aPos,0 ,1.0);
                TexCoord = aTexCoord;
            }";
        //片段着色器
        protected override string FragmentShaderSource => @"
            #version 450 core
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D uiTexture;

            uniform vec2 pictureSize;
            uniform vec4 uColor;

            void main()
            {
                vec2 trueTexCoord = vec2(TexCoord.x / pictureSize.x,TexCoord.y / pictureSize.y);
                FragColor = texture(uiTexture,trueTexCoord);
            }";

        public void GetMatrix(Vector2i size)
        {
            GL.UseProgram(_program);
            Matrix4.CreateOrthographicOffCenter(0, size.X, size.Y, 0,-1,1,out var _projectionMatrix);
            int location = GL.GetUniformLocation(_program, "projection");
            GL.UniformMatrix4(location, false, ref _projectionMatrix);
        }
        public void GetRectMesh(UIRectMesh mesh) 
        {
            if (!mesh.isLoad)
                mesh.GetVertexObject();
            if (!mesh.isUpdate)
                mesh.UpdateVertices();

            GetVertices(mesh.vao, mesh.vbo, mesh.vertices.Length);
        }

        public Texture font = new Texture();
        public override void Draw()
        {
            GL.UseProgram(_program);
            Vector2 size = new Vector2(1, 1);
            if (isFont)
            {
                font.Bind(TextureUnit.Texture1);
            }
            else
            {
                size = new Vector2(AtlasTable.Ins.uiAtlas.texture.Width, AtlasTable.Ins.uiAtlas.texture.Height);
                AtlasTable.Ins.uiAtlas.texture.Bind(TextureUnit.Texture1);
            }
            int sizeLoc = GL.GetUniformLocation(_program, "pictureSize");
            GL.Uniform2(sizeLoc, size);

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
