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
        /// <summary>
        /// 把模型的面数据烘焙成带图集 UV 的四边形：
        /// 顶点 UV 只能是 0/1，先向内收缩 0.4375（对应图集的 padding），再按面叠加 tile 偏移 u/v。
        /// u/v 数组按面索引取值，长度不足时沿用最后一个值（与旧配置中“只写一个值代表所有面”的语义一致）。
        /// </summary>
        public static QuadFace[] CreateModel(Model model, int[] u, int[] v)
        {
            if (model?.faces == null || model.faces.Length == 0)
                return Array.Empty<QuadFace>();

            var result = new QuadFace[model.faces.Length];
            for (int i = 0; i < model.faces.Length; i++)
            {
                var source = model.faces[i] ?? Array.Empty<float>();
                var face = new QuadFace(source);

                int tileU = u.Length == 0 ? 0 : (i < u.Length ? u[i] : u[^1]);
                int tileV = v.Length == 0 ? 0 : (i < v.Length ? v[i] : v[^1]);

                for (int j = 3; j + 1 < face.vertices.Length; j += 6)
                {
                    if (face.vertices[j] == 0.0f) face.vertices[j] += 0.4375f;
                    else face.vertices[j] -= 0.4375f;
                    if (face.vertices[j + 1] == 0.0f) face.vertices[j + 1] += 0.4375f;
                    else face.vertices[j + 1] -= 0.4375f;
                    face.vertices[j] += tileU;
                    face.vertices[j + 1] += tileV;
                }

                result[i] = face;
            }

            return result;
        }
    }
}
