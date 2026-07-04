using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Table;

namespace TextCraft.src.UI
{
    public class UITable
    {
        public UIComponent components = new Panel(new Rect());
        Dictionary<string,UIComponent> _componentDic = new Dictionary<string,UIComponent>();

        public UIComponent this[string name]
        {
            get
            {
                if (_componentDic.ContainsKey(name))
                    return _componentDic[name];
                else return components;
            }
        }

        public UITable()
        {

        }
        public void LoadUI()
        {
            Rect gameRect = new Rect(0, 0, 0, 0) { UpperLeftPoint = AnchorPoint.UpperLeft, LowerRightPoint = AnchorPoint.LowerRight };
            Rect sightRect = new Rect(-8, -8, 16, 16) { UpperLeftPoint = AnchorPoint.Center, LowerRightPoint = AnchorPoint.Center };
            Rect menuRect = new Rect(-128, -64, 256, 32) { UpperLeftPoint = AnchorPoint.Center, LowerRightPoint = AnchorPoint.Center };
            Rect menuTextRect = new Rect(0, 48, 512, 32) { UpperLeftPoint = AnchorPoint.UpperLeft, LowerRightPoint = AnchorPoint.None };
            Rect displayRect = new Rect(0, 128, 192, 192) { UpperLeftPoint = AnchorPoint.UpperLeft, LowerRightPoint = AnchorPoint.None };
            Rect displayBlockRect = new Rect(40, 40, 112, 112) { UpperLeftPoint = AnchorPoint.UpperLeft, LowerRightPoint = AnchorPoint.None };

            var gamePanel = new Panel(gameRect);
            var sight = new SpiritComponent(sightRect);
            var mainMenuPanel = new SpiritComponent(menuRect);
            var mainMenuText = new TextComponent(menuTextRect);
            var displayPanel = new SpiritComponent(displayRect);
            var displayBlock = new SpiritComponent(displayBlockRect);

            mainMenuPanel.SetSpirit(AtlasTable.Ins.uiAtlas.Spirits["title"].rect);
            sight.SetSpirit(AtlasTable.Ins.uiAtlas.Spirits["sight"].rect);
            displayPanel.SetSpirit(AtlasTable.Ins.uiAtlas.Spirits["displayTable"].rect);
            displayBlock.SetSpirit(AtlasTable.Ins.uiAtlas.Spirits["1"].rect);
            mainMenuText.ChangeContent("FPS:");


            AddComponent(gamePanel, components, "gamePanel");
            AddComponent(mainMenuText, components, "mainMenuText");
            AddComponent(mainMenuPanel, components, "mainMenuPanel");
            AddComponent(displayPanel, gamePanel, "displayPanel");
            AddComponent(displayBlock, displayPanel, "displayBlock");
            gamePanel.AddSubComponent(sight);
        }
        public void AddComponent(UIComponent component,UIComponent parent, string name)
        {
            parent.AddSubComponent(component);
            _componentDic[name] = component;
        }

        public void OnSizeChange(Vector2i size)
        {
            components.rect.posA = Vector2i.Zero;
            components.rect.size = size;
            List<UIComponent> uiCs = new List<UIComponent>();
            components.Traverse(uiCs,true);
            for (int i = 0;i< uiCs.Count;i++)
            {
                Anchor.GetPosWithAnchor(uiCs[i]);
            }
        }
    }
}
