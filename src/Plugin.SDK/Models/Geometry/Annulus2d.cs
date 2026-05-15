using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 圆环结构ROI
    /// 用于同圆度检测、环形区域分析等。
    /// 支持圆弧和完整圆环。
    /// </remarks>
    public readonly struct Annulus2d : IEquatable<Annulus2d>
    {
        /// <summary>
        /// 圆心
        /// </summary>
        public readonly Point2d Center;

        /// <summary>
        /// 内半径
        /// </summary>
        public readonly double InnerRadius;

        /// <summary>
        /// 外半径
        /// </summary>
        public readonly double OuterRadius;

        /// <summary>
        /// 起始角度（弧度）
        /// </summary>
        public readonly double StartAngle;

        /// <summary>
        /// 终止角度（弧度）
        /// </summary>
        public readonly double EndAngle;

        /// <summary>
        /// 创建完整圆环
        /// </summary>
        public Annulus2d(Point2d center, double innerRadius, double outerRadius)
        {
            Center = center;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            StartAngle = 0;
            EndAngle = 2 * Math.PI;
        }

        /// <summary>
        /// 创建完整圆环
        /// </summary>
        public Annulus2d(double centerX, double centerY, double innerRadius, double outerRadius)
            : this(new Point2d(centerX, centerY), innerRadius, outerRadius) { }

        /// <summary>
        /// 创建圆弧圆环
        /// </summary>
        public Annulus2d(Point2d center, double innerRadius, double outerRadius,
                        double startAngle, double endAngle)
        {
            Center = center;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// 创建圆弧圆环
        /// </summary>
        public Annulus2d(double centerX, double centerY, double innerRadius, double outerRadius,
                        double startAngle, double endAngle)
            : this(new Point2d(centerX, centerY), innerRadius, outerRadius, startAngle, endAngle) { }

        /// <summary>
        /// 空圆环
        /// </summary>
        public static readonly Annulus2d Empty = new Annulus2d(0, 0, 0, 0);

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => OuterRadius <= 0 || InnerRadius >= OuterRadius;

        /// <summary>
        /// 平均半径
        /// </summary>
        public double MeanRadius => (InnerRadius + OuterRadius) / 2;

        /// <summary>
        /// 宽度
        /// </summary>
        public double Width => OuterRadius - InnerRadius;

        /// <summary>
        /// 内圆
        /// </summary>
        public Circle2d InnerCircle => new Circle2d(Center, InnerRadius);

        /// <summary>
        /// 外圆
        /// </summary>
        public Circle2d OuterCircle => new Circle2d(Center, OuterRadius);

        /// <summary>
        /// 是否为完整圆（360度）
        /// </summary>
        public bool IsFullCircle => EndAngle - StartAngle >= 2 * Math.PI - 0.001;

        /// <summary>
        /// 角度跨度（弧度）
        /// </summary>
        public double AngularSpan => EndAngle - StartAngle;

        /// <summary>
        /// 面积
        /// </summary>
        public double Area
        {
            get
            {
                double area = Math.PI * (OuterRadius * OuterRadius - InnerRadius * InnerRadius);
                if (!IsFullCircle)
                {
                    area *= AngularSpan / (2 * Math.PI);
                }
                return area;
            }
        }

        /// <summary>
        /// 边界矩形
        /// </summary>
        public Rect2d BoundingBox => OuterCircle.BoundingBox;

        /// <summary>
        /// 是否包含点
        /// </summary>
        public bool Contains(Point2d point)
        {
            double distance = Center.DistanceTo(point);
            if (distance < InnerRadius || distance > OuterRadius)
                return false;

            if (IsFullCircle)
                return true;

            double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);
            // 规范化角度到 [StartAngle, StartAngle + 2π)
            angle = NormalizeAngle(angle, StartAngle);
            return angle >= StartAngle && angle <= EndAngle;
        }

        /// <summary>
        /// 获取圆环上的点（平均半径）
        /// </summary>
        public Point2d GetPointOnAnnulus(double angleRadians)
        {
            return new Point2d(
                Center.X + MeanRadius * Math.Cos(angleRadians),
                Center.Y + MeanRadius * Math.Sin(angleRadians)
            );
        }

        /// <summary>
        /// 获取圆环边界上的采样点
        /// </summary>
        /// <param name="pointCount">点数</param>
        public Point2d[] GetSamplePoints(int pointCount)
        {
            Point2d[] points = new Point2d[pointCount];
            double angleStep = AngularSpan / pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                double angle = StartAngle + i * angleStep;
                points[i] = GetPointOnAnnulus(angle);
            }

            return points;
        }

        /// <summary>
        /// 平移
        /// </summary>
        public Annulus2d Offset(double dx, double dy)
        {
            return new Annulus2d(
                new Point2d(Center.X + dx, Center.Y + dy),
                InnerRadius, OuterRadius, StartAngle, EndAngle
            );
        }

        /// <summary>
        /// 缩放
        /// </summary>
        public Annulus2d Scale(double factor)
        {
            return new Annulus2d(
                Center,
                InnerRadius * factor,
                OuterRadius * factor,
                StartAngle,
                EndAngle
            );
        }

        private static double NormalizeAngle(double angle, double reference)
        {
            while (angle < reference)
                angle += 2 * Math.PI;
            while (angle >= reference + 2 * Math.PI)
                angle -= 2 * Math.PI;
            return angle;
        }

        #region IEquatable<Annulus2d>

        public bool Equals(Annulus2d other) =>
            Center.Equals(other.Center) &&
            InnerRadius.Equals(other.InnerRadius) && OuterRadius.Equals(other.OuterRadius) &&
            StartAngle.Equals(other.StartAngle) && EndAngle.Equals(other.EndAngle);

        public override bool Equals(object? obj) => obj is Annulus2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Center, InnerRadius, OuterRadius, StartAngle, EndAngle);

        public override string ToString() => IsFullCircle
            ? $"Annulus2d(Center={Center}, R=[{InnerRadius:F1}, {OuterRadius:F1}])"
            : $"Annulus2d(Center={Center}, R=[{InnerRadius:F1}, {OuterRadius:F1}], Angle=[{StartAngle:F2}, {EndAngle:F2}])";

        #endregion
    }
}
