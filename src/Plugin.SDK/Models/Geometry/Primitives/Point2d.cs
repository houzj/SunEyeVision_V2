using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 双精度二维点
    /// </summary>
    public readonly struct Point2d : IEquatable<Point2d>
    {
        public readonly double X;
        public readonly double Y;

        public Point2d(double x, double y) { X = x; Y = y; }

        public static Point2d Zero => new Point2d(0, 0);

        /// <summary>计算到另一点的距离</summary>
        public double DistanceTo(Point2d other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));

        /// <summary>向量长度</summary>
        public double Length() => Math.Sqrt(X * X + Y * Y);

        /// <summary>向量归一化</summary>
        public Point2d Normalize()
        {
            double len = Length();
            if (len == 0) return Zero;
            return new Point2d(X / len, Y / len);
        }

        /// <summary>向量角度（弧度）</summary>
        public double Angle() => Math.Atan2(Y, X);

        /// <summary>向量点积</summary>
        public double Dot(Point2d other) => X * other.X + Y * other.Y;

        /// <summary>向量叉积（返回标量）</summary>
        public double Cross(Point2d other) => X * other.Y - Y * other.X;

        /// <summary>旋转向量</summary>
        public Point2d Rotate(double angleRadians)
        {
            double cos = Math.Cos(angleRadians);
            double sin = Math.Sin(angleRadians);
            return new Point2d(X * cos - Y * sin, X * sin + Y * cos);
        }

        /// <summary>绕指定点旋转</summary>
        public Point2d RotateAround(Point2d center, double angleRadians) => (this - center).Rotate(angleRadians) + center;

        /// <summary>点到直线的距离</summary>
        public double DistanceToLine(Point2d linePoint, Point2d lineDirection)
        {
            var dir = lineDirection.Normalize();
            var v = this - linePoint;
            return Math.Abs(v.Cross(dir));
        }

        /// <summary>点到线段的距离</summary>
        public double DistanceToSegment(Point2d segStart, Point2d segEnd)
        {
            var v = segEnd - segStart;
            var w = this - segStart;
            double c1 = w.Dot(v);
            if (c1 <= 0) return DistanceTo(segStart);
            double c2 = v.Dot(v);
            if (c2 <= c1) return DistanceTo(segEnd);
            double b = c1 / c2;
            var pb = segStart + v * b;
            return DistanceTo(pb);
        }

        /// <summary>从极坐标创建点</summary>
        public static Point2d FromPolar(double radius, double angleRadians) =>
            new Point2d(radius * Math.Cos(angleRadians), radius * Math.Sin(angleRadians));

        public static Point2d operator +(Point2d a, Point2d b) => new Point2d(a.X + b.X, a.Y + b.Y);
        public static Point2d operator -(Point2d a, Point2d b) => new Point2d(a.X - b.X, a.Y - b.Y);
        public static Point2d operator *(Point2d p, double s) => new Point2d(p.X * s, p.Y * s);
        public static Point2d operator *(double s, Point2d p) => new Point2d(p.X * s, p.Y * s);
        public static Point2d operator /(Point2d p, double s) => new Point2d(p.X / s, p.Y / s);
        public static Point2d operator -(Point2d p) => new Point2d(-p.X, -p.Y);
        public static bool operator ==(Point2d a, Point2d b) => a.Equals(b);
        public static bool operator !=(Point2d a, Point2d b) => !a.Equals(b);

        public bool Equals(Point2d other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Point2d other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"Point2d({X:F2}, {Y:F2})";
    }
}
