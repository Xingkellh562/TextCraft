using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.UI
{
    public class Rect
    {
        public Vector2i posA;
        public Vector2i posB;
        public Vector2i size;

        public Vector4 color;
        public float colorCoefficient;

        public AnchorPoint UpperLeftPoint;
        public AnchorPoint LowerRightPoint;
        public Rect() { }

        public Rect(int left,int top,int width,int height)
        {
            posA = new Vector2i(left,top);
            posB = new Vector2i(left+width,top + height);
            size = new Vector2i(width,height);
        }
    }
}
