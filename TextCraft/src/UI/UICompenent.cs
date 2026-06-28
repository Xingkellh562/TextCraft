using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    public class UIComponent
    {
        private bool _awake = false;
        public bool Awake => _awake;

        public Rect rect;
        public readonly UIRectMesh rectMesh;

        public UIComponent? parent;
        public List<UIComponent> subObjects = new();

        public UIComponent(Rect rect)
        {
            this.rect = rect;

            rectMesh = new UIRectMesh(Rect.GetVertices(rect));
        }

        public void AddSubComponent(UIComponent component)
        {
            component.parent = this;
            subObjects.Add(component);
        }

        public void Traverse(List<UIComponent> result)
        {
            if(_awake && result != null)
            {
                result.Add(this);
                foreach (var obj in subObjects)
                    obj.Traverse(result);
            }
        }

        public void Wake()
        {
            _awake = true;
        }

        public void Sleep()
        {
            _awake = false;
        }
    }
}
