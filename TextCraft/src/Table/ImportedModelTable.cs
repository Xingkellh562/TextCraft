using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using TextCraft.src.Core;
using TextCraft.src.Core.Config;
using TextCraft.src.Rendering;
using TextCraft.src.Tools;
using TextCraft.src.Tools.ModelImport;

namespace TextCraft.src.Table
{
    /// <summary>
    /// imported_models.json 中单个外部模型的定义。
    /// name 是内置查找入口使用的名字；file 是模型文件（相对 searchPaths 或程序目录）。
    /// </summary>
    public sealed class ImportedModelDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("file")]
        public string File { get; set; } = "";

        [JsonPropertyName("scale")]
        public float Scale { get; set; } = 1f;

        [JsonPropertyName("offset")]
        public float[]? Offset { get; set; }

        [JsonPropertyName("flipV")]
        public bool FlipV { get; set; } = true;

        [JsonPropertyName("texture")]
        public string? Texture { get; set; }
    }

    /// <summary>imported_models.json 的根对象。</summary>
    public sealed class ImportedModelFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>是否自动扫描 searchPaths 下所有受支持的模型文件（未在 models 中显式声明的）。</summary>
        [JsonPropertyName("autoDiscover")]
        public bool AutoDiscover { get; set; } = true;

        /// <summary>模型搜索目录（相对程序输出目录）。</summary>
        [JsonPropertyName("searchPaths")]
        public List<string> SearchPaths { get; set; } = new();

        /// <summary>显式声明的模型（可覆盖自动发现时的默认参数）。</summary>
        [JsonPropertyName("models")]
        public List<ImportedModelDefinition> Models { get; set; } = new();
    }

    /// <summary>
    /// 运行时外部模型：持有转换后的网格顶点与纹理，可通过 <see cref="ImportedModelTable.TryCreateGrid"/>
    /// 生成可直接交给渲染器/实体使用的 Grid。
    /// </summary>
    public sealed class ImportedModel
    {
        public string Name { get; init; } = "";

        public string SourceFile { get; init; } = "";

        public ImportedMesh Mesh { get; init; } = new();

        /// <summary>模型自带纹理绝对路径，可能为空。</summary>
        public string? TexturePath { get; init; }

        /// <summary>已上传到 GPU 的纹理（由 <see cref="ImportedModelTable.UploadTextures"/> 填充）。</summary>
        public Texture? Texture { get; set; }
    }

    /// <summary>
    /// 外部模型库：读取 Config/Tables/imported_models.json，使用 <see cref="ModelImporterRegistry"/>
    /// 中的导入器（内置 OBJ）把外部文件转换为内置顶点格式，并提供按名称查找/生成 Grid 的入口。
    /// 例如：<c>if (ImportedModelTable.Ins.TryCreateGrid("iron_pickaxe", out var grid)) { ... }</c>
    /// </summary>
    public class ImportedModelTable : BaseSingleton<ImportedModelTable>
    {
        public const int SupportedVersion = 1;

        private const string DefaultSearchDirectory = "test_model";

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

        private readonly Dictionary<string, ImportedModel> _models = new(StringComparer.OrdinalIgnoreCase);
        private List<string> _searchPaths = new();
        private bool _texturesUploaded;

        public int Count => _models.Count;

        public IReadOnlyDictionary<string, ImportedModel> Models => _models;

        public bool IsLoaded { get; private set; }

        public ImportedModelTable()
        {
        }

        /// <summary>按名字取外部模型（不区分大小写），不存在返回 null。</summary>
        public ImportedModel? this[string name] => TryGet(name, out var model) ? model : null;

        public bool TryGet(string? name, out ImportedModel? model)
        {
            if (!string.IsNullOrWhiteSpace(name) && _models.TryGetValue(name, out var found))
            {
                model = found;
                return true;
            }

            model = null;
            return false;
        }

        /// <summary>清空已加载的外部模型。</summary>
        public void Clear()
        {
            _models.Clear();
            _searchPaths.Clear();
            _texturesUploaded = false;
            IsLoaded = false;
        }

        /// <summary>
        /// 加载 imported_models.json。配置文件不存在时只打印一条提示并保持为空模型库（功能可选，不影响启动）；
        /// 配置存在但内容有错误时抛出 <see cref="TableLoadException"/>。
        /// </summary>
        public void Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[ImportedModelTable] 未找到外部模型配置 {configPath}，跳过外部模型加载。");
                IsLoaded = true;
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(configPath);
            }
            catch (Exception ex)
            {
                throw new TableLoadException($"读取外部模型配置失败: {configPath}（{ex.Message}）", ex);
            }

            ImportedModelFile? file;
            try
            {
                using var document = JsonDocument.Parse(json, DocumentOptions);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new TableLoadException($"外部模型配置的根节点必须是对象: {configPath}");

                file = JsonSerializer.Deserialize<ImportedModelFile>(json, JsonOptions) ?? new ImportedModelFile();
            }
            catch (JsonException ex)
            {
                throw new TableLoadException($"外部模型配置 JSON 解析失败: {configPath}{Environment.NewLine}{ex.Message}", ex);
            }

            if (file.Version < 1 || file.Version > SupportedVersion)
                throw new TableLoadException($"外部模型配置版本 {file.Version} 不受支持（当前支持 {SupportedVersion}）: {configPath}");

            var searchPaths = ResolveSearchPaths(file.SearchPaths);
            var models = new Dictionary<string, ImportedModel>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();

            foreach (var definition in file.Models)
                LoadDefinition(definition, searchPaths, models, errors);

            if (file.AutoDiscover)
            {
                // 显式登记的文件即使文件名和模型名不同，也不要再被自动发现一次
                var loadedFiles = new HashSet<string>(
                    models.Values.Select(model => model.SourceFile),
                    StringComparer.OrdinalIgnoreCase);
                DiscoverModels(searchPaths, models, loadedFiles);
            }

            if (errors.Count > 0)
                throw new TableLoadException(
                    $"外部模型加载失败: {configPath}{Environment.NewLine}  - " +
                    string.Join(Environment.NewLine + "  - ", errors));

            _models.Clear();
            foreach (var pair in models)
                _models.Add(pair.Key, pair.Value);

            _searchPaths = searchPaths;
            _texturesUploaded = false;
            IsLoaded = true;

            if (_models.Count > 0)
            {
                Console.WriteLine(
                    $"[ImportedModelTable] 已加载外部模型 {_models.Count} 个：" +
                    string.Join(", ", _models.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)));
            }
            else
            {
                Console.WriteLine("[ImportedModelTable] 配置中没有可用的外部模型。");
            }
        }

        /// <summary>
        /// 按名字生成一个可直接渲染的 Grid（每次调用返回独立顶点数组，生命周期由调用方管理）。
        /// 纹理需要在 OpenGL 上下文中调用过 <see cref="UploadTextures"/> 才会绑定。
        /// </summary>
        public bool TryCreateGrid(string? name, out Grid grid)
        {
            if (TryGet(name, out var model) && model != null)
            {
                grid = new Grid((float[])model.Mesh.Vertices.Clone(), Vector3.Zero)
                {
                    Texture = model.Texture,
                    UvScale = 1f,
                    ModelName = model.Name,
                };
                return true;
            }

            grid = new Grid();
            return false;
        }

        /// <summary>按名字生成 Grid，找不到时抛出 <see cref="TableLoadException"/>。</summary>
        public Grid CreateGrid(string name)
        {
            if (TryCreateGrid(name, out var grid))
                return grid;

            var available = _models.Count == 0
                ? "（无，请检查 Config/Tables/imported_models.json）"
                : string.Join(", ", _models.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            throw new TableLoadException($"未找到外部模型 '{name}'。可用的外部模型: {available}");
        }

        /// <summary>
        /// 在 OpenGL 上下文中上传所有外部模型纹理。
        /// 有纹理用纹理；没有纹理但有基础材质颜色（MTL Kd / FBX DiffuseColor）时用 1×1 纯色纹理；
        /// 都没有则使用 1×1 白色占位纹理。必须在渲染线程、OpenGL 上下文创建之后调用（由 GameRenderer.Load 负责）。
        /// </summary>
        public void UploadTextures()
        {
            if (_texturesUploaded)
                return;

            Texture? whiteTexture = null;
            // 多个模型共用同一张纹理时只上传一次
            var textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
            var colorTextureCache = new Dictionary<uint, Texture>();

            foreach (var model in _models.Values)
            {
                // 导入器生成的纹理（例如纯色 MTL 的调色板）
                if (model.Mesh.GeneratedTexturePixels is { Length: > 0 } generatedPixels &&
                    model.Mesh.GeneratedTextureWidth > 0 &&
                    model.Mesh.GeneratedTextureHeight > 0)
                {
                    var generatedTexture = new Texture();
                    generatedTexture.LoadPixels(
                        generatedPixels,
                        model.Mesh.GeneratedTextureWidth,
                        model.Mesh.GeneratedTextureHeight,
                        generateMipmap: false);

                    if (generatedTexture.Handle == 0)
                    {
                        Console.WriteLine("[ImportedModelTable] 警告: OpenGL 上下文尚未就绪，纹理上传推迟到下次调用。");
                        return;
                    }

                    model.Texture = generatedTexture;
                    Console.WriteLine(
                        $"[ImportedModelTable] 调色板纹理: 模型 '{model.Name}' " +
                        $"({generatedTexture.Width}x{generatedTexture.Height})");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(model.TexturePath) && File.Exists(model.TexturePath))
                {
                    if (textureCache.TryGetValue(model.TexturePath, out var cachedTexture))
                    {
                        model.Texture = cachedTexture;
                        continue;
                    }

                    try
                    {
                        var texture = new Texture();
                        texture.Load(model.TexturePath, new TextureLoader());
                        if (texture.Handle == 0)
                        {
                            Console.WriteLine("[ImportedModelTable] 警告: OpenGL 上下文尚未就绪，纹理上传推迟到下次调用。");
                            return;
                        }

                        textureCache[model.TexturePath] = texture;
                        model.Texture = texture;
                        Console.WriteLine(
                            $"[ImportedModelTable] 纹理已上传: {Path.GetFileName(model.TexturePath)} " +
                            $"({texture.Width}x{texture.Height}, handle={texture.Handle})");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ImportedModelTable] 警告: 模型 '{model.Name}' 的纹理加载失败（{ex.Message}），使用基础材质颜色/白色占位。");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(model.TexturePath))
                {
                    Console.WriteLine($"[ImportedModelTable] 警告: 模型 '{model.Name}' 的纹理文件不存在: {model.TexturePath}，使用基础材质颜色/白色占位。");
                }

                // 没有可用纹理时，退回基础材质颜色（OBJ 的 Kd / FBX 的 DiffuseColor）
                if (model.Mesh.BaseColor is { } baseColor)
                {
                    var key = PackColor(baseColor);
                    if (!colorTextureCache.TryGetValue(key, out var colorTexture))
                    {
                        colorTexture = CreateSolidColorTexture(baseColor);
                        if (colorTexture.Handle == 0)
                        {
                            Console.WriteLine("[ImportedModelTable] 警告: OpenGL 上下文尚未就绪，纹理上传推迟到下次调用。");
                            return;
                        }

                        colorTextureCache[key] = colorTexture;
                        Console.WriteLine(
                            $"[ImportedModelTable] 基础色纹理: 模型 '{model.Name}' " +
                            $"rgba=({baseColor.X:0.##},{baseColor.Y:0.##},{baseColor.Z:0.##},{baseColor.W:0.##})");
                    }

                    model.Texture = colorTexture;
                    continue;
                }

                if (whiteTexture == null)
                {
                    whiteTexture = CreateWhiteTexture();
                    if (whiteTexture.Handle == 0)
                    {
                        Console.WriteLine("[ImportedModelTable] 警告: OpenGL 上下文尚未就绪，纹理上传推迟到下次调用。");
                        return;
                    }
                }

                model.Texture = whiteTexture;
            }

            _texturesUploaded = true;
        }

        private static Texture CreateSolidColorTexture(Vector4 color)
        {
            static byte ToByte(float value)
                => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

            var texture = new Texture();
            texture.LoadPixels(new[]
            {
                ToByte(color.X),
                ToByte(color.Y),
                ToByte(color.Z),
                ToByte(color.W),
            }, 1, 1);
            return texture;
        }

        private static uint PackColor(Vector4 color)
        {
            static uint Channel(float value)
                => (uint)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

            return (Channel(color.X) << 24) | (Channel(color.Y) << 16) | (Channel(color.Z) << 8) | Channel(color.W);
        }

        private static Texture CreateWhiteTexture()
        {
            var texture = new Texture();
            texture.LoadPixels(new byte[] { 255, 255, 255, 255 }, 1, 1);
            return texture;
        }

        private static List<string> ResolveSearchPaths(List<string>? configured)
        {
            var result = new List<string>();

            if (configured != null)
            {
                foreach (var path in configured)
                {
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    var fullPath = Path.IsPathRooted(path)
                        ? Path.GetFullPath(path)
                        : Path.GetFullPath(Path.Combine(ResourcePaths.BaseDirectory, path));
                    if (!result.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                        result.Add(fullPath);
                }
            }

            if (result.Count == 0)
                result.Add(Path.GetFullPath(Path.Combine(ResourcePaths.ResourcesDirectory, DefaultSearchDirectory)));

            return result;
        }

        private static void LoadDefinition(
            ImportedModelDefinition definition,
            List<string> searchPaths,
            Dictionary<string, ImportedModel> models,
            List<string> errors)
        {
            var name = (definition.Name ?? "").Trim();
            var file = (definition.File ?? "").Trim();

            if (name.Length == 0)
            {
                errors.Add("外部模型条目缺少 name");
                return;
            }

            if (file.Length == 0)
            {
                errors.Add($"外部模型 '{name}' 缺少 file");
                return;
            }

            if (!float.IsFinite(definition.Scale) || definition.Scale <= 0f)
            {
                errors.Add($"外部模型 '{name}' 的 scale 必须是大于 0 的有限数值");
                return;
            }

            var offset = Vector3.Zero;
            if (definition.Offset != null)
            {
                if (definition.Offset.Length != 3 || definition.Offset.Any(value => !float.IsFinite(value)))
                {
                    errors.Add($"外部模型 '{name}' 的 offset 必须是 3 个有限数值，例如 [0, 0, 0]");
                    return;
                }

                offset = new Vector3(definition.Offset[0], definition.Offset[1], definition.Offset[2]);
            }

            var sourceFile = ResolveModelFile(file, searchPaths);
            if (sourceFile == null)
            {
                errors.Add(
                    $"外部模型 '{name}' 的文件不存在: {file}（搜索路径: {string.Join(", ", searchPaths)}）");
                return;
            }

            var importer = ModelImporterRegistry.FindFor(sourceFile);
            if (importer == null)
            {
                errors.Add(
                    $"外部模型 '{name}' 的格式不受支持: {Path.GetExtension(sourceFile)}" +
                    $"（当前支持: {string.Join(", ", ModelImporterRegistry.SupportedExtensions())}）");
                return;
            }

            if (models.ContainsKey(name))
            {
                errors.Add($"外部模型名重复: '{name}'");
                return;
            }

            ImportedMesh mesh;
            try
            {
                mesh = importer.Import(sourceFile, new ModelImportOptions
                {
                    Scale = definition.Scale,
                    Offset = offset,
                    FlipV = definition.FlipV,
                    TextureOverride = ResolveTextureOverride(definition.Texture, sourceFile, searchPaths),
                });
            }
            catch (Exception ex)
            {
                errors.Add($"外部模型 '{name}' 导入失败: {ex.Message}");
                return;
            }

            foreach (var warning in mesh.Warnings)
                Console.WriteLine($"[ImportedModelTable] 警告: 模型 '{name}': {warning}");

            if (mesh.VertexCount == 0)
            {
                errors.Add($"外部模型 '{name}' 没有可用三角面");
                return;
            }

            if (string.IsNullOrWhiteSpace(mesh.TexturePath) &&
                (mesh.GeneratedTexturePixels is not { Length: > 0 }) &&
                mesh.BaseColor == null)
            {
                Console.WriteLine($"[ImportedModelTable] 提示: 模型 '{name}' 没有关联纹理或材质颜色，将使用白色占位纹理（可在配置里用 texture 指定）。");
            }

            models.Add(name, new ImportedModel
            {
                Name = name,
                SourceFile = sourceFile,
                Mesh = mesh,
                TexturePath = mesh.TexturePath,
            });
        }

        private static void DiscoverModels(
            List<string> searchPaths,
            Dictionary<string, ImportedModel> models,
            HashSet<string> loadedFiles)
        {
            var extensions = new HashSet<string>(
                ModelImporterRegistry.SupportedExtensions(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ImportedModelTable] 警告: 无法扫描目录 {searchPath}（{ex.Message}）");
                    continue;
                }

                foreach (var file in files)
                {
                    if (!extensions.Contains(Path.GetExtension(file)))
                        continue;

                    var fullPath = Path.GetFullPath(file);
                    if (loadedFiles.Contains(fullPath))
                        continue; // 已通过显式配置加载

                    var name = Path.GetFileNameWithoutExtension(file);
                    if (models.ContainsKey(name))
                        continue; // 显式配置优先

                    var importer = ModelImporterRegistry.FindFor(file);
                    if (importer == null)
                        continue;

                    try
                    {
                        var mesh = importer.Import(file, new ModelImportOptions());
                        foreach (var warning in mesh.Warnings)
                            Console.WriteLine($"[ImportedModelTable] 警告: 自动发现模型 '{name}': {warning}");

                        if (mesh.VertexCount == 0)
                        {
                            Console.WriteLine($"[ImportedModelTable] 警告: 自动发现模型 '{name}' 没有可用三角面，已跳过: {file}");
                            continue;
                        }

                        models.Add(name, new ImportedModel
                        {
                            Name = name,
                            SourceFile = fullPath,
                            Mesh = mesh,
                            TexturePath = mesh.TexturePath,
                        });
                        loadedFiles.Add(fullPath);
                        Console.WriteLine($"[ImportedModelTable] 自动发现外部模型 '{name}' <- {file}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ImportedModelTable] 警告: 自动发现模型 '{name}' 导入失败（{ex.Message}），已跳过: {file}");
                    }
                }
            }
        }

        private static string? ResolveModelFile(string file, List<string> searchPaths)
        {
            if (Path.IsPathRooted(file))
                return File.Exists(file) ? Path.GetFullPath(file) : null;

            foreach (var searchPath in searchPaths)
            {
                var candidate = Path.Combine(searchPath, file);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            var direct = Path.Combine(ResourcePaths.BaseDirectory, file);
            return File.Exists(direct) ? Path.GetFullPath(direct) : null;
        }

        private static string? ResolveTextureOverride(string? texture, string sourceFile, List<string> searchPaths)
        {
            if (string.IsNullOrWhiteSpace(texture))
                return null;

            var value = texture.Trim();
            if (Path.IsPathRooted(value))
            {
                if (File.Exists(value))
                    return Path.GetFullPath(value);

                Console.WriteLine($"[ImportedModelTable] 警告: 配置的纹理不存在: {value}");
                return null;
            }

            var modelDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFile)) ?? ".";
            var candidates = new List<string> { Path.Combine(modelDirectory, value) };
            candidates.AddRange(searchPaths.Select(searchPath => Path.Combine(searchPath, value)));

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            Console.WriteLine($"[ImportedModelTable] 警告: 配置的纹理 '{value}' 未找到，将使用模型自带材质纹理");
            return null;
        }
    }
}
