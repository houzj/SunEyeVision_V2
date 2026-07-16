using System;
using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 旋转矩形结构（双精度）
    /// </summary>
    public readonly struct RotatedRect2d : IEquatable<RotatedRect2d>
    {
        /// <summary>
        /// 中心点
        /// </summary>
        public readonly Point Center;

        /// <summary>
        /// 宽度
        /// </summary>
        public readonly double Width;

        /// <summary>
        /// 高度
        /// </summary>
        public readonly double Height;

        /// <summary>
        /// 旋转角度（度，逆时针为正）
        /// </summary>
        public readonly double Angle;

        /// <summary>
        /// 创建旋转矩形
        /// </summary>
        public RotatedRect2d(Point center, double width, double height, double angle)
        {
            Center = center;
            Width = width;
            Height = height;
            Angle = angle;
        }

        /// <summary>
        /// 创建旋转矩形
        /// </summary>
        public RotatedRect2d(double centerX, double centerY, double width, double height, double angle)
            : this(new Point(centerX, centerY), width, height, angle) { }

        /// <summary>
        /// 空矩形（无效矩形）
        /// </summary>
        public static readonly RotatedRect2d Empty = new RotatedRect2d(0, 0, 0, 0, 0);

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>
        /// 中心X
        /// </summary>
        public double CenterX => Center.X;

        /// <summary>
        /// 中心Y
        /// </summary>
        public double CenterY => Center.Y;

        /// <summary>
        /// 旋转角（弧度）
        /// </summary>
        public double AngleRadians => Angle * Math.PI / 180.0;

        /// <summary>
        /// 面积
        /// </summary>
        public double Area => Width * Height;

        /// <summary>
        /// 轴对齐包围盒
        /// </summary>
        public Rect BoundingBox
        {
            get
            {
                var corners = GetCorners();
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var c in corners)
                {
                    if (c.X < minX) minX = c.X;
                    if (c.Y < minY) minY = c.Y;
                    if (c.X > maxX) maxX = c.X;
                    if (c.Y > maxY) maxY = c.Y;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }

        /// <summary>
        /// 获取四个角点
        /// </summary>
        public Point[] GetCorners()
        {
            double angleRad = AngleRadians;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            double hw = Width / 2;
            double hh = Height / 2;

            return new Point[]
            {
                Transform(-hw, -hh, cos, sin),  // TopLeft
                Transform( hw, -hh, cos, sin),  // TopRight
                Transform( hw,  hh, cos, sin),  // BottomRight
                Transform(-hw,  hh, cos, sin),  // BottomLeft
            };
        }

        private Point Transform(double localX, double localY, double cos, double sin)
        {
            return new Point(
                Center.X + localX * cos - localY * sin,
                Center.Y + localX * sin + localY * cos
            );
        }

        /// <summary>
        /// 是否包含点
        /// </summary>
        public bool Contains(Point point)
        {
            double dx = point.X - Center.X;
            double dy = point.Y - Center.Y;
            double angleRad = -AngleRadians;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;
            return Math.Abs(localX) <= Width / 2 && Math.Abs(localY) <= Height / 2;
        }

        /// <summary>
        /// 平移
        /// </summary>
        public RotatedRect2d Offset(double dx, double dy)
        {
            return new RotatedRect2d(Center.X + dx, Center.Y + dy, Width, Height, Angle);
        }

        /// <summary>
        /// 缩放
        /// </summary>
        public RotatedRect2d Scale(double factor)
        {
            return new RotatedRect2d(Center, Width * factor, Height * factor, Angle);
        }

        /// <summary>
        /// 旋转
        /// </summary>
        public RotatedRect2d Rotate(double deltaAngleDeg)
        {
            return new RotatedRect2d(Center, Width, Height, Angle + deltaAngleDeg);
        }

        #region IEquatable<RotatedRect2d>

        public bool Equals(RotatedRect2d other) =>
            Center.Equals(other.Center) &&
            Width.Equals(other.Width) && Height.Equals(other.Height) &&
            Angle.Equals(other.Angle);

        public override bool Equals(object? obj) => obj is RotatedRect2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Center, Width, Height, Angle);

        public override string ToString() =>
            $"RotatedRect2d(Center={Center}, W={Width:F1}, H={Height:F1}, Angle={Angle:F1}°)";

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("中心X", Center.X),
                ("中心Y", Center.Y),
                ("宽", Width),
                ("高", Height),
                ("角度", Angle),
            };
        }

        #endregion
    }
}
