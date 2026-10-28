using OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime;
using TextCraft.src.Core.Config;
using TextCraft.src.Core.ConsoleModule;
using TextCraft.src.Core.EntityModule;
using TextCraft.src.Core.Event;
using TextCraft.src.Core.Input;
using TextCraft.src.Core.Physic;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Table;
using TextCraft.src.Tools;
using TextCraft.src.UI;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace TextCraft.src.Core
{
    internal class Game : GameWindow
    {
        int _seed = 0;
        private bool _paused;
        private bool _escapeHeld;
        private Vector2 _pointerPosition;
        private bool _hasPointerPosition;
        private UICursorShape _appliedCursorShape = UICursorShape.Default;
        public GameSession session = new GameSession();
        public UIMgr uIMgr = new UIMgr();
        public GameStateMgr gameStateMgr;
        public ConsoleMgr consoleMgr = new ConsoleMgr();

        public Game(int width,int height,string title) : base(
            new GameWindowSettings
            {
                UpdateFrequency = 0.0
            },
            new NativeWindowSettings
            { 
                ClientSize = new OpenTK.Mathematics.Vector2i(width, height),
                Title = title,
                APIVersion = new Version(3,3),
                Profile = ContextProfile.Core,
                Flags = ContextFlags.ForwardCompatible,
                NumberOfSamples = 16,
                
            })
            
        {
            VSync = VSyncMode.Off;
            gameStateMgr = new(this);
            EventMgr.Ins.Subscribe(typeof(LoadWorldEventArg),arg => OnLoadWorld(arg as LoadWorldEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(UnLoadWorldEventArg), arg => OnUnLoadWorld(arg as UnLoadWorldEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(WorldLoadingFinishEventArg), arg => OnFinishLoadWorld(arg as WorldLoadingFinishEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(RequestExitEventArg), arg => OnRequestExit(arg as RequestExitEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(ToggleVSyncEventArg), arg => OnToggleVSync(arg as ToggleVSyncEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(SelectBlockEventArg), arg => OnSelectBlock(arg as SelectBlockEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(ResumeGameEventArg), arg => OnResumeGame(arg as ResumeGameEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(SaveAndQuitWorldEventArg), arg => OnSaveAndQuit(arg as SaveAndQuitWorldEventArg ?? new()));
        }

        /// <summary>
        /// 内容区（client area）尺寸 = 真正的可绘制区域，也是鼠标坐标所在的坐标系。
        ///
        /// 注意：<see cref="OpenTK.Windowing.Desktop.NativeWindow.Size"/> 是**外部尺寸**
        /// （含标题栏与边框）；而 <see cref="OpenTK.Windowing.Desktop.NativeWindow.ClientSize"/>
        /// 才是内容区。鼠标事件（<c>MouseMoveEventArgs.X/Y</c>、<c>MousePosition</c>）都是相对内容区的，
        /// 所以视口、投影矩阵、UI 布局必须统一用内容区尺寸，否则整个画面会按"标题栏高度"上移，
        /// 表现为 UI 的碰撞箱整体向下偏移一个固定值。
        /// </summary>
        private Vector2i ContentSize
        {
            get
            {
                var client = ClientSize;
                return client.X > 0 && client.Y > 0 ? client : Size;
            }
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            // 上传 UI 图集/字体纹理（需要 GL 上下文），并构建控件树
            uIMgr.Load();

            var contentSize = ContentSize;
            GL.Viewport(0, 0, contentSize.X, contentSize.Y);
            uIMgr.OnSizeChange(contentSize);

            consoleMgr.Load();

            while (GL.GetError() != ErrorCode.NoError) { }

            uIMgr.uITable.EnterMainMenu();
        }

        
        public void OnLoadWorld(LoadWorldEventArg arg)
        {
            string name = arg.name;
            _seed = arg.seed;

            Console.WriteLine($"准备创建世界: {name}（种子 {_seed}）");

            gameStateMgr.SwitchState(GameState.InGame);
            _paused = false;
            session.Paused = false;
            CursorState = CursorState.Grabbed;
            uIMgr.ReleasePointer();

            // 世界加载是同步阻塞的，先画一帧加载遮罩，避免窗口看起来"卡死"
            PresentLoadingFrame();

            Console.WriteLine("正在创建世界中");
            session.GameRender?.OnSizeChange(ContentSize);
            session.LoadWorld(name, _seed);

            if (session.World != null)
                uIMgr.uITable.SetWorldInfo(session.World.Name, session.World.Seed);

            WindowState = WindowState.Maximized;
        }

        /// <summary>把当前 UI（加载遮罩）画出来并交换缓冲，用于阻塞式加载前的一次呈现。</summary>
        private void PresentLoadingFrame()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            uIMgr.Render();
            SwapBuffers();
        }

        public void OnUnLoadWorld(UnLoadWorldEventArg arg)
        {
            Console.WriteLine("准备卸载世界");
            _paused = false;
            session.Paused = false;
            gameStateMgr.SwitchState(GameState.MainMenu);
            CursorState = CursorState.Normal;
            uIMgr.ReleasePointer();
        }

        public void OnFinishLoadWorld(WorldLoadingFinishEventArg e)
        {
            uIMgr.uITable.EnterGame();
            if (session.World != null)
                uIMgr.uITable.SetWorldInfo(session.World.Name, session.World.Seed);
        }

        // ---------------- 暂停 ----------------

        /// <summary>是否处于暂停（ESC 面板打开）。</summary>
        public bool IsPaused => _paused;

        private void PauseGame()
        {
            if (_paused) return;
            if (!session.IsLoad || session.World == null) return;

            _paused = true;
            session.Paused = true;
            CursorState = CursorState.Normal;   // 暂停面板要能点，必须先把光标放出来
            uIMgr.ReleasePointer();
            uIMgr.uITable.OpenPause(session.World.Name, session.World.Seed);
            Console.WriteLine("已暂停");
        }

        private void ResumeGame()
        {
            if (!_paused) return;

            _paused = false;
            session.Paused = false;
            uIMgr.uITable.ClosePause();
            CursorState = CursorState.Grabbed;
            uIMgr.ReleasePointer();
            Console.WriteLine("继续游戏");
        }

        /// <summary>ESC：UI 优先（失焦/收起下拉/关窗口），否则切换暂停。</summary>
        private void HandleEscape()
        {
            if (uIMgr.HandleEscape()) return;

            if (!session.IsLoad || session.World == null) return;

            if (_paused) ResumeGame();
            else PauseGame();
        }

        /// <summary>暂停面板的"继续游戏"。</summary>
        public void OnResumeGame(ResumeGameEventArg arg) => ResumeGame();

        /// <summary>暂停面板的"保存并退回主菜单"。</summary>
        public void OnSaveAndQuit(SaveAndQuitWorldEventArg arg)
        {
            if (!session.IsLoad || session.World == null)
            {
                uIMgr.uITable.EnterMainMenu();
                return;
            }

            Console.WriteLine("正在保存世界并退回主菜单…");
            _paused = false;
            session.Paused = false;
            CursorState = CursorState.Normal;

            // 卸载即保存（World.UnLoad 写 worldData.xml 并等待区块落盘）
            EventMgr.Ins.Publish(new UnLoadWorldEventArg());
            uIMgr.uITable.EnterMainMenu();
            Console.WriteLine("已保存并退回主菜单");
        }

        public void OnRequestExit(RequestExitEventArg arg)
        {
            Console.WriteLine("退出游戏");
            Close();
        }

        public void OnToggleVSync(ToggleVSyncEventArg arg)
        {
            VSync = arg.enabled ? VSyncMode.On : VSyncMode.Off;
            Console.WriteLine($"垂直同步: {(arg.enabled ? "开启" : "关闭")}");
        }

        /// <summary>UI 快捷栏选择方块时，同步到实体的 InputComponent.nowBlock。</summary>
        public void OnSelectBlock(SelectBlockEventArg arg)
        {
            if (!session.IsLoad || session.World == null) return;

            World world = session.World;
            int maxBlock = Math.Max(1, BlockTable.Ins.MaxPlaceableId);
            int block = Math.Clamp(arg.blockId, 1, maxBlock);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                input.nowBlock = block;
                world.ecsMgr.AddComponent(entity, input);
            }
        }
        private int _frameCount;
        private double _timeAccumulator;
        private double _currentFps;
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            _frameCount++;
            _timeAccumulator += args.Time;
            if(_timeAccumulator >= 1.0)
            {
                _currentFps = _frameCount / _timeAccumulator;
                _frameCount = 0;
                _timeAccumulator = 0;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            session.Render();

            uIMgr.Render();

            SwapBuffers();
        }

        float _time = 1.0f;

        private double _accumTime = 0.0f;
        private double rateTime = 1/60.0;
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            _accumTime += UpdateTime;

            if (_accumTime < rateTime) return;

            session.Update((float)rateTime);

            consoleMgr.Update();

            // UI 自身更新（悬停动画等）与帧率显示
            uIMgr.Update((float)rateTime);
            uIMgr.uITable.UpdateFps(_currentFps);
            ApplyCursorShape();
            if (_escapeHeld && KeyboardState.IsKeyReleased(Keys.Escape)) _escapeHeld = false;

            if (_time > 1)
            {
                if (uIMgr.uITable["fps"] is UILabel fps)
                    fps.Text = "FPS: " + (int)_currentFps;
                _time = 0.0f;
            }

            _time += (float)rateTime;
            _accumTime -= rateTime;
            //Console.WriteLine("Time: " + session.World != null ? (int)session.World.GameTime:0);
        }

        /// <summary>根据悬停控件切换指针形状（输入框 I 形、按钮手形），只在变化时设置。</summary>
        private void ApplyCursorShape()
        {
            var shape = CursorState == CursorState.Grabbed ? UICursorShape.Default : uIMgr.CursorShape;
            if (shape == _appliedCursorShape) return;
            _appliedCursorShape = shape;

            Cursor = shape switch
            {
                UICursorShape.Text => MouseCursor.IBeam,
                UICursorShape.Hand => MouseCursor.PointingHand,
                _ => MouseCursor.Default,
            };
        }
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // 用内容区尺寸（与鼠标坐标同一坐标系）驱动视口、3D 渲染器与 UI 布局
            var size = ContentSize;
            GL.Viewport(0, 0, size.X, size.Y);
            session.GameRender?.OnSizeChange(size);
            uIMgr.OnSizeChange(size);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            EventMgr.Ins.Publish(new UnLoadWorldEventArg() { });
        }

        static void Main(string[] args)
        {
            bool validateOnly = args.Any(arg => string.Equals(arg, "--validate-tables", StringComparison.OrdinalIgnoreCase));
            bool validateUi = args.Any(arg => string.Equals(arg, "--validate-ui", StringComparison.OrdinalIgnoreCase));
            int captureIndex = Array.FindIndex(args, a => string.Equals(a, "--ui-capture", StringComparison.OrdinalIgnoreCase));
            var captureSize = ParseSize(args, 1280, 720);

            ConfigMgr.Ins.OnLoad(ResourcePaths.GameConfigFile);

            try
            {
                TableLoader.LoadAll();
            }
            catch (TableLoadException ex)
            {
                Console.Error.WriteLine("方块/模型资源加载失败：");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("如果这是首次运行，请确认 RequiredResources 目录已同步到程序输出目录（正常构建会自动同步）。");
                Environment.ExitCode = 1;
                return;
            }

            // UI 资源（图集 / 字体 / 主题）只做 CPU 侧加载，纹理上传在窗口创建后进行
            try
            {
                UIAssets.Ins.Load(ResourcePaths.UIConfigFile);
            }
            catch (TableLoadException ex)
            {
                Console.Error.WriteLine("UI 资源加载失败：");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("请检查 RequiredResources/Config/Tables/ui/ui.json 与所引用的图集/字体文件是否已同步到输出目录。");
                Environment.ExitCode = 1;
                return;
            }

            // 只校验数据表（模型/方块 JSON）而不创建窗口，便于编辑资源后快速自检
            if (validateOnly)
            {
                TableLoader.PrintSummary();
                UIAssets.Ins.PrintSummary();
                return;
            }

            // 只校验 UI（构建控件树 + 合批统计），不开窗口
            if (validateUi)
            {
                UIAssets.Ins.PrintSummary();
                Environment.ExitCode = UIDiagnostics.RunHeadless(new Vector2i(captureSize.X, captureSize.Y));
                return;
            }

            // 离屏渲染 UI 截图（隐藏窗口 + FBO），用于文档与视觉回归
            if (captureIndex >= 0)
            {
                string output = captureIndex + 1 < args.Length ? args[captureIndex + 1] : "ui-capture";
                Environment.ExitCode = UIDiagnostics.CaptureAll(output, new Vector2i(captureSize.X, captureSize.Y));
                return;
            }

            GCSettings.LatencyMode = GCLatencyMode.Interactive;
            using (Game game = new Game(800, 600, "craft"))
            {
                game.Run();
            }
        }

        /// <summary>解析 --size 1280x720（默认 1280x720）。</summary>
        private static Vector2i ParseSize(string[] args, int defaultWidth, int defaultHeight)
        {
            int index = Array.FindIndex(args, a => string.Equals(a, "--size", StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= args.Length) return new Vector2i(defaultWidth, defaultHeight);

            string[] parts = args[index + 1].Split('x', 'X', '*');
            if (parts.Length != 2) return new Vector2i(defaultWidth, defaultHeight);
            if (!int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
                return new Vector2i(defaultWidth, defaultHeight);

            return new Vector2i(Math.Clamp(width, 320, 7680), Math.Clamp(height, 240, 4320));
        }

        /// <summary>
        /// 游戏内输入是否生效：已载入世界、未暂停、鼠标被抓取、且没有输入框在编辑。
        /// 暂停面板/存档界面打开时，键盘与鼠标都不应再驱动角色。
        /// </summary>
        private bool GameplayInputActive
            => session.IsLoad && session.World != null && !_paused
               && CursorState == CursorState.Grabbed && !uIMgr.HasTextFocus;

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);

            // F3/F4/ESC 是"切换型"按键：忽略自动重复 + 边沿触发，按住也只会切换一次
            if (e.IsRepeat && (e.Key == Keys.Escape || e.Key == Keys.F3 || e.Key == Keys.F4))
                    return;

            if (e.Key == Keys.Escape)
            {
                    if (_escapeHeld) return;
                    _escapeHeld = true;
                }

            if (e.Key == Keys.F3)
            {
                uIMgr.uITable.ToggleDebug();
                return;
            }
            if (e.Key == Keys.F4)
            {
                uIMgr.uITable.ToggleRenderMode();
                Console.WriteLine($"UI 渲染模式: {UIAssets.Ins.Mode}");
                return;
            }

            // ESC 统一入口：先让 UI 处理（输入框失焦 / 收起下拉 / 关闭设置窗口），否则切换暂停
            if (e.Key == Keys.Escape)
            {
                HandleEscape();
                return;
            }

            // 输入框编辑中：按键全部交给 UI（Backspace/方向键/Enter 等）
            if (uIMgr.HandleKeyDown(e.Key)) return;

            if (!GameplayInputActive) return;

            {
                World world = session.World!;
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                    if (e.Key == Keys.W) input.forward = true;
                    if (e.Key == Keys.S) input.back = true;
                    if (e.Key == Keys.A) input.left = true;
                    if (e.Key == Keys.D) input.right = true;
                    if (e.Key == Keys.Q) input.createEntity = true;
                    if (e.Key == Keys.Space)
                    {
                        if (input.spaceTimer == 0)
                            input.spaceTimer = input.spaceInterval;
                        if (input.spaceTimer > 0 && !input.up)
                            input.spacePress += 1;
                        input.up = true;
                    }
                    if (e.Key == Keys.LeftShift) input.down = true;
                    if (e.Key == Keys.F12) GC.Collect();
                    if (e.Key == Keys.F11) {
                        if (VSync == VSyncMode.On)
                            VSync = VSyncMode.Off;
                        else
                            VSync = VSyncMode.On;
                    }
                    world.ecsMgr.AddComponent(entity, input);
                }
            }
        }
        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (e.Key == Keys.Escape) _escapeHeld = false;
            if (session.IsLoad && session.World != null)
            {
                World world = session.World;
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                    if (e.Key == Keys.W) input.forward = false;
                    if (e.Key == Keys.S) input.back = false;
                    if (e.Key == Keys.A) input.left = false;
                    if (e.Key == Keys.D) input.right = false;
                    if (e.Key == Keys.Space)
                    {
                        input.up = false;
                    }
                    if (e.Key == Keys.LeftShift) input.down = false;

                    world.ecsMgr.AddComponent(entity, input);
                }
            }
        }
        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

            // 记录光标位置：按下/抬起都用它，保证与悬停命中使用同一个坐标来源
            // （MousePosition 由 GLFW 单独维护，在窗口缩放/最大化后可能滞后一帧，导致"点不中"）
            _pointerPosition = new Vector2(e.X, e.Y);
            _hasPointerPosition = true;

            // 光标自由时把移动交给 UI（悬停/拖动），抓取时只用于视角
            if (CursorState != CursorState.Grabbed)
                uIMgr.HandleMouseMove(e.X, e.Y);


            if (session.IsLoad && session.World != null)
            {
                World world = session.World;
                if (CursorState == CursorState.Grabbed)
                {
                    foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                    {
                        var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                        input.mouseX = e.X;
                        input.mouseY = e.Y;
                        world.ecsMgr.AddComponent(entity, input);
                    }
                }
            }
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            bool uiConsumed = false;
            if (CursorState != CursorState.Grabbed && e.Button == MouseButton.Left)
                uiConsumed = uIMgr.HandleMouseDown(PointerPosition.X, PointerPosition.Y);

            if (uiConsumed) return;

            if (GameplayInputActive)
            {
                World world = session.World!;
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                    if (e.Button == MouseButton.Left) input.destory = true;
                    if (e.Button == MouseButton.Right) input.build = true;

                    world.ecsMgr.AddComponent(entity, input);
                }
            }
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (CursorState != CursorState.Grabbed && e.Button == MouseButton.Left)
                uIMgr.HandleMouseUp(PointerPosition.X, PointerPosition.Y);

            if (session.IsLoad && session.World != null)
            {
                World world = session.World;
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                    if (e.Button == MouseButton.Left) input.destory = false;
                    if (e.Button == MouseButton.Right) input.build = false;

                    world.ecsMgr.AddComponent(entity, input);
                }
            }
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (GameplayInputActive)
            {
                World world = session.World!;
                int nowBlock = 1;
                // 快捷栏范围由方块表推导，新增/删除方块后无需再改这里的硬编码数字
                int maxBlock = Math.Max(1, BlockTable.Ins.MaxPlaceableId);
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                    if (e.OffsetY > 0)
                    {
                        input.nowBlock -= 1;
                        input.nowBlock = Math.Clamp(input.nowBlock, 1, maxBlock);
                    }
                    else if (e.OffsetY < 0)
                    {
                        input.nowBlock += 1;
                        input.nowBlock = Math.Clamp(input.nowBlock, 1, maxBlock);
                    }
                    nowBlock = input.nowBlock;
                    world.ecsMgr.AddComponent(entity, input);
                }

                uIMgr.uITable.SetSelectedBlock(nowBlock);
            }
        }

        /// <summary>当前指针位置：优先用移动事件记录的坐标，没有移动过时退回 GLFW 的 MousePosition。</summary>
        private Vector2 PointerPosition
            => _hasPointerPosition ? _pointerPosition : new Vector2(MousePosition.X, MousePosition.Y);

        /// <summary>把输入字符转给 UI 输入框（GLFW 的 char 回调，支持 Shift 符号）。</summary>
        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            uIMgr.HandleTextInput(e.Unicode);
        }
    }
}
