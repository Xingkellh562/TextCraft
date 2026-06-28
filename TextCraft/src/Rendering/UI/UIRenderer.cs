using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.UI;

namespace TextCraft.src.Rendering.UI
{
    internal class UIRenderer : IRenderer
    {
        UIShader _shader = new UIShader();

        Vector2i _size;

        private UIMgr _uIMgr;

        public UIRenderer(UIMgr uIMgr)
        {
            _uIMgr = uIMgr;
        }

        public void Load()
        {
            _shader.CreateShaderProgram();

            _shader.Load(AppContext.BaseDirectory + "Resources\\ui\\sight.png",new Tools.TextureLoader());
            //_shader.GetVertices();
        }
        public void Draw()
        {
            
            _shader.GetMatrix(_size);

            GL.Disable(EnableCap.DepthTest);

            List<UIComponent> uiCs = new List<UIComponent>();
            _uIMgr.components.Traverse(uiCs);
            foreach (var component in uiCs)
            {
                _shader.GetRectMesh(component.rectMesh);
                _shader.Draw();
            }
        }

        public void GetCamera(Vector3 pos, Vector3 dir) { }

        public void OnSizeChange(Vector2i size)
        {
            _size = size;
        }
    }
}
