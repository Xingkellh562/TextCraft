# TextCraft 方块模型与方块元数据 JSON 化改造说明

> 基线版本：`a9d1e32`（“渲染帧速与逻辑帧速分离”，改造开始时工作区干净）
>
> 改造主题：用 JSON 资源替换 `ModelTable` / `BlockTable` 中的硬编码表，统一资源路径、增加校验与错误提示，并让构建自动同步 `RequiredResources` 到输出目录。
>
> 本文档同时作为**修改点清单**与**回滚手册**。改造没有提交到 git，所有改动都在工作区中，可用 `git diff` / `git status` 完整审阅。

---

## 1. 目标与结论

| 目标 | 结果 |
| --- | --- |
| 模型不再硬编码在 C# 字段里 | ✅ `ModelTable.cube / cube15pxH / step / stair_posX` 字段已删除，模型来自 `Config/Tables/models/*.json` |
| 方块种类与元数据不再硬编码 / 反序列化 XML | ✅ 方块来自 `Config/Tables/blocks.json`，旧的 `BlockConfig` + `XmlSerializer` 路径已移除 |
| 资源有明确、可扩展的存放位置 | ✅ 新增 `ResourcePaths` 统一路径；模型一个文件一个模型，方块一个索引文件 |
| 修改 RequiredResources 后同步到输出目录 | ✅ `TextCraft.csproj` 增加构建/发布同步 Target，增量复制；示例存档只在输出目录没有 `Save` 时复制 |
| 数据可校验、错误可定位 | ✅ 加载器聚合校验错误（文件、方块、模型、面数、版本、重复键等），一次性抛出 `TableLoadException` |
| 存档兼容 | ✅ 方块键仍为 `id * 16 + type`，存档二进制格式未变，旧存档可直接读取 |
| 代码可回滚 | ✅ 保留旧 XML/旧格式 JSON 作为回滚资源；回滚步骤与两种回滚方式见第 10 节，且已实测旧代码可运行 |
| 运行可验证 | ✅ 新增无窗口校验模式 `--validate-tables`；已做逐位数据对比、错误注入、真实运行与截图验证 |

构建结果：`dotnet build` **0 warning / 0 error**。

---

## 2. 改造前的问题

1. **模型硬编码**：`ModelTable` 用 `readonly Model cube = new Model(new float[][](...), "cube")` 这类大段字面量定义 4 个模型，修改模型必须改代码并重新编译。
2. **方块表一半硬编码**：`BlockTable` 构造函数里保留了整段注释掉的 `AddBlock(...)` 硬编码初始化，真正的数据又放在 `Config/Tables/blockTable.xml` 里用 `XmlSerializer` 读取，形成两套来源。
3. **干净环境必崩**：`ModelTable` 构造函数直接 `File.ReadAllText("Config\\Tables\\modelTable.json")`，而该文件**不在** `RequiredResources` 中（只在开发者本机的 `bin` 里），新克隆的仓库按 README 复制资源后依然会在加载世界时抛 `FileNotFoundException`。
4. **静默容错**：`ModelTable[name]` 找不到模型时返回空 `Model`，方块表继续加载，最终表现为“方块存在但不可见”，很难排查。
5. **附加面只支持一个**：`GridGenerator` 只处理 `Model[6]`，模型第 8 个及以后的面会被静默丢弃；`step` / `stair_posX` 这种模型很容易踩坑。
6. **热路径重复查表**：网格构建时对同一个方块反复调用 `BlockTable.Ins[block]`（每个面、每个顶点都要查一次字典）。
7. **资源部署全手工**：README 要求手工复制整个 `RequiredResources`，容易漏文件；本改造前就存在漏 `modelTable.json` 的问题。
8. **路径拼接风格不统一**：`AppContext.BaseDirectory + "Config\\Tables\\..."` 使用反斜杠拼接，依赖 Windows 且不易集中修改。
9. **验证时发现的存量问题**：`RequiredResources` 的 UI 图集 `spirits.xml` 缺少 `UITable` 启动时必需的 `black` 精灵，干净构建会在 UI 初始化阶段抛 `KeyNotFoundException`（见 6.10）。

---

## 3. 改造后的加载流程

```
Program.Main
 ├─ ConfigMgr.Ins.OnLoad(ResourcePaths.GameConfigFile)      // Config/config.xml
 ├─ TableLoader.LoadAll()                                   // 数据表统一入口（幂等）
 │   ├─ ModelTable.Ins.Load(ResourcePaths.ModelsDirectory)   // Config/Tables/models
 │   │    └─ 扫描 *.json → 解析（对象或数组）→ 校验 → 注册名字（忽略大小写）
 │   └─ BlockTable.Ins.Load(ResourcePaths.BlocksFile, ModelTable.Ins)
 │        ├─ 解析 blocks.json → 校验（聚合所有错误）
 │        ├─ 按 model 名取模型 → ModelCreator 烘焙 UV（tile 偏移 + 0.4375 内缩）
 │        └─ 写入 ConcurrentDictionary<int, Block>，键 = id * 16 + type
 └─ 若带 --validate-tables：打印表摘要后退出；否则创建窗口进入游戏
```

启动时先加载数据表、再开窗口：资源缺失或 JSON 写错时会在控制台给出明确原因并以退出码 1 结束，而不是在加载世界时随机崩溃。

---

## 4. 资源目录

```
RequiredResources/
├─ Config/
│  ├─ config.xml
│  └─ Tables/
│     ├─ blocks.json              ← 新增：方块定义（唯一数据源）
│     ├─ models/                  ← 新增：模型定义目录
│     │  ├─ cube.json
│     │  ├─ cube15pxH.json
│     │  ├─ step.json
│     │  ├─ stair_posX.json
│     │  └─ textEntity.json       ← 实体模型（原 EntityModelTable 硬编码）
│     ├─ blockTable.xml           ← 旧版方块表，仅保留给回滚/旧代码，新代码不读取
│     └─ modelTable.json          ← 旧格式模型表，仅保留给回滚/旧代码，新代码不读取
├─ Resources/                     ← 纹理、字体、UI 图集（结构未变）
└─ Save/624/                      ← 示例存档（结构未变）
```

说明：

- **新代码只读取 `blocks.json` 与 `models/`**。`blocks.json` 与 `models/` 是本次改造的单一数据源。
- `blockTable.xml`、`modelTable.json` 是**回滚兼容资源**。保留它们后，只回滚 `TextCraft/src` 下的代码即可回到旧的 XML 加载逻辑并直接运行（见第 10 节，已实测）。
- 原 `modelTable.xml`（28KB，无 `<Name>` 节点、内容已过期）没有保留价值，已删除。
- 模型文件采用“一个文件一个模型”的形式，避免每次改模型都动一个几百行的大 JSON，也方便多人并行修改。

---

## 5. JSON 规范

### 5.1 模型文件（`models/*.json`）

- 根节点可以是**单个模型对象**，也可以是**模型数组**（兼容旧的单文件格式）。
- 单个模型文件可以省略 `name`，此时使用文件名（不含扩展名）作为模型名。
- 模型名不区分大小写；重名会报错并指出两个来源文件。

```json
{
  "name": "cube",
  "faces": [
    [1,0,1,0,1,0, 1,0,0,1,1,0, 1,1,0,1,0,0, 1,0,1,0,1,0, 1,1,0,1,0,0, 1,1,1,0,0,0],
    [ ... ],
    [ ... ],
    [ ... ],
    [ ... ],
    [ ... ]
  ]
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `name` | string，可省略 | 模型名，方块通过它引用；单模型文件省略时取文件名 |
| `faces` | `float[][]` | 每个元素是一个“面”的扁平顶点数组 |

**面的顺序**（与旧实现一致）：

| 索引 | 方向 | 是否参与相邻面剔除 |
| --- | --- | --- |
| 0 | +X（右） | 是 |
| 1 | -X（左） | 是 |
| 2 | +Y（上） | 是 |
| 3 | -Y（下） | 是 |
| 4 | +Z（前） | 是 |
| 5 | -Z（后） | 是 |
| 6 及以后 | 自定义附加面 | 否，始终绘制 |

**顶点数据布局**：每个面由若干个 6 float 的顶点组成，一个四边形通常是 2 个三角形 = 6 个顶点 = 36 个 float。

| 偏移 | 含义 |
| --- | --- |
| `+0` | x |
| `+1` | y |
| `+2` | z |
| `+3` | u（图集 tile 内 U） |
| `+4` | v（图集 tile 内 V） |
| `+5` | ao（占位值；实际 AO 由网格构建时重新计算） |

- 面的顶点数必须是 3 的倍数（构成三角形），实际数据均为 36 或 72。
- 空的 `faces[i] = []` 表示该方向没有几何体（例如 `step` 的第 2 个面）。
- `u/v` 加载时会经过 `ModelCreator` 处理：值为 0 的加 `0.4375`、其余减 `0.4375`（对应图集 padding 的内缩），再叠加方块配置的 tile 偏移。除 0/1 外，也允许 `0.5`、`0.9375` 这类“纹理比例”写法（现有 `step` / `cube15pxH` 就是这么写的），加载器只对超出 `[0,1]` 的值给出警告。
- 支持 JSON 注释（`//`、`/* */`）与尾逗号，方便手写和调试。

### 5.2 方块文件（`blocks.json`）

根节点为对象（推荐，带版本号），也兼容根数组：

```json
{
  "version": 1,
  "blocks": [
    { "id": 0, "name": "air", "blockType": "Air" },
    {
      "id": 2,
      "name": "grass",
      "blockType": "Solid",
      "model": "cube",
      "tiles": [[3,0], [3,0], [2,0], [1,0], [3,0], [3,0]]
    },
    {
      "id": 6,
      "type": 1,
      "name": "log",
      "blockType": "Solid",
      "model": "cube",
      "tiles": [[6,0], [6,0], [7,0], [7,0], [7,0], [7,0]]
    },
    {
      "id": 9,
      "name": "water",
      "blockType": "Liquid",
      "model": "cube15pxH",
      "tiles": [[1,3], [1,3], [0,3], [1,3], [1,3], [1,3]],
      "isLucency": true
    }
  ]
}
```

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `id` | int | 必填 | 0..65535。方块键 = `id * 16 + type` |
| `type` | int | 0 | 0..15。用于同一方块的朝向/状态变体（例如 log 的 3 种朝向） |
| `name` | string | 必填 | 方块名，仅用于日志/调试/导出 |
| `blockType` | string | `"Solid"` | `Air` / `Solid` / `Liquid` / `IncompleteBlock`（不区分大小写，也接受 `Incomplete` 别名） |
| `model` | string | 无 | 引用 `models/` 中的模型名。**非 Air 方块必填**，且模型至少有 6 个面 |
| `tiles` | `int[][]` | `[[0,0]]` | 按面给出图集 tile 坐标 `[u, v]`；数量少于面数时**自动重复最后一项**，多于面数时忽略多余项并警告 |
| `isLucency` | bool | `false` | 是否走半透明渲染通道 |
| `damp` | `float[3]` | `[0.1,0.02,0.1]` | 移动阻尼，可选 |

关于 `tiles`：

- 图集为 16×16 个 tile（`GameShader` 中 `atlasSize = 16`），`u` 是列、`v` 是行，从左上角 0 开始。
- 只写一项（如 `"tiles": [[1,1]]`）时，所有面都使用该 tile。
- 按 6 个面分别写时，顺序与模型的面顺序一致（+X、-X、+Y、-Y、+Z、-Z）。
- 模型有附加面（第 7 个面起）时，可用第 7 项单独指定附加面的 tile；不写则沿用最后一项。

### 5.3 校验与错误处理

加载器会**先收集全部错误再一次性抛出**，避免改一处、跑一次只看到一个错误。校验项：

模型表：

- 文件不存在 / 目录下没有 `*.json`；
- JSON 解析失败（含行列号）；
- 根节点既不是对象也不是数组；
- 模型缺少名字（多模型文件必须命名）；
- 模型没有任何面；
- 单个面的 float 数不是 6 的倍数；
- 面数据包含 `NaN` / `Infinity`；
- 模型重名；
- UV 超出 `[0,1]`（只警告，不中断）。

方块表：

- 文件不存在 / JSON 解析失败；
- `version` 不是 1；
- 方块列表为空；
- `id` 超出 0..65535、`type` 超出 0..15；
- `name` 为空；
- `blockType` 非法（错误信息会列出可选值）；
- 同一 `id + type` 重复定义；
- 引用了不存在的模型（错误信息会列出当前可用模型；若模型表未加载会明确提示）；
- 非 Air 方块没有 `model`，或模型的面数少于 6；
- `tiles` 项不是 `[u, v]` 两个整数，或超出 0..255；
- `damp` 不是 3 个有限数值；
- 缺少 `id=0, type=0` 的 Air 方块；
- `id=0` 的方块类型不是 Air（警告）。

错误示例（注入错误后的真实输出）：

```
方块/模型资源加载失败：
方块表加载失败: D:\...\Config\Tables\blocks.json
  - 方块 #1:0 'stone' 引用了不存在的模型 'no_such_model'（可用模型: cube, cube15pxH, stair_posX, step, textEntity）
  - 方块 #2:0 'grass' 引用了不存在的模型 'no_such_model'（...）
如果这是首次运行，请确认 RequiredResources 目录已同步到程序输出目录（正常构建会自动同步）。
```

---

## 6. 代码修改点（逐文件）

### 6.1 `TextCraft/src/Core/Config/ResourcePaths.cs`（新增）

统一登记运行期数据文件路径（全部基于 `AppContext.BaseDirectory`、用 `Path.Combine` 组装）：

- `ConfigDirectory`、`GameConfigFile`
- `TablesDirectory`、`BlocksFile`、`ModelsDirectory`

后续新增资源路径应优先加在这里，避免业务代码散落字符串拼接。

### 6.2 `TextCraft/src/Table/TableLoadException.cs`（新增）

数据表加载专用异常，消息中带文件/方块/模型上下文，供 `Program` 统一捕获并给出部署提示。

### 6.3 `TextCraft/src/Table/TableLoader.cs`（新增）

- `LoadAll()`：先 `ModelTable` 后 `BlockTable`（方块按名字引用模型），幂等；
- 启动时打印“模型 N 个、方块 M 种、耗时 X ms”；
- `PrintSummary()`：供 `--validate-tables` 列出所有模型与方块。

### 6.4 `TextCraft/src/Table/ModelTable.cs`（重写）

- **删除** 4 个硬编码 `Model` 字段（`cube`、`cube15pxH`、`step`、`stair_posX`），数据迁移到 `models/*.json`；
- `Load(path)` 支持**目录**（递归扫描 `*.json`，按文件名排序保证确定性）或**单个文件**；
- 单个文件可以是模型对象，也可以是模型数组（兼容旧格式）；
- 单模型文件省略 `name` 时用文件名；
- 校验并聚合错误（见 5.3），失败时**不破坏**已加载数据；
- 模型名不区分大小写；`TryGet` / `Contains` / `Models` / `Count` / `IsLoaded` 等查询接口；
- `Save(path)` 保留（导出为旧版单文件数组格式），用于工具/备份；
- `JaggedArrayConverter<T>` 保留，保证内层数组单行输出。

### 6.5 `TextCraft/src/Table/BlockTable.cs`（重写）

- 新增 JSON DTO：`BlockDefinition`（`id/type/name/blockType/model/tiles/isLucency/damp`）与 `BlockTableFile`（`version/blocks`）；
- **删除** 注释掉的硬编码 `AddBlock` 初始化、`BlockConfig`、XML `Load/Save`、`AddBlock(...)` 重载；
- `Load(path)` / `Load(path, ModelTable)`：解析 → 校验（聚合）→ 通过后才替换现有数据；
- 方块键保持 `id * 16 + type`；索引器回退语义保持不变：精确键 → 同 id 的 type 0 → 空气；
- 新增 `MaxPlaceableId`：由数据推导快捷栏上限（`type == 0` 且非 Air 的最大 id），替换 `Program.OnMouseWheel` 里硬编码的 `1..14`；
- 新增 `Definitions` / `Count` / `IsLoaded` / `TryGet` / `Clear`；
- `Save(path)` 保留（导出当前方块定义为 JSON，便于工具生成/备份）；
- 修正旧实现的 `Block.Type` 未赋值问题（旧 XML 路径下 log 的 type 变体 `Type` 永远是 0，虽然当前没有代码读取该字段）。

### 6.6 `TextCraft/src/Table/EntityModelTable.cs`（重写）

- 删除 100+ 行硬编码实体模型；
- `model1` 改为属性，从 `ModelTable.Ins["textEntity"]` 取模型；
- 模型缺失时打印一次警告并返回空模型（实体不可见但不会崩溃）；
- `World.cs` 中 `EntityModelTable.model1.faces` 的调用点无需改动。

### 6.7 `TextCraft/src/Tools/ModelCreator.cs`（修改）

- 保持 `CreateModel(Model, int[] u, int[] v)` 签名与逐位行为不变；
- 增加空模型/空面保护（`u`/`v` 为空时不再触发 `u[^1]` 越界）；
- tile 索引提取到顶点循环外（每个面只算一次）；
- 补充注释，说明 0.4375 内缩与 tile 偏移的含义。

### 6.8 `TextCraft/src/Tools/GridGenerator.cs`（修改）

- 网格构建热路径中缓存 `BlockTable.Ins[...]` 的查询结果（原实现每个面、每个顶点都查字典）；
- 附加面从“只处理 `Model[6]`”改为“处理第 7 个面及之后的所有面”，并跳过空面；
- 前 6 个面仍参与相邻面剔除，行为与旧版一致（对现有 13 种方块逐位验证通过）。

### 6.9 `TextCraft/src/Core/Program.cs`（修改）

- `Main` 改为：
  1. `ConfigMgr.Ins.OnLoad(ResourcePaths.GameConfigFile)`；
  2. `TableLoader.LoadAll()`，捕获 `TableLoadException` 并输出部署提示、`Environment.ExitCode = 1`；
  3. 支持 `--validate-tables`：不创建窗口，打印模型/方块摘要后退出（便于改资源后自检、可用于 CI）；
  4. 正常进入游戏；
- 删除原先注释掉的 `ModelTable.Ins.Save(...)` 调试代码；
- 鼠标滚轮选择方块的上限改为 `BlockTable.Ins.MaxPlaceableId`，图集精灵查找改用容错的 `GetSpirit`。

### 6.10 `TextCraft/src/Rendering/Atlas.cs` + `TextCraft/src/UI/UITable.cs`（辅助修复）

验证时发现：`RequiredResources` 自带的 `uiAtlas/spirits.xml` **缺少 `black` 精灵**，而 `UITable.LoadUI()` 在窗口初始化时必须读取它，导致“干净构建 + 按 README 复制资源”必然在 UI 初始化阶段抛 `KeyNotFoundException`（这是本次改造前就存在的问题，与方块加载无关）。

处理：

- `Atlas.GetSpirit(name)`：缺失时返回零尺寸矩形、只警告一次，不再抛异常；
- `UITable` 的 5 处精灵查找改用 `GetSpirit`；
- `RequiredResources/Resources/atlas/uiAtlas/spirits.xml` 补一个 `black` 精灵，指向图集中已有的不透明黑色像素 `(0,64)` 的 1×1 区域（实际用途是加载遮罩/进度条底色，1×1 拉伸后为纯黑）；
- 快捷栏图标仍使用图集中的 `0`..`10` 号精灵，超出范围的方块会回退为空矩形并警告。

> 说明：`black` 精灵的矩形是按图集内容重建的最小可用值，不是开发者本机版本的原值（该版本未入库）。如需更好的视觉效果，可直接修改这个矩形。

### 6.11 `TextCraft/TextCraft.csproj`（修改）

新增构建/发布资源同步（详见第 7 节）。

### 6.12 `RequiredResources` 变更

| 文件 | 变更 |
| --- | --- |
| `Config/Tables/blocks.json` | 新增：13 种方块定义（由原 `blockTable.xml` 逐项迁移） |
| `Config/Tables/models/*.json` | 新增：5 个模型（4 个方块模型 + 1 个实体模型），由原硬编码数据逐位导出 |
| `Config/Tables/modelTable.json` | 新增：旧格式模型表，仅用于回滚/旧代码 |
| `Config/Tables/blockTable.xml` | 保留（未修改）：仅用于回滚/旧代码 |
| `Config/Tables/modelTable.xml` | **删除**：内容已过期（无 `<Name>`）且无代码再读取 |
| `Resources/atlas/uiAtlas/spirits.xml` | 新增 `black` 精灵（见 6.10） |

### 6.13 `README.md`（修改）

更新特性、目录结构、安装运行说明（不再需要手工复制资源）、数据驱动说明与文档入口。

---

## 7. 构建期资源同步

`TextCraft.csproj` 末尾新增两个 Target：

| Target | 触发 | 行为 |
| --- | --- | --- |
| `SyncRequiredResources` | `AfterTargets="Build"` | 把 `RequiredResources\Config`、`Resources`（及其它非 `Save` 内容）增量复制到 `$(OutDir)`，保留相对目录结构 |
| `SyncRequiredResourcesToPublish` | `AfterTargets="Publish"` | 同上，目标为 `$(PublishDir)` |

细节：

- 使用 `SkipUnchangedFiles="true"`，只复制新增/变化的文件；
- `RequiredResources\Save\**` 不参与常规同步，只在**输出目录还没有 `Save` 文件夹**时复制示例存档，避免覆盖游戏内产生的存档；
- 可用 `-p:SkipRequiredResourcesSync=true` 关闭同步（例如想测试“资源未部署”的报错路径）：
  ```powershell
  dotnet build TextCraft\TextCraft.csproj -p:SkipRequiredResourcesSync=true
  ```
- 已实测：
  - 修改 `RequiredResources/Config/Tables/blocks.json` 后重新构建，输出目录中的文件哈希与源文件一致；
  - 在输出目录 `Save` 下放标记文件、修改源资源后重新构建，标记文件仍在，示例存档未被覆盖；
  - `dotnet publish` 后发布目录包含 `Config`、`Resources`、`Save`，发布产物可直接运行。

---

## 8. 兼容性说明

**保持不变：**

- 方块键编码：`id * 16 + type`；`Chunk` / `ChunkStorage` / `PhysicSystem` / `Radial` / `InputMgr` 等无需改动；
- 存档格式：`.bin` 与 `worldData.xml` 未改动，仅存方块键，不含任何表数据；
- `Block`、`BlockType`、`Model`、`QuadFace`、`ModelCreator.CreateModel`、`ModelTable.Ins[...]`、`BlockTable.Ins[...]` 等对外接口保留；
- 方块 ID 与旧 `blockTable.xml` 完全一致（13 项逐位比对通过）；
- 模型几何逐位一致（4 个方块模型 + 实体模型）；
- 半透明、AO、面剔除、UV 内缩逻辑未改。

**行为差异（有意为之）：**

1. `air` 方块不再挂一个“空气立方体”模型（旧 XML 里 air 也配置了 cube 模型，但永远不会被渲染）。现在 air 的 `Model` 为空数组，渲染路径本来就会跳过 Air。
2. `Block.Type` 现在按定义正确赋值。旧 XML 加载路径遗漏了该字段，导致 log 的 type 1/2 变体 `Type` 都为 0；当前没有代码依赖 `Block.Type`，属于顺手修正。
3. `ModelTable` / `BlockTable` 不再“首次访问时静默读文件”：由 `TableLoader.LoadAll()` 在启动时显式加载，失败立即报错退出。
4. `ModelTable[未知名字]` 仍返回空模型（保持旧接口容错），但方块表会在加载阶段把“引用不存在模型”视为错误，不再静默生成不可见方块。
5. 鼠标滚轮可选方块范围由数据推导（当前数据为 1..10），不再固定 1..14。

**已移除的 API（仅原文件内部使用）：**

- `BlockConfig` 类、`BlockTable.AddBlock(int, int, string, BlockType, QuadFace[], bool)`、`BlockTable.AddBlock(BlockConfig)`、XML 版 `BlockTable.Load/Save`；
- `ModelTable` 的 4 个公开只读模型字段；
- `EntityModelTable.model1` 由 `static readonly` 字段变为只读属性（调用写法不变）。

---

## 9. 验证记录

### 9.1 构建

```
dotnet build TextCraft\TextCraft.csproj -c Debug
已成功生成。0 个警告 0 个错误
```

### 9.2 数据逐位对比（迁移正确性）

用一个一次性校验工具（临时工程，未入库）：

1. 在改造**前**的代码上，把 `ModelTable` 硬编码字段、`EntityModelTable.model1`、`BlockTable` 从 XML 加载出的 13 个方块（含烘焙后的 QuadFace 顶点、damp、isLucency、索引器回退结果）导出为“按 float 位模式（`BitConverter.SingleToInt32Bits`）编码”的快照；
2. 改造后用新加载器读取 `models/` 与 `blocks.json`，生成同样格式的快照并逐行比较。

结果：

- 5 个模型（cube、cube15pxH、step、stair_posX、textEntity）的所有面 **逐位一致**；
- 13 个方块的烘焙模型、damp、isLucency、索引器回退（包含 `0/1/2/3/6/16/17/32/33/48/96/97/112/144/145/160/9999/-1` 等探测键）**逐位一致**；
- 仅有的两处差异正是 8 节列出的有意变更：`air` 的模型由 6 个面变为空、log type 1/2 的 `Block.Type` 由 0 变为 1/2。

另外验证：

- 模型“目录形式”与“单文件数组形式”加载结果完全一致；
- `ModelTable.Save` → `Load`、`BlockTable.Save` → `Load` 往返后数据一致。

### 9.3 无窗口校验

```
> TextCraft.exe --validate-tables
[资源] 已加载模型 5 个、方块 13 种，耗时 39 ms。
模型列表：
  cube             面数=6
  cube15pxH        面数=6
  stair_posX       面数=7
  step             面数=7
  textEntity       面数=12
方块列表：
    0:0  air          Air              model=-
    1:0  stone        Solid            model=cube
    ...
   10:0  leave        Solid            model=cube
```

### 9.4 错误注入

| 场景 | 结果 |
| --- | --- |
| 删除 `blocks.json` | 报“方块配置文件不存在”，退出码 1 |
| 把 `model` 改成不存在的名字 | 聚合列出所有引用错误的方块，并给出可用模型列表，退出码 1 |
| 写入非法 JSON | 报 JSON 解析失败与行列号，退出码 1 |
| 缺少模型目录 | 报“模型目录/文件不存在” |

### 9.5 实际运行

- `TextCraft.exe` + 控制台 `/LoadWorld 624 6767` 运行 18 秒，无 stderr 输出、无异常退出；
- 截屏确认：草地/泥土/石头纹理、AO、方块选择面板、准星、FPS 文本均正常渲染（JSON 模型的几何与 UV 正确）；
- 构建输出目录中的资源由 csproj 自动同步，无需手工复制。

### 9.6 回滚可行性（实测）

用 `git archive HEAD` 导出改造前的源码，套用**当前**的 `RequiredResources`（含保留的 `blockTable.xml`、`modelTable.json`、补全 `black` 精灵的 `spirits.xml`）后构建并运行：

- 编译成功；
- 启动后无异常，控制台 `/LoadWorld 624 6767` 成功加载世界；
- 旧 XML 加载器正常打印 13 个方块名。

说明“只回滚代码、保留新资源”的方式可用，见第 10 节方案 A。

---

## 10. 回滚方案

改造没有做 git commit，所有改动都在工作区。两种回滚方式都**不涉及存档迁移**，回滚前建议先 `git stash`/提交/打 tag 留存现场。

### 方案 A：只回滚代码，保留新的 JSON 资源（推荐，用于 A/B 对比）

保留 `RequiredResources` 下本次新增的 JSON 与旧格式文件（`modelTable.json` / `blockTable.xml` 是旧代码运行所需的），只把源码恢复到改造前：

```powershell
# 1) 恢复 TextCraft/src 下所有被修改的已跟踪源码（BlockTable/ModelTable/Program/GridGenerator…）
git checkout HEAD -- TextCraft/src

# 2) 删除本次新增的源码文件（TableLoader 调用了新 BlockTable 的重载，不删会编译失败）
Remove-Item TextCraft\src\Core\Config\ResourcePaths.cs
Remove-Item TextCraft\src\Table\TableLoader.cs
Remove-Item TextCraft\src\Table\TableLoadException.cs

# 3) 保留以下资源（旧代码读取 XML 与旧格式 JSON，其余文件旧代码会忽略）
#    RequiredResources\Config\Tables\blockTable.xml    （旧代码读取）
#    RequiredResources\Config\Tables\modelTable.json   （旧代码读取）
#    RequiredResources\Config\Tables\blocks.json       （旧代码忽略）
#    RequiredResources\Config\Tables\models\           （旧代码忽略）
#    RequiredResources\Resources\atlas\uiAtlas\spirits.xml 的 black 精灵（旧代码也需要，务必保留）
#
#    同时保留 TextCraft/TextCraft.csproj：它只负责把 RequiredResources 同步到输出目录，
#    与旧代码兼容，可继续免去手工复制。

# 4) 重新构建（构建期资源同步仍会把 RequiredResources 复制到输出目录）
dotnet build TextCraft\TextCraft.csproj -c Debug
```

回滚后代码回到 XML + `modelTable.json` 加载逻辑，方块数量、ID、渲染结果与改造前一致。
该组合（改造前源码 + 当前 RequiredResources）已在临时目录中实测：编译成功、启动正常、`/LoadWorld 624 6767` 可加载世界。

> 想同时回滚文档时，再执行：`git checkout HEAD -- README.md` 并删除 `docs\block-model-json-loading.md`。

### 方案 B：完全回到改造前状态

```powershell
# 1) 恢复所有已跟踪文件（含 csproj、README、spirits.xml，并恢复被删除的 modelTable.xml）
git checkout -- .

# 2) 删除本次新增的未跟踪文件/目录
Remove-Item -Recurse -Force RequiredResources\Config\Tables\models
Remove-Item RequiredResources\Config\Tables\blocks.json
Remove-Item RequiredResources\Config\Tables\modelTable.json
Remove-Item TextCraft\src\Core\Config\ResourcePaths.cs
Remove-Item TextCraft\src\Table\TableLoader.cs
Remove-Item TextCraft\src\Table\TableLoadException.cs
Remove-Item -Recurse -Force docs

# 3) 重新构建（旧 csproj 没有自动同步，需要手工复制资源）
dotnet build TextCraft\TextCraft.csproj -c Debug
Copy-Item -Recurse -Force RequiredResources\* TextCraft\bin\Debug\net8.0\
```

此时仓库回到基线 `a9d1e32` 的状态。注意：该状态下 `ModelTable` 仍要求 `Config\Tables\modelTable.json`，而该文件从未入库；如完全删除了它，旧代码会复现改造前的 `FileNotFoundException`。若需要旧代码可运行，可保留方案 A 中提到的 `modelTable.json`（或用附录 C 的命令重新生成），并保留 `spirits.xml` 的 `black` 精灵。

### 回滚检查清单

- [ ] `git status` 中不再有本次的源码修改；
- [ ] 输出目录存在 `Config\Tables\blockTable.xml` 与 `Config\Tables\modelTable.json`；
- [ ] 运行游戏，控制台启动时不再打印 `[资源] 已加载…`（旧代码没有该输出）；
- [ ] `/LoadWorld` 能正常加载，方块贴图/几何正常。

---
## 11. 已知限制与后续方向

1. **快捷栏图标仍依赖 UI 图集**：方块数量超过图集精灵数量时，缺失图标会回退为空矩形并警告；要完善需要给方块增加 `icon` 字段并扩展 UI 图集。
2. **多图集未接入**：`Resources` 下还有 `blockatlas2.png`、`blockatlas3.png`，但当前渲染只绑定 `blockatlas1.png`；`tiles` 也没有“图集编号”字段。后续可在 `blocks.json` 增加 `atlas` 字段并让渲染器管理多张纹理。
3. **`BiomeTable` 仍硬编码方块 id**（`2*16`、`4*16` 等），可继续用 JSON 化（`biomes.json`，引用方块名）。
4. **模型格式仍是扁平顶点数组**：可读性一般，后续可增加盒式/元素式 DSL 编译到现有面格式，或提供模型编辑/导出工具（`Save()` 已保留）。
5. **`TeatureTable` 仍是空壳**，与本次改造无关。
6. **UI `black` 精灵为重建值**（见 6.10），只保证“不崩溃 + 纯黑”，未还原开发者本机版本的原始尺寸/渐变。

---

## 附录 A：当前方块表

面顺序：0 +X、1 -X、2 +Y、3 -Y、4 +Z、5 -Z。

| id | type | name | blockType | model | tiles (u,v) |
| --- | --- | --- | --- | --- | --- |
| 0 | 0 | air | Air | - | - |
| 1 | 0 | stone | Solid | cube | (1,1) |
| 2 | 0 | grass | Solid | cube | 3,0 / 3,0 / 2,0 / 1,0 / 3,0 / 3,0 |
| 3 | 0 | sand | Solid | cube | (2,1) |
| 4 | 0 | dirt | Solid | cube | (1,0) |
| 5 | 0 | wood | Solid | cube | (4,0) |
| 6 | 0 | log | Solid | cube | 5,0 / 5,0 / 6,0 / 6,0 / 5,0 / 5,0 |
| 6 | 1 | log | Solid | cube | 6,0 / 6,0 / 7,0 / 7,0 / 7,0 / 7,0 |
| 6 | 2 | log | Solid | cube | 7,0 / 7,0 / 5,0 / 5,0 / 6,0 / 6,0 |
| 7 | 0 | roundstone | Solid | cube | (0,1) |
| 8 | 0 | glass | IncompleteBlock | cube | (0,2)，isLucency=true |
| 9 | 0 | water | Liquid | cube15pxH | 1,3 / 1,3 / 0,3 / 1,3 / 1,3 / 1,3，isLucency=true |
| 10 | 0 | leave | Solid | cube | (1,2) |

模型：`cube`、`cube15pxH`（6 面）、`step`、`stair_posX`（7 面，含附加面）、`textEntity`（12 面，实体模型）。

---

## 附录 B：文件变更清单

**新增**

- `TextCraft/src/Core/Config/ResourcePaths.cs`
- `TextCraft/src/Table/TableLoader.cs`
- `TextCraft/src/Table/TableLoadException.cs`
- `RequiredResources/Config/Tables/blocks.json`
- `RequiredResources/Config/Tables/models/cube.json`
- `RequiredResources/Config/Tables/models/cube15pxH.json`
- `RequiredResources/Config/Tables/models/step.json`
- `RequiredResources/Config/Tables/models/stair_posX.json`
- `RequiredResources/Config/Tables/models/textEntity.json`
- `RequiredResources/Config/Tables/modelTable.json`（旧格式兼容，仅供回滚/旧代码）
- `docs/block-model-json-loading.md`（本文档）

**修改**

- `TextCraft/TextCraft.csproj`
- `TextCraft/src/Core/Program.cs`
- `TextCraft/src/Rendering/Atlas.cs`
- `TextCraft/src/Table/BlockTable.cs`
- `TextCraft/src/Table/EntityModelTable.cs`
- `TextCraft/src/Table/ModelTable.cs`
- `TextCraft/src/Tools/GridGenerator.cs`
- `TextCraft/src/Tools/ModelCreator.cs`
- `TextCraft/src/UI/UITable.cs`
- `RequiredResources/Resources/atlas/uiAtlas/spirits.xml`
- `README.md`

**删除**

- `RequiredResources/Config/Tables/modelTable.xml`（过期且无代码读取）

---

## 附录 C：常用命令

```powershell
# 构建（自动同步 RequiredResources 到 bin\Debug\net8.0）
dotnet build TextCraft\TextCraft.csproj -c Debug

# 只校验数据表，不开窗口
dotnet run --project TextCraft\TextCraft.csproj -- --validate-tables
# 或直接运行已构建的程序
TextCraft\bin\Debug\net8.0\TextCraft.exe --validate-tables

# 临时关闭资源自动同步（测试“资源缺失”报错路径）
dotnet build TextCraft\TextCraft.csproj -p:SkipRequiredResourcesSync=true

# 由 models/*.json 重新生成旧格式 modelTable.json（需要旧代码读取时）
$models = Get-ChildItem RequiredResources\Config\Tables\models\*.json |
    ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json } |
    ForEach-Object { [pscustomobject]@{ Name = $_.name; faces = $_.faces } }
ConvertTo-Json -InputObject @($models) -Depth 10 |
    Set-Content RequiredResources\Config\Tables\modelTable.json -Encoding UTF8
```

> 注：旧代码的 `ModelTable.Load` 使用默认 `JsonSerializerOptions`（区分大小写），因此旧格式文件里模型名必须是 `"Name"`（大写 N）。新的 `models/*.json` 使用 `"name"`，两者不要混用。
