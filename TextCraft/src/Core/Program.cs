using OpenTK;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp.Drawing;
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

namespace TextCraft.src.Core
{
    internal class Game : GameWindow
    {
        int _seed = 0;
        
        public GameSession session = new GameSession();
        public UIMgr uIMgr = new UIMgr();
        public GameStateMgr gameStateMgr;
        public ConsoleMgr consoleMgr = new ConsoleMgr();

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
            gameStateMgr = new(this);
            EventMgr.Ins.Subscribe(typeof(LoadWorldEventArg),arg => OnLoadWorld(arg as LoadWorldEventArg ?? new()));
            EventMgr.Ins.Subscribe(typeof(UnLoadWorldEventArg), arg => OnUnLoadWorld(arg as UnLoadWorldEventArg ?? new()));
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            uIMgr.Load();
            consoleMgr.Load();

            uIMgr.uITable["gamePanel"].Sleep();
            uIMgr.uITable["mainMenuPanel"].Wake();
            uIMgr.uITable["mainMenuText"].Wake();
        }

        
        public void OnLoadWorld(LoadWorldEventArg arg)
        {
            string name = arg.name;
            _seed = arg.seed;

            Console.WriteLine("准备创建世界");
            
            gameStateMgr.SwitchState(GameState.InGame);
            Console.WriteLine("正在创建世界中");
            CursorState = CursorState.Grabbed;

            session.GameRender?.OnSizeChange(Size);
            session.LoadWorld(name,_seed);
            WindowState = WindowState.Maximized;
        }

        public void OnUnLoadWorld(UnLoadWorldEventArg arg)
        {
            Console.WriteLine("准备卸载世界");
            gameStateMgr.SwitchState(GameState.MainMenu);
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            session.Render();

            uIMgr.Render();

            SwapBuffers();
        }

        float _time = 1.0f;
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            session.Update((float)UpdateTime);

            consoleMgr.Update();

            //Console.Clear();
            if(uIMgr.uITable["mainMenuText"] is TextComponent fps && _time > 1)
            {
                fps.ChangeContent("FPS: " + (int)(1.0f / UpdateTime));
                _time = 0.0f;
            }

            _time += (float)UpdateTime;
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

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            EventMgr.Ins.Publish(new UnLoadWorldEventArg() { });
        }

        static void Main(string[] args)
        {
            ConfigMgr.Ins.OnLoad(System.IO.Path.Combine(AppContext.BaseDirectory + "Config\\config.xml"));
            //ModelTable.Ins.Save(System.IO.Path.Combine(AppContext.BaseDirectory + "Config\\Tables\\modelTable.xml"));

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
                (uIMgr.uITable["displayBlock"] as SpiritComponent)?.SetSpirit(AtlasTable.Ins.uiAtlas.Spirits[nowBlock.ToString()].rect);
            }
        }
    }
}
