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
            Generate(System.IO.Path.Combine(AppContext.BaseDirectory + "Resources\\ui\\msyh.ttc"), 32,
                "qwertyuiopasdfghjklzxcvbnmQWERTYUIIOPOASDFGHJKLZXCVBNM[]{};'\\,./\'\"|<>?1234567890!@#$%^&*()_+-=`~ 的一了是不在人有这大上主为个中到以说要就他会可也得下自时来面过出起当你都把还由其些小对我们都现能工向分然很并感方知多同年日前头道后里之么总从无情己所之爱定本两去现把");
        }

        public void Generate(string fontPath,float fontSize,string characters,int padding = 2,int maxWidth = 1024)
        {
            var collection = new FontCollection();
            var families = collection.AddCollection(fontPath);
            var family = families.First();
            var font = family.CreateFont(fontSize,FontStyle.Regular);

            var glyphSizes = new Dictionary<char,Size>();

            foreach(char c in characters)
            {
                var size = TextMeasurer.MeasureSize(c.ToString(),new TextOptions(font));
                glyphSizes[c] = new Size((int)Math.Ceiling(size.Width) + padding * 2,
                                         (int)Math.Ceiling(size.Height) + padding * 2);
            }

            int curX = padding, curY = padding, maxRowHeight = 0;
            var charPositions = new Dictionary<char, Point>();

            foreach (char c in characters)
            {
                var size = glyphSizes[c];
                if (curX + size.Width > maxWidth)
                {
                    curX = padding;
                    curY += maxRowHeight + padding;
                    maxRowHeight = 0;
                }

                charPositions[c] = new Point(curX, curY);
                curX += size.Width + padding;
                if (size.Height > maxRowHeight) maxRowHeight = size.Height;
            }


            Width = maxWidth;
            Height = curY + maxRowHeight + padding;

            using (var atlas = new Image<Rgba32>(Width,Height))
            {
               // atlas.Mutate(ctx => ctx.BackgroundColor(Color.White));

                foreach (char c in characters)
                {
                    var pos = charPositions[c];
                    var size = glyphSizes[c];

                    var options = new RichTextOptions(font)
                    {
                        Origin = new System.Numerics.Vector2(pos.X + padding, pos.Y)
                    };
                    atlas.Mutate(ctx => ctx.DrawText(options, c.ToString(), Color.White));
                    // 记录映射信息 (像素边界)
                    var bounds = new Rectangle(pos.X, pos.Y, size.Width, size.Height);

                    // UV 归一化（Y 轴翻转）
                    var uv = new RectangleF(
                        bounds.X / (float)Width,
                        bounds.Y / (float)Height,  // 翻转 V，并减去高度
                        bounds.Width / (float)Width,
                        bounds.Height / (float)Height
                    );

                    // 获取 XAdvance (字间距)
                    var advance = TextMeasurer.MeasureSize(c.ToString(), new TextOptions(font));

                    GlyphMap[c] = new GlyphInfo
                    {
                        Character = c,
                        Bounds = bounds,
                        UV = uv,
                        XAdvance = advance.Width,
                        YOffset = 0 // 简单实现忽略垂直偏移，或根据 Ascent 计算
                    };

                    atlas.Save("D:\\craftText\\TextCraft\\TextCraft\\bin\\Debug\\net8.0\\Resources\\ui\\1234567.png");
                }

                PixelData = new byte[Width * Height * 4];
                atlas.CopyPixelDataTo(PixelData);
            }
        }
    }
}
