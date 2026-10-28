using System;
using OpenTK.Mathematics;

namespace TextCraft.src.UI
{
    /// <summary>
    /// UI 矩形（屏幕像素坐标，左上角为原点，Y 轴向下）。
    /// 与旧版 <c>Rect</c> 的区别：这是一个值类型，同时保存 min/max 语义，
    /// 由布局系统在每帧计算，不再被锚点逻辑原地改写（旧实现重复调用会累加偏移）。
    /// </summary>
    public struct UIRect : IEquatable<UIRect>
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public UIRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public static UIRect FromMinMax(Vector2 min, Vector2 max)
            => new UIRect(min.X, min.Y, max.X - min.X, max.Y - min.Y);

        /// <summary>
        /// "不裁剪"矩形。注意用有限大值而不是 ±Infinity：
        /// <c>X + Width</c> 在 X=-inf、Width=+inf 时会得到 NaN，
        /// 一旦参与 Intersect 就会把整棵子树裁成空（曾经的 ClipChildren 失效 bug）。
        /// </summary>
        public const float InfiniteExtent = 1_000_000f;

        public static readonly UIRect Infinite = new UIRect(
            -InfiniteExtent, -InfiniteExtent, InfiniteExtent * 2f, InfiniteExtent * 2f);

        public float Left => X;
        public float Right => X + Width;
        public float Top => Y;
        public float Bottom => Y + Height;

        public Vector2 Min => new Vector2(X, Y);
        public Vector2 Max => new Vector2(X + Width, Y + Height);
        public Vector2 Size => new Vector2(Width, Height);
        public Vector2 Center => new Vector2(X + Width * 0.5f, Y + Height * 0.5f);

        /// <summary>宽或高不为正即视为空（裁剪后常见）。</summary>
        public bool IsEmpty => !(Width > 0f) || !(Height > 0f);

        /// <summary>是否为"不裁剪"的无限矩形。</summary>
        public bool IsInfinite => Width >= InfiniteExtent;

        public bool Contains(Vector2 point)
            => point.X >= X && point.Y >= Y && point.X < X + Width && point.Y < Y + Height;

        public bool Intersects(in UIRect other)
            => X < other.X + other.Width && other.X < X + Width
            && Y < other.Y + other.Height && other.Y < Y + Height;

        public static UIRect Intersect(in UIRect a, in UIRect b)
        {
            float x0 = MathF.Max(a.X, b.X);
            float y0 = MathF.Max(a.Y, b.Y);
            float x1 = MathF.Min(a.Right, b.Right);
            float y1 = MathF.Min(a.Bottom, b.Bottom);
            if (x1 <= x0 || y1 <= y0) return default;
            return new UIRect(x0, y0, x1 - x0, y1 - y0);
        }

        public UIRect Inset(float left, float top, float right, float bottom)
            => new UIRect(X + left, Y + top, Width - left - right, Height - top - bottom);

        public UIRect Inflate(float amount)
            => new UIRect(X - amount, Y - amount, Width + amount * 2f, Height + amount * 2f);

        public UIRect ExpandToInclude(in UIRect other)
        {
            if (other.IsEmpty) return this;
            if (IsEmpty && Width <= 0f && Height <= 0f && X == 0f && Y == 0f) return other;
            float x0 = MathF.Min(X, other.X);
            float y0 = MathF.Min(Y, other.Y);
            float x1 = MathF.Max(Right, other.Right);
            float y1 = MathF.Max(Bottom, other.Bottom);
            return FromMinMax(new Vector2(x0, y0), new Vector2(x1, y1));
        }

        public bool Equals(UIRect other)
            => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is UIRect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public static bool operator ==(UIRect a, UIRect b) => a.Equals(b);
        public static bool operator !=(UIRect a, UIRect b) => !a.Equals(b);

        public override string ToString() => $"({X:0.#},{Y:0.#} {Width:0.#}x{Height:0.#})";
    }
}
