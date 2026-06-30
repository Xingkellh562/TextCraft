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
            try
            {
                
                _shader.atlas.Load(Path.Combine(AppContext.BaseDirectory + "Resources\\ui\\UISpirits.png"), new Tools.TextureLoader());
            }
            catch
            {
                Console.WriteLine("无法找到资源文件，请确保已将项目目录下的RequiredResources文件夹中的所有文件全部复制到编译输出目录");
            }
            
            //_shader.GetVertices();
        }
        public void Draw()
        {
            GL.Disable(EnableCap.DepthTest);
            _shader.GetMatrix( _size);

            List<UIComponent> uiCs = new List<UIComponent>();
            _uIMgr.uITable.components.Traverse(uiCs,false);
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
