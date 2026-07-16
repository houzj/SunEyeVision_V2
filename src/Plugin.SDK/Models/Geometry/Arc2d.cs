using System;
using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 弧形结构（双精度）
    /// </summary>
    public readonly struct Arc2d : IEquatable<Arc2d>
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
        /// 起始角度（弧度）
        /// </summary>
        public readonly double StartAngle;

        /// <summary>
        /// 终止角度（弧度）
        /// </summary>
        public readonly double EndAngle;

        /// <summary>
        /// 创建弧形
        /// </summary>
        public Arc2d(Point center, double radius, double startAngle, double endAngle)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// 创建弧形
        /// </summary>
        public Arc2d(double centerX, double centerY, double radius, double startAngle, double endAngle)
            : this(new Point(centerX, centerY), radius, startAngle, endAngle) { }

        /// <summary>
        /// 空弧（无效弧）
        /// </summary>
        public static readonly Arc2d Empty = new Arc2d(0, 0, 0, 0, 0);

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => Radius <= 0 || StartAngle == EndAngle;

        /// <summary>
        /// 角度跨度（弧度）
        /// </summary>
        public double AngularSpan => EndAngle - StartAngle;

        /// <summary>
        /// 弧长
        /// </summary>
        public double Length => Radius * Math.Abs(AngularSpan);

        /// <summary>
        /// 是否为完整圆
        /// </summary>
        public bool IsFullCircle => Math.Abs(AngularSpan) >= 2 * Math.PI - 0.001;

        /// <summary>
        /// 获取弧上指定角度的点
        /// </summary>
        public Point GetPointOnArc(double angleRadians)
        {
            return new Point(
                Center.X + Radius * Math.Cos(angleRadians),
                Center.Y + Radius * Math.Sin(angleRadians)
            );
        }

        /// <summary>
        /// 获取弧上的采样点
        /// </summary>
        public Point[] GetSamplePoints(int pointCount)
        {
            if (pointCount <= 0) return Array.Empty<Point>();
            Point[] points = new Point[pointCount];
            double step = AngularSpan / pointCount;
            for (int i = 0; i < pointCount; i++)
            {
                double angle = StartAngle + i * step;
                points[i] = GetPointOnArc(angle);
            }
            return points;
        }

        /// <summary>
        /// 是否包含点
        /// </summary>
        public bool Contains(Point point)
        {
            double distance = (Center - point).Length;
            if (Math.Abs(distance - Radius) > 0.001)
                return false;

            double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);
            return angle >= StartAngle && angle <= EndAngle;
        }

        /// <summary>
        /// 边界矩形
        /// </summary>
        public Rect BoundingBox
        {
            get
            {
                var samples = GetSamplePoints(32);
                if (samples.Length == 0) return Rect.Empty;
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var p in samples)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }

        #region IEquatable<Arc2d>

        public bool Equals(Arc2d other) =>
            Center.Equals(other.Center) &&
            Radius.Equals(other.Radius) &&
            StartAngle.Equals(other.StartAngle) &&
            EndAngle.Equals(other.EndAngle);

        public override bool Equals(object? obj) => obj is Arc2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Center, Radius, StartAngle, EndAngle);

        public override string ToString() =>
            $"Arc2d(Center={Center}, R={Radius:F2}, Angle=[{StartAngle:F2}, {EndAngle:F2}])";

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("中心X", Center.X),
                ("中心Y", Center.Y),
                ("半径", Radius),
                ("起始角度", StartAngle),
                ("结束角度", EndAngle),
            };
        }

        #endregion
    }
}
