using OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using TextCraft.src.Rendering;
using TextCraft.src.Tools;
using OpenTK.Graphics;
using System.Runtime;

namespace TextCraft.src.Core
{
    internal class Game : GameWindow
    {
        IRenderer renderer;

        World world = new World();

        public Game(int width,int height,string title) : base(GameWindowSettings.Default,
            new NativeWindowSettings
            { 
                ClientSize = new OpenTK.Mathematics.Vector2i(width, height),
                Title = title,
                APIVersion = new Version(3,3),
                Profile = ContextProfile.Core,
                Flags = ContextFlags.ForwardCompatible,
            })
            
        {
            VSync = VSyncMode.On;
            renderer = new GameRenderer(world);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            CursorState = CursorState.Grabbed;

            renderer.Load();

            world.Load();

        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            //渲染
            renderer.GetCamera(world.playerPos, world.playerDir);
            renderer.Draw();

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            world.Update((float)UpdateTime);

            Console.Clear();
            Console.WriteLine("FPS: " + 1 / UpdateTime);
            Console.WriteLine("Time: " + (int)world.GameTime);
        }
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
            renderer.OnSizeChange(Size);
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
            if (e.Key == Keys.W) world.inputMgr.forward = true;
            if (e.Key == Keys.S) world.inputMgr.back = true;
            if (e.Key == Keys.A) world.inputMgr.left = true;
            if (e.Key == Keys.D) world.inputMgr.right = true;
            if (e.Key == Keys.Space) 
            { 
                world.inputMgr.up = true;
                if(world.player.onGround)
                    world.player.velocity += Vector3.UnitY *15;
            }
            if (e.Key == Keys.LeftShift) world.inputMgr.down = true;
            if (e.Key == Keys.F12) GC.Collect();
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key == Keys.W) world.inputMgr.forward = false;
            if (e.Key == Keys.S) world.inputMgr.back = false;
            if (e.Key == Keys.A) world.inputMgr.left = false;
            if (e.Key == Keys.D) world.inputMgr.right = false;
            if (e.Key == Keys.Space) world.inputMgr.up = false;
            if (e.Key == Keys.LeftShift) world.inputMgr.down = false;
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);
            Vector4 move = new Vector4(world.playerDir, 0) * world.inputMgr.MouseMoveX(0.5f, e.X);
            move *= world.inputMgr.MouseMoveY(0.5f, e.Y, world.playerDir);
            world.playerDir = new Vector3(move.X,move.Y,move.Z);
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButton.Left)
                world.inputMgr.LeftMouseButton(world);
            if (e.Button == MouseButton.Right)
                world.inputMgr.RightMouseButton(world);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.OffsetY > 0)
            {
                world.inputMgr.nowBlock -= 1;
                world.inputMgr.nowBlock = Math.Clamp(world.inputMgr.nowBlock, 1, 9);
            }
            else if (e.OffsetY < 0)
            {
                world.inputMgr.nowBlock += 1;
                world.inputMgr.nowBlock = Math.Clamp(world.inputMgr.nowBlock, 1, 9);
            }
        }
    }
}
