using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Rendering
{
    internal class UIRenderer : IRenderer
    {
        UIShader _shader = new UIShader();

        Vector2i _size;

        public void Load()
        {
            _shader.CreateShaderProgram();

            _shader.LoadTexture(AppContext.BaseDirectory + "Resources\\blockatlas1.png");

            _shader.GetVertices();
        }
        public void Draw()
        {
            //GL.ClearColor(1, 0, 0, 0);

            _shader.GetMatrix(_size);

            GL.Disable(EnableCap.DepthTest);
            //GL.DepthMask(false);

            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _shader.Draw();
        }

        public void GetCamera(Vector3 pos, Vector3 dir) { }

        public void OnSizeChange(Vector2i size)
        {
            _size = size;
        }
    }
}
