using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using TextCraft.src.Core;
using TextCraft.src.Core.Config;
using TextCraft.src.Rendering.OIT;
using TextCraft.src.Tools;

namespace TextCraft.src.Rendering
{
    internal interface IRenderer
    {
        public void Load();
        //用于画面渲染的函数
        public void Draw();

        public void GetCamera(Vector3 pos,Vector3 dir);

        public void OnSizeChange(Vector2i size);
    }

    internal class GameRenderer : IRenderer
    {
        GameShader shader = new GameShader();

        OITContext context;

        Vector2i _size = new Vector2i();

        Vector3 cameraPos = new Vector3();
        Vector3 cameraDir = new Vector3();

        World world;
        public Vector3 ClearColor { get; set; } = new Vector3(0.6f, 0.7f, 1);

        public GameRenderer(World world)
        {
            this.world = world;

            context = new OITContext();
        }

        public void Load()
        {
            shader.CreateShaderProgram();

            shader.atlas.Load(Path.Combine(AppContext.BaseDirectory + "Resources\\blockatlas1.png"),new TextureLoader(),16,16,56);
            context.OitShader.atlas.Load(Path.Combine(AppContext.BaseDirectory + "Resources\\blockatlas1.png"), new TextureLoader(), 16, 16, 56);

            //启用一些设置
            context.GetFullScreen();

            context.ReSize(_size);

            GL.DepthFunc(DepthFunction.Lequal);

            GL.Enable(EnableCap.CullFace);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        public void Draw()
        {
            //Clear();
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Multisample);
            GL.DepthMask(true);

            shader.GetMatrix(cameraPos, cameraDir, _size);
            OITShader oITShader = context.OitShader;
            oITShader.GetMatrix(cameraPos, cameraDir, _size);

            if (ConfigMgr.Ins.graphicConfig.fog)
            {
                shader.SetFog(ClearColor, ConfigMgr.Ins.graphicConfig.ViewRange - 48, ConfigMgr.Ins.graphicConfig.ViewRange);
                oITShader.SetFog(ClearColor * 0.7f, ConfigMgr.Ins.graphicConfig.ViewRange - 48, ConfigMgr.Ins.graphicConfig.ViewRange);
            }
            if (world.chunkDataMgr.GetBlock((int)cameraPos.X, (int)cameraPos.Y, (int)cameraPos.Z) == 144)
            {
                shader.SetFog(new Vector3(0.2f, 0.4f, 0.8f), 4, 16);
                oITShader.SetFog(new Vector3(0.2f, 0.4f, 0.8f), 4, 16);
                GL.ClearColor(0.2f, 0.4f, 0.8f, 1.0f);
            }
            else
                GL.ClearColor(ClearColor.X, ClearColor.Y, ClearColor.Z, 0);

            GL.Enable(EnableCap.CullFace);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            foreach (var grid in world.gridMgr.grids["default"].Values)
            {
                shader.GetGrid(grid);
                shader.Draw();
            }
            foreach (var grid in world.gridMgr.grids["Entity"].Values)
            {
                shader.GetGrid(grid);
                shader.Draw();
            }
            context.BeginRenderTransparentGrid();
            //GL.DepthMask(false);

            List<Grid> grids = world.gridMgr.grids["lucency"].Values.ToList<Grid>();
            grids.Sort((a, b) => -(a.Pos - cameraPos).Length.CompareTo((b.Pos - cameraPos).Length));

            foreach (var grid in grids)
            {
                oITShader.GetGrid(grid);
                oITShader.Draw();
            }
            GL.Viewport(0, 0, _size.X, _size.Y);
            context.Composite();

            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(true);

            //var error = GL.GetError();
            //if (error != ErrorCode.NoError)
            //    Console.WriteLine($"绘制透明物体时发生OpenGL错误: {error}");
        }

        public void GetCamera(Vector3 pos, Vector3 dir)
        {
            cameraDir = dir;
            cameraPos = pos;
        }

        public void OnSizeChange(Vector2i size)
        {
            _size = size;
            context.ReSize(_size);
        }

        public static void Clear()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
    }
}
