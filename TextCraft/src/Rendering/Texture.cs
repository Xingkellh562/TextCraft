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
    public class Texture
    {
        // 纹理ID，只生成一次
        public int Handle { get; private set; }
        private int _width, _height;
        public int Width => _width;
        public int Height => _height;
        private byte[] _atlasData = Array.Empty<byte>();

        public void Load(string location, TextureLoader loader)
        {
            loader.LoadTexture(location);
            Upload(loader.pixelData, loader.imageX, loader.imageY,
                TextureMinFilter.NearestMipmapLinear, TextureMagFilter.Nearest, generateMipmap: true);
        }

        public void Load(string location, TextureLoader loader, int cols, int rows, int padding)
        {
            loader.LoadAtlasWithPadding(location, cols, rows, padding);
            Upload(loader.pixelData, loader.imageX, loader.imageY,
                TextureMinFilter.NearestMipmapLinear, TextureMagFilter.Nearest, generateMipmap: true);
        }

        /// <summary>
        /// 直接上传 RGBA 像素数据（用于程序生成的纹理，例如外部模型缺纹理时的白色/纯色/调色板纹理）。
        /// </summary>
        public void LoadPixels(byte[] rgbaPixels, int width, int height, bool generateMipmap = true)
        {
            if (rgbaPixels == null || rgbaPixels.Length < width * height * 4)
                throw new ArgumentException("像素数据长度与宽高不匹配", nameof(rgbaPixels));

            Upload(rgbaPixels, width, height,
                generateMipmap ? TextureMinFilter.NearestMipmapLinear : TextureMinFilter.Nearest,
                TextureMagFilter.Nearest,
                generateMipmap);
        }

        public void LoadFont()
        {
            Upload(FontLoader.Ins.PixelData, FontLoader.Ins.Width, FontLoader.Ins.Height,
                TextureMinFilter.Nearest, TextureMagFilter.Nearest, generateMipmap: false);
        }

        private void Upload(byte[] data, int width, int height, TextureMinFilter minFilter, TextureMagFilter magFilter, bool generateMipmap)
        {
            _atlasData = data;
            _width = width;
            _height = height;

            // 创建纹理并上传数据（只在此处调用 GenTexture）
            Handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, _atlasData);

            if (generateMipmap)
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public void Bind(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }
    }
}
