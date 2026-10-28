using System;
using System.Threading;
using OpenTK.Graphics.OpenGL4;
using TextCraft.src.Tools;

namespace TextCraft.src.Rendering
{
    /// <summary>
    /// 纹理采样参数。UI 需要三种典型组合：
    /// 像素风图集（Nearest + mipmap）、平滑皮肤（Linear）、字体遮罩（Linear + Clamp）。
    /// </summary>
    public readonly struct TextureOptions
    {
        public readonly TextureMinFilter Min;
        public readonly TextureMagFilter Mag;
        public readonly TextureWrapMode Wrap;
        public readonly bool Mipmap;

        public TextureOptions(TextureMinFilter min, TextureMagFilter mag, TextureWrapMode wrap, bool mipmap)
        {
            Min = min;
            Mag = mag;
            Wrap = wrap;
            Mipmap = mipmap;
        }

        /// <summary>像素风：点采样 + mipmap（与旧版 UI 图集行为一致）。</summary>
        public static TextureOptions PixelArt => new(TextureMinFilter.NearestMipmapLinear, TextureMagFilter.Nearest, TextureWrapMode.Repeat, true);

        /// <summary>平滑拉伸：线性采样，边缘钳制，避免九宫格边界采样到相邻精灵。</summary>
        public static TextureOptions Smooth => new(TextureMinFilter.Linear, TextureMagFilter.Linear, TextureWrapMode.ClampToEdge, false);

        /// <summary>字体图集：白色 + alpha，线性采样。</summary>
        public static TextureOptions FontMask => new(TextureMinFilter.Linear, TextureMagFilter.Linear, TextureWrapMode.ClampToEdge, false);
    }

    public class Texture
    {
        // 创建顺序即唯一 Id，用于 UI 合批键（不依赖 GL Handle，无窗口校验时也能用）
        private static int _nextId;

        public int Id { get; } = Interlocked.Increment(ref _nextId);

        // 纹理ID，只生成一次
        public int Handle { get; private set; }

        private int _width, _height;
        public int Width => _width;
        public int Height => _height;

        public bool IsUploaded => Handle != 0;

        private byte[] _atlasData = Array.Empty<byte>();
        private TextureOptions _options = TextureOptions.PixelArt;

        public void Load(string location, TextureLoader loader)
            => Load(location, loader, TextureOptions.PixelArt);

        public void Load(string location, TextureLoader loader, TextureOptions options)
        {
            loader.LoadTexture(location);
            Upload(loader.pixelData, loader.imageX, loader.imageY, options);
        }

        public void Load(string location, TextureLoader loader, int cols, int rows, int padding)
        {
            loader.LoadAtlasWithPadding(location, cols, rows, padding);
            Upload(loader.pixelData, loader.imageX, loader.imageY, TextureOptions.PixelArt);
        }

        /// <summary>
        /// 直接上传 RGBA 像素数据（用于程序生成的纹理，例如外部模型缺纹理时的白色/纯色/调色板纹理）。
        /// </summary>
        public void LoadPixels(byte[] rgbaPixels, int width, int height, bool generateMipmap = true)
        {
            if (rgbaPixels == null || rgbaPixels.Length < width * height * 4)
                throw new ArgumentException("像素数据长度与宽高不匹配", nameof(rgbaPixels));

            Upload(rgbaPixels, width, height,
                generateMipmap ? TextureOptions.PixelArt : TextureOptions.Smooth);
        }

        /// <summary>上传任意采样参数的 RGBA 数据（UI 图集 / 字体图集用它）。</summary>
        public void UploadPixels(byte[] rgbaPixels, int width, int height, TextureOptions options)
        {
            if (rgbaPixels == null || rgbaPixels.Length < width * height * 4)
                throw new ArgumentException("像素数据长度与宽高不匹配", nameof(rgbaPixels));

            Upload(rgbaPixels, width, height, options);
        }

        private void Upload(byte[] data, int width, int height, TextureOptions options)
        {
            _atlasData = data;
            _width = width;
            _height = height;
            _options = options;

            EnsureHandle();

            GL.BindTexture(TextureTarget.Texture2D, Handle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)options.Wrap);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)options.Wrap);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)options.Min);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)options.Mag);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, _atlasData);

            if (options.Mipmap)
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void EnsureHandle()
        {
            if (Handle != 0) return;
            Handle = GL.GenTexture();
        }

        public void Bind(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            if (Handle == 0) return;
            GL.DeleteTexture(Handle);
            Handle = 0;
        }

        public override string ToString() => $"Texture#{Id} {_width}x{_height} {_options.Min}/{_options.Mag}";
    }
}
