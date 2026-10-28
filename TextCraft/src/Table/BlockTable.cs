using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using TextCraft.src.Core;
using TextCraft.src.Tools;

namespace TextCraft.src.Table
{

    public enum BlockType
    {
        Air,
        Solid,
        Liquid,
        IncompleteBlock
    }

    /// <summary>
    /// blocks.json 中单个方块的定义。字段与 JSON 一一对应：
    /// id / type 组成方块键（id * 16 + type），model 引用模型表中的模型名，
    /// tiles 按面给出图集 tile 坐标（[u, v]），isLucency 表示是否需要走半透明渲染，
    /// damp 可选，用于覆盖默认的移动阻尼。
    /// </summary>
    public sealed class BlockDefinition
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("blockType")]
        public string BlockTypeName { get; set; } = nameof(BlockType.Solid);

        [JsonPropertyName("model")]
        public string? ModelName { get; set; }

        [JsonPropertyName("tiles")]
        public int[][]? Tiles { get; set; }

        [JsonPropertyName("isLucency")]
        public bool IsLucency { get; set; }

        [JsonPropertyName("damp")]
        public float[]? Damp { get; set; }

        public override string ToString() => $"{Id}:{Type} {Name}";
    }

    /// <summary>blocks.json 的根对象。</summary>
    public sealed class BlockTableFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("blocks")]
        public List<BlockDefinition> Blocks { get; set; } = new();
    }

    /// <summary>
    /// 方块表：从 Config/Tables/blocks.json 加载方块种类与元数据，
    /// 再按模型名从 <see cref="ModelTable"/> 取几何数据并烘焙出带 UV 的 <see cref="QuadFace"/>。
    /// 方块键与旧实现保持一致（id * 16 + type），因此旧存档可以直接复用。
    /// </summary>
    public class BlockTable : BaseSingleton<BlockTable>
    {
        /// <summary>当前支持的 blocks.json 版本。</summary>
        public const int SupportedVersion = 1;

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

        private readonly ConcurrentDictionary<int, Block> _blocks = new();
        private readonly List<BlockDefinition> _definitions = new();
        private readonly Block _airFallback = new(0, "air", BlockType.Air, Array.Empty<QuadFace>());

        /// <summary>已加载的方块数量（含 type 变体）。</summary>
        public int Count => _blocks.Count;

        /// <summary>已加载的方块定义（与 blocks.json 对应，可用于导出/调试）。</summary>
        public IReadOnlyList<BlockDefinition> Definitions => _definitions;

        /// <summary>是否已经成功加载过方块表。</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// 快捷栏可放置方块的最大 id（type 为 0 且不是空气）。由数据推导，
        /// 避免快捷栏数量写死在代码里。没有任何非空气方块时为 0。
        /// </summary>
        public int MaxPlaceableId { get; private set; }

        /// <summary>
        /// 按方块键取方块。键不存在时先回退到同 id 的 type 0，
        /// 再回退到空气方块，与旧实现的容错语义一致。
        /// </summary>
        public Block this[int id]
        {
            get
            {
                if (_blocks.TryGetValue(id, out var block))
                    return block;

                int baseId = id - id % 16;
                if (_blocks.TryGetValue(baseId, out var baseBlock))
                    return baseBlock;

                return _blocks.TryGetValue(0, out var air) ? air : _airFallback;
            }
        }

        public BlockTable()
        {
        }

        /// <summary>按键精确查找方块（不做回退）。</summary>
        public bool TryGet(int id, out Block block)
            => _blocks.TryGetValue(id, out block!);

        /// <summary>清空已加载的方块。</summary>
        public void Clear()
        {
            _blocks.Clear();
            _definitions.Clear();
            IsLoaded = false;
        }

        /// <summary>使用全局模型表加载 blocks.json。</summary>
        public void Load(string path) => Load(path, ModelTable.Ins);

        /// <summary>
        /// 加载 blocks.json。全部校验通过后才替换现有数据；
        /// 任何错误都会以 <see cref="TableLoadException"/> 汇总抛出，便于一次性修正配置。
        /// </summary>
        public void Load(string path, ModelTable modelTable)
        {
            if (!File.Exists(path))
                throw new TableLoadException($"方块配置文件不存在: {path}");

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new TableLoadException($"读取方块配置文件失败: {path}（{ex.Message}）", ex);
            }

            BlockTableFile file;
            try
            {
                using var document = JsonDocument.Parse(json, DocumentOptions);
                switch (document.RootElement.ValueKind)
                {
                    case JsonValueKind.Array:
                        file = new BlockTableFile
                        {
                            Blocks = JsonSerializer.Deserialize<List<BlockDefinition>>(json, JsonOptions) ?? new List<BlockDefinition>(),
                        };
                        break;
                    case JsonValueKind.Object:
                        file = JsonSerializer.Deserialize<BlockTableFile>(json, JsonOptions) ?? new BlockTableFile();
                        break;
                    default:
                        throw new TableLoadException($"方块配置文件的根节点必须是对象或数组: {path}");
                }
            }
            catch (JsonException ex)
            {
                throw new TableLoadException($"方块配置文件 JSON 解析失败: {path}{Environment.NewLine}{ex.Message}", ex);
            }

            if (file.Version < 1 || file.Version > SupportedVersion)
                throw new TableLoadException($"方块配置版本 {file.Version} 不受支持（当前支持 {SupportedVersion}）: {path}");

            var (blocks, definitions, errors) = Build(file.Blocks, modelTable, path);

            if (errors.Count > 0)
                throw new TableLoadException(
                    $"方块表加载失败: {path}{Environment.NewLine}  - " +
                    string.Join(Environment.NewLine + "  - ", errors));

            _blocks.Clear();
            foreach (var pair in blocks)
                _blocks[pair.Key] = pair.Value;

            _definitions.Clear();
            _definitions.AddRange(definitions);
            MaxPlaceableId = definitions
                .Where(d => d.Type == 0 && blocks[d.Id * 16].BlockType != BlockType.Air)
                .Select(d => d.Id)
                .DefaultIfEmpty(0)
                .Max();
            IsLoaded = true;
        }

        /// <summary>导出当前方块定义为 blocks.json（便于工具生成/备份）。</summary>
        public void Save(string path)
        {
            var file = new BlockTableFile
            {
                Version = SupportedVersion,
                Blocks = _definitions.ToList(),
            };
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(file, options));
        }

        private static (Dictionary<int, Block> Blocks, List<BlockDefinition> Definitions, List<string> Errors)
            Build(List<BlockDefinition> source, ModelTable modelTable, string path)
        {
            var blocks = new Dictionary<int, Block>();
            var definitions = new List<BlockDefinition>();
            var errors = new List<string>();
            var warnings = new List<string>();

            if (source.Count == 0)
                errors.Add($"方块列表为空: {path}");

            foreach (var definition in source)
            {
                if (definition == null)
                {
                    errors.Add("方块列表中存在 null 条目");
                    continue;
                }

                var label = $"方块 #{definition.Id}:{definition.Type} '{definition.Name}'";

                if (string.IsNullOrWhiteSpace(definition.Name))
                {
                    errors.Add($"{label} 缺少 name");
                    continue;
                }

                if (definition.Id < 0 || definition.Id > 65535)
                {
                    errors.Add($"{label} 的 id 必须在 0..65535 之间");
                    continue;
                }

                if (definition.Type < 0 || definition.Type > 15)
                {
                    errors.Add($"{label} 的 type 必须在 0..15 之间（方块键 = id * 16 + type）");
                    continue;
                }

                if (!TryParseBlockType(definition.BlockTypeName, out var blockType))
                {
                    errors.Add($"{label} 的 blockType '{definition.BlockTypeName}' 无效，可选值: Air / Solid / Liquid / IncompleteBlock");
                    continue;
                }

                int key = definition.Id * 16 + definition.Type;
                if (blocks.ContainsKey(key))
                {
                    errors.Add($"{label} 与已定义的方块冲突（键 = {key}，同一 id/type 被重复定义）");
                    continue;
                }

                var modelName = string.IsNullOrWhiteSpace(definition.ModelName) ? null : definition.ModelName!.Trim();
                QuadFace[] model = Array.Empty<QuadFace>();

                if (modelName != null)
                {
                    if (!modelTable.TryGet(modelName, out var sourceModel))
                    {
                        var hint = modelTable.Count == 0
                            ? "（模型表尚未加载，请先加载 Config/Tables/models 目录）"
                            : $"（可用模型: {string.Join(", ", modelTable.Models.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}）";
                        errors.Add($"{label} 引用了不存在的模型 '{modelName}'{hint}");
                        continue;
                    }

                    if (blockType != BlockType.Air && sourceModel.faces.Length < 6)
                    {
                        errors.Add(
                            $"{label} 的模型 '{modelName}' 只有 {sourceModel.faces.Length} 个面；" +
                            "非 Air 方块的模型至少需要 6 个面（前 6 个面参与剔除，第 7 个面起为始终绘制的附加面）");
                        continue;
                    }

                    var (u, v, tileError) = BuildTileIndices(definition.Tiles, sourceModel.faces.Length, label);
                    if (tileError != null)
                    {
                        errors.Add(tileError);
                        continue;
                    }

                    model = ModelCreator.CreateModel(sourceModel, u, v);
                }
                else if (blockType != BlockType.Air)
                {
                    errors.Add($"{label} 未指定 model（非 Air 方块必须提供 model）");
                    continue;
                }

                var block = new Block(definition.Id, definition.Name.Trim(), blockType, model, definition.IsLucency)
                {
                    Type = definition.Type,
                };

                if (definition.Damp != null)
                {
                    if (definition.Damp.Length != 3 || definition.Damp.Any(d => !float.IsFinite(d)))
                    {
                        errors.Add($"{label} 的 damp 必须是 3 个有限数值，例如 [0.1, 0.02, 0.1]");
                        continue;
                    }

                    block.damp = new Vector3(definition.Damp[0], definition.Damp[1], definition.Damp[2]);
                }

                if (blockType == BlockType.Air && model.Length > 0)
                    warnings.Add($"{label} 的 blockType 为 Air 却定义了模型，该模型不会被渲染");

                blocks.Add(key, block);
                definitions.Add(definition);
            }

            if (!blocks.ContainsKey(0))
                errors.Add($"方块表中缺少 id=0,type=0 的 Air 方块（世界数据用 0 表示空气）: {path}");
            else if (blocks[0].BlockType != BlockType.Air)
                warnings.Add($"id=0,type=0 的方块 '{blocks[0].Name}' 的 blockType 不是 Air（世界数据用 0 表示空气）");

            foreach (var warning in warnings)
                Console.WriteLine("[BlockTable] 警告: " + warning);

            return (blocks, definitions, errors);
        }

        private static (int[] U, int[] V, string? Error) BuildTileIndices(int[][]? tiles, int faceCount, string label)
        {
            if (tiles == null || tiles.Length == 0)
                return (new[] { 0 }, new[] { 0 }, null);

            var u = new int[tiles.Length];
            var v = new int[tiles.Length];

            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null || tile.Length != 2)
                    return (u, v, $"{label} 的 tiles 第 {i} 项必须是 [u, v] 两个整数");

                if (tile[0] < 0 || tile[1] < 0 || tile[0] > 255 || tile[1] > 255)
                    return (u, v, $"{label} 的 tiles 第 {i} 项超出范围 0..255: [{tile[0]}, {tile[1]}]");

                u[i] = tile[0];
                v[i] = tile[1];
            }

            if (tiles.Length > faceCount)
                Console.WriteLine($"[BlockTable] 警告: {label} 的 tiles 有 {tiles.Length} 项，但模型只有 {faceCount} 个面，多余项会被忽略");

            return (u, v, null);
        }

        private static bool TryParseBlockType(string? value, out BlockType blockType)
        {
            blockType = BlockType.Air;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim();
            foreach (BlockType candidate in Enum.GetValues(typeof(BlockType)))
            {
                if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    blockType = candidate;
                    return true;
                }
            }

            if (string.Equals(text, "Incomplete", StringComparison.OrdinalIgnoreCase))
            {
                blockType = BlockType.IncompleteBlock;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 运行时方块对象：包含烘焙后的几何（UV 已按图集 tile 偏移并内缩），以及渲染/物理所需的元数据。
    /// </summary>
    public class Block
    {
        public int Id { get; set; } = 0;
        public int Type { get; set; } = 0;
        public string Name { get; set; } = "Air";
        public BlockType BlockType { get; set; } = BlockType.Air;
        public QuadFace[] Model { get; set; } = Array.Empty<QuadFace>();

        public Vector3 damp = new Vector3(0.1f, 0.02f, 0.1f);
        public bool IsLucency { get; set; } = false;

        public Block()
        {
        }

        public Block(int id, string name, BlockType blockType, QuadFace[] model, bool isLucency = false)
        {
            Id = id;
            Name = name;
            BlockType = blockType;
            Model = model;
            IsLucency = isLucency;
        }
    }
}
