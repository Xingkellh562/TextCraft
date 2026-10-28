using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace TextCraft.src.Tools.ModelImport
{
    /// <summary>
    /// Wavefront OBJ 导入器（支持同目录/相对路径的 .mtl、map_Kd 纹理与纯色 MTL）。
    /// 支持 v / vt / vn / f / o / g / s / usemtl / mtllib，面支持三角形与多边形（扇形三角化），
    /// 索引支持负数（相对末尾）。法线会被忽略，因为内置渲染顶点格式只有位置、UV 与 AO。
    ///
    /// 纯色 MTL：当模型没有任何可用纹理时，会把 MTL 的 Kd（以及 d/Tr 透明度）烘焙成一张
    /// 1×N 调色板纹理，并把每个面的 UV 指向对应材质的像素，因此多材质的纯色模型也能按材质着色。
    /// </summary>
    public sealed class ObjModelImporter : IModelImporter
    {
        private sealed class MaterialLibrary
        {
            public readonly Dictionary<string, string?> Textures = new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, Vector4> Colors = new(StringComparer.OrdinalIgnoreCase);
        }

        public string Name => "Wavefront OBJ";

        public IReadOnlyList<string> Extensions { get; } = new[] { ".obj" };

        public ImportedMesh Import(string filePath, ModelImportOptions options)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"OBJ 文件不存在: {filePath}", filePath);

            var positions = new List<Vector3>();
            var texCoords = new List<Vector2>();
            var vertices = new List<float>();
            var vertexMaterials = new List<int>();
            var warnings = new List<string>();
            var usedMaterials = new List<string>();
            var materialIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var mtlLibs = new List<string>();
            var currentMaterialIndex = -1;
            var badFaces = 0;
            var missingUv = false;

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine;
                var hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line[..hash];
                line = line.Trim();
                if (line.Length == 0)
                    continue;

                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0])
                {
                    case "v":
                        if (parts.Length < 4)
                        {
                            warnings.Add("忽略格式不正确的顶点行: " + line);
                            break;
                        }

                        var position = new Vector3(
                            ParseFloat(parts[1], filePath),
                            ParseFloat(parts[2], filePath),
                            ParseFloat(parts[3], filePath));
                        positions.Add(position * options.Scale + options.Offset);
                        break;

                    case "vt":
                        var u = parts.Length > 1 ? ParseFloat(parts[1], filePath) : 0f;
                        var v = parts.Length > 2 ? ParseFloat(parts[2], filePath) : 0f;
                        if (options.FlipV)
                            v = 1f - v;
                        texCoords.Add(new Vector2(u, v));
                        break;

                    case "vn":
                        // 引擎顶点格式没有法线属性，光照由 AO 与雾效处理。
                        break;

                    case "mtllib":
                        for (var i = 1; i < parts.Length; i++)
                            mtlLibs.Add(parts[i]);
                        break;

                    case "usemtl":
                        var materialName = parts.Length > 1 ? parts[1] : "";
                        if (!materialIndices.TryGetValue(materialName, out currentMaterialIndex))
                        {
                            currentMaterialIndex = usedMaterials.Count;
                            materialIndices[materialName] = currentMaterialIndex;
                            usedMaterials.Add(materialName);
                        }

                        break;

                    case "f":
                        AppendFace(
                            parts,
                            positions,
                            texCoords,
                            vertices,
                            vertexMaterials,
                            currentMaterialIndex,
                            ref badFaces,
                            ref missingUv);
                        break;
                }
            }

            if (vertices.Count == 0)
                throw new InvalidDataException($"OBJ 中没有可用的三角面: {filePath}");

            if (badFaces > 0)
                warnings.Add($"忽略了 {badFaces} 个索引非法的面");

            var min = positions.Count > 0 ? positions[0] : Vector3.Zero;
            var max = min;
            foreach (var point in positions)
            {
                min = Vector3.ComponentMin(min, point);
                max = Vector3.ComponentMax(max, point);
            }

            var materialLibrary = LoadMaterials(filePath, mtlLibs, warnings);
            var texturePath = ResolveTexture(materialLibrary.Textures, usedMaterials, options, warnings);
            var baseColor = PickColor(materialLibrary.Colors, usedMaterials);

            byte[]? generatedPixels = null;
            var generatedWidth = 0;
            var generatedHeight = 0;

            // 没有任何可用纹理时，用 MTL 的 Kd 颜色生成调色板纹理
            if (texturePath == null)
            {
                var palette = BuildColorPalette(
                    materialLibrary.Colors,
                    usedMaterials,
                    vertexMaterials,
                    vertices,
                    warnings);
                if (palette is { } generated)
                {
                    generatedPixels = generated.Pixels;
                    generatedWidth = generated.Width;
                    generatedHeight = 1;
                    baseColor = null;
                }
            }

            if (missingUv && generatedPixels == null)
                warnings.Add("部分面缺少有效的纹理坐标，已使用 (0,0) 代替");

            return new ImportedMesh
            {
                Vertices = vertices.ToArray(),
                Min = min,
                Max = max,
                TexturePath = texturePath,
                BaseColor = baseColor,
                GeneratedTexturePixels = generatedPixels,
                GeneratedTextureWidth = generatedWidth,
                GeneratedTextureHeight = generatedHeight,
                Warnings = warnings,
            };
        }

        private static void AppendFace(
            string[] parts,
            List<Vector3> positions,
            List<Vector2> texCoords,
            List<float> vertices,
            List<int> vertexMaterials,
            int materialIndex,
            ref int badFaces,
            ref bool missingUv)
        {
            if (parts.Length < 4)
            {
                badFaces++;
                return;
            }

            var indices = new List<(int Position, int Uv)>(parts.Length - 1);

            for (var i = 1; i < parts.Length; i++)
            {
                var pieces = parts[i].Split('/');
                if (pieces.Length == 0 ||
                    !int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawPosition))
                {
                    badFaces++;
                    return;
                }

                var positionIndex = ResolveIndex(rawPosition, positions.Count);
                if (positionIndex < 0)
                {
                    badFaces++;
                    return;
                }

                var uvIndex = -1;
                if (pieces.Length > 1 && pieces[1].Length > 0 &&
                    int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawUv))
                {
                    uvIndex = ResolveIndex(rawUv, texCoords.Count);
                    if (uvIndex < 0)
                        missingUv = true; // 越界的 UV 按缺失处理，不影响几何
                }
                else
                {
                    missingUv = true;
                }

                indices.Add((positionIndex, uvIndex));
            }

            if (indices.Count < 3)
            {
                badFaces++;
                return;
            }

            // 扇形三角化：a, b, c / a, c, d / ...
            for (var i = 1; i + 1 < indices.Count; i++)
            {
                EmitVertex(indices[0]);
                EmitVertex(indices[i]);
                EmitVertex(indices[i + 1]);
            }

            void EmitVertex((int Position, int Uv) index)
            {
                var point = positions[index.Position];
                var uv = index.Uv >= 0 && index.Uv < texCoords.Count ? texCoords[index.Uv] : Vector2.Zero;

                vertices.Add(point.X);
                vertices.Add(point.Y);
                vertices.Add(point.Z);
                vertices.Add(uv.X);
                vertices.Add(uv.Y);
                vertices.Add(1f); // AO：外部模型不参与体素 AO 计算，使用 1（不衰减）
                vertexMaterials.Add(materialIndex);
            }
        }

        /// <summary>OBJ 索引：正数从 1 开始，负数表示从末尾倒数。</summary>
        private static int ResolveIndex(int rawIndex, int count)
        {
            if (rawIndex > 0)
            {
                var index = rawIndex - 1;
                return index < count ? index : -1;
            }

            if (rawIndex < 0)
            {
                var index = count + rawIndex;
                return index >= 0 ? index : -1;
            }

            return -1;
        }

        private static float ParseFloat(string text, string filePath)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value))
                return value;

            throw new InvalidDataException($"OBJ 数值解析失败: '{text}'（{filePath}）");
        }

        private static MaterialLibrary LoadMaterials(string objPath, List<string> mtlLibs, List<string> warnings)
        {
            var library = new MaterialLibrary();
            var objDirectory = Path.GetDirectoryName(Path.GetFullPath(objPath)) ?? ".";

            foreach (var mtlLib in mtlLibs)
            {
                var mtlPath = Path.IsPathRooted(mtlLib) ? mtlLib : Path.Combine(objDirectory, mtlLib);
                if (!File.Exists(mtlPath))
                {
                    warnings.Add($"找不到材质库: {mtlLib}");
                    continue;
                }

                ParseMtl(mtlPath, objDirectory, library.Textures, library.Colors, warnings);
            }

            return library;
        }

        private static string? ResolveTexture(
            Dictionary<string, string?> textures,
            List<string> usedMaterials,
            ModelImportOptions options,
            List<string> warnings)
        {
            if (!string.IsNullOrWhiteSpace(options.TextureOverride) && File.Exists(options.TextureOverride))
                return Path.GetFullPath(options.TextureOverride);

            if (textures.Count == 0)
                return null;

            var selected = new List<string>();
            foreach (var material in usedMaterials)
            {
                if (textures.TryGetValue(material, out var texture) &&
                    !string.IsNullOrWhiteSpace(texture) &&
                    !selected.Contains(texture, StringComparer.OrdinalIgnoreCase))
                {
                    selected.Add(texture);
                }
            }

            if (selected.Count == 0)
            {
                // 没有 usemtl，或所有材质都没有纹理：只有材质库里恰好只有一个纹理时才采用
                var allTextures = textures.Values
                    .Where(texture => !string.IsNullOrWhiteSpace(texture))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return allTextures.Count == 1 ? allTextures[0] : null;
            }

            if (selected.Count > 1)
            {
                warnings.Add(
                    $"模型使用了 {selected.Count} 个不同纹理，当前每个 Grid 只支持一张纹理，" +
                    $"已使用 {Path.GetFileName(selected[0])}");
            }

            return selected[0];
        }

        private static Vector4? PickColor(Dictionary<string, Vector4> colors, List<string> usedMaterials)
        {
            foreach (var materialName in usedMaterials)
            {
                if (colors.TryGetValue(materialName, out var materialColor))
                    return materialColor;
            }

            return colors.Count > 0 ? colors.Values.First() : null;
        }

        /// <summary>
        /// 把 MTL 的纯色材质烘焙成 1×N 调色板纹理，并把每个顶点 UV 指向对应材质的像素中心。
        /// 返回 null 表示没有可用的材质颜色。
        /// </summary>
        private static (byte[] Pixels, int Width)? BuildColorPalette(
            Dictionary<string, Vector4> colors,
            List<string> usedMaterials,
            List<int> vertexMaterials,
            List<float> vertices,
            List<string> warnings)
        {
            if (colors.Count == 0)
                return null;

            var palette = new List<Vector4>();
            var materialToPalette = new int[usedMaterials.Count];

            if (usedMaterials.Count == 0)
            {
                // 没有 usemtl：所有面使用材质库里的第一个颜色
                palette.Add(colors.Values.First());
            }
            else
            {
                for (var i = 0; i < usedMaterials.Count; i++)
                {
                    palette.Add(colors.TryGetValue(usedMaterials[i], out var color) ? color : Vector4.One);
                    materialToPalette[i] = palette.Count - 1;
                }
            }

            if (palette.Count > 4096)
            {
                warnings.Add("纯色材质数量超过 4096，调色板退化为单色");
                palette.Clear();
                palette.Add(colors.Values.First());
            }

            var width = palette.Count;
            var pixels = new byte[width * 4];
            for (var i = 0; i < palette.Count; i++)
            {
                var color = palette[i];
                pixels[i * 4] = ToByte(color.X);
                pixels[i * 4 + 1] = ToByte(color.Y);
                pixels[i * 4 + 2] = ToByte(color.Z);
                pixels[i * 4 + 3] = ToByte(color.W);
            }

            var vertexCount = vertices.Count / 6;
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                var paletteIndex = 0;
                if (usedMaterials.Count > 0 && vertex < vertexMaterials.Count)
                {
                    var materialIndex = vertexMaterials[vertex];
                    if (materialIndex >= 0 && materialIndex < materialToPalette.Length)
                        paletteIndex = materialToPalette[materialIndex];
                }

                var u = width == 1 ? 0.5f : (paletteIndex + 0.5f) / width;
                var offset = vertex * 6;
                vertices[offset + 3] = u;
                vertices[offset + 4] = 0.5f;
            }

            warnings.Add($"未找到可用纹理，使用 MTL 基础色调色板（{width} 种颜色）");
            return (pixels, width);

            static byte ToByte(float value)
                => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
        }

        private static void ParseMtl(
            string mtlPath,
            string objDirectory,
            Dictionary<string, string?> textures,
            Dictionary<string, Vector4> colors,
            List<string> warnings)
        {
            string? currentMaterial = null;
            var directory = Path.GetDirectoryName(mtlPath) ?? ".";

            foreach (var rawLine in File.ReadLines(mtlPath))
            {
                var line = rawLine;
                var hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line[..hash];
                line = line.Trim();
                if (line.Length == 0)
                    continue;

                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0].ToLowerInvariant())
                {
                    case "newmtl":
                        currentMaterial = parts.Length > 1 ? parts[1] : "";
                        textures[currentMaterial] = null;
                        colors.Remove(currentMaterial);
                        break;

                    case "kd":
                        if (currentMaterial == null || parts.Length < 4)
                            break;
                        colors[currentMaterial] = new Vector4(
                            ParseMtlFloat(parts[1]),
                            ParseMtlFloat(parts[2]),
                            ParseMtlFloat(parts[3]),
                            colors.TryGetValue(currentMaterial, out var existingColor) ? existingColor.W : 1f);
                        break;

                    case "d":
                        if (currentMaterial == null || parts.Length < 2)
                            break;
                        var alpha = ParseMtlFloat(parts[1]);
                        colors[currentMaterial] = colors.TryGetValue(currentMaterial, out var colorValue)
                            ? new Vector4(colorValue.X, colorValue.Y, colorValue.Z, alpha)
                            : new Vector4(1f, 1f, 1f, alpha);
                        break;

                    case "tr":
                        if (currentMaterial == null || parts.Length < 2)
                            break;
                        var inverseAlpha = 1f - ParseMtlFloat(parts[1]);
                        colors[currentMaterial] = colors.TryGetValue(currentMaterial, out var trColor)
                            ? new Vector4(trColor.X, trColor.Y, trColor.Z, inverseAlpha)
                            : new Vector4(1f, 1f, 1f, inverseAlpha);
                        break;

                    case "map_kd":
                        if (currentMaterial == null)
                            break;

                        var textureArgument = line[parts[0].Length..].Trim().Trim('"');
                        if (textureArgument.Length == 0)
                            break;

                        var candidate = Path.IsPathRooted(textureArgument)
                            ? textureArgument
                            : Path.Combine(directory, textureArgument);

                        // MTL 与 OBJ 不在同一目录时，再尝试相对 OBJ 所在目录解析纹理
                        if (!File.Exists(candidate) && !Path.IsPathRooted(textureArgument))
                        {
                            var alternative = Path.Combine(objDirectory, textureArgument);
                            if (File.Exists(alternative))
                                candidate = alternative;
                        }

                        if (File.Exists(candidate))
                        {
                            textures[currentMaterial] = Path.GetFullPath(candidate);
                        }
                        else
                        {
                            textures[currentMaterial] = null;
                            warnings.Add($"材质 '{currentMaterial}' 的纹理不存在: {candidate}");
                        }

                        break;
                }
            }
        }

        private static float ParseMtlFloat(string text)
            => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
                ? value
                : 1f;
    }
}
