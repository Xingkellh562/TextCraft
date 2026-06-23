using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Tools
{
    public class PerlinNoise
    {
        private readonly int[] permutation = new int[512];
        private readonly int[] p;

        // 标准梯度方向（8个，也可用12个）
        private static readonly int[,] grad3 = {
        {1,1}, {-1,1}, {1,-1}, {-1,-1},
        {1,0}, {-1,0}, {0,1}, {0,-1}
    };

        public PerlinNoise(int seed = 0)
        {
            var rand = new Random(seed);
            // 初始化置换表 0~255
            int[] perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }
            // 填充两次，避免越界
            for (int i = 0; i < 512; i++) permutation[i] = perm[i & 255];
            p = permutation;
        }

        // 缓和曲线
        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        // 线性插值
        private static double Lerp(double a, double b, double t) => a + t * (b - a);

        // 获取晶格顶点的梯度点积结果
        private double Grad(int hash, double x, double y)
        {
            int h = hash & 7;          // 8个梯度方向
            double u = h < 4 ? x : y;
            double v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public double Noise(double x, double y)
        {
            // 找到晶格坐标
            int xi = (int)Math.Floor(x) & 255;
            int yi = (int)Math.Floor(y) & 255;
            double fx = x - Math.Floor(x);
            double fy = y - Math.Floor(y);

            // 缓和曲线权值
            double u = Fade(fx);
            double v = Fade(fy);

            // 四个角点的哈希值
            int aa = p[p[xi] + yi];
            int ab = p[p[xi] + yi + 1];
            int ba = p[p[xi + 1] + yi];
            int bb = p[p[xi + 1] + yi + 1];

            // 点积并插值
            double x1 = Lerp(Grad(aa, fx, fy), Grad(ba, fx - 1, fy), u);
            double x2 = Lerp(Grad(ab, fx, fy - 1), Grad(bb, fx - 1, fy - 1), u);
            double result = Lerp(x1, x2, v);

            // 结果范围大约 [-0.8,0.8]，映射到 [0,1]
            return (result + 0.8) / 1.6;
        }
    }
}
