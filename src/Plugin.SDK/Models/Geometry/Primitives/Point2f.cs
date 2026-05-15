using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 单精度二维点
    /// </summary>
    public readonly struct Point2f : IEquatable<Point2f>
    {
        public readonly float X;
        public readonly float Y;

        public Point2f(float x, float y) { X = x; Y = y; }

        public static Point2f Zero => new Point2f(0, 0);

        public static explicit operator Point2f(Point2d p) => new Point2f((float)p.X, (float)p.Y);
        public static explicit operator Point2d(Point2f p) => new Point2d(p.X, p.Y);

        public static Point2f operator +(Point2f a, Point2f b) => new Point2f(a.X + b.X, a.Y + b.Y);
        public static Point2f operator -(Point2f a, Point2f b) => new Point2f(a.X - b.X, a.Y - b.Y);
        public static Point2f operator *(Point2f p, float s) => new Point2f(p.X * s, p.Y * s);
        public static Point2f operator /(Point2f p, float s) => new Point2f(p.X / s, p.Y / s);

        public static bool operator ==(Point2f a, Point2f b) => a.Equals(b);
        public static bool operator !=(Point2f a, Point2f b) => !a.Equals(b);

        public bool Equals(Point2f other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Point2f other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"Point2f({X:F2}, {Y:F2})";
    }
}
