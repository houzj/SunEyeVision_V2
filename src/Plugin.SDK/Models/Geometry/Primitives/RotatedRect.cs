using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 旋转矩形
    /// </summary>
    public readonly struct RotatedRect : IEquatable<RotatedRect>
    {
        /// <summary>中心点</summary>
        public readonly Point2d Center;
        /// <summary>宽度</summary>
        public readonly double Width;
        /// <summary>高度</summary>
        public readonly double Height;
        /// <summary>旋转角度（度，顺时针为正）</summary>
        public readonly double Angle;

        public RotatedRect(Point2d center, double width, double height, double angleDegrees)
        {
            Center = center; Width = width; Height = height; Angle = angleDegrees;
        }

        /// <summary>尺寸</summary>
        public Size2d Size => new Size2d(Width, Height);
        /// <summary>面积</summary>
        public double Area => Width * Height;
        /// <summary>宽高比</summary>
        public double AspectRatio => Height > 0 ? Width / Height : 0;
        /// <summary>角度（弧度）</summary>
        public double AngleRadians => Angle * Math.PI / 180.0;

        /// <summary>获取四个角点</summary>
        public Point2d[] GetCornerPoints()
        {
            double angleRad = AngleRadians;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            double hw = Width / 2;
            double hh = Height / 2;

            var p1 = new Point2d(-hw, -hh).Rotate(angleRad) + Center;
            var p2 = new Point2d(hw, -hh).Rotate(angleRad) + Center;
            var p3 = new Point2d(hw, hh).Rotate(angleRad) + Center;
            var p4 = new Point2d(-hw, hh).Rotate(angleRad) + Center;
            return new[] { p1, p2, p3, p4 };
        }

        /// <summary>获取轴对齐边界矩形</summary>
        public Rect2d BoundingRect()
        {
            var corners = GetCornerPoints();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in corners)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new Rect2d(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>判断点是否在旋转矩形内（射线法）</summary>
        public bool Contains(Point2d p)
        {
            var corners = GetCornerPoints();
            int n = corners.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((corners[i].Y > p.Y) != (corners[j].Y > p.Y)) &&
                    (p.X < (corners[j].X - corners[i].X) * (p.Y - corners[i].Y) /
                           (corners[j].Y - corners[i].Y) + corners[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>从中心点和尺寸创建</summary>
        public static RotatedRect FromCenter(Point2d center, double width, double height, double angleDegrees = 0) =>
            new RotatedRect(center, width, height, angleDegrees);

        /// <summary>平移</summary>
        public RotatedRect Offset(double dx, double dy) =>
            new RotatedRect(new Point2d(Center.X + dx, Center.Y + dy), Width, Height, Angle);

        /// <summary>缩放</summary>
        public RotatedRect Scale(double factor) =>
            new RotatedRect(Center, Width * factor, Height * factor, Angle);

        public bool Equals(RotatedRect other) =>
            Center.Equals(other.Center) && Width.Equals(other.Width) &&
            Height.Equals(other.Height) && Angle.Equals(other.Angle);
        public override bool Equals(object? obj) => obj is RotatedRect other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Center, Width, Height, Angle);
        public override string ToString() => $"RotatedRect(Center={Center}, Size={Width:F1}x{Height:F1}, Angle={Angle:F1}deg)";
    }
}
