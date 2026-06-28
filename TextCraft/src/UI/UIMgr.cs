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
        public UIComponent components = new(new Rect());
        public IRenderer? UiRender { get; private set; }

        public UIComponent gamePanel;
        public UIComponent mainMenuPanel;

        public UIMgr()
        {
            UiRender = new UIRenderer(this);

            Rect gameRect = new Rect() { pos = new Vector2(398, 302),size = new Vector2(16,16)};
            Rect menuRect = new Rect() { pos = new Vector2(20, 20), size = new Vector2(100, 100) };

            gamePanel = new UIComponent(gameRect);
            mainMenuPanel = new UIComponent(menuRect);
        }
        public void Load()
        {
            UiRender?.Load();
            components.Wake();
            components.AddSubComponent(gamePanel);
            components.AddSubComponent(mainMenuPanel);
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


    }
}
