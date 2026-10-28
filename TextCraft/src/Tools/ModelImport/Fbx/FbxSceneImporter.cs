using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace TextCraft.src.Tools.ModelImport.Fbx
{
    /// <summary>
    /// 把解析后的 FBX 节点树转换成渲染用的网格：
    /// 处理 Mesh Geometry 的顶点 / 多边形 / 第一套 UV / 材质槽与漫反射纹理，
    /// 应用 Model 的基础 Lcl Translation/Rotation/Scaling（含父级链）。
    /// 骨骼、蒙皮、动画、PBR、变形目标等复杂数据全部忽略。
    /// </summary>
    internal static class FbxSceneImporter
    {
        private const float DegreesToRadians = MathF.PI / 180f;

        public static ImportedMesh Import(FbxNode root, string filePath, ModelImportOptions options)
        {
            var warnings = new List<string>();
            var objects = root.FirstChild("Objects")
                ?? throw new InvalidDataException("FBX 中缺少 Objects 节点");

            var connections = ParseConnections(root);
            var textures = IndexById(objects.ChildrenNamed("Texture"));
            var materials = IndexById(objects.ChildrenNamed("Material"));
            var models = IndexById(objects.ChildrenNamed("Model"));
            var geometries = objects.ChildrenNamed("Geometry")
                .Where(IsMeshGeometry)
                .ToList();

            if (geometries.Count == 0)
                throw new InvalidDataException("FBX 中没有可用的 Mesh Geometry（只支持基础网格，忽略骨骼/动画）");

            var modelIds = new HashSet<long>(models.Keys);
            var materialIds = new HashSet<long>(materials.Keys);
            var geometryIds = new HashSet<long>(geometries.Select(node => node.GetLong(0)));

            var modelParents = new Dictionary<long, long>();
            var geometryModels = new Dictionary<long, long>();
            var modelMaterials = new Dictionary<long, List<long>>();

            foreach (var connection in connections)
            {
                if (!connection.Type.Equals("OO", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (modelIds.Contains(connection.Source) && modelIds.Contains(connection.Destination))
                    modelParents[connection.Source] = connection.Destination;

                if (geometryIds.Contains(connection.Source) && modelIds.Contains(connection.Destination))
                    geometryModels[connection.Source] = connection.Destination;

                if (materialIds.Contains(connection.Source) && modelIds.Contains(connection.Destination))
                {
                    if (!modelMaterials.TryGetValue(connection.Destination, out var materialList))
                    {
                        materialList = new List<long>();
                        modelMaterials[connection.Destination] = materialList;
                    }

                    materialList.Add(connection.Source);
                }
            }

            // OO 连接中 Texture 不会直接连 Model；纹理通过 OP 连接到 Material，下面的映射负责这一步
            var worldTransforms = BuildWorldTransforms(models, modelParents, warnings);
            var materialTextures = BuildMaterialTextures(textures, materials, connections, filePath, warnings);
            var materialColors = BuildMaterialColors(materials);
            var fallbackMaterialId = materials.Count > 0 ? materials.Keys.First() : -1;

            var vertices = new List<float>();
            var usedTextures = new List<string>();
            var usedColors = new List<Vector4>();
            var badPolygons = 0;
            var missingUvWarned = false;
            var fallbackTextureWarned = false;
            var unboundGeometryWarned = false;
            var hasBounds = false;
            var min = Vector3.Zero;
            var max = Vector3.Zero;

            foreach (var geometry in geometries)
            {
                var geometryId = geometry.GetLong(0);
                var transform = Matrix4.Identity;
                var modelId = -1L;

                if (geometryModels.TryGetValue(geometryId, out var boundModelId) &&
                    worldTransforms.TryGetValue(boundModelId, out var worldTransform))
                {
                    modelId = boundModelId;
                    transform = worldTransform;
                }
                else if (!unboundGeometryWarned)
                {
                    warnings.Add("存在未关联 Model 的几何体，使用单位变换");
                    unboundGeometryWarned = true;
                }

                var positions = FbxNode.ToDoubles(geometry.FirstChild("Vertices")?.GetArrayValue());
                var polygonIndices = FbxNode.ToLongs(geometry.FirstChild("PolygonVertexIndex")?.GetArrayValue());

                if (positions == null || positions.Length < 3)
                {
                    warnings.Add($"几何体 {geometryId} 没有有效的顶点数据，已跳过");
                    continue;
                }

                if (polygonIndices == null || polygonIndices.Length < 3)
                {
                    warnings.Add($"几何体 {geometryId} 没有有效的面数据，已跳过");
                    continue;
                }

                var positionData = positions;
                var uvLayer = geometry.FirstChild("LayerElementUV");
                var uvValues = FbxNode.ToDoubles(uvLayer?.FirstChild("UV")?.GetArrayValue());
                var uvIndexValues = FbxNode.ToLongs(uvLayer?.FirstChild("UVIndex")?.GetArrayValue());
                var uvMapping = uvLayer?.FirstChild("MappingInformationType")?.GetString(0) ?? "ByPolygonVertex";
                var uvReference = uvLayer?.FirstChild("ReferenceInformationType")?.GetString(0) ?? "Direct";
                var indexedUv = uvIndexValues != null &&
                                !uvReference.Equals("Direct", StringComparison.OrdinalIgnoreCase);

                var materialLayer = geometry.FirstChild("LayerElementMaterial");
                var materialSlots = FbxNode.ToLongs(materialLayer?.FirstChild("Materials")?.GetArrayValue());
                var materialMapping = materialLayer?.FirstChild("MappingInformationType")?.GetString(0) ?? "ByPolygon";

                var geometryMaterials = modelMaterials.TryGetValue(modelId, out var materialList)
                    ? materialList
                    : null;
                var fallbackMaterial = geometryMaterials is { Count: > 0 }
                    ? geometryMaterials[0]
                    : fallbackMaterialId;

                var polygon = new List<long>();
                var polygonIndex = 0;
                var cornerBase = 0;

                foreach (var rawIndex in polygonIndices)
                {
                    polygon.Add(rawIndex);
                    if (rawIndex >= 0)
                        continue;

                    var polygonSize = polygon.Count;
                    ProcessPolygon(polygon);
                    cornerBase += polygonSize;
                    polygon.Clear();
                    polygonIndex++;
                }

                if (polygon.Count > 0)
                    warnings.Add($"几何体 {geometryId} 的最后一个多边形没有闭合索引，已忽略");

                void ProcessPolygon(List<long> rawPolygon)
                {
                    var polygonPositions = new int[rawPolygon.Count];
                    for (var i = 0; i < rawPolygon.Count; i++)
                    {
                        var positionIndex = rawPolygon[i] < 0 ? ~rawPolygon[i] : rawPolygon[i];
                        if (positionIndex < 0 || positionIndex * 3 + 2 >= positionData.Length)
                        {
                            badPolygons++;
                            return;
                        }

                        polygonPositions[i] = (int)positionIndex;
                    }

                    if (polygonPositions.Length < 3)
                        return;

                    var slot = ResolveMaterialSlot(polygonIndex, cornerBase, polygonPositions[0]);
                    var materialId = slot >= 0 && geometryMaterials != null && slot < geometryMaterials.Count
                        ? geometryMaterials[slot]
                        : fallbackMaterial;

                    string? texture = null;
                    if (materialId >= 0 && materialTextures.TryGetValue(materialId, out var materialTexture))
                        texture = materialTexture;

                    if (string.IsNullOrWhiteSpace(texture))
                    {
                        var firstTexture = materialTextures.Values
                            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
                        if (!string.IsNullOrWhiteSpace(firstTexture))
                        {
                            texture = firstTexture;
                            if (!fallbackTextureWarned)
                            {
                                warnings.Add("材质未关联纹理，使用 FBX 中的第一个纹理");
                                fallbackTextureWarned = true;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(texture))
                        usedTextures.Add(texture!);
                    else if (materialId >= 0 &&
                             materialColors.TryGetValue(materialId, out var materialColor))
                        usedColors.Add(materialColor);

                    for (var i = 1; i + 1 < polygonPositions.Length; i++)
                    {
                        AddVertex(polygonPositions[0], cornerBase);
                        AddVertex(polygonPositions[i], cornerBase + i);
                        AddVertex(polygonPositions[i + 1], cornerBase + i + 1);
                    }
                }

                void AddVertex(int positionIndex, int cornerIndex)
                {
                    var local = new Vector3(
                        (float)positionData[positionIndex * 3],
                        (float)positionData[positionIndex * 3 + 1],
                        (float)positionData[positionIndex * 3 + 2]);
                    var world = Vector3.TransformPosition(local, transform);

                    vertices.Add(world.X);
                    vertices.Add(world.Y);
                    vertices.Add(world.Z);

                    var uv = GetUv(positionIndex, cornerIndex);
                    vertices.Add(uv.X);
                    vertices.Add(uv.Y);
                    vertices.Add(1f);

                    if (!hasBounds)
                    {
                        min = world;
                        max = world;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector3.ComponentMin(min, world);
                        max = Vector3.ComponentMax(max, world);
                    }
                }

                Vector2 GetUv(int positionIndex, int cornerIndex)
                {
                    if (uvValues == null || uvValues.Length < 2)
                    {
                        if (!missingUvWarned)
                        {
                            warnings.Add("几何体缺少 UV 数据，使用 (0,0)");
                            missingUvWarned = true;
                        }

                        return Vector2.Zero;
                    }

                    var baseIndex = uvMapping.ToLowerInvariant() switch
                    {
                        "bycontrolpoint" => positionIndex,
                        "bypolygon" => polygonIndex,
                        "allsame" => 0,
                        _ => cornerIndex,
                    };

                    var uvIndex = indexedUv && uvIndexValues != null
                        ? (int)(baseIndex >= 0 && baseIndex < uvIndexValues.Length
                            ? uvIndexValues[baseIndex]
                            : -1)
                        : baseIndex;

                    if (uvIndex < 0 || uvIndex * 2 + 1 >= uvValues.Length)
                    {
                        if (!missingUvWarned)
                        {
                            warnings.Add("几何体的 UV 索引越界，使用 (0,0)");
                            missingUvWarned = true;
                        }

                        return Vector2.Zero;
                    }

                    var uv = new Vector2(
                        (float)uvValues[uvIndex * 2],
                        (float)uvValues[uvIndex * 2 + 1]);
                    if (options.FlipV)
                        uv.Y = 1f - uv.Y;
                    return uv;
                }

                int ResolveMaterialSlot(int currentPolygonIndex, int currentCornerBase, int firstPositionIndex)
                {
                    if (materialSlots == null || materialSlots.Length == 0)
                        return -1;

                    var slotIndex = materialMapping.ToLowerInvariant() switch
                    {
                        "bypolygon" => currentPolygonIndex,
                        "allsame" => 0,
                        "bycontrolpoint" => firstPositionIndex,
                        _ => currentCornerBase,
                    };

                    if (slotIndex < 0 || slotIndex >= materialSlots.Length)
                        return -1;

                    return (int)materialSlots[slotIndex];
                }
            }

            if (vertices.Count == 0)
                throw new InvalidDataException("FBX 中没有可用的三角面");

            if (badPolygons > 0)
                warnings.Add($"忽略了 {badPolygons} 个索引非法的多边形");

            var distinctTextures = usedTextures
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinctTextures.Count > 1)
                warnings.Add($"模型使用了 {distinctTextures.Count} 个纹理，当前只支持单纹理，使用 {Path.GetFileName(distinctTextures[0])}");

            return new ImportedMesh
            {
                Vertices = vertices.ToArray(),
                Min = min,
                Max = max,
                TexturePath = distinctTextures.Count > 0 ? distinctTextures[0] : null,
                BaseColor = distinctTextures.Count == 0 && usedColors.Count > 0 ? usedColors[0] : null,
                Warnings = warnings,
            };
        }

        private static bool IsMeshGeometry(FbxNode geometry)
        {
            var type = geometry.GetString(2);
            return string.IsNullOrEmpty(type) ||
                   type.Equals("Mesh", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<long, FbxNode> IndexById(IEnumerable<FbxNode> nodes)
        {
            var result = new Dictionary<long, FbxNode>();
            foreach (var node in nodes)
            {
                var id = node.GetLong(0);
                if (id != 0 && !result.ContainsKey(id))
                    result[id] = node;
            }

            return result;
        }

        private static List<FbxConnection> ParseConnections(FbxNode root)
        {
            var result = new List<FbxConnection>();
            var connections = root.FirstChild("Connections");
            if (connections == null)
                return result;

            foreach (var node in connections.ChildrenNamed("C"))
            {
                var type = node.GetString(0);
                if (string.IsNullOrWhiteSpace(type))
                    continue;

                result.Add(new FbxConnection
                {
                    Type = type!,
                    Source = node.GetLong(1),
                    Destination = node.GetLong(2),
                    Property = node.Values.Count > 3 ? node.GetString(3) : null,
                });
            }

            return result;
        }

        private static Dictionary<long, Matrix4> BuildWorldTransforms(
            Dictionary<long, FbxNode> models,
            Dictionary<long, long> parents,
            List<string> warnings)
        {
            var locals = new Dictionary<long, Matrix4>();
            foreach (var pair in models)
                locals[pair.Key] = BuildLocalTransform(pair.Value, warnings);

            var result = new Dictionary<long, Matrix4>();

            Matrix4 Resolve(long id, int depth)
            {
                if (result.TryGetValue(id, out var cached))
                    return cached;

                if (depth > 64)
                    return Matrix4.Identity;

                var local = locals.TryGetValue(id, out var matrix) ? matrix : Matrix4.Identity;
                if (parents.TryGetValue(id, out var parent) && models.ContainsKey(parent))
                    local = local * Resolve(parent, depth + 1);

                result[id] = local;
                return local;
            }

            foreach (var id in models.Keys)
                Resolve(id, 0);

            return result;
        }

        private static Matrix4 BuildLocalTransform(FbxNode model, List<string> warnings)
        {
            var translation = GetVectorProperty(model, "Lcl Translation", Vector3.Zero, warnings);
            var rotationDegrees = GetVectorProperty(model, "Lcl Rotation", Vector3.Zero, warnings);
            var scaling = GetVectorProperty(model, "Lcl Scaling", Vector3.One, warnings);

            if (scaling.X == 0f || scaling.Y == 0f || scaling.Z == 0f)
            {
                warnings.Add("Model 的 Lcl Scaling 含 0，已按 1 处理");
                scaling = Vector3.One;
            }

            var radians = rotationDegrees * DegreesToRadians;
            // FBX 常用 Euler XYZ；这里按 X→Y→Z 的旋转组合，仅覆盖基础模型变换
            var rotation = Matrix4.CreateRotationZ(radians.Z) *
                           Matrix4.CreateRotationY(radians.Y) *
                           Matrix4.CreateRotationX(radians.X);

            return Matrix4.CreateScale(scaling) *
                   rotation *
                   Matrix4.CreateTranslation(translation);
        }

        private static Vector3 GetVectorProperty(FbxNode node, string name, Vector3 fallback, List<string> warnings)
        {
            if (!node.TryGetPropertyNumbers(name, out var numbers))
                return fallback;

            if (numbers.Length < 3)
            {
                warnings.Add($"Model 的 {name} 数值不足 3 个，已使用默认值");
                return fallback;
            }

            return new Vector3((float)numbers[0], (float)numbers[1], (float)numbers[2]);
        }

        private static Dictionary<long, Vector4> BuildMaterialColors(Dictionary<long, FbxNode> materials)
        {
            var result = new Dictionary<long, Vector4>();
            foreach (var pair in materials)
            {
                if (!pair.Value.TryGetPropertyNumbers("DiffuseColor", out var numbers) &&
                    !pair.Value.TryGetPropertyNumbers("Diffuse", out numbers))
                {
                    continue;
                }

                if (numbers.Length < 3)
                    continue;

                var alpha = numbers.Length >= 4 ? (float)numbers[3] : 1f;
                result[pair.Key] = new Vector4(
                    (float)numbers[0],
                    (float)numbers[1],
                    (float)numbers[2],
                    alpha);
            }

            return result;
        }

        private static Dictionary<long, string?> BuildMaterialTextures(
            Dictionary<long, FbxNode> textures,
            Dictionary<long, FbxNode> materials,
            List<FbxConnection> connections,
            string filePath,
            List<string> warnings)
        {
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
            var texturePaths = new Dictionary<long, string?>();
            foreach (var pair in textures)
            {
                var fileName = GetTextureFileName(pair.Value);
                texturePaths[pair.Key] = ResolveTexturePath(fileName, baseDirectory, warnings);
            }

            var result = new Dictionary<long, string?>();
            foreach (var materialId in materials.Keys)
                result[materialId] = null;

            // 优先 DiffuseColor/空属性的连接
            foreach (var connection in connections)
            {
                if (!connection.Type.Equals("OP", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!texturePaths.TryGetValue(connection.Source, out var path) || string.IsNullOrWhiteSpace(path))
                    continue;
                if (!materials.ContainsKey(connection.Destination))
                    continue;
                if (!string.IsNullOrEmpty(connection.Property) &&
                    !connection.Property!.Equals("DiffuseColor", StringComparison.OrdinalIgnoreCase))
                    continue;

                result[connection.Destination] ??= path;
            }

            // 再接受任意纹理属性（不同导出器属性名不一样）
            foreach (var connection in connections)
            {
                if (!connection.Type.Equals("OP", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!texturePaths.TryGetValue(connection.Source, out var path) || string.IsNullOrWhiteSpace(path))
                    continue;
                if (!materials.ContainsKey(connection.Destination))
                    continue;

                result[connection.Destination] ??= path;
            }

            return result;
        }

        private static string? GetTextureFileName(FbxNode texture)
        {
            foreach (var propertyName in new[] { "RelativeFilename", "FileName" })
            {
                if (texture.TryGetPropertyString(propertyName, out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                var child = texture.FirstChild(propertyName);
                var direct = child?.GetString(0);
                if (!string.IsNullOrWhiteSpace(direct))
                    return direct;
            }

            return null;
        }

        private static string? ResolveTexturePath(string? rawPath, string baseDirectory, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return null;

            var path = rawPath!.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(path))
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);

                warnings.Add($"纹理文件不存在: {path}");
                return null;
            }

            var candidate = Path.Combine(baseDirectory, path);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            var fileName = Path.GetFileName(path);
            candidate = Path.Combine(baseDirectory, fileName);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            warnings.Add($"纹理文件不存在: {rawPath}（FBX 目录: {baseDirectory}）");
            return null;
        }
    }
}
