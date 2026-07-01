using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Tools;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TextCraft.src.Rendering
{
    internal class Texture
    {
        // 纹理ID，只生成一次
        public int Handle { get; private set; }
        private int _width, _height;
        public int Width => _width;
        public int Height => _height;
        private byte[] _atlasData = new byte[] { };
        public void Load(string location, TextureLoader loader)
        {
            loader.LoadTexture(location);
            _atlasData = loader.pixelData;
            _width = loader.imageX;
            _height = loader.imageY;

            // 创建纹理并上传数据（只在此处调用 GenTexture）
            Handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, _atlasData);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }
        public void Load(string location, TextureLoader loader,int cols,int rows,int padding)
        {
            loader.LoadAtlasWithPadding(location, cols, rows, padding);
            _atlasData = loader.pixelData;
            _width = loader.imageX;
            _height = loader.imageY;

            // 创建纹理并上传数据（只在此处调用 GenTexture）
            Handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, _atlasData);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }
        public void LoadFont()
        {
            _atlasData = FontLoader.Ins.PixelData;
            _width = FontLoader.Ins.Width;
            _height = FontLoader.Ins.Height;

            // 创建纹理并上传数据（只在此处调用 GenTexture）
            Handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, _atlasData);

        }
        public void Bind(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }
    }
}
