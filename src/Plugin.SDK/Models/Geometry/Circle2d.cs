using System;
using System.Collections.Generic;
using System.Windows;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 圆形结构（双精度）
    /// </summary>
    public readonly struct Circle2d : IEquatable<Circle2d>
    {
        /// <summary>
        /// 圆心
        /// </summary>
        public readonly Point Center;

        /// <summary>
        /// 半径
        /// </summary>
        public readonly double Radius;

        /// <summary>
        /// 创建圆形
        /// </summary>
        public Circle2d(Point center, double radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// 创建圆形
        /// </summary>
        public Circle2d(double centerX, double centerY, double radius)
            : this(new Point(centerX, centerY), radius) { }

        /// <summary>
        /// 空圆（无效圆）
        /// </summary>
        public static readonly Circle2d Empty = new Circle2d(0, 0, 0);

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => Radius <= 0;

        /// <summary>
        /// 直径
        /// </summary>
        public double Diameter => 2 * Radius;

        /// <summary>
        /// 周长
        /// </summary>
        public double Circumference => 2 * Math.PI * Radius;

        /// <summary>
        /// 面积
        /// </summary>
        public double Area => Math.PI * Radius * Radius;

        /// <summary>
        /// 边界矩形
        /// </summary>
        public Rect BoundingBox => new Rect(
            Center.X - Radius,
            Center.Y - Radius,
            Diameter,
            Diameter
        );

        /// <summary>
        /// 是否包含点
        /// </summary>
        public bool Contains(Point point)
        {
            return (Center - point).Length <= Radius;
        }

        /// <summary>
        /// 是否包含点（指定容差）
        /// </summary>
        public bool Contains(Point point, double tolerance)
        {
            return (Center - point).Length <= Radius + tolerance;
        }

        /// <summary>
        /// 点是否在圆上
        /// </summary>
        public bool IsOnCircle(Point point, double tolerance = 0.001)
        {
            double distance = (Center - point).Length;
            return Math.Abs(distance - Radius) <= tolerance;
        }

        /// <summary>
        /// 获取圆上指定角度的点
        /// </summary>
        /// <param name="angleRadians">角度（弧度，0为右侧，逆时针为正）</param>
        public Point GetPointOnCircle(double angleRadians)
        {
            return new Point(
                Center.X + Radius * Math.Cos(angleRadians),
                Center.Y + Radius * Math.Sin(angleRadians)
            );
        }

        /// <summary>
        /// 计算点到圆心的角度
        /// </summary>
        public double GetAngleToPoint(Point point)
        {
            return Math.Atan2(point.Y - Center.Y, point.X - Center.X);
        }

        /// <summary>
        /// 是否与另一个圆相交
        /// </summary>
        public bool Intersects(Circle2d other)
        {
            double distance = (Center - other.Center).Length;
            return distance <= Radius + other.Radius;
        }

        /// <summary>
        /// 是否与矩形相交
        /// </summary>
        public bool Intersects(Rect rect)
        {
            // 找到矩形上离圆心最近的点
            double closestX = Math.Clamp(Center.X, rect.X, rect.X + rect.Width);
            double closestY = Math.Clamp(Center.Y, rect.Y, rect.Y + rect.Height);

            double distance = (Center - new Point(closestX, closestY)).Length;
            return distance <= Radius;
        }

        /// <summary>
        /// 平移
        /// </summary>
        public Circle2d Offset(double dx, double dy)
        {
            return new Circle2d(Center.X + dx, Center.Y + dy, Radius);
        }

        /// <summary>
        /// 缩放
        /// </summary>
        public Circle2d Scale(double factor)
        {
            return new Circle2d(Center, Radius * factor);
        }

        /// <summary>
        /// 从三点拟合圆
        /// </summary>
        public static Circle2d FromThreePoints(Point p1, Point p2, Point p3)
        {
            double x1 = p1.X, y1 = p1.Y;
            double x2 = p2.X, y2 = p2.Y;
            double x3 = p3.X, y3 = p3.Y;

            double a = x1 * (y2 - y3) - y1 * (x2 - x3) + x2 * y3 - x3 * y2;
            if (Math.Abs(a) < 1e-10)
            {
                // 三点共线，返回退化圆
                return Empty;
            }

            double b = (x1 * x1 + y1 * y1) * (y3 - y2) +
                       (x2 * x2 + y2 * y2) * (y1 - y3) +
                       (x3 * x3 + y3 * y3) * (y2 - y1);

            double c = (x1 * x1 + y1 * y1) * (x2 - x3) +
                       (x2 * x2 + y2 * y2) * (x3 - x1) +
                       (x3 * x3 + y3 * y3) * (x1 - x2);

            double centerX = -b / (2 * a);
            double centerY = -c / (2 * a);
            double radius = Math.Sqrt((x1 - centerX) * (x1 - centerX) +
                                      (y1 - centerY) * (y1 - centerY));

            return new Circle2d(centerX, centerY, radius);
        }

        #region IEquatable<Circle2d>

        public bool Equals(Circle2d other) =>
            Center.Equals(other.Center) && Radius.Equals(other.Radius);

        public override bool Equals(object? obj) => obj is Circle2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Center, Radius);

        public override string ToString() => $"Circle2d(Center={Center}, R={Radius:F2})";

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("中心X", Center.X),
                ("中心Y", Center.Y),
                ("半径", Radius),
            };
        }

        #endregion
    }
}
