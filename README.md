<div align="center">

# TextCraft

一个基于 C# 和 OpenTK 开发的 3D 体素游戏引擎

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-100%25-239120.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)

</div>

## 📖 项目简介

TextCraft 是一个使用 C# 开发的开源 3D 体素游戏引擎，灵感来源于 Minecraft。项目采用模块化设计，使用 OpenGL 进行渲染，是一个轻量、性能良好的体素游戏开发框架。

## ✨ 特性

- 🎮 **体素世界渲染** - 基于区块（Chunk）的世界管理系统
- 🎨 **现代 OpenGL 渲染** - 使用 GLSL 着色器，支持纹理、环境光遮蔽（AO）
- 🖱️ **第一人称控制** - 完整的玩家移动和视角控制系统
- 🎯 **物理系统** - 基础的碰撞检测与物理模拟
- 📦 **资源管理** - 纹理、模型等资源的高效加载与管理
- 🧩 **数据驱动** - 方块模型与方块种类由 `Config/Tables` 下的 JSON 定义，增删方块无需修改代码
- 🔧 **模块化架构** - 清晰的代码结构，易于扩展和维护

## 🛠️ 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 语言 | C# | 10.0 |
| 框架 | .NET | 8.0 |
| 图形库 | OpenTK | 4.9.4 |
| 图像处理 | SixLabors.ImageSharp | 3.1.12 |
| 字符处理 | SixLabors.Font | 3.0.0 |

## 📁 项目结构
```
 TextCraft/
 ├── TextCraft/              # 主项目目录
 │   ├── src/
 │   │   ├── Core/           # 核心模块
 │   │   │   ├── ChunkModel/ # 区块模型
 │   │   │   ├── Config/     # 配置管理
 │   │   │   ├── Input/      # 输入系统
 │   │   │   ├── Physic/     # 物理系统
 │   │   │   ├── Game.cs     # 游戏主类
 │   │   │   ├── World.cs    # 世界管理
 │   │   │   └── ... 
 │   │   ├── Rendering/      # 渲染模块
 │   │   │   ├── BaseShader.cs
 │   │   │   ├── GameShader.cs
 │   │   │   ├── UIShader.cs
 │   │   │   ├── Grid.cs
 │   │   │   ├── GridMgr.cs
 │   │   │   └── IRenderer.cs
 │   │   ├── Table/          # 数据表（从 JSON 加载）
 │   │   │   ├── BlockTable.cs       # 方块元数据与烘焙后的几何
 │   │   │   ├── ModelTable.cs       # 具名模型
 │   │   │   ├── TableLoader.cs      # 启动时统一加载入口
 │   │   │   └── EntityModelTable.cs
 │   │   ├── Collections/    # 集合工具
 │   │   └── Tools/          # 工具类
 │   └── TextCraft.csproj    # 项目文件
 ├── RequiredResources/      # 运行期资源（构建时自动同步到输出目录）
 ├── TextCraft.sln           # 解决方案文件
 ├── docs/                   # 开发文档（含 JSON 化改造说明与回滚手册）
 ├── LICENSE.txt             # 许可证
 └── README.md               # 项目说明
```
### 📦 运行期资源

```
RequiredResources/
├── Config/
│   ├── config.xml
│   └── Tables/
│       ├── blocks.json          # 方块定义（id/type/名称/类型/模型/图集 tile…）
│       └── models/*.json        # 模型定义（一个文件一个模型）
├── Resources/                   # 纹理、字体、UI 图集
└── Save/                        # 示例存档
```

`blocks.json` 与 `models/*.json` 的字段说明、校验规则、改动点与回滚方式见
[docs/block-model-json-loading.md](docs/block-model-json-loading.md)。

### 安装与运行

1. **克隆仓库**
```
git clone https://github.com/Xingkellh562/TextCraft.git
```

2. **还原依赖**
 ```
dotnet restore
 ```
3. **构建项目**
 ```
dotnet build --configuration Release
 ```
4. **运行游戏**
 ```
dotnet run --project TextCraft/TextCraft.csproj
```
5. **资源同步与数据表自检**

构建时会把仓库根目录的 `RequiredResources` **自动同步**到输出目录（如 `bin\Debug\net8.0`），不再需要手工复制：

- `Config`、`Resources` 每次构建增量复制；
- `Save` 下的示例存档仅在输出目录还没有 `Save` 时复制，避免覆盖游戏存档；
- 可用 `-p:SkipRequiredResourcesSync=true` 关闭同步。

修改方块/模型 JSON 后，可以不开窗口快速校验：

```
dotnet run --project TextCraft/TextCraft.csproj -- --validate-tables
```

看到“已加载模型 5 个、方块 13 种”及模型/方块清单即表示配置正确；否则会打印具体错误、文件位置与修改建议。

🎮 **操作说明**
 
| 按键 | 功能 |
|------|------|
| W / A / S / D  | 移动 |
| 鼠标 | 视角控制 |
| 空格 | 上升 |
| Shift | 下降 |
 
## 🗺️ 开发路线图
 
- 基础渲染框架(完成)
- 区块系统雏形(完成)
- 玩家控制系统(完成)
- 世界生成算法(完成)
- 方块放置与破坏(完成)
- 多纹理支持(完成)
- JSON 数据驱动的方块/模型系统(完成)
- UI系统(基本完成)
- 存档功能(基本完成)
- 地物放置(开发中)
- 光照系统(开发中)
- 音效系统(计划中)
- 多人联机(计划中)

***注:完成不代表我停止开发，我可能会在后续新增(完善)功能或优化***

## 🖼️ 运行效果

![游戏截图](./Pictures/game_picture.png)

## 🤝 贡献指南
 
欢迎提交 Issue 和 Pull Request！
 
1. Fork 本仓库

2. 创建你的特性分支 ( git checkout -b feature/AmazingFeature )

3. 提交你的更改 ( git commit -m 'Add some AmazingFeature' )

4. 推送到分支 ( git push origin feature/AmazingFeature )

5. 开启一个 Pull Request
 
## 📄 许可证
 
本项目采用 MIT 许可证 - 详见 LICENSE.txt 文件。
 
## 🙏 致谢
 
- OpenTK - .NET 平台的 OpenGL 绑定

- SixLabors.ImageSharp - C# 图像处理库

- SixLabors.Font - C# 字符处理库