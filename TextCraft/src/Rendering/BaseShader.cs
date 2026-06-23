using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Tools;

namespace TextCraft.src.Rendering
{
    internal abstract class BaseShader
    {
        protected int _vao;
        protected int _vbo;

        protected int _program;
        public int Program => _program;

        protected Matrix4 _projectionMatrix;
        protected int _vertexCount;

        protected byte[] _atlasData = new byte[] { };
        protected int _shaderTexture;
        //顶点着色器
        protected abstract string VertexShaderSource { get; }
        //片段着色器
        protected abstract string FragmentShaderSource { get; }

        public void CreateShaderProgram()
        {
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, VertexShaderSource);
            GL.CompileShader(vertexShader);
            CheckShaderCompilation(vertexShader, "VERTEX");

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, FragmentShaderSource);
            GL.CompileShader(fragmentShader);
            CheckShaderCompilation(fragmentShader, "FRAGMENT");

            _program = GL.CreateProgram();
            GL.AttachShader(_program, vertexShader);
            GL.AttachShader(_program, fragmentShader);
            GL.LinkProgram(_program);
            CheckProgramLink(_program);

            GL.DetachShader(_program, vertexShader);
            GL.DetachShader(_program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }
        public void LoadTexture(string location)
        {
            var _textureLoader = new TextureLoader();
            _textureLoader.LoadTexture(location);

            _atlasData = _textureLoader.pixelData;

            _shaderTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _shaderTexture);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _textureLoader.imageX, _textureLoader.imageY, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, _atlasData);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }
        public abstract void Draw();
        protected void CheckShaderCompilation(int shader, string type)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"ShaderCompilationError({type}):\n{infoLog}");
            }
        }
        protected void CheckProgramLink(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(success);
                throw new Exception($"ProgramLinkError:\n{infoLog}");
            }
        }

        protected void GetVertices(int vao,int vbo,int length)
        {
            _vao = vao;
            _vbo = vbo;
            _vertexCount = length;
        }
    }
}
