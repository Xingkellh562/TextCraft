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
        private bool _awake = true;
        public bool Awake => _awake;

        public Rect rect;
        public readonly UIRectMesh rectMesh;

        public UIComponent? parent;
        public List<UIComponent> subObjects = new();

        public UIComponent(Rect rect)
        {
            this.rect = rect;
            rectMesh = new UIRectMesh(new float[] { });
        }

        public void AddSubComponent(UIComponent component)
        {
            component.parent = this;
            subObjects.Add(component);
        }

        public void Traverse(List<UIComponent> result,bool allCompenents)
        {
            if ((_awake || allCompenents) && result != null)
            {
                result.Add(this);
                foreach (var obj in subObjects)
                {
                    obj.Traverse(result, allCompenents);
                }
            }
        }

        public List<UIComponent> Traverse(bool allCompenents)
        {
            var result = new List<UIComponent>();
            var stack = new Stack<UIComponent>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current._awake || allCompenents)   // 可按需控制条件
                {
                    result.Add(current);
                    // 注意：逆序压栈以保持原顺序（若需要）
                    for (int i = current.subObjects.Count - 1; i >= 0; i--)
                        stack.Push(current.subObjects[i]);
                }
            }
            return result;
        }

        public void SetSpirit(int[] spirit)
        {
            this.rect.spirit = (spirit.Clone() as int[]);
            Anchor.GetPosWithAnchor(this);
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
