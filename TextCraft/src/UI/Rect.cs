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
        public Vector2 pos;
        public Vector2 size;

        public Vector4 color;
        public float colorCoefficient;

        public int[] spirit = new int[4] {0,0,1,1 };

        public static float[] GetVertices(Rect rect)
        {
            float[] vertices =
            {
                rect.pos.X              ,rect.pos.Y              ,rect.spirit[0],rect.spirit[1],
                rect.pos.X + rect.size.X,rect.pos.Y              ,rect.spirit[2],rect.spirit[1],
                rect.pos.X + rect.size.X,rect.pos.Y + rect.size.Y,rect.spirit[2],rect.spirit[3],
                rect.pos.X              ,rect.pos.Y              ,rect.spirit[0],rect.spirit[1],
                rect.pos.X + rect.size.X,rect.pos.Y + rect.size.Y,rect.spirit[2],rect.spirit[3],
                rect.pos.X              ,rect.pos.Y + rect.size.Y,rect.spirit[0],rect.spirit[3],
            };
            return vertices;
        }
    }
}
