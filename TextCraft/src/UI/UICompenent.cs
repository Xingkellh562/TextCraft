using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Rendering.UI;
using TextCraft.src.Tools;

namespace TextCraft.src.UI
{
    public abstract class UIComponent
    {
        private bool _awake = true;
        public bool Awake => _awake;

        public Rect rect;


        public UIComponent? parent;
        public List<UIComponent> subObjects = new();

        public UIComponent(Rect rect)
        {
            this.rect = rect;

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



        public void Wake()
        {
            _awake = true;
        }

        public void Sleep()
        {
            _awake = false;
        }
    }

    public class Panel : UIComponent
    {
        public Panel(Rect rect) : base(rect)
        {
        }
    }

    public class Spirit : UIComponent
    {
        public int[]? spirit = new int[4] { 0, 0, 1, 1 };

        public readonly UIRectMesh rectMesh;
        public Spirit(Rect rect) : base(rect)
        {
            rectMesh = new UIRectMesh(new float[] { });
        }

        public void SetSpirit(int[] spirit)
        {
            this.spirit = (spirit.Clone() as int[]);
            Anchor.GetPosWithAnchor(this);
        }

        public void GetVertices(Vector2i posA, Vector2i posB, int[] spirit)
        {
            float[] vertices =
            {
                posA.X ,posA.Y ,spirit[0] ,spirit[1],
                posB.X ,posA.Y ,spirit[2] ,spirit[1],
                posB.X ,posB.Y ,spirit[2] ,spirit[3],
                posA.X ,posA.Y ,spirit[0] ,spirit[1],
                posB.X ,posB.Y ,spirit[2] ,spirit[3],
                posA.X ,posB.Y ,spirit[0] ,spirit[3],
            };
            rectMesh.vertices = vertices;
            rectMesh.isUpdate = false;
        }
    }

    public class Text : UIComponent
    {
        public string Content { get; private set; } = "";

        public readonly UIRectMesh textMesh;
        public Text(Rect rect) : base(rect) { textMesh = new(new float[] { }); }
        public Text(Rect rect,string content) : base(rect)
        {
            this.Content = content;
            textMesh = new(new float[] { });
            ComposeAndGetTextMesh();
        }

        public void ChangeContent(string content)
        {
            Content = content;
            ComposeAndGetTextMesh();
        }

        public void ComposeAndGetTextMesh(int spacingX = 2,int spacingY = 2,int size = 24)
        {
            textMesh.vertices = new float[24 * Content.Length];
            int nowIndex = 0;
            int curX = rect.posA.X + spacingX, curY = rect.posA.Y + spacingY;
            foreach (var c in Content)
            {
                if (curX + size + spacingX > rect.size.X)
                {
                    curX = spacingX + rect.posA.X;
                    curY += size + spacingY;
                }
                float[] vertices = GetVertices(curX, curY, size, c);
                for(int i = 0; i < vertices.Length; i++)
                {
                    textMesh.vertices[i + nowIndex] = vertices[i];
                }
                curX += spacingX + size;
                nowIndex += 24;
            }
        }

        private float[] GetVertices(int x,int y,int size,char charactor)
        {
            if (!FontLoader.Ins.GlyphMap.TryGetValue(charactor,out var info)) return new float[0];
            float[] vertices =
            {
                x      ,y      ,info.UV.X                 ,info.UV.Y, 
                x+size ,y      ,info.UV.X + info.UV.Width ,info.UV.Y,
                x+size ,y+size ,info.UV.X + info.UV.Width ,info.UV.Y + info.UV.Height ,
                x      ,y      ,info.UV.X                 ,info.UV.Y,
                x+size ,y+size ,info.UV.X + info.UV.Width ,info.UV.Y + info.UV.Height,
                x      ,y+size ,info.UV.X                 ,info.UV.Y + info.UV.Height,
            };
            return vertices;
        }
    }
}
