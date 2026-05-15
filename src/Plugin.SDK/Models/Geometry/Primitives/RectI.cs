using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 整数矩形（像素坐标）
    /// </summary>
    public readonly struct RectI : IEquatable<RectI>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public RectI(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public bool IsEmpty => Width <= 0 || Height <= 0;

        public static explicit operator RectI(Rect2d r) => new RectI(
            (int)Math.Floor(r.X), (int)Math.Floor(r.Y),
            (int)Math.Ceiling(r.Width), (int)Math.Ceiling(r.Height));

        public static explicit operator Rect2d(RectI r) => new Rect2d(r.X, r.Y, r.Width, r.Height);

        public bool Equals(RectI other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is RectI other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"RectI({X}, {Y}, {Width}x{Height})";
    }
}
