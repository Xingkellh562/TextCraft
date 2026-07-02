using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;

namespace TextCraft.src.Tools
{

    public class GlyphInfo
    {
        public char Character { get; set; }
        public Rectangle Bounds { get; set; }          // 像素区域（含 padding）
        public RectangleF UV { get; set; }             // 归一化 UV（0~1）
        public float XAdvance { get; set; }            // 绘制后光标前进距离（像素）
        public float YOffset { get; set; }             // 垂直偏移（用于基线对齐）
    }
    public class FontLoader : BaseSingleton<FontLoader>
    {
        public Dictionary<char, GlyphInfo> GlyphMap { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] PixelData { get; private set; }

        public FontLoader()
        {
            GlyphMap = new();
            PixelData = new byte[] { };
            Generate(System.IO.Path.Combine(AppContext.BaseDirectory + "Resources\\ui\\msyhl.ttc"), 32,
                "qwertyuiopasdfghjklzxcvbnmQWERTYUIIOPOASDFGHJKLZXCVBNM[]{}:;'\\,./\'\"|<>?1234567890!@#$%^&*()_+-=`~ ");
        }

        public void Generate(string fontPath, float fontSize, string characters, int padding = 2, int maxWidth = 1024)
        {
            var collection = new FontCollection();
            var families = collection.AddCollection(fontPath);
            var family = families.First();
            var font = family.CreateFont(fontSize, FontStyle.Regular);

            var textOptions = new TextOptions(font);
            var rawRects = new Dictionary<char, FontRectangle>();
            foreach (char c in characters)
                rawRects[c] = TextMeasurer.MeasureSize(c.ToString(), textOptions);

            // 2. 确定统一框尺寸
            float maxHeight = 32f;
            float maxFullWidth = 32f;
            float fullWidth = maxFullWidth;

            // 框尺寸
            int boxHeight = (int)Math.Ceiling(maxHeight);
            int fullBoxWidth = (int)Math.Ceiling(fullWidth);

            // 3. 布局（自动换行）
            int curX = padding, curY = padding, maxRowHeight = 0;
            var charPositions = new Dictionary<char, Rectangle>();
            foreach (char c in characters)
            {
                int boxW = fullBoxWidth;
                int boxH = boxHeight;
                if (curX + boxW > maxWidth)
                {
                    curX = padding;
                    curY += boxH + padding;
                    maxRowHeight = 0;
                }
                charPositions[c] = new Rectangle(curX, curY, boxW, boxH);
                curX += boxW + padding;
                if (boxH > maxRowHeight) maxRowHeight = boxH;
            }

            Width = maxWidth;
            Height = curY + maxRowHeight + padding;

            // 4. 生成图集
            using (var atlas = new Image<Rgba32>(Width, Height))
            {
                //atlas.Mutate(ctx => ctx.Fill(Color.White)); // 调试用白色背景

                foreach (char c in characters)
                {
                    var rect = charPositions[c];

                    // 4.1 在临时图像上绘制字符（背景透明）
                    int rawW = (int)Math.Ceiling(maxHeight);
                    int rawH = (int)Math.Ceiling(maxFullWidth);
                    using (var temp = new Image<Rgba32>(rawW, rawH))
                    {
                        temp.Mutate(ctx => ctx.Clear(Color.Transparent));
                        // 绘制原点使字符位于临时图像中心（基线居中）
                        float originX = 0;//rawW / 2f - rawRect.Width / 2f;
                        float originY = 0;
                        var options = new RichTextOptions(font)
                        {
                            Origin = new System.Numerics.Vector2(originX, originY)
                        };
                        temp.Mutate(ctx => ctx.DrawText(options, c.ToString(), Color.White));

                        int width = !IsWideChar(c) && rawRects[c].Width > rect.Width / 2 ? (int)rawRects[c].Width : rect.Width;
                        //Console.WriteLine(rawRects[c].Width);

                        // 4.2 拉伸到目标框尺寸（含 padding）
                        using (var stretched = temp.Clone(ctx => ctx.Resize(width, rect.Height)))
                        {
                            // 4.3 绘制到图集对应位置
                            atlas.Mutate(ctx => ctx.DrawImage(stretched, new Point(rect.X, rect.Y), 1f));
                        }
                    }

                    // 4.4 记录映射信息
                    var bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                    var uv = new RectangleF(
                        bounds.X / (float)Width,
                        bounds.Y / (float)Height,
                        bounds.Width / (float)Width,
                        bounds.Height / (float)Height
                    );

                    GlyphMap[c] = new GlyphInfo
                    {
                        Character = c,
                        Bounds = bounds,
                        UV = uv,
                        XAdvance = rect.Width, // 步进等于框宽度
                        YOffset = 0
                    };
                }

                // 保存调试图集
                atlas.Save("font_atlas_fixed.png");
                PixelData = new byte[Width * Height * 4];
                atlas.CopyPixelDataTo(PixelData);
            }
        }

        public static bool IsWideChar(char c)
        {
            // 1. 中文字符（基本区）
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            // 2. 全角ASCII、标点等（！＂＃ ～ 等）
            if (c >= 0xFF00 && c <= 0xFFEF) return true;
            // 3. 日文/韩文等常用宽字符（可根据项目需要添加）
            if (c >= 0x3040 && c <= 0x30FF) return true; // 平假名/片假名
            return false;
        }
    }
}
