using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
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
        [JsonInclude]
        public readonly Point Center;

        /// <summary>
        /// 半径
        /// </summary>
        [JsonInclude]
        public readonly double Radius;

        /// <summary>
        /// 起始角度（弧度）
        /// </summary>
        [JsonInclude]
        public readonly double StartAngle;

        /// <summary>
        /// 终止角度（弧度）
        /// </summary>
        [JsonInclude]
        public readonly double EndAngle;

        /// <summary>
        /// 创建弧形
        /// </summary>
        [JsonConstructor]
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
        /// 从起点 + 终点 + 半径创建圆弧（圆心由几何计算）
        /// </summary>
        /// <param name="startPoint">弧起点</param>
        /// <param name="endPoint">弧终点</param>
        /// <param name="radius">半径（需 ≥ 弦长/2，否则无解）</param>
        /// <param name="clockwise">true=顺时针（圆心在弦一侧），false=逆时针（圆心在弦另一侧，默认劣弧方向）</param>
        /// <param name="majorArc">true=大弧（张角 &gt; 180°），false=小弧（张角 &lt;= 180°）；决定起始/终止角互补到优弧</param>
        /// <returns>圆弧；半径过小或两点重合时返回 <see cref="Empty"/></returns>
        public static Arc2d FromEndpointsAndRadius(Point startPoint, Point endPoint, double radius, bool clockwise, bool majorArc = false)
        {
            var chord = endPoint - startPoint;
            var halfChord = chord.Length / 2;
            if (halfChord < 1e-6) return Empty;

            // 半径必须 ≥ 弦长/2，否则不存在经过两点的圆弧
            if (radius < halfChord) return Empty;

            // 弦中垂线方向单位向量
            // 圆心在弧凸起的一侧：逆时针（默认）圆心在弦的"左前"方向（使弧为劣弧）
            var chordDir = chord / (halfChord * 2);
            var normal = clockwise
                ? new Vector(chordDir.Y, -chordDir.X)   // 顺时针：圆心在弦的"右后"方向
                : new Vector(-chordDir.Y, chordDir.X);  // 逆时针：圆心在弦的"左前"方向

            // 圆心距弦中点距离（勾股定理）
            double h = Math.Sqrt(radius * radius - halfChord * halfChord);
            var mid = new Point((startPoint.X + endPoint.X) / 2, (startPoint.Y + endPoint.Y) / 2);
            var center = mid + normal * h;

            double a1 = Math.Atan2(startPoint.Y - center.Y, startPoint.X - center.X);
            double a3 = Math.Atan2(endPoint.Y - center.Y, endPoint.X - center.X);

            // 根据方向调整终止角，使 AngularSpan 符号与方向一致
            double endAngle = a3;
            if (clockwise && endAngle > a1)
                endAngle -= 2 * Math.PI;   // 顺时针：结束角度在起始角度之前
            else if (!clockwise && endAngle < a1)
                endAngle += 2 * Math.PI;   // 逆时针：结束角度在起始角度之后

            // 大小弧状态：小弧（默认）保持当前终止角；大弧将终止角互补到 2π - 小弧跨度
            if (majorArc)
            {
                double smallSpan = Math.Abs(endAngle - a1);   // 当前小弧跨度（< π）
                double majorSpan = 2 * Math.PI - smallSpan;    // 大弧跨度（> π）
                endAngle = clockwise ? a1 - majorSpan : a1 + majorSpan;
            }

            return new Arc2d(center, radius, a1, endAngle);
        }

        /// <summary>
        /// 从起点 + 终点创建圆弧（默认半径 = 弦长，张角 60°）
        /// </summary>
        /// <param name="startPoint">弧起点</param>
        /// <param name="endPoint">弧终点</param>
        /// <param name="clockwise">true=顺时针，false=逆时针（默认逆时针）</param>
        /// <param name="majorArc">true=大弧，false=小弧（默认小弧）</param>
        /// <returns>圆弧；两点重合时返回 <see cref="Empty"/></returns>
        public static Arc2d FromTwoPointsDefault(Point startPoint, Point endPoint, bool clockwise = false, bool majorArc = false)
        {
            double radius = (endPoint - startPoint).Length;
            return FromEndpointsAndRadius(startPoint, endPoint, radius, clockwise, majorArc);
        }

        /// <summary>
        /// 从三个点创建圆弧（三点定弧，外接圆）
        /// </summary>
        /// <param name="p1">弧起点</param>
        /// <param name="p2">弧终点</param>
        /// <param name="p3">弧上一点（决定弯曲方向、大小弧与半径）</param>
        /// <param name="clockwise">输出弧方向（true=顺时针，false=逆时针）</param>
        /// <param name="majorArc">输出是否为大弧（张角 &gt; 180°）</param>
        /// <returns>经过三点的圆弧；三点共线或退化时返回 <see cref="Empty"/></returns>
        public static Arc2d FromThreePoints(Point p1, Point p2, Point p3, out bool clockwise, out bool majorArc)
        {
            clockwise = false;
            majorArc = false;

            // 外接圆圆心：两条中垂线交点
            double ax = p1.X - p2.X, ay = p1.Y - p2.Y;
            double bx = p3.X - p2.X, by = p3.Y - p2.Y;
            double det = 2 * (ax * by - ay * bx);
            if (Math.Abs(det) < 1e-9)
                return Empty; // 三点共线，无法定弧

            double a2 = ax * ax + ay * ay;
            double b2 = bx * bx + by * by;
            double ux = (by * a2 - ay * b2) / det;
            double uy = (ax * b2 - bx * a2) / det;
            var center = new Point(p2.X + ux, p2.Y + uy);

            double radius = Math.Sqrt(ux * ux + uy * uy);
            if (radius < 1e-6)
                return Empty;

            double a1 = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
            double a2m = Math.Atan2(p2.Y - center.Y, p2.X - center.X);
            double a3 = Math.Atan2(p3.Y - center.Y, p3.X - center.X);

            // p2 相对 p1 的逆时针距离（0~2π）与 p3 相对 p1 的逆时针位置（0~2π）
            double ccwToP2 = (a2m - a1) % (2 * Math.PI);
            if (ccwToP2 < 0) ccwToP2 += 2 * Math.PI;
            double ccwToP3 = (a3 - a1) % (2 * Math.PI);
            if (ccwToP3 < 0) ccwToP3 += 2 * Math.PI;

            // 判断弧方向：p3 是否落在 p1→p2 的逆时针弧段内
            // 若落在逆时针弧段 → 逆时针弧；否则 → 顺时针弧（p3 在互补的顺时针弧段内）
            if (ccwToP3 <= ccwToP2)
            {
                // 逆时针弧：起点 a1，终点沿逆时针走 ccwToP2；跨度 = ccwToP2
                clockwise = false;
                majorArc = ccwToP2 > Math.PI;
                double end = a1 + ccwToP2;
                return new Arc2d(center, radius, a1, end);
            }
            else
            {
                // 顺时针弧：起点 a1，终点沿顺时针走 (2π - ccwToP2)；跨度 = -(2π - ccwToP2)
                clockwise = true;
                majorArc = (2 * Math.PI - ccwToP2) > Math.PI;
                double end = a1 + ccwToP2 - 2 * Math.PI;
                return new Arc2d(center, radius, a1, end);
            }
        }

        /// <summary>
        /// 从三个点创建圆弧（三点定弧，外接圆）——兼容旧签名，自动提取方向/大小弧
        /// </summary>
        public static Arc2d FromThreePoints(Point p1, Point p2, Point p3)
        {
            return FromThreePoints(p1, p2, p3, out _, out _);
        }

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
        /// 获取弧上的采样点（首尾采样点精确落在 StartAngle 与 EndAngle）
        /// </summary>
        public Point[] GetSamplePoints(int pointCount)
        {
            if (pointCount <= 0) return Array.Empty<Point>();
            if (pointCount == 1) return new[] { GetPointOnArc(StartAngle) };
            Point[] points = new Point[pointCount];
            double step = AngularSpan / (pointCount - 1);
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
