using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Tools
{
    internal class TextureLoader
    {
        public int imageX;
        public int imageY;

        public byte[] pixelData = new byte[] { };

        public void LoadTexture(string filePath)
        {
            using (var image = Image.Load<Rgba32>(filePath))
            {
                pixelData = GetPixelData(image);
                imageX = image.Width;
                imageY = image.Height;
            }
        }
        private static byte[] GetPixelData(Image<Rgba32> image)
        {
            var bytes = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(bytes);
            return bytes;
        }
    }
}
