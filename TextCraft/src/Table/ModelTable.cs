using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextCraft.src.Core;

namespace TextCraft.src.Table
{
    /// <summary>
    /// 用于交错数组 T[][] 的转换器，使内层数组序列化为单行，外层数组保持整体缩进。
    /// </summary>
    public class JaggedArrayConverter<T> : JsonConverter<T[][]>
    {
        // 用于内层数组序列化的选项（不缩进）
        private static readonly JsonSerializerOptions _innerOptions = new()
        {
            WriteIndented = false
        };

        // 反序列化（标准读取，因为写入格式仍是合法的 JSON）
        public override T[][] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected start of array");

            var result = new List<T[]>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                // 读取内层数组（由于类型是 T[]，不会递归调用本转换器）
                T[]? inner = JsonSerializer.Deserialize<T[]>(ref reader, options);
                result.Add(inner ?? Array.Empty<T>());
            }
            return result.ToArray();
        }

        // 序列化
        public override void Write(Utf8JsonWriter writer, T[][] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();  // 外层数组开始

            foreach (var inner in value)
            {
                writer.WriteStartArray();
                foreach (var count in inner)
                {
                    // 将内层数组序列化为不带缩进的 JSON 字符串
                    string countJson = JsonSerializer.Serialize(count, _innerOptions);
                    writer.WriteRawValue(countJson); // 直接写入原始 JSON
                }
                writer.WriteEndArray();
            }

            writer.WriteEndArray();  // 外层数组结束
        }
    }

    /// <summary>
    /// 模型定义。faces 中每个面是一段扁平的 float 数据：
    /// 每 6 个 float 描述一个顶点（x, y, z, u, v, ao），通常一个面由 6 个顶点（两个三角形）组成。
    /// faces[0..5] 依次对应 +X、-X、+Y、-Y、+Z、-Z 六个方向，faces[6] 及之后为始终绘制的附加面。
    /// 模型数据来自 Config/Tables/models/*.json，不再硬编码在代码中。
    /// </summary>
    public class Model
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("faces")]
        public float[][] faces { get; set; } = Array.Empty<float[]>();

        public Model(float[][] faces, string name)
        {
            this.Name = name;
            this.faces = faces;
        }

        public Model()
        {
        }
    }

    public class QuadFace
    {
        public float[] vertices = Array.Empty<float>();

        public QuadFace(float[] vertices)
        {
            this.vertices = (float[])vertices.Clone();
        }

        public QuadFace()
        {
            this.vertices = Array.Empty<float>();
        }
    }

    /// <summary>
    /// 模型表：从 JSON 资源加载具名模型，供方块表（以及实体模型）按名字引用。
    /// 支持两种资源形式：
    /// 1. 目录：递归读取目录下所有 *.json，每个文件可以是单个模型对象，也可以是模型数组；
    /// 2. 单个文件：读取一个模型对象或模型数组。
    /// 模型名不区分大小写；单个模型文件可省略 name，此时使用文件名（不含扩展名）。
    /// </summary>
    public class ModelTable : BaseSingleton<ModelTable>
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly JsonDocumentOptions DocumentOptions = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private readonly Dictionary<string, Model> _models = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>模型数量。</summary>
        public int Count => _models.Count;

        /// <summary>已加载的模型（按名字索引）。</summary>
        public IReadOnlyDictionary<string, Model> Models => _models;

        /// <summary>是否已经成功加载过模型。</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>按名字取模型；找不到时返回空模型，保持旧接口的容错语义。</summary>
        public Model this[string name] => TryGet(name, out var model) ? model : new Model();

        public ModelTable()
        {
        }

        /// <summary>是否存在指定名字的模型（不区分大小写）。</summary>
        public bool Contains(string? name)
            => !string.IsNullOrWhiteSpace(name) && _models.ContainsKey(name);

        /// <summary>尝试获取模型。</summary>
        public bool TryGet(string? name, out Model model)
        {
            if (!string.IsNullOrWhiteSpace(name) && _models.TryGetValue(name, out var found))
            {
                model = found;
                return true;
            }

            model = new Model();
            return false;
        }

        /// <summary>清空已加载的模型。</summary>
        public void Clear()
        {
            _models.Clear();
            IsLoaded = false;
        }

        /// <summary>
        /// 从文件或目录加载模型。加载失败时抛出 <see cref="TableLoadException"/>，
        /// 并且不会破坏当前已加载的数据（校验通过后才替换）。
        /// </summary>
        public void Load(string path)
        {
            var files = ResolveFiles(path);
            var loaded = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();

            foreach (var file in files)
                LoadFile(file, loaded, errors);

            if (errors.Count > 0)
                throw new TableLoadException("模型表加载失败：" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", errors));

            if (loaded.Count == 0)
                throw new TableLoadException($"未在 {path} 中加载到任何模型。");

            _models.Clear();
            foreach (var pair in loaded)
                _models.Add(pair.Key, pair.Value);

            IsLoaded = true;
        }

        /// <summary>把所有模型保存为旧版单文件 JSON（数组形式），便于导出/备份。</summary>
        public void Save(string path)
        {
            var list = _models.Values
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JaggedArrayConverter<float>() },
            };
            File.WriteAllText(path, JsonSerializer.Serialize(list, options));
        }

        private static List<string> ResolveFiles(string path)
        {
            if (File.Exists(path))
                return new List<string> { path };

            if (Directory.Exists(path))
            {
                var files = Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (files.Count == 0)
                    throw new TableLoadException($"模型目录中没有任何 *.json 文件: {path}");
                return files;
            }

            throw new TableLoadException($"模型目录/文件不存在: {path}");
        }

        private static void LoadFile(string file, Dictionary<string, Model> loaded, List<string> errors)
        {
            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                errors.Add($"读取模型文件失败: {file}（{ex.Message}）");
                return;
            }

            List<Model> models;
            try
            {
                using var document = JsonDocument.Parse(json, DocumentOptions);
                switch (document.RootElement.ValueKind)
                {
                    case JsonValueKind.Array:
                        models = JsonSerializer.Deserialize<List<Model>>(json, JsonOptions) ?? new List<Model>();
                        break;
                    case JsonValueKind.Object:
                        var single = JsonSerializer.Deserialize<Model>(json, JsonOptions);
                        models = single == null ? new List<Model>() : new List<Model> { single };
                        break;
                    default:
                        errors.Add($"模型文件的根节点必须是对象或数组: {file}");
                        return;
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"模型文件 JSON 解析失败: {file}{Environment.NewLine}    {ex.Message}");
                return;
            }

            var fileStem = Path.GetFileNameWithoutExtension(file);

            foreach (var model in models)
            {
                if (model == null)
                {
                    errors.Add($"模型文件包含 null 条目: {file}");
                    continue;
                }

                var name = (model.Name ?? "").Trim();
                if (name.Length == 0 && models.Count == 1)
                    name = fileStem.Trim();
                if (name.Length == 0)
                {
                    errors.Add($"模型缺少 name 字段（同一文件包含多个模型时必须为每个模型命名）: {file}");
                    continue;
                }

                var faces = model.faces ?? Array.Empty<float[]>();
                if (faces.Length == 0)
                {
                    errors.Add($"模型 '{name}' 没有任何面数据: {file}");
                    continue;
                }

                var invalid = false;
                for (var i = 0; i < faces.Length; i++)
                {
                    var face = faces[i] ?? Array.Empty<float>();
                    if (face.Length % 6 != 0)
                    {
                        errors.Add($"模型 '{name}' 第 {i} 个面有 {face.Length} 个 float，不是 6 的倍数（每个顶点需要 x,y,z,u,v,ao）: {file}");
                        invalid = true;
                    }
                    else if (face.Any(v => !float.IsFinite(v)))
                    {
                        errors.Add($"模型 '{name}' 第 {i} 个面包含 NaN/Infinity: {file}");
                        invalid = true;
                    }
                    faces[i] = face;
                }

                if (invalid)
                    continue;

                if (loaded.ContainsKey(name))
                {
                    errors.Add($"模型名重复: '{name}'，出现位置: {file}");
                    continue;
                }

                model.Name = name;
                model.faces = faces;
                loaded.Add(name, model);
                WarnIfUnexpectedUv(model, file);
            }
        }

        private static void WarnIfUnexpectedUv(Model model, string file)
        {
            foreach (var face in model.faces)
            {
                for (var j = 3; j < face.Length; j += 6)
                {
                    // 方块模型的 UV 除了 0/1 之外，也允许 0.5、0.9375 这类“纹理缩放”值（用于矮台阶/水面）；
                    // 这里只对超出 [0,1] 的明显错误值给出提示。
                    if (face[j] < 0f || face[j] > 1f || face[j + 1] < 0f || face[j + 1] > 1f)
                    {
                        Console.WriteLine(
                            $"[ModelTable] 警告: 模型 '{model.Name}' 的 UV 超出 [0, 1] 范围，" +
                            $"当前为 ({face[j]}, {face[j + 1]})，见 {file}");
                        return;
                    }
                }
            }
        }
    }
}
