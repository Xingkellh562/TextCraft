using System;
using OpenTK.Mathematics;
using TextCraft.src.Rendering;
using TextCraft.src.UI;

namespace TextCraft.src.Rendering.UI
{
    /// <summary>
    /// UI 顶点：位置（屏幕像素）+ 纹理坐标 + 顶点色。
    /// 顶点色让"换色"不再触发新的 DrawCall（旧实现每个颜色都要独立 uniform）。
    /// 布局：8 个 float = 32 字节，与 <see cref="UIShader"/> 中的 attribution 一致。
    /// </summary>
    public struct UIVertex
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Vector4 Color;

        public const int Stride = 8 * sizeof(float);

        public UIVertex(Vector2 position, Vector2 texCoord, Vector4 color)
        {
            Position = position;
            TexCoord = texCoord;
            Color = color;
        }
    }

    /// <summary>
    /// 采样方式。字体图集是"白色 + alpha 覆盖率"的遮罩，用 <see cref="AlphaMask"/>
    /// 可以让文字颜色完全由顶点色决定；普通精灵用 <see cref="Rgba"/> 做整体着色（染色）。
    /// </summary>
    public enum UIMaterial : byte
    {
        Rgba = 0,
        AlphaMask = 1,
    }

    /// <summary>
    /// 合批键：纹理 + 采样方式 + 裁剪矩形。键相同的绘制单元才能合并进同一次 DrawCall。
    /// </summary>
    public readonly struct UIBatchKey : IEquatable<UIBatchKey>
    {
        public readonly Texture? Texture;
        public readonly UIMaterial Material;
        public readonly UIRect Clip;

        public UIBatchKey(Texture? texture, UIMaterial material, in UIRect clip)
        {
            Texture = texture;
            Material = material;
            Clip = clip;
        }

        /// <summary>纹理唯一 Id（无纹理时为 0），用于日志与统计。</summary>
        public int TextureId => Texture?.Id ?? 0;

        public bool Equals(UIBatchKey other)
            => ReferenceEquals(Texture, other.Texture) && Material == other.Material && Clip == other.Clip;

        public override bool Equals(object? obj) => obj is UIBatchKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(TextureId, (byte)Material, Clip);

        public static bool operator ==(UIBatchKey a, UIBatchKey b) => a.Equals(b);
        public static bool operator !=(UIBatchKey a, UIBatchKey b) => !a.Equals(b);
    }
}
