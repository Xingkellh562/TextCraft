using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using OpenTK.Mathematics;
using TextCraft.src.UI;
using TextCraft.src.Tools;

namespace TextCraft.src.Rendering
{
    /// <summary>
    /// 图集里的一个精灵定义（与 spirits.xml 一一对应）。
    /// <c>rect</c> 为像素矩形 [x0, y0, x1, y1]（左上角原点）；
    /// <c>border</c> 为九宫格边距 [left, top, right, bottom]，全 0 表示普通精灵。
    /// </summary>
    public class Spirit
    {
        public string name = "";
        public int[] rect = Array.Empty<int>();
        public int[] border = Array.Empty<int>();

        public int X => rect.Length >= 4 ? rect[0] : 0;
        public int Y => rect.Length >= 4 ? rect[1] : 0;
        public int Width => rect.Length >= 4 ? rect[2] - rect[0] : 0;
        public int Height => rect.Length >= 4 ? rect[3] - rect[1] : 0;
    }

    /// <summary>
    /// 运行时精灵：已解析好的 UV、像素尺寸与九宫格边距。
    /// 控件只持有它，绘制时不再查字典，也不再每帧算 UV。
    /// </summary>
    public sealed class UISprite
    {
        public static readonly UISprite Empty = new UISprite(null, "empty", default, default, 0, 0, 0, 0, 0, 0);

        public Texture? Texture { get; }
        public string Name { get; }
        public Vector2 UVMin { get; }
        public Vector2 UVMax { get; }
        public int Width { get; }
        public int Height { get; }
        public int BorderLeft { get; }
        public int BorderTop { get; }
        public int BorderRight { get; }
        public int BorderBottom { get; }

        public bool IsValid => Texture != null && Width > 0 && Height > 0;
        public bool IsNineSlice => BorderLeft + BorderTop + BorderRight + BorderBottom > 0;
        public UIRect UVRect => new UIRect(UVMin.X, UVMin.Y, UVMax.X - UVMin.X, UVMax.Y - UVMin.Y);

        public UISprite(Texture? texture, string name, Vector2 uvMin, Vector2 uvMax,
            int width, int height, int borderLeft, int borderTop, int borderRight, int borderBottom)
        {
            Texture = texture;
            Name = name;
            UVMin = uvMin;
            UVMax = uvMax;
            Width = width;
            Height = height;
            BorderLeft = borderLeft;
            BorderTop = borderTop;
            BorderRight = borderRight;
            BorderBottom = borderBottom;
        }

        public override string ToString() => $"sprite:{Name}";
    }

    /// <summary>
    /// 图集：一张纹理 + 若干精灵。
    /// 加载拆成两步：<see cref="Load"/> 只做 CPU 侧解码（可在无 OpenGL 上下文时校验），
    /// <see cref="UploadGpu"/> 在窗口创建后上传纹理。
    /// </summary>
    public class Atlas
    {
        public string Name { get; private set; } = "";
        public Texture texture { get; private set; } = new Texture();
        public TextureOptions Options { get; private set; } = TextureOptions.PixelArt;

        public string TexturePath { get; private set; } = "";
        public string SpiritsPath { get; private set; } = "";

        private readonly Dictionary<string, UISprite> _sprites = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Spirit> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingSpirits = new();

        private byte[]? _pendingPixels;
        private int _pendingWidth;
        private int _pendingHeight;

        public IReadOnlyDictionary<string, Spirit> Spirits => _definitions;
        public int Count => _definitions.Count;
        public bool IsLoaded => _pendingPixels != null || texture.IsUploaded;
        public bool IsUploaded => texture.IsUploaded;

        public Atlas()
        {
        }

        /// <summary>
        /// 从配置加载：解码 texture.png 与 spirits.xml（CPU 阶段）。
        /// </summary>
        public void Load(string name, string texturePath, string spiritsPath, TextureOptions options)
        {
            Name = name;
            TexturePath = texturePath;
            SpiritsPath = spiritsPath;
            Options = options;

            _definitions.Clear();
            _sprites.Clear();
            _missingSpirits.Clear();

            var loader = new TextureLoader();
            loader.LoadTexture(texturePath);
            _pendingPixels = loader.pixelData;
            _pendingWidth = loader.imageX;
            _pendingHeight = loader.imageY;

            if (File.Exists(spiritsPath))
            {
                var serializer = new XmlSerializer(typeof(List<Spirit>));
                using var stream = new FileStream(spiritsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var list = (List<Spirit>?)serializer.Deserialize(stream) ?? new List<Spirit>();
                foreach (var spirit in list)
                {
                    if (string.IsNullOrWhiteSpace(spirit.name)) continue;
                    _definitions[spirit.name] = spirit;
                }
            }
            else
            {
                Console.WriteLine($"[Atlas] 警告: 精灵表不存在: {spiritsPath}（图集 '{name}' 将没有任何精灵）");
            }

            RebuildSprites();
        }

        /// <summary>兼容旧接口：读取目录下的 texture.png + spirits.xml。</summary>
        public void LoadAtlas(string path)
            => Load(Path.GetFileName(path.TrimEnd('/', '\\')),
                Path.Combine(path, "texture.png"),
                Path.Combine(path, "spirits.xml"),
                TextureOptions.PixelArt);

        private void RebuildSprites()
        {
            _sprites.Clear();
            if (_pendingWidth <= 0 || _pendingHeight <= 0) return;

            float invW = 1f / _pendingWidth;
            float invH = 1f / _pendingHeight;

            foreach (var pair in _definitions)
            {
                var spirit = pair.Value;
                if (spirit.rect.Length < 4) continue;

                int x0 = Math.Clamp(spirit.rect[0], 0, _pendingWidth);
                int y0 = Math.Clamp(spirit.rect[1], 0, _pendingHeight);
                int x1 = Math.Clamp(spirit.rect[2], x0, _pendingWidth);
                int y1 = Math.Clamp(spirit.rect[3], y0, _pendingHeight);

                int borderLeft = spirit.border.Length >= 4 ? Math.Max(0, spirit.border[0]) : 0;
                int borderTop = spirit.border.Length >= 4 ? Math.Max(0, spirit.border[1]) : 0;
                int borderRight = spirit.border.Length >= 4 ? Math.Max(0, spirit.border[2]) : 0;
                int borderBottom = spirit.border.Length >= 4 ? Math.Max(0, spirit.border[3]) : 0;

                // 半像素内缩：保证四边形边缘正好采样到首/尾纹素的中心，避免相邻精灵渗色
                var uvMin = new Vector2((x0 + 0.5f) * invW, (y0 + 0.5f) * invH);
                var uvMax = new Vector2((x1 - 0.5f) * invW, (y1 - 0.5f) * invH);
                if (uvMax.X < uvMin.X) uvMax.X = uvMin.X;
                if (uvMax.Y < uvMin.Y) uvMax.Y = uvMin.Y;

                _sprites[pair.Key] = new UISprite(texture, spirit.name, uvMin, uvMax,
                    x1 - x0, y1 - y0, borderLeft, borderTop, borderRight, borderBottom);
            }
        }

        /// <summary>在 OpenGL 上下文中创建纹理。</summary>
        public void UploadGpu()
        {
            if (texture.IsUploaded) return;
            if (_pendingPixels == null || _pendingWidth <= 0 || _pendingHeight <= 0)
                throw new InvalidOperationException($"图集 '{Name}' 尚未加载像素数据，无法上传纹理");

            texture.UploadPixels(_pendingPixels, _pendingWidth, _pendingHeight, Options);
        }

        /// <summary>按名字取精灵。缺失时返回 <see cref="UISprite.Empty"/> 并只警告一次。</summary>
        public UISprite GetSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return UISprite.Empty;
            if (_sprites.TryGetValue(name, out var sprite)) return sprite;

            if (_missingSpirits.Add(name))
                Console.WriteLine($"[Atlas] 警告: 精灵 '{name}' 不存在于图集 '{Name}'，已使用空精灵（请检查 spirits.xml 与 ui.json）。");

            return UISprite.Empty;
        }

        public bool TryGetSprite(string name, out UISprite sprite)
        {
            if (_sprites.TryGetValue(name, out var found) && found.IsValid)
            {
                sprite = found;
                return true;
            }
            sprite = UISprite.Empty;
            return false;
        }

        /// <summary>兼容旧接口：返回精灵定义（像素矩形）。</summary>
        public Spirit GetSpirit(string name)
        {
            if (_definitions.TryGetValue(name, out var spirit)) return spirit;

            if (_missingSpirits.Add(name))
                Console.WriteLine($"[Atlas] 警告: 精灵 '{name}' 不存在，已使用空矩形代替（请检查 spirits.xml 与 UI 配置）。");

            return new Spirit { name = name, rect = new[] { 0, 0, 0, 0 } };
        }

        public void AddSpirit(string name, int[] rect)
            => AddSpirit(name, rect, Array.Empty<int>());

        public void AddSpirit(string name, int[] rect, int[] border)
        {
            _definitions[name] = new Spirit { name = name, rect = rect, border = border };
            RebuildSprites();
        }

        public void SaveAtlas(string path)
        {
            var serializer = new XmlSerializer(typeof(List<Spirit>));
            using var stream = new FileStream(Path.Combine(path, "spirits.xml"), FileMode.Create);
            serializer.Serialize(stream, _definitions.Values.ToList());
        }
    }
}
