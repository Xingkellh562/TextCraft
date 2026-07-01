using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Tools
{
    public class TextureLoader
    {
        public int imageX;
        public int imageY;
        public byte[] pixelData = new byte[] { };

        // 原有加载方法（保留）
        public void LoadTexture(string filePath)
        {
            using (var image = Image.Load<Rgba32>(filePath))
            {
                pixelData = GetPixelData(image);
                imageX = image.Width;
                imageY = image.Height;
            }
        }

        // 新方法：给图集加内边距
        public void LoadAtlasWithPadding(string filePath, int cols, int rows, int padding = 4)
        {
            using (var src = Image.Load<Rgba32>(filePath))
            {
                int tileW = src.Width / cols;
                int tileH = src.Height / rows;
                int newTileW = tileW + padding * 2;
                int newTileH = tileH + padding * 2;
                int newWidth = cols * newTileW;
                int newHeight = rows * newTileH;

                using (var dst = new Image<Rgba32>(newWidth, newHeight))
                {
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            // 1. 裁剪原格子
                            var srcRect = new Rectangle(c * tileW, r * tileH, tileW, tileH);
                            var tile = src.Clone(ctx => ctx.Crop(srcRect));

                            // 2. 将原格子绘制到新图中间（留出 padding）
                            int dstX = c * newTileW + padding;
                            int dstY = r * newTileH + padding;
                            dst.Mutate(ctx => ctx.DrawImage(tile, new Point(dstX, dstY), 1f));

                            // 3. 填充边框：复制边缘像素向外扩
                            // 简单方法：将 tile 边缘像素扩展到 padding 区域
                            // 这里我们用 ImageSharp 的 Clone 和 Resize 模拟“边缘扩展”（Nearest 模式）
                            // 更精确：分别复制上下左右边缘
                            FillPadding(dst, tile, c, r, padding, newTileW, newTileH, tileW, tileH);
                        }
                    }

                    // 输出新图集数据
                    pixelData = GetPixelData(dst);
                    imageX = dst.Width;
                    imageY = dst.Height;
                }
            }
        }

        private void FillPadding(Image<Rgba32> dst, Image<Rgba32> tile, int col, int row, int pad, int newTileW, int newTileH, int tileW, int tileH)
        {
            // 简便实现：用 Resize 将 tile 放大到 newTileW x newTileH，使用 NearestNeighbor 保持像素硬边
            // 手动复制边缘像素到 padding 区
            // 遍历 padding 区域，从原 tile 边缘取色
            int baseX = col * newTileW;
            int baseY = row * newTileH;

            for (int y = 0; y < newTileH; y++)
            {
                for (int x = 0; x < newTileW; x++)
                {
                    // 如果在中间区域，跳过（已绘制原图）
                    if (x >= pad && x < pad + tileW && y >= pad && y < pad + tileH)
                        continue;

                    // 映射到原 tile 的坐标（钳制到边缘）
                    int srcX = Math.Clamp(x - pad, 0, tileW - 1);
                    int srcY = Math.Clamp(y - pad, 0, tileH - 1);
                    Rgba32 color = tile[srcX, srcY];
                    dst[baseX + x, baseY + y] = color;
                }
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
