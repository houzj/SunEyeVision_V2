using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 双精度矩形
    /// </summary>
    public readonly struct Rect2d : IEquatable<Rect2d>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Width;
        public readonly double Height;

        public Rect2d(double x, double y, double width, double height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;

        /// <summary>中心点</summary>
        public Point2d Center => new Point2d(X + Width / 2, Y + Height / 2);
        /// <summary>左上角</summary>
        public Point2d TopLeft => new Point2d(X, Y);
        /// <summary>右上角</summary>
        public Point2d TopRight => new Point2d(X + Width, Y);
        /// <summary>左下角</summary>
        public Point2d BottomLeft => new Point2d(X, Y + Height);
        /// <summary>右下角</summary>
        public Point2d BottomRight => new Point2d(X + Width, Y + Height);
        /// <summary>面积</summary>
        public double Area => Width * Height;
        /// <summary>周长</summary>
        public double Perimeter => 2 * (Width + Height);
        /// <summary>宽高比</summary>
        public double AspectRatio => Height > 0 ? Width / Height : 0;
        /// <summary>是否为空</summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>是否包含点</summary>
        public bool Contains(Point2d p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

        /// <summary>扩展矩形</summary>
        public Rect2d Inflate(double amount) => new Rect2d(X - amount, Y - amount, Width + 2 * amount, Height + 2 * amount);

        /// <summary>平移</summary>
        public Rect2d Offset(double dx, double dy) => new Rect2d(X + dx, Y + dy, Width, Height);

        /// <summary>两矩形求交集</summary>
        public Rect2d Intersect(Rect2d other)
        {
            double x = Math.Max(X, other.X);
            double y = Math.Max(Y, other.Y);
            double right = Math.Min(Right, other.Right);
            double bottom = Math.Min(Bottom, other.Bottom);
            if (x < right && y < bottom)
                return new Rect2d(x, y, right - x, bottom - y);
            return new Rect2d(0, 0, 0, 0);
        }

        /// <summary>两矩形求并集</summary>
        public Rect2d Union(Rect2d other)
        {
            double x = Math.Min(X, other.X);
            double y = Math.Min(Y, other.Y);
            double right = Math.Max(Right, other.Right);
            double bottom = Math.Max(Bottom, other.Bottom);
            return new Rect2d(x, y, right - x, bottom - y);
        }

        /// <summary>从中心点和尺寸创建矩形</summary>
        public static Rect2d FromCenter(Point2d center, double width, double height) =>
            new Rect2d(center.X - width / 2, center.Y - height / 2, width, height);

        /// <summary>从两点创建边界矩形</summary>
        public static Rect2d FromPoints(Point2d p1, Point2d p2)
        {
            double minX = Math.Min(p1.X, p2.X);
            double minY = Math.Min(p1.Y, p2.Y);
            double maxX = Math.Max(p1.X, p2.X);
            double maxY = Math.Max(p1.Y, p2.Y);
            return new Rect2d(minX, minY, maxX - minX, maxY - minY);
        }

        public bool Equals(Rect2d other) => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object? obj) => obj is Rect2d other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"Rect2d({X:F1}, {Y:F1}, {Width:F1}x{Height:F1})";
    }
}
