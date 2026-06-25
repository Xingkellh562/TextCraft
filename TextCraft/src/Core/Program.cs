using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Runtime;
using TextCraft.src.Core.EntityModule;
using TextCraft.src.Core.Input;
using TextCraft.src.Core.Physic;
using TextCraft.src.Rendering;
using TextCraft.src.Tools;

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

            //Console.Clear();
            //Console.WriteLine("FPS: " + 1 / UpdateTime);
            //Console.WriteLine("Time: " + (int)world.GameTime);
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
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] {typeof(InputComponent)}))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                if (e.Key == Keys.W) input.forward = true;
                if (e.Key == Keys.S) input.back = true;
                if (e.Key == Keys.A) input.left = true;
                if (e.Key == Keys.D) input.right = true;
                if (e.Key == Keys.Space) input.up = true;
                if (e.Key == Keys.LeftShift) input.down = true;
                if (e.Key == Keys.F12) GC.Collect();

                world.ecsMgr.AddComponent(entity, input);
            }
            
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                if (e.Key == Keys.W) input.forward = false;
                if (e.Key == Keys.S) input.back = false;
                if (e.Key == Keys.A) input.left = false;
                if (e.Key == Keys.D) input.right = false;
                if (e.Key == Keys.Space) input.up = false;
                if (e.Key == Keys.LeftShift) input.down = false;

                world.ecsMgr.AddComponent(entity, input);
            }
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);
                input.mouseX = e.X;
                input.mouseY = e.Y;
                world.ecsMgr.AddComponent(entity, input);
            }
            //Vector4 move = new Vector4(world.playerDir, 0) * world.inputMgr.MouseMoveX(0.5f, e.X);
            //move *= world.inputMgr.MouseMoveY(0.5f, e.Y, world.playerDir);
            //world.playerDir = new Vector3(move.X,move.Y,move.Z);
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                if (e.Button == MouseButton.Left) input.destory = true;
                if (e.Button == MouseButton.Right) input.build = true;

                world.ecsMgr.AddComponent(entity, input);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                if (e.Button == MouseButton.Left) input.destory = false;
                if (e.Button == MouseButton.Right) input.build = false;

                world.ecsMgr.AddComponent(entity, input);
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            foreach (var entity in world.ecsMgr.GetEntitiesWith(new Type[] { typeof(InputComponent) }))
            {
                var input = world.ecsMgr.GetComponent<InputComponent>(entity);

                if (e.OffsetY > 0)
                {
                    input.nowBlock -= 1;
                    input.nowBlock = Math.Clamp(input.nowBlock, 1, 9);
                }
                else if (e.OffsetY < 0)
                {
                    input.nowBlock += 1;
                    input.nowBlock = Math.Clamp(input.nowBlock, 1, 9);
                }

                world.ecsMgr.AddComponent(entity, input);
            }
            
        }
    }
}
