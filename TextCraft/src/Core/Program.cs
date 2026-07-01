using OpenTK;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections.Concurrent;
using System.Runtime;
using TextCraft.src.Core.EntityModule;
using TextCraft.src.Core.Input;
using TextCraft.src.Core.Physic;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Table;
using TextCraft.src.Tools;
using TextCraft.src.UI;

namespace TextCraft.src.Core
{
    public enum GameState{ MainMenu,InGame}
    internal class Game : GameWindow
    {
        int _seed = 0;
        public GameState _state = GameState.MainMenu;
        GameSession session = new GameSession();
        UIMgr uIMgr = new UIMgr();

        ConcurrentQueue<string> _commandQueue = new();

        public Game(int width,int height,string title) : base(GameWindowSettings.Default,
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
            VSync = VSyncMode.On;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            uIMgr.Load();

            uIMgr.uITable["gamePanel"].Sleep();
            uIMgr.uITable["mainMenuPanel"].Wake();
            uIMgr.uITable["mainMenuText"].Wake();
            

            Console.WriteLine("输入 /LoadWorld [seed] 加载世界");
            Console.WriteLine("输入 /UnLoadWorld 卸载世界");

            Task.Run(async () => {
                while (true)
                {
                    // ReadLine 本身是阻塞的，但它阻塞的是这个子线程，不是你的游戏主线程！
                    string command = await Console.In.ReadLineAsync() ?? "";
                    if (command != null)
                    {
                        // 将命令放入一个线程安全的队列，在主线程 Update 里去处理
                        _commandQueue.Enqueue(command);
                    }
                }
            });
        }

        public void SwitchState(GameState state)
        {
            if(_state == GameState.InGame && session.IsLoad)
            {
                session.UnLoadWorld();
                Console.WriteLine("正在卸载世界中");
            }

            _state = state;

            if (state == GameState.MainMenu)
            {
                GL.ClearColor(0,0,0,0);
                uIMgr.uITable["gamePanel"].Sleep();
                uIMgr.uITable["mainMenuPanel"].Wake();
            }
            else
            {
                uIMgr.uITable["gamePanel"].Wake();
                uIMgr.uITable["mainMenuPanel"].Sleep();
            }

            
            uIMgr.uITable.OnSizeChange(Size);
        }

        public void OnLoadWorld()
        {
            SwitchState(GameState.InGame);
            Console.WriteLine("正在创建世界中");
            CursorState = CursorState.Grabbed;
            session.LoadWorld(_seed);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //渲染
            //renderer.GetCamera(world.playerPos, world.playerDir);
            //renderer.Draw();
            session.Render();
            uIMgr.Render();

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            session.Update((float)UpdateTime);

            if(_commandQueue.TryDequeue(out var command))
            {
                string[] s = command.Split();
                if(s.Length == 2 && s[0] == "/LoadWorld")
                {
                    _seed = int.Parse(s[1]);

                    Console.WriteLine("准备创建世界");

                    OnLoadWorld();

                    session.GameRender?.OnSizeChange(Size);

                    //this.WindowState = WindowState.Fullscreen;
                }
                else if (s.Length == 1 && s[0] == "/UnLoadWorld")
                {
                    Console.WriteLine("准备卸载世界");
                    SwitchState(GameState.MainMenu);
                }
            }

            //Console.Clear();
            //Console.WriteLine("FPS: " + 1 / UpdateTime);
            //Console.WriteLine("Time: " + session.World != null ? (int)session.World.GameTime:0);
        }
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
            //renderer.OnSizeChange(Size);
            session.GameRender?.OnSizeChange(Size);
            uIMgr.OnSizeChange(Size);
        }

        static void Main(string[] args)
        {
            GCSettings.LatencyMode = GCLatencyMode.Interactive;
            using (Game game = new Game(800, 600, "craft"))
            {
                game.Run();
            }
        }
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (session.IsLoad && session.World != null)
            {
                World world = session.World;
                if (e.Key == Keys.Escape)
                {
                    if (CursorState == CursorState.Grabbed) CursorState = CursorState.Normal;
                    else if (CursorState != CursorState.Grabbed) CursorState = CursorState.Grabbed;
                }
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                    if (e.Key == Keys.W) input.forward = true;
                    if (e.Key == Keys.S) input.back = true;
                    if (e.Key == Keys.A) input.left = true;
                    if (e.Key == Keys.D) input.right = true;
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
                    world.ecsMgr.AddComponent(entity, input);
                }
            }
        }
        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);
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
            if (session.IsLoad && session.World != null)
            {
                World world = session.World;
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
            base.OnMouseDown(e);
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
            if (session.IsLoad && session.World != null)
            {
                World world = session.World;
                int nowBlock = 1;
                foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
                {
                    var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                    if (e.OffsetY > 0)
                    {
                        input.nowBlock -= 1;
                        input.nowBlock = Math.Clamp(input.nowBlock, 1, 10);
                    }
                    else if (e.OffsetY < 0)
                    {
                        input.nowBlock += 1;
                        input.nowBlock = Math.Clamp(input.nowBlock, 1, 10);
                    }
                    nowBlock = input.nowBlock;
                    world.ecsMgr.AddComponent(entity, input);
                }
                (uIMgr.uITable["displayBlock"] as Spirit)?.SetSpirit(SpiritTable.Ins.BlockSpirits[nowBlock]);
            }
        }
    }
}
