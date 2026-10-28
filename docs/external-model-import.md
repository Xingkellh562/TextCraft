# 外部模型（OBJ / FBX）导入与内置网格查找

> 前置改造：`docs/block-model-json-loading.md`（方块模型 / 方块元数据 JSON 化，基线 commit `64b8003`）
>
> 本次主题：让外部模型加载系统同时支持 **Wavefront OBJ（+MTL）** 与 **Autodesk FBX**，
> 把外部数据转换为内置渲染格式，并提供“按名称直接取得 Grid”的内置入口，
> 可直接把实体的网格换成外部导入的模型。FBX 只处理基础网格、UV、材质与漫反射纹理，
> 骨骼/蒙皮/动画/PBR 等复杂数据全部忽略。
>
> 与上次改造一样，本次改动**未提交 git**，全部在工作区中，可用 `git diff` / `git status` 审阅；
> 修改点与回滚方式见第 6、8 节。

---

## 1. 目标与结果

| 目标 | 结果 |
| --- | --- |
| 支持 OBJ 外部模型 | ✅ 内置 OBJ 导入器（`.obj` + 同目录 `.mtl` + `map_Kd` 纹理） |
| 支持 FBX 外部模型 | ✅ 内置 Binary FBX（7.x，32/64 位节点头，deflate 压缩数组）与常见 ASCII FBX 解析器 |
| 转换为内置渲染可识别的数据 | ✅ 顶点转换为渲染管线的 `x,y,z,u,v,ao`（6 float/顶点），多边形三角化，AO=1 |
| 能按名称直接加载 Grid | ✅ `ImportedModelTable.Ins.TryCreateGrid("fbx_cube_binary", out Grid grid)` |
| 能把实体网格设为外部模型 | ✅ `World.SpawnModelEntity("fbx_cube_binary")`；控制台 `/SpawnModel` 演示 |
| 资源可扩展、可配置 | ✅ `Config/Tables/imported_models.json` 声明搜索目录、模型名 → 文件、缩放/偏移/UV/纹理 |
| 纹理与材质正确显示 | ✅ 每个 Grid 可携带独立纹理与 UV 缩放，方块图集与外部纹理互不影响 |
| 修改 RequiredResources 后同步输出目录 | ✅ 沿用 csproj 同步 Target：新增模型/配置在构建时自动复制 |
| 可校验、可留痕、可回滚 | ✅ `--validate-tables` 可无窗口校验外部模型；文档记录全部修改点与回滚步骤 |

构建结果：`dotnet build` **0 warning / 0 error**。

---

## 2. 资源与配置

### 2.1 目录结构

```
RequiredResources/
├─ Config/
│  └─ Tables/
│     ├─ imported_models.json        ← 外部模型注册表
│     └─ ...                         ← blocks.json / models/（方块与内置模型）
└─ Resources/
   ├─ test_model/                    ← 外部模型测试资源
   │  ├─ iron_pickaxe.obj / .mtl / .png
   │  └─ fbx/                        ← FBX 测试夹具（本次生成）
   │     ├─ fbx_cube_binary.fbx      （7.4，deflate 压缩数组）
   │     ├─ fbx_cube_ascii.fbx
   │     └─ fbx_cube.png
   └─ ...                            ← 纹理、字体、UI 图集
```

`Resources/test_model` 只是默认搜索目录之一；通过 `imported_models.json` 的 `searchPaths`
可以添加任意目录（相对程序输出目录），例如以后把正式模型放到 `Resources/models`。

> 仓库原本没有 FBX 测试文件，本次为验证 FBX 支持生成了一个带纹理的立方体夹具
> （二进制 + ASCII 各一份）。它们的结构见附录 A，可随时替换成真实美术资源。

### 2.2 `imported_models.json`

```json
{
  "version": 1,
  "autoDiscover": true,
  "searchPaths": [
    "Resources/test_model",
    "Resources/models"
  ],
  "models": [
    {
      "name": "iron_pickaxe",
      "file": "iron_pickaxe.obj",
      "scale": 1.0,
      "flipV": true
    },
    {
      "name": "fbx_cube_binary",
      "file": "fbx/fbx_cube_binary.fbx",
      "scale": 1.0,
      "flipV": true
    },
    {
      "name": "fbx_cube_ascii",
      "file": "fbx/fbx_cube_ascii.fbx",
      "scale": 1.0,
      "flipV": true
    }
  ]
}
```

根对象字段：

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `version` | int | 1 | 配置版本，当前只支持 1 |
| `autoDiscover` | bool | `true` | 是否自动扫描 `searchPaths` 下所有受支持格式的文件（未在 `models` 中显式声明的） |
| `searchPaths` | string[] | `["Resources/test_model"]` | 模型搜索目录，相对程序输出目录；不存在的目录会被忽略 |
| `models` | object[] | `[]` | 显式模型条目，优先级高于自动发现 |

单个模型字段：

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `name` | string | 必填 | 内置查找使用的名字（不区分大小写） |
| `file` | string | 必填 | 模型文件，相对任一 `searchPaths`，或相对程序输出目录 |
| `scale` | float | 1 | 统一缩放，必须 > 0 |
| `offset` | float[3] | `[0,0,0]` | 在缩放之后叠加的平移（世界单位） |
| `flipV` | bool | `true` | 是否翻转 V 坐标（见 3.4） |
| `texture` | string | 无 | 覆盖模型自带纹理；相对 OBJ/FBX 所在目录或 `searchPaths` |

行为说明：

- 显式条目先加载，自动发现会跳过同名模型，也会跳过**已经被显式加载的同一个文件**；
- 自动发现的模型名 = 文件名（不含扩展名），使用默认参数（`scale=1`、`flipV=true`）；
- 显式条目加载失败（文件不存在、格式不支持、解析失败等）会**汇总报错并终止启动**；
- 自动发现失败只打印警告并跳过该文件，不影响启动；
- 配置文件本身不存在时只打印提示并保持空模型库（外部模型是可选功能）；
- JSON 支持 `//`、`/* */` 注释与尾逗号（与 `blocks.json` 一致）。

### 2.3 纹理解析顺序

1. 配置中的 `models[].texture`（若存在且文件可解析）优先；
2. OBJ：读取 `mtllib` / `.mtl` 的 `map_Kd`；
   FBX：读取 `Texture` 节点的 `RelativeFilename` / `FileName`，并按 `Connections` 中
   `Texture → Material` 的 OP 连接关联到材质；
3. 纹理路径先按模型文件所在目录解析，OBJ 的 MTL 还会额外回退到 OBJ 目录；
4. 仍没有纹理（或文件缺失）时：OBJ 会把 MTL 的 `Kd`/`d`/`Tr` 颜色烘焙成 **1×N 调色板纹理**（按材质索引把每个面 UV 指向对应像素），支持多材质纯色模型；FBX 使用 `DiffuseColor` 生成 **1×1 纯色纹理**；都没有时才使用 **1×1 白色占位纹理**，并打印警告。

当前一个 Grid 只支持**一张纹理**。若模型使用多个材质/纹理，导入器会警告并采用第一个纹理/颜色。

### 2.4 测试模型

**OBJ**：`iron_pickaxe.obj`（Blockbench 5.2.2 导出）：48 顶点、144 UV、36 四边形、1 材质，
纹理 `iron_pickexe.png`（32×32）。转换结果 216 顶点 / 72 三角形。

**FBX**（本次生成的夹具）：

| 文件 | 说明 |
| --- | --- |
| `fbx/fbx_cube_binary.fbx` | FBX 7.4，32 位节点偏移，数组使用 zlib/deflate 压缩 |
| `fbx/fbx_cube_ascii.fbx` | 相同内容的 ASCII FBX |
| `fbx/fbx_cube.png` | 16×16 测试纹理（F 形 + 四角颜色标记） |

立方体：8 顶点、6 个四边形（12 个三角形）、1 套 UV、1 个材质 + 漫反射纹理；
转换结果 36 顶点 / 12 三角形，包围盒 `(-0.5,0,-0.5) ~ (0.5,1,0.5)`。

---

## 3. 支持格式与转换规则

### 3.1 Wavefront OBJ

| OBJ 语法 | 支持情况 |
| --- | --- |
| `v x y z [w]` | ✅ |
| `vt u [v] [w]` | ✅（缺省 v=0） |
| `vn` | ✅ 解析但忽略（内置顶点格式没有法线属性） |
| `f v` / `f v/vt` / `f v//vn` / `f v/vt/vn` | ✅ |
| 三角形 / 四边形 / n 边形 | ✅ 扇形三角化 |
| 索引从 1 开始 / 负数索引 | ✅ |
| `o` / `g` / `s` | ✅ 忽略分组，合并为一个网格 |
| `usemtl` / `mtllib` | ✅ 用于确定纹理；多材质时取第一个纹理并警告 |
| 非法面 / 越界索引 | ✅ 跳过并计数、警告 |

MTL：识别 `newmtl`、`map_Kd`（简单文件名/路径形式）、`Kd` 基础色、`d` 透明度与 `Tr` 逆透明度；其它参数忽略。

### 3.2 Autodesk FBX

**支持范围（只提取基础网格）**：

| FBX 内容 | 支持情况 |
| --- | --- |
| Binary FBX 7.x（版本 7.1~7.7） | ✅ 32 位（< 7500）与 64 位（>= 7500）节点头 |
| 数组 deflate 压缩 / 未压缩 | ✅ 两种都支持 |
| ASCII FBX | ✅ 尽力支持常见导出器格式（按行/花括号解析） |
| `Geometry`（类型 `Mesh`） | ✅ 顶点、多边形（任意边数，扇形三角化） |
| `LayerElementUV` | ✅ 第一套 UV；支持 `ByControlPoint` / `ByPolygonVertex` / `ByPolygon` / `AllSame`，`Direct` / `IndexToDirect` |
| `LayerElementMaterial` | ✅ 基础材质槽（`ByPolygon` / `ByPolygonVertex` / `AllSame` / `ByControlPoint`） |
| `Model` 基础变换 | ✅ `Lcl Translation` / `Lcl Rotation` / `Lcl Scaling`，含 Model 父级链 |
| `Material` → `Texture`（OP 连接） | ✅ 识别 `DiffuseColor` 属性连接，取漫反射纹理 |
| 多 Geometry / 多 Model | ✅ 合并为一个网格（各自应用对应 Model 变换） |
| 骨骼 / 蒙皮 / 动画 | ❌ 忽略（只使用绑定姿态的原始顶点） |
| Morph Target / Blend Shape | ❌ 忽略 |
| PBR 材质（Metallic/Roughness/Normal 等） | ❌ 忽略，只取基础颜色（DiffuseColor）纹理/颜色 |
| 内嵌纹理（`Content`） | ❌ 只支持外部纹理文件 |
| Pivot / PreRotation / PostRotation / RotationOrder / GeometricTransform | ❌ 忽略，只应用基础 Lcl TRS |
| 相机 / 灯光 / 单位 / 坐标轴转换 | ❌ 忽略（FBX 单位、Z-up/Y-up 不做自动换算） |

> 注意：由于忽略 PreRotation/Pivot 与坐标轴换算，部分从 3ds Max/Blender 导出的 FBX
> 可能出现尺寸/朝向差异。建议在 DCC 中“应用变换（Apply Transform）”后导出，
> 或使用 `scale`/`offset` 调整；需要旋转时可先在建模软件里调整朝向。

### 3.3 转换到内置顶点格式

`ImportedMesh.Vertices` 与 `Grid` 完全兼容：每个顶点 6 个 float。

| 偏移 | 含义 | 来源 |
| --- | --- | --- |
| `+0..2` | x, y, z | 顶点位置 × `scale` + `offset`（FBX 还叠加 Model 世界变换） |
| `+3..4` | u, v | 第一套 UV（按 `flipV` 处理） |
| `+5` | ao | 固定 1（外部模型不参与体素 AO 计算） |

- 所有面按三角形展开（不做索引复用），可直接 `GL.DrawArrays(Triangles)`；
- 网格 `UvScale = 1`：着色器用 `uv / atlasSize`，外部纹理不做 16×16 tile 缩放；
- 网格携带自己的 `Texture`，与方块图集分开绑定；纯色 MTL 会生成 1×N 调色板纹理，FBX 基础色生成 1×1 纯色纹理；
- 模型坐标原样使用（不自动居中）；需要调整时用 `scale` / `offset` 或放置时的 `Grid.Pos`。

### 3.4 UV 与 V 翻转

引擎纹理上传按 **图片第一行 = V=0**（OpenGL 的 (0,0) 在纹理数据首行）；
标准 OBJ/FBX 的 V=0 在图片底部，因此默认 `flipV: true` 把 `v` 变成 `1 - v`。

- OBJ（Blockbench 导出）与 FBX 夹具默认 `flipV: true` 时显示正确；
- 若换用其它工具导出的模型，发现纹理上下颠倒，把该模型的 `flipV` 改为 `false`；
- UV 不做图集内缩（`0.4375` 那套逻辑只用于方块图集），外部纹理直接采样。

### 3.5 朝向与剔除

三角形保留源文件中的顶点顺序。渲染器启用了背面剔除（`GL.CullFace`），因此要求模型的面是
**逆时针朝外**。OBJ/FBX 夹具均按此约定生成；如果自己的模型从外面看是透明的、从里面看才正常，
请在建模软件里翻转面/法线后重新导出。

---

## 4. 内置查找入口

### 4.1 API

```csharp
using TextCraft.src.Table;
using TextCraft.src.Rendering;

// 按名称查询（不区分大小写，OBJ/FBX 一致）
ImportedModelTable.Ins.TryGet("fbx_cube_binary", out var model);
int count = ImportedModelTable.Ins.Count;
IReadOnlyDictionary<string, ImportedModel> all = ImportedModelTable.Ins.Models;

// 直接取得 Grid（每次调用返回独立顶点数组，可安全交给实体/渲染层）
if (ImportedModelTable.Ins.TryCreateGrid("fbx_cube_binary", out Grid grid))
{
    grid.Pos = new Vector3(0, 64, 0);
    // 交给 GridMgr 的任意图层渲染，例如 Entity
}

// 找不到时抛 TableLoadException，错误信息会列出可用模型
Grid grid2 = ImportedModelTable.Ins.CreateGrid("fbx_cube_binary");
```

`ImportedModel` 的成员：

| 成员 | 说明 |
| --- | --- |
| `Name` | 模型名 |
| `SourceFile` | 源文件绝对路径 |
| `Mesh` | `ImportedMesh`（顶点、三角数、包围盒、纹理路径、警告） |
| `TexturePath` | 纹理绝对路径（可能为空） |
| `Texture` | 上传到 GPU 后的纹理（由渲染器初始化时填充） |

### 4.2 实体网格 = 外部模型

```csharp
// 在玩家视线前方 2.5 格生成一个使用外部模型的实体
world.SpawnModelEntity("fbx_cube_binary");

// 或指定世界坐标
world.SpawnModelEntity("fbx_cube_binary", new Vector3(100, 70, 100));
```

内部流程：

1. `ImportedModelTable.Ins.TryCreateGrid(name, out grid)` 取得网格；
2. `gridMgr.SetChunkGrids("Entity", pos, grid)` 放入 Entity 图层（覆盖同位置旧网格）；
3. 创建 `EntityObject` + `Transform` + `ModelCompenent`，之后由 `ModelRenderMgr`
   每帧把 `Transform.position` 同步到 `Grid.Pos`。

### 4.3 控制台命令（演示/调试）

启动游戏后（先 `/LoadWorld <name> [seed]`）：

| 命令 | 作用 |
| --- | --- |
| `/Models` 或 `/ML` | 列出已加载的外部模型、顶点/三角数、来源文件 |
| `/SpawnModel <name>` 或 `/SM <name>` | 在视线前方生成使用该外部模型的实体 |

示例输出：

```
[ImportedModelTable] 已加载外部模型 5 个：aris, color_cube, fbx_cube_ascii, fbx_cube_binary, iron_pickaxe
[资源] 已加载模型 5 个、方块 13 种、外部模型 5 个，耗时 180 ms。
...
已加载的外部模型：
  fbx_cube_ascii   顶点=   36 三角=   12 来源=fbx_cube_ascii.fbx
  fbx_cube_binary  顶点=   36 三角=   12 来源=fbx_cube_binary.fbx
  iron_pickaxe     顶点=  216 三角=   72 来源=iron_pickaxe.obj
[模型] 已生成实体网格 'fbx_cube_binary'（顶点 36），位置 (45.99, 148.21, 125.43)
```

> 提示：如果生成位置正好在山体/方块内部，模型会被遮挡；可先用 `/tp x y z` 到开阔处再生成。

---

## 5. 渲染管线改动

### 5.1 Grid 支持自带材质

| 成员 | 默认 | 说明 |
| --- | --- | --- |
| `Texture` | `null` | 为 null 时使用方块图集；外部模型绑定自己的纹理 |
| `UvScale` | 16 | 着色器 `uv / UvScale`；方块图集为 16，独立纹理为 1 |
| `ModelName` | `""` | 调试用来源名 |

`Grid.ResetForPool()` 会同时清空 `Texture` / `UvScale` / `ModelName`，避免外部模型材质
泄漏到复用的区块网格。

### 5.2 着色器按 Grid 绑定材质

`GameShader` 与 `OITShader` 的 `GetGrid(g)` 记录当前 Grid，`Draw()` 时绑定
`g.Texture ?? 方块图集`，并把 `atlasSize`（float）设为 `g.UvScale`。

**顺带修复了一个真实 bug**：原先 `atlasSize` 在 shader 中声明为 `int`，按 Grid 设置时传的是 `float`。
OpenGL 对这种类型不匹配不会报错却**不生效**，导致外部纹理一直按 `/16` 采样（采样到透明背景，模型不可见）。
现把 GLSL 中的 `atlasSize` 改为 `float`；方块图集仍为 16，外部模型为 1。已通过实际运行验证。

### 5.3 纹理上传时机

- `TableLoader.LoadAll()`（启动时、无 OpenGL 上下文）：
  只解析模型几何、校验纹理**路径是否存在**，不上传纹理；
  因此 `--validate-tables` 可以在无窗口环境运行；
- `GameRenderer.Load()`（有 GL 上下文）：
  调用 `ImportedModelTable.Ins.UploadTextures()`，统一创建 GPU 纹理；
  缺失纹理使用 1×1 白色占位纹理；相同纹理文件只上传一次；
- `UploadTextures()` 幂等；若发现没有 GL 上下文（Handle=0），会打印提示并在下次调用时重试。

### 5.4 渲染图层

外部模型生成的 Grid 是普通 `Grid`，可放在任意图层：

- `Entity`（推荐，实体模型）：不透明通道，`GameShader`；
- `default`：不透明通道；
- `lucency`：WBOIT 半透明通道，`OITShader` 同样支持自带纹理。

---

## 6. 逐文件修改点

### 6.1 新增文件

| 文件 | 说明 |
| --- | --- |
| `TextCraft/src/Tools/ModelImport/ModelImporter.cs` | 导入器接口、选项、统一网格格式、导入器注册表 |
| `TextCraft/src/Tools/ModelImport/ObjModelImporter.cs` | OBJ + MTL 解析（顶点/UV/纹理/Kd 基础色） |
| `TextCraft/src/Tools/ModelImport/FbxModelImporter.cs` | FBX 导入入口（识别二进制/ASCII） |
| `TextCraft/src/Tools/ModelImport/Fbx/FbxNode.cs` | 二进制/ASCII 共用的节点与数值/数组读取工具 |
| `TextCraft/src/Tools/ModelImport/Fbx/FbxBinaryParser.cs` | Binary FBX 7.x 解析（32/64 位节点头、deflate 数组） |
| `TextCraft/src/Tools/ModelImport/Fbx/FbxAsciiParser.cs` | ASCII FBX 解析（行/花括号） |
| `TextCraft/src/Tools/ModelImport/Fbx/FbxSceneImporter.cs` | 从节点树提取顶点/多边形/UV/材质/纹理/基础色/Model 变换 |
| `TextCraft/src/Table/ImportedModelTable.cs` | 外部模型注册表、名称查找、`TryCreateGrid`、纹理/基础色上传 |
| `RequiredResources/Config/Tables/imported_models.json` | 外部模型配置（OBJ + FBX 示例） |
| `RequiredResources/Resources/test_model/fbx/*` | FBX 测试夹具（二进制/ASCII/纹理） |
| `docs/external-model-import.md` | 本文档 |

### 6.2 修改文件

| 文件 | 修改内容 |
| --- | --- |
| `TextCraft/src/Core/Config/ResourcePaths.cs` | 新增 `ImportedModelsFile`、`ResourcesDirectory` |
| `TextCraft/src/Table/TableLoader.cs` | 加载外部模型；启动日志与 `--validate-tables` 摘要增加外部模型列表 |
| `TextCraft/src/Rendering/Texture.cs` | 抽出 `Upload` 公共流程；新增 `LoadPixels`（白色占位纹理）；`LoadFont` 复用 |
| `TextCraft/src/Rendering/Grid.cs` | 新增 `Texture` / `UvScale` / `ModelName`；`ResetForPool` 重置材质 |
| `TextCraft/src/Rendering/GameShader.cs` | 按 Grid 绑定纹理与 `atlasSize`（float） |
| `TextCraft/src/Rendering/OIT/OITShader.cs` | 同上（半透明通道） |
| `TextCraft/src/Rendering/IRenderer.cs` | `GameRenderer.Load` 中调用 `UploadTextures()` |
| `TextCraft/src/Rendering/GridMgr.cs` | 新增覆盖式 `SetChunkGrids` |
| `TextCraft/src/Core/World.cs` | 新增 `SpawnModelEntity`、订阅并处理 `SpawnModelEventArg` |
| `TextCraft/src/Core/Event/EventArg.cs` | 新增 `SpawnModelEventArg` |
| `TextCraft/src/Core/ConsoleModule/ConsoleMgr.cs` | 新增 `/Models`、`/SpawnModel` 命令与帮助文本 |
| `RequiredResources/...` 资源 | 新增 FBX 夹具与配置条目 |
| `README.md` | 特性、目录、资源说明、外部模型示例、路线图 |

### 6.3 没有改动的部分

- 方块 JSON 加载、方块表、区块/存档格式、物理系统；
- 现有 `ModelTable` / `EntityModelTable` / 方块模型格式（JSON 面数据）；
- `RequiredResources/Resources/test_model` 中已有的 OBJ 模型内容。

---

## 7. 验证记录

### 7.1 构建

```
dotnet build TextCraft\TextCraft.csproj -c Debug
已成功生成。0 个警告 0 个错误
```

### 7.2 无窗口校验

```
> TextCraft.exe --validate-tables
[ImportedModelTable] 已加载外部模型 5 个：aris, color_cube, fbx_cube_ascii, fbx_cube_binary, iron_pickaxe
[资源] 已加载模型 5 个、方块 13 种、外部模型 5 个，耗时 180 ms。
...
外部模型列表：
  aris             顶点=255663 三角=85221 纹理=调色板(37x1) 来源=aris.obj
                  包围盒 min=(-1.367, -0.990, -0.989) max=(1.367, 2.906, 0.852)
  color_cube       顶点=   36 三角=   12 纹理=调色板(6x1) 来源=color_cube.obj
                  包围盒 min=(-0.500, 0.000, -0.500) max=(0.500, 1.000, 0.500)
  fbx_cube_ascii   顶点=   36 三角=   12 纹理=fbx_cube.png 来源=fbx_cube_ascii.fbx
                  包围盒 min=(-0.500, 0.000, -0.500) max=(0.500, 1.000, 0.500)
  fbx_cube_binary  顶点=   36 三角=   12 纹理=fbx_cube.png 来源=fbx_cube_binary.fbx
                  包围盒 min=(-0.500, 0.000, -0.500) max=(0.500, 1.000, 0.500)
  iron_pickaxe     顶点=  216 三角=   72 纹理=iron_pickexe.png 来源=iron_pickaxe.obj
                  包围盒 min=(-0.433, -0.031, -0.062) max=(0.433, 0.969, 0.062)
```

FBX 解析覆盖：

- 二进制 7.4（32 位节点头、deflate 压缩数组）→ 36 顶点 / 12 三角；
- 二进制 7.5 变体（64 位节点头）→ 36 顶点 / 12 三角；
- 二进制未压缩数组变体 → 36 顶点 / 12 三角；
- ASCII 版本 → 36 顶点 / 12 三角；
- 把 ASCII 中的 `Lcl Translation` 改为 `(1,2,3)` 后，包围盒精确平移为
  `(0.5,2,2.5) ~ (1.5,3,3.5)`，验证 Model 变换生效。

补充：`test_model` 下如有其它模型文件（例如后来加入的 `aris.obj`），自动发现会一并加载。
实测 `aris.obj`（Blender 导出，44,375 顶点 / 80,793 面）解析为 **255,663 顶点 / 85,221 三角**；
其 71 个材质都没有贴图，但有 `Kd` 颜色，按规则烘焙为 **37×1 调色板**（实际用到 37 种材质）。

### 7.3 实机运行

1. 启动游戏，控制台执行 `/LoadWorld 624 6767`；
2. `/tp 45 150 124` 到空中，执行 `/Models` 与 `/SpawnModel fbx_cube_binary`；
3. 截屏确认：FBX 立方体完整渲染，六个面纹理正常，四角颜色标记与 F 形图案方向正确，
   无面缺失/内外翻转；`fbx_cube.png` 材质被正确绑定。

另做过：

- **OBJ 回归**：`/SpawnModel iron_pickaxe` 仍正常渲染（银色镐头 + 棕色手柄）；
- **白色占位纹理测试**：临时移走纹理，模型以纯白显示，证明几何/UV/材质链路正确；
- **错误注入**：未知模型名、坏 JSON、缺失纹理、不支持的扩展名均按预期报错且不崩溃；
- **自动发现**：把未显式配置的 FBX 变体放进搜索目录，`--validate-tables` 会自动列出；

### 7.4 资源同步

构建后输出目录自动包含：

```
bin\Debug\net8.0\Config\Tables\imported_models.json
bin\Debug\net8.0\Resources\test_model\iron_pickaxe.obj / .mtl / .png
bin\Debug\net8.0\Resources\test_model\fbx\fbx_cube_binary.fbx
bin\Debug\net8.0\Resources\test_model\fbx\fbx_cube_ascii.fbx
bin\Debug\net8.0\Resources\test_model\fbx\fbx_cube.png
```

（沿用上次改造的 `SyncRequiredResources` Target，无需手工复制。）

---

## 8. 回滚方案

本次改动基于已提交的 `64b8003`（JSON 方块/模型系统）。回滚只涉及代码与新增资源，
**不涉及存档迁移**。

### 方案 A：只回滚本次外部模型功能（推荐）

```powershell
# 1) 恢复本次修改的已跟踪文件
git checkout HEAD -- `
  TextCraft/src/Core/Config/ResourcePaths.cs `
  TextCraft/src/Table/TableLoader.cs `
  TextCraft/src/Rendering/Texture.cs `
  TextCraft/src/Rendering/Grid.cs `
  TextCraft/src/Rendering/GameShader.cs `
  TextCraft/src/Rendering/OIT/OITShader.cs `
  TextCraft/src/Rendering/IRenderer.cs `
  TextCraft/src/Rendering/GridMgr.cs `
  TextCraft/src/Core/World.cs `
  TextCraft/src/Core/Event/EventArg.cs `
  TextCraft/src/Core/ConsoleModule/ConsoleMgr.cs `
  README.md

# 2) 删除本次新增的源码/文档/配置
Remove-Item -Recurse -Force TextCraft/src/Tools/ModelImport
Remove-Item TextCraft/src/Table/ImportedModelTable.cs
Remove-Item RequiredResources/Config/Tables/imported_models.json
Remove-Item docs/external-model-import.md

# 3) 重新构建
dotnet build TextCraft\TextCraft.csproj -c Debug
```

说明：

- `Resources/test_model`（含 OBJ 与 FBX 夹具）可保留（旧代码不会读取），也可以一并删除；
- 回滚后恢复为“JSON 方块/模型系统”，外部模型配置不存在时不会影响启动；
- 若输出目录里还留着新构建产物，建议清理 `TextCraft\bin`、`TextCraft\obj` 后再构建。

### 方案 B：连同上一次 JSON 改造一起回滚

见 `docs/block-model-json-loading.md` 第 10 节；先完成本次方案 A，再按上次文档回滚到 `a9d1e32`。

### 回滚检查清单

- [ ] `git status` 中不再有本次源码修改；
- [ ] 启动日志不再包含 `外部模型` 相关输出；
- [ ] `--validate-tables` 恢复为“模型 5 个、方块 13 种”；
- [ ] 游戏可正常启动、`/LoadWorld` 加载世界、方块渲染正常。

上述组合（`64b8003` 源码 + 当前 RequiredResources）已实测：构建成功、游戏启动并成功执行
`/LoadWorld` 加载世界（外部模型相关文件被旧代码忽略）。

---

## 9. 已知限制与后续方向

1. **格式范围**：内置 OBJ 与 FBX；FBX 只覆盖基础 Mesh。可通过 `IModelImporter` +
   `ModelImporterRegistry` 注册更多格式（glTF 等）。
2. **FBX 忽略项**：骨骼、蒙皮、动画、Morph Target、PBR 材质参数、内嵌纹理、Pivot/PreRotation/
   PostRotation/RotationOrder、GeometricTransform、单位与坐标轴换算、相机/灯光。
3. **FBX Model 变换**：仅应用基础 `Lcl Translation` / `Lcl Rotation`（Euler，按 X→Y→Z 组合）/
   `Lcl Scaling` 及父级链；旋转顺序与 Pivot 等未完整还原。
4. **材质数量与单纹理限制**：每个 Grid 只能绑定一张纹理；OBJ 纯色 MTL 会生成 1×N 调色板（每个材质一个像素），多材质纯色模型可用；但“纹理 + 纯色”混合材质仍只取第一个纹理，多纹理模型只取第一张。
5. **OBJ/MTL**：MTL 只识别 `newmtl` / `map_Kd` 的简单形式，不支持 `-s`、`-o` 等纹理选项。
6. **动画**：不支持（OBJ/FBX 的动画数据均忽略）。
7. **物理**：外部模型实体目前只有渲染网格，没有碰撞体；需要碰撞时可添加 `Box`/`RigidBody`。
8. **纹理过滤**：外部纹理使用 NEAREST（放大）与 NEAREST_MIPMAP_LINEAR（缩小），适合像素风；
   写实模型可能需要线性/各向异性过滤，后续可做成模型级配置。
9. **纹理尺寸/环绕**：外部纹理使用 REPEAT 环绕；UV 超出 [0,1] 时会重复采样。
10. **性能**：每个外部模型实体使用独立 Grid，未做合批/LOD；大量实体时需要进一步优化。

---

## 附录 A：测试模型

### OBJ

| 项 | 值 |
| --- | --- |
| 来源 | Blockbench 5.2.2 导出 |
| OBJ | `Resources/test_model/iron_pickaxe.obj`（48 v / 144 vt / 36 vn / 36 四边形） |
| MTL | `Resources/test_model/iron_pickaxe.mtl` |
| 纹理 | `Resources/test_model/iron_pickexe.png`（32×32，RGBA） |
| 转换结果 | 216 顶点 / 72 三角形 |

### OBJ 纯色 MTL（本次生成）

| 项 | 值 |
| --- | --- |
| OBJ/MTL | `Resources/test_model/color_cube.obj` / `.mtl` |
| 材质 | 6 个纯色材质（红/绿/蓝/黄/品红/青），没有 `map_Kd` |
| 转换结果 | 36 顶点 / 12 三角形，生成 `调色板(6x1)` |
### FBX（本次生成的夹具）

| 项 | 值 |
| --- | --- |
| 二进制 | `Resources/test_model/fbx/fbx_cube_binary.fbx`（FBX 7.4，deflate 数组） |
| ASCII | `Resources/test_model/fbx/fbx_cube_ascii.fbx` |
| 纹理 | `Resources/test_model/fbx/fbx_cube.png`（16×16，F 形 + 四角颜色标记） |
| 网格 | 8 顶点、6 四边形、1 套 UV、1 材质 |
| 转换结果 | 36 顶点 / 12 三角形，包围盒 `(-0.5,0,-0.5) ~ (0.5,1,0.5)` |
| 本地额外验证 | 7.5 的 64 位节点头变体、未压缩数组变体（未随仓库提供） |

## 附录 B：本次文件变更清单

**新增**

- `TextCraft/src/Tools/ModelImport/ModelImporter.cs`
- `TextCraft/src/Tools/ModelImport/ObjModelImporter.cs`
- `TextCraft/src/Tools/ModelImport/FbxModelImporter.cs`
- `TextCraft/src/Tools/ModelImport/Fbx/FbxNode.cs`
- `TextCraft/src/Tools/ModelImport/Fbx/FbxBinaryParser.cs`
- `TextCraft/src/Tools/ModelImport/Fbx/FbxAsciiParser.cs`
- `TextCraft/src/Tools/ModelImport/Fbx/FbxSceneImporter.cs`
- `TextCraft/src/Table/ImportedModelTable.cs`
- `RequiredResources/Config/Tables/imported_models.json`
- `RequiredResources/Resources/test_model/fbx/fbx_cube_binary.fbx`
- `RequiredResources/Resources/test_model/fbx/fbx_cube_ascii.fbx`
- `RequiredResources/Resources/test_model/fbx/fbx_cube.png`
- `RequiredResources/Resources/test_model/color_cube.obj`
- `RequiredResources/Resources/test_model/color_cube.mtl`
- `docs/external-model-import.md`

**修改**

- `TextCraft/src/Core/Config/ResourcePaths.cs`
- `TextCraft/src/Table/TableLoader.cs`
- `TextCraft/src/Rendering/Texture.cs`
- `TextCraft/src/Rendering/Grid.cs`
- `TextCraft/src/Rendering/GameShader.cs`
- `TextCraft/src/Rendering/OIT/OITShader.cs`
- `TextCraft/src/Rendering/IRenderer.cs`
- `TextCraft/src/Rendering/GridMgr.cs`
- `TextCraft/src/Core/World.cs`
- `TextCraft/src/Core/Event/EventArg.cs`
- `TextCraft/src/Core/ConsoleModule/ConsoleMgr.cs`
- `README.md`

## 附录 C：常用命令

```powershell
# 构建（自动同步 RequiredResources）
dotnet build TextCraft\TextCraft.csproj -c Debug

# 无窗口校验方块/模型/外部模型
dotnet run --project TextCraft\TextCraft.csproj -- --validate-tables

# 运行游戏后，在控制台输入：
#   /LoadWorld 624 6767
#   /tp 45 150 124
#   /Models
#   /SpawnModel fbx_cube_binary
#   /SpawnModel iron_pickaxe
```
