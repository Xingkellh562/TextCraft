using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextCraft.src.Table;

namespace TextCraft.src.Tools
{
    internal static class ModelCreator
    {
        public static QuadFace[] CreateModel(Model model,int[] u,int[] v)
        {
            QuadFace[] result = new QuadFace[model.faces.GetLength(0)];
            for(int i = 0; i < model.faces.Length; i++)
            {
                var face = new QuadFace(model.faces[i]);
                for (int j = 3; j < model.faces[i].Length; j += 6)
                {
                    face.vertices[j] += i < u.Length ? u[i] : u[^1];
                    face.vertices[j+1] += i < v.Length ? v[i] : v[^1];
                }
                result[i] = face;
            }
            return result;
        }

    }
}
