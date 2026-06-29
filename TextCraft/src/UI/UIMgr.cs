using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Core;
using TextCraft.src.Rendering;
using TextCraft.src.Rendering.UI;
using OpenTK.Mathematics;

namespace TextCraft.src.UI
{
    internal class UIMgr
    {
        public IRenderer? UiRender { get; private set; }
        public UITable uITable { get; private set; }

        public UIMgr()
        {
            UiRender = new UIRenderer(this);
            uITable = new UITable();
        }
        public void Load()
        {
            UiRender?.Load();
            uITable?.LoadUI();
        }

        public void Render()
        {
            UiRender?.Draw();
        }

        public void Update(float updateTime)
        {
            
        }

        public void UnLoad()
        {

        }

        public void OnSizeChange(Vector2i size)
        {
            UiRender?.OnSizeChange(size);
            uITable.OnSizeChange(size);
        }
    }
}
