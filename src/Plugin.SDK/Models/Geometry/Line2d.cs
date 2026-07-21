using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 直线结构（双精度）
    /// </summary>
    public readonly struct Line2d : IEquatable<Line2d>
    {
        /// <summary>
        /// 直线上的一点
        /// </summary>
        [JsonInclude]
        public readonly Point Point;

        /// <summary>
        /// 方向向量（单位向量）
        /// </summary>
        [JsonInclude]
        public readonly Vector Direction;

        /// <summary>
        /// 创建直线（点向式）
        /// </summary>
        [JsonConstructor]
        public Line2d(Point point, Vector direction)
        {
            Point = point;
            direction.Normalize();
            Direction = direction;
        }

        /// <summary>
        /// 从两点创建直线
        /// </summary>
        public static Line2d FromTwoPoints(Point p1, Point p2)
        {
            Vector dir = p2 - p1;
            if (dir.LengthSquared < 1e-20)
                throw new ArgumentException("两点重合，无法确定直线");

            return new Line2d(p1, dir);
        }

        /// <summary>
        /// 从一般式创建直线 (Ax + By + C = 0)
        /// </summary>
        public static Line2d FromGeneralForm(double a, double b, double c)
        {
            if (Math.Abs(a) < 1e-10 && Math.Abs(b) < 1e-10)
                throw new ArgumentException("A和B不能同时为0");

            // 方向向量: (-B, A)
            Vector direction = new Vector(-b, a);
            direction.Normalize();

            // 找直线上一点
            Point point;
            if (Math.Abs(a) > Math.Abs(b))
            {
                point = new Point(-c / a, 0);
            }
            else
            {
                point = new Point(0, -c / b);
            }

            return new Line2d(point, direction);
        }

        /// <summary>
        /// 从点斜式创建直线 (y = kx + b)
        /// </summary>
        public static Line2d FromSlopeIntercept(double slope, double intercept)
        {
            Vector direction = new Vector(1, slope);
            direction.Normalize();
            Point point = new Point(0, intercept);
            return new Line2d(point, direction);
        }

        /// <summary>
        /// 创建水平线
        /// </summary>
        public static Line2d Horizontal(double y)
        {
            return new Line2d(new Point(0, y), new Vector(1, 0));
        }

        /// <summary>
        /// 创建垂直线
        /// </summary>
        public static Line2d Vertical(double x)
        {
            return new Line2d(new Point(x, 0), new Vector(0, 1));
        }

        /// <summary>
        /// 斜率
        /// </summary>
        public double Slope => Direction.X != 0 ? Direction.Y / Direction.X : double.PositiveInfinity;

        /// <summary>
        /// 与X轴的夹角（弧度）
        /// </summary>
        public double Angle => Math.Atan2(Direction.Y, Direction.X);

        /// <summary>
        /// 法向量
        /// </summary>
        public Vector Normal => new Vector(-Direction.Y, Direction.X);

        /// <summary>
        /// 一般式系数A (Ax + By + C = 0)
        /// </summary>
        public double A => -Direction.Y;

        /// <summary>
        /// 一般式系数B
        /// </summary>
        public double B => Direction.X;

        /// <summary>
        /// 一般式系数C
        /// </summary>
        public double C => -(A * Point.X + B * Point.Y);

        /// <summary>
        /// 点到直线的距离
        /// </summary>
        public double DistanceToPoint(Point p)
        {
            return Math.Abs(A * p.X + B * p.Y + C) / Math.Sqrt(A * A + B * B);
        }

        /// <summary>
        /// 计算点在直线上的投影
        /// </summary>
        public Point ProjectPoint(Point p)
        {
            double t = Vector.Multiply(p - Point, Direction);
            return Point + Direction * t;
        }

        /// <summary>
        /// 判断点在直线的哪一侧
        /// </summary>
        /// <returns>正数在法向量方向，负数在相反方向，0在直线上</returns>
        public double SignedDistance(Point p)
        {
            return A * p.X + B * p.Y + C;
        }

        /// <summary>
        /// 获取直线上指定参数t处的点
        /// </summary>
        public Point GetPointAt(double t)
        {
            return Point + Direction * t;
        }

        /// <summary>
        /// 计算两条直线的交点
        /// </summary>
        public bool TryGetIntersection(Line2d other, out Point intersection)
        {
            double denom = Vector.CrossProduct(Direction, other.Direction);
            if (Math.Abs(denom) < 1e-10)
            {
                intersection = default;
                return false; // 平行或重合
            }

            double t = Vector.CrossProduct(other.Point - Point, other.Direction) / denom;
            intersection = GetPointAt(t);
            return true;
        }

        /// <summary>
        /// 计算两条直线的夹角（弧度）
        /// </summary>
        public double AngleTo(Line2d other)
        {
            double dot = Math.Abs(Vector.Multiply(Direction, other.Direction));
            dot = Math.Clamp(dot, -1, 1);
            return Math.Acos(dot);
        }

        /// <summary>
        /// 判断两直线是否平行
        /// </summary>
        public bool IsParallelTo(Line2d other, double tolerance = 1e-6)
        {
            return Math.Abs(Vector.CrossProduct(Direction, other.Direction)) < tolerance;
        }

        /// <summary>
        /// 判断两直线是否垂直
        /// </summary>
        public bool IsPerpendicularTo(Line2d other, double tolerance = 1e-6)
        {
            return Math.Abs(Vector.Multiply(Direction, other.Direction)) < tolerance;
        }

        /// <summary>
        /// 获取偏移直线
        /// </summary>
        public Line2d Offset(double distance)
        {
            Point newPoint = Point + Normal * distance;
            return new Line2d(newPoint, Direction);
        }

        #region IEquatable<Line2d>

        public bool Equals(Line2d other) =>
            Point.Equals(other.Point) && Direction.Equals(other.Direction);

        public override bool Equals(object? obj) => obj is Line2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Point, Direction);

        public override string ToString() => $"Line2d(Point={Point}, Dir={Direction})";

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("点X", Point.X),
                ("点Y", Point.Y),
                ("方向X", Direction.X),
                ("方向Y", Direction.Y),
            };
        }

        #endregion
    }
}
