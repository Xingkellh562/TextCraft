using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TextCraft.src.Core.Event;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Table;

namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 自检工具：
    /// <list type="bullet">
    /// <item><c>--validate-ui</c>：不开窗口，构建控件树并做布局，分别以"合批"和"逐控件"两种模式
    /// 生成绘制列表，打印 DrawCall 对比、层级分布，并检查顶点数据是否合法（NaN / 空批次）；</item>
    /// <item><c>--ui-capture &lt;目录&gt;</c>：创建隐藏窗口 + 离屏 FBO，渲染主菜单 / 设置 / HUD 三张截图，
    /// 用于文档与视觉回归。</item>
    /// </list>
    /// 两者都不依赖人工打开游戏，因此可以在命令行里回归验证。
    /// </summary>
    public static class UIDiagnostics
    {
        /// <summary>无窗口构建 UI 并输出统计。返回进程退出码（0 = 通过）。</summary>
        public static int RunHeadless(Vector2i size)
        {
            var stopwatch = Stopwatch.StartNew();

            var root = new UIRoot { Size = new Vector2(size.X, size.Y) };
            var manager = new UIMgr();
            UIContext.Theme = UIAssets.Ins.Theme;

            try
            {
                manager.uITable.LoadUI(root);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UI 构建失败: {ex.Message}");
                return 1;
            }

            root.Layout(default);
            stopwatch.Stop();

            var components = root.Traverse(true);
            Console.WriteLine($"UI 控件树：共 {components.Count} 个控件，{CountInteractable(components)} 个可交互，布局耗时 {stopwatch.ElapsedMilliseconds} ms。");
            PrintTree(root, 0);

            var list = new UIDrawList();

            // 合批模式
            list.SplitAll = false;
            list.CoalesceBatches = UIAssets.Ins.Coalesce;
            list.CoalesceWindow = UIAssets.Ins.CoalesceWindow;
            list.Begin();
            root.CollectDraw(list, UIRect.Infinite, Vector4.One);
            var batched = list.End();
            var batchedLayers = DescribeLayers(list);
            int batchedVertices = list.VertexCount;
            bool batchedValid = ValidateVertices(list, out string batchedError);

            // 逐控件模式（旧行为）
            list.SplitAll = true;
            list.Begin();
            root.CollectDraw(list, UIRect.Infinite, Vector4.One);
            var legacy = list.End();
            bool legacyValid = ValidateVertices(list, out string legacyError);

            Console.WriteLine();
            Console.WriteLine($"画面尺寸 {size.X}x{size.Y}");
            Console.WriteLine($"  合批模式 : DrawCall={batched.DrawCalls,4}  批次={batched.Batches,4}  图元={batched.Primitives,4}  顶点={batched.Vertices,5}  层级={batched.Layers}");
            Console.WriteLine($"  逐控件   : DrawCall={legacy.DrawCalls,4}  批次={legacy.Batches,4}  图元={legacy.Primitives,4}  顶点={legacy.Vertices,5}  层级={legacy.Layers}");
            if (batched.DrawCalls > 0)
                Console.WriteLine($"  DrawCall 合并比: {legacy.DrawCalls / (float)batched.DrawCalls:0.0}x（{legacy.DrawCalls} → {batched.DrawCalls}）");
            Console.WriteLine("  合批后的层级分布：");
            foreach (var line in batchedLayers)
                Console.WriteLine("    " + line);

            PrintScreenModes(root, manager);

            bool same = batchedVertices == legacy.Vertices;
            int exit = 0;

            if (!same)
            {
                Console.Error.WriteLine($"错误: 两种模式的顶点数不一致（合批 {batchedVertices} / 逐控件 {legacy.Vertices}），合批过程丢失了几何。");
                exit = 1;
            }
            if (!batchedValid)
            {
                Console.Error.WriteLine($"错误: 合批模式顶点数据非法: {batchedError}");
                exit = 1;
            }
            if (!legacyValid)
            {
                Console.Error.WriteLine($"错误: 逐控件模式顶点数据非法: {legacyError}");
                exit = 1;
            }
            if (batched.DrawCalls == 0)
            {
                Console.Error.WriteLine("错误: 没有产生任何绘制调用，UI 树可能为空。");
                exit = 1;
            }

            Console.WriteLine(exit == 0 ? "UI 自检通过。" : "UI 自检失败。");

            if (RunInteractionTests())
                Console.WriteLine("UI 交互自检通过。");
            else
            {
                Console.Error.WriteLine("UI 交互自检失败。");
                exit = 1;
            }

            return exit;
        }

        /// <summary>
        /// 不开窗口模拟指针事件，验证"命中检测 → 控件回调 → 状态变化"这条链路。
        /// </summary>
        private static bool RunInteractionTests()
        {
            var root = new UIRoot { Size = new Vector2(1280f, 720f) };
            var manager = new UIMgr();
            UIContext.Theme = UIAssets.Ins.Theme;
            manager.uITable.LoadUI(root);
            root.Layout(default);

            var input = manager.Input;
            bool pass = true;

            void Check(string name, bool condition, string detail = "")
            {
                Console.WriteLine($"  [{(condition ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? "  " + detail : "")}");
                if (!condition) pass = false;
            }

            // 1) 悬停：指针移到"设置"按钮中心
            if (manager.uITable["settingsButton"] is not UIButton settingsButton)
            {
                Check("找不到设置按钮", false);
                return false;
            }

            var point = settingsButton.ScreenRect.Center;
            input.Update(root, point);
            Check("悬停命中按钮", ReferenceEquals(input.Hover, settingsButton));

            // 2) 点击：按下 + 抬起 → 设置窗口显示
            input.PointerPress(root, point);
            input.PointerRelease(root, point);
            var window = manager.uITable["settingsWindow"];
            Check("点击打开设置窗口", window is { ActiveSelf: true });

            root.Layout(default);

            // 3) 窗口显示后应可命中其内部控件（验证布局真的重新解算过）
            if (manager.uITable["fogToggle"] is not UIToggle fogToggle || fogToggle.ScreenRect.IsEmpty)
            {
                Check("设置窗口内容未布局", false);
                return false;
            }

            bool before = fogToggle.IsOn;
            var togglePoint = fogToggle.ScreenRect.Center;
            input.Update(root, togglePoint);
            input.PointerPress(root, togglePoint);
            input.PointerRelease(root, togglePoint);
            Check("点击开关翻转状态", fogToggle.IsOn != before, $"{before} -> {fogToggle.IsOn}");

            // 4) 滑块：在 1/4 处按下，值应落在 0.25 附近
            if (manager.uITable["viewSlider"] is not UISlider slider)
            {
                Check("找不到滑块", false);
                return false;
            }

            var sliderRect = slider.ScreenRect;
            var lowPoint = new Vector2(sliderRect.X + sliderRect.Width * 0.25f, sliderRect.Center.Y);
            input.Update(root, lowPoint);
            input.PointerPress(root, lowPoint);
            input.PointerRelease(root, lowPoint);
            Check("点击滑块改变数值", slider.Value < 0.45f, $"value={slider.Value:0.##}");

            // 5) 空白处不应命中任何控件
            input.Update(root, new Vector2(20f, 700f));
            Check("空白处不命中控件", input.Hover == null);

            // 6) 主菜单 →「开始游戏」进入存档/新建界面
            manager.uITable.EnterMainMenu();
            root.Layout(default);
            if (manager.uITable["playButton"] is not UIButton playButton)
            {
                Check("找不到开始游戏按钮", false);
                return false;
            }

            ClickPoint(input, root, playButton.ScreenRect.Center);
            root.Layout(default);
            var worldSelect = manager.uITable["worldSelect"];
            Check("点击开始游戏进入存档界面", worldSelect is { ActiveSelf: true });

            // 7) 下拉框应列出存档（示例存档 "624"）
            var dropdown = worldSelect?.Find<UIDropdown>("saveList");
            if (dropdown == null)
            {
                Check("找不到存档下拉框", false);
                return false;
            }

            Check("下拉框读到存档", dropdown.Items.Count > 0,
                $"共 {dropdown.Items.Count} 个，选中={(dropdown.SelectedItem ?? "-")}");

            // 8) 展开候选并点击第一行选中
            ClickPoint(input, root, dropdown.ScreenRect.Center);
            root.Layout(default);
            Check("点击下拉框展开候选", dropdown.IsOpen);

            var firstRow = dropdown.Find<UIDropdownItem>("row0");
            if (firstRow != null && !firstRow.ScreenRect.IsEmpty)
            {
                ClickPoint(input, root, firstRow.ScreenRect.Center);
                root.Layout(default);
                Check("点击候选行收起并选中", !dropdown.IsOpen && dropdown.SelectedItem != null,
                    $"{dropdown.SelectedItem}");
            }
            else
            {
                Check("候选行未布局", false);
            }

            // 9) 输入框：点击聚焦 + 输入字符 + 退格
            var nameField = worldSelect?.Find<UITextField>("nameField");
            if (nameField == null)
            {
                Check("找不到名称输入框", false);
                return false;
            }

            nameField.Text = "";
            ClickPoint(input, root, nameField.ScreenRect.Center);
            Check("点击输入框获得焦点", input.HasTextFocus && ReferenceEquals(input.FocusedText, nameField));

            foreach (char c in "abc") input.HandleTextInput(c);
            Check("输入框接收字符", nameField.Text == "abc", $"text='{nameField.Text}'");

            input.HandleKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Backspace);
            Check("输入框退格", nameField.Text == "ab", $"text='{nameField.Text}'");

            // 数字型输入框应拒绝字母
            var seedField = worldSelect?.Find<UITextField>("seedField");
            if (seedField != null)
            {
                seedField.Text = "";
                input.SetTextFocus(seedField);
                input.HandleTextInput('x');
                input.HandleTextInput('7');
                Check("种子输入框只接受数字", seedField.Text == "7", $"text='{seedField.Text}'");
            }

            // 10) 创建按钮应发出 LoadWorldEventArg
            LoadWorldEventArg? requested = null;
            void Probe(EventArg arg) => requested = arg as LoadWorldEventArg;
            EventMgr.Ins.Subscribe(typeof(LoadWorldEventArg), Probe);

            if (worldSelect?.Find<UIButton>("createButton") is { } createButton)
            {
                ClickPoint(input, root, createButton.ScreenRect.Center);
                Check("创建世界发出加载请求", requested != null && requested.name == "ab",
                    requested == null ? "无事件" : $"name={requested.name} seed={requested.seed}");
            }

            // 11) 暂停面板
            manager.uITable.EnterGame();
            manager.uITable.OpenPause("624", 12345);
            root.Layout(default);
            var pauseMenu = manager.uITable["pauseMenu"];
            Check("暂停面板打开", pauseMenu is { ActiveSelf: true });

            bool resumed = false;
            void ProbeResume(EventArg arg) => resumed = true;
            EventMgr.Ins.Subscribe(typeof(ResumeGameEventArg), ProbeResume);

            if (pauseMenu?.Find<UIButton>("pause_继续游戏") is { } resumeButton)
            {
                ClickPoint(input, root, resumeButton.ScreenRect.Center);
                Check("点击继续游戏发出恢复请求", resumed);
            }

            // 12) 暂停面板里的「设置」应能打开设置窗口，且盖在暂停面板之上
            if (pauseMenu?.Find<UIButton>("pause_设置") is { } pauseSettingsButton)
            {
                ClickPoint(input, root, pauseSettingsButton.ScreenRect.Center);
                root.Layout(default);
                var settingsWindow = manager.uITable["settingsWindow"];
                Check("暂停面板可打开设置窗口", settingsWindow is { ActiveSelf: true }
                        && pauseMenu != null && settingsWindow.Layer > pauseMenu.Layer,
                    $"settings.Layer={settingsWindow?.Layer} pause.Layer={pauseMenu?.Layer}");
            }

            // 13) 设置窗口打开时，ESC 应先关闭窗口（UI 优先于"继续游戏"）
            manager.uITable.OpenSettings();
            root.Layout(default);
            Check("ESC 优先关闭设置窗口", manager.uITable.HandleEscape()
                && manager.uITable["settingsWindow"] is { ActiveSelf: false });

            manager.uITable.CloseSettings();
            manager.uITable.ClosePause();
            manager.uITable.EnterMainMenu();

            return pass;
        }

        /// <summary>在某个屏幕点做一次完整的"移动 → 按下 → 抬起"。</summary>
        private static void ClickPoint(UIInput input, UIComponent root, Vector2 point)
        {
            input.Update(root, point);
            input.PointerPress(root, point);
            input.PointerRelease(root, point);
        }

        private static int CountInteractable(List<UIComponent> components)
            => components.Count(c => c.IsInteractable || c.BlocksPointer);

        /// <summary>分别统计"主菜单 / 设置窗口 / 游戏 HUD"三种画面下的 DrawCall。</summary>
        private static void PrintScreenModes(UIRoot root, UIMgr manager)
        {
            Console.WriteLine();
            Console.WriteLine("  各画面 DrawCall 对比（合批 / 逐控件）：");

            var list = new UIDrawList();
            list.CoalesceBatches = UIAssets.Ins.Coalesce;
            list.CoalesceWindow = UIAssets.Ins.CoalesceWindow;

            (int batched, int legacy, int vertices) Measure()
            {
                list.SplitAll = false;
                list.Begin();
                root.CollectDraw(list, UIRect.Infinite, Vector4.One);
                var b = list.End();

                list.SplitAll = true;
                list.Begin();
                root.CollectDraw(list, UIRect.Infinite, Vector4.One);
                var l = list.End();
                return (b.DrawCalls, l.DrawCalls, b.Vertices);
            }

            manager.uITable.EnterMainMenu();
            root.Layout(default);
            var menu = Measure();
            Console.WriteLine($"    主菜单      {menu.batched,3} / {menu.legacy,3}   顶点 {menu.vertices}");

            manager.uITable.OpenSettings();
            root.Layout(default);
            var settings = Measure();
            Console.WriteLine($"    设置窗口    {settings.batched,3} / {settings.legacy,3}   顶点 {settings.vertices}");

            manager.uITable.CloseSettings();
            manager.uITable.EnterGame();
            manager.uITable.SetDebugVisible(true);
            root.Layout(default);
            var hud = Measure();
            Console.WriteLine($"    游戏 HUD    {hud.batched,3} / {hud.legacy,3}   顶点 {hud.vertices}");

            manager.uITable.SetDebugVisible(false);
            manager.uITable.EnterMainMenu();
            root.Layout(default);
        }

        private static void PrintTree(UIComponent component, int depth)
        {
            if (depth > 6) return;
            string indent = new string(' ', depth * 2);
            Console.WriteLine($"{indent}{component.GetType().Name,-14} name={component.Name,-18} layer={UILayers.ToName(component.Layer),-9} rect={component.ScreenRect}");
            foreach (var child in component.Children)
                PrintTree(child, depth + 1);
        }

        private static List<string> DescribeLayers(UIDrawList list)
        {
            var result = new List<string>();
            var batches = list.Batches;
            var order = new List<int>();
            var drawCalls = new Dictionary<int, int>();
            var vertices = new Dictionary<int, int>();

            for (int i = 0; i < batches.Length; i++)
            {
                int layer = batches[i].Layer;
                if (!drawCalls.ContainsKey(layer))
                {
                    order.Add(layer);
                    drawCalls[layer] = 0;
                    vertices[layer] = 0;
                }
                drawCalls[layer]++;
                vertices[layer] += batches[i].Count;
            }

            order.Sort();
            foreach (int layer in order)
                result.Add($"{UILayers.ToName(layer),-10} DrawCall={drawCalls[layer],3}  顶点={vertices[layer],5}");
            return result;
        }

        private static bool ValidateVertices(UIDrawList list, out string error)
        {
            var vertices = list.Vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                ref readonly var v = ref vertices[i];
                if (float.IsNaN(v.Position.X) || float.IsNaN(v.Position.Y)
                    || float.IsInfinity(v.Position.X) || float.IsInfinity(v.Position.Y))
                {
                    error = $"第 {i} 个顶点位置非法 ({v.Position})";
                    return false;
                }
            }

            var batches = list.Batches;
            for (int i = 0; i < batches.Length; i++)
            {
                ref readonly var batch = ref batches[i];
                if (batch.Count <= 0 || batch.Count % 3 != 0)
                {
                    error = $"第 {i} 个批次顶点数 {batch.Count} 不是 3 的倍数";
                    return false;
                }
                if (batch.Start < 0 || batch.Start + batch.Count > list.VertexCount)
                {
                    error = $"第 {i} 个批次区间 [{batch.Start},{batch.Start + batch.Count}) 超出顶点总数 {list.VertexCount}";
                    return false;
                }
            }

            error = "";
            return true;
        }

        // =====================================================================
        // 离屏截图
        // =====================================================================

        public const string ScreenMain = "main";
        public const string ScreenSettings = "settings";
        public const string ScreenHud = "hud";
        public const string ScreenWorldSelect = "world-select";
        public const string ScreenWorldSelectOpen = "world-select-open";
        public const string ScreenPause = "pause";

        /// <summary>渲染三张 UI 截图到指定目录。返回退出码。</summary>
        public static int CaptureAll(string outputDirectory, Vector2i size)
        {
            Directory.CreateDirectory(outputDirectory);
            using var window = new UICaptureWindow(size, outputDirectory);
            window.Run();
            return window.ExitCode;
        }

        /// <summary>隐藏窗口 + FBO 的截图宿主：不显示窗口，也不需要显示器交互。</summary>
        private sealed class UICaptureWindow : GameWindow
        {
            private readonly string _outputDirectory;
            private UIMgr? _uiMgr;

            public int ExitCode { get; private set; }

            public UICaptureWindow(Vector2i size, string outputDirectory)
                : base(
                    new GameWindowSettings { UpdateFrequency = 60.0 },
                    new NativeWindowSettings
                    {
                        ClientSize = size,
                        Title = "TextCraft UI Capture",
                        APIVersion = new Version(3, 3),
                        Profile = ContextProfile.Core,
                        Flags = ContextFlags.ForwardCompatible,
                        StartVisible = false,
                        StartFocused = false,
                        IsEventDriven = false,
                    })
            {
                _outputDirectory = outputDirectory;
                VSync = VSyncMode.Off;
            }

            protected override void OnLoad()
            {
                base.OnLoad();
                try
                {
                    _uiMgr = new UIMgr();
                    _uiMgr.Load();
                    _uiMgr.OnSizeChange(ClientSize);

                    Capture(ScreenMain, () => _uiMgr.uITable.EnterMainMenu());
                    Capture(ScreenSettings, () =>
                    {
                        _uiMgr.uITable.EnterMainMenu();
                        _uiMgr.uITable.OpenSettings();
                    });

                    Capture(ScreenWorldSelect, () =>
                    {
                        _uiMgr.uITable.EnterMainMenu();
                        _uiMgr.uITable.OpenWorldSelect();
                    });

                    Capture(ScreenWorldSelectOpen, () =>
                    {
                        _uiMgr.uITable.EnterMainMenu();
                        _uiMgr.uITable.OpenWorldSelect();
                        _uiMgr.uITable["worldSelect"]?.Find<UIDropdown>("saveList")?.Open();
                    });

                    Capture(ScreenPause, () =>
                    {
                        _uiMgr.uITable.EnterGame();
                        _uiMgr.uITable.OpenPause("624", 12345);
                    });

                    Capture(ScreenHud, () =>
                    {
                        _uiMgr.uITable.EnterGame();
                        _uiMgr.uITable.UpdateFps(144);
                        _uiMgr.uITable.SetSelectedSlot(2);
                        _uiMgr.uITable.SetDebugVisible(true);
                    });

                    Capture("debug-overlay", () =>
                    {
                        _uiMgr.uITable.EnterMainMenu();
                        _uiMgr.uITable.SetDebugVisible(true);
                    });

                    Console.WriteLine($"[UI截图] 已输出到 {Path.GetFullPath(_outputDirectory)}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[UI截图] 失败: {ex}");
                    ExitCode = 1;
                }

                Close();
            }

            private void Capture(string name, Action setup)
            {
                setup();
                // 先跑两帧：第一帧让调试文本读到上一帧的统计，第二帧渲染出稳定画面
                _uiMgr!.Update(0.016f);
                _uiMgr.Render();
                _uiMgr.Update(0.016f);

                int width = ClientSize.X;
                int height = ClientSize.Y;
                var pixels = new byte[width * height * 4];

                int fbo = GL.GenFramebuffer();
                int texture = GL.GenTexture();
                try
                {
                    GL.BindTexture(TextureTarget.Texture2D, texture);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                        TextureTarget.Texture2D, texture, 0);

                    if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                        throw new InvalidOperationException("离屏 FBO 创建失败");

                    GL.Viewport(0, 0, width, height);
                    DrawBackdrop(width, height);

                    _uiMgr.Render();

                    GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

                    // 截图是不透明画面：把 alpha 通道归一化，避免看图工具按透明叠加导致"发白"
                    for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                }
                finally
                {
                    GL.DeleteFramebuffer(fbo);
                    GL.DeleteTexture(texture);
                }

                using var image = Image.LoadPixelData<Rgba32>(pixels, width, height);
                image.Mutate(ctx => ctx.Flip(FlipMode.Vertical));
                string path = Path.Combine(_outputDirectory, $"ui-{name}.png");
                image.SaveAsPng(path);
                Console.WriteLine($"[UI截图] {path}  DrawCall={_uiMgr.Stats.DrawCalls} 批次={_uiMgr.Stats.Batches} 顶点={_uiMgr.Stats.Vertices}");
            }

            /// <summary>
            /// 用"裁剪 + Clear"画一个竖向渐变背景（Core Profile 下不能用固定管线立即模式）。
            /// </summary>
            private static void DrawBackdrop(int width, int height)
            {
                const int bands = 32;
                GL.Disable(EnableCap.DepthTest);
                GL.Disable(EnableCap.Blend);
                GL.Disable(EnableCap.CullFace);
                GL.Enable(EnableCap.ScissorTest);

                for (int i = 0; i < bands; i++)
                {
                    float t = i / (float)(bands - 1);
                    float r = MathHelper.Lerp(0.04f, 0.13f, t);
                    float g = MathHelper.Lerp(0.05f, 0.17f, t);
                    float b = MathHelper.Lerp(0.08f, 0.26f, t);

                    int y0 = height * i / bands;
                    int y1 = height * (i + 1) / bands;
                    GL.Scissor(0, y0, width, y1 - y0);
                    GL.ClearColor(r, g, b, 1f);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                }

                GL.Disable(EnableCap.ScissorTest);
                GL.Enable(EnableCap.Blend);
            }

            protected override void OnRenderFrame(FrameEventArgs args)
            {
                base.OnRenderFrame(args);
                Close();
            }
        }
    }
}
