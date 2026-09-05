using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 二维点结构（双精度）
    /// </summary>
    public readonly struct Point2d : IEquatable<Point2d>, IMonitorable
    {
        /// <summary>
        /// X 坐标
        /// </summary>
        [JsonInclude]
        public readonly double X;

        /// <summary>
        /// Y 坐标
        /// </summary>
        [JsonInclude]
        public readonly double Y;

        /// <summary>
        /// 创建二维点
        /// </summary>
        [JsonConstructor]
        public Point2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 创建二维点
        /// </summary>
        public Point2d(Point point)
            : this(point.X, point.Y) { }

        /// <summary>
        /// 空点（原点）
        /// </summary>
        public static readonly Point2d Empty = new Point2d(0, 0);

        /// <summary>
        /// 是否为空（原点）
        /// </summary>
        public bool IsEmpty => Math.Abs(X) < 0.001 && Math.Abs(Y) < 0.001;

        /// <summary>
        /// 两点距离
        /// </summary>
        public double DistanceTo(Point2d other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 平移
        /// </summary>
        public Point2d Offset(double dx, double dy)
            => new Point2d(X + dx, Y + dy);

        /// <summary>
        /// 缩放
        /// </summary>
        public Point2d Scale(double factor)
            => new Point2d(X * factor, Y * factor);

        /// <summary>
        /// 转 WPF Point
        /// </summary>
        public Point ToWpfPoint() => new Point(X, Y);

        #region IEquatable<Point2d>

        public bool Equals(Point2d other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object? obj) => obj is Point2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X:F2}, {Y:F2})";

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("X", X),
                ("Y", Y),
            };
        }

        #endregion
    }
}
