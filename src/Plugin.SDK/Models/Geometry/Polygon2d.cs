using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 多边形（顶点数可变）
    /// </summary>
    public class Polygon2d : IMonitorable
    {
        /// <summary>
        /// 顶点列表
        /// </summary>
        public Point[] Vertices { get; set; }

        /// <summary>
        /// 是否闭合
        /// </summary>
        public bool IsClosed { get; set; } = true;

        /// <summary>
        /// 创建多边形
        /// </summary>
        public Polygon2d(Point[] vertices, bool isClosed = true)
        {
            Vertices = vertices ?? Array.Empty<Point>();
            IsClosed = isClosed;
        }

        /// <summary>
        /// 顶点数
        /// </summary>
        public int Count => Vertices.Length;

        /// <summary>
        /// 是否有效（至少3个顶点）
        /// </summary>
        public bool IsValid => Vertices.Length >= 3;

        /// <summary>
        /// 中心点（几何中心）
        /// </summary>
        public Point Center
        {
            get
            {
                if (Vertices.Length == 0)
                    return new Point(0, 0);
                return new Point(
                    Vertices.Average(v => v.X),
                    Vertices.Average(v => v.Y)
                );
            }
        }

        /// <summary>
        /// 外接矩形
        /// </summary>
        public Rect BoundingBox
        {
            get
            {
                if (Vertices.Length == 0)
                    return Rect.Empty;
                double minX = Vertices.Min(v => v.X);
                double minY = Vertices.Min(v => v.Y);
                double maxX = Vertices.Max(v => v.X);
                double maxY = Vertices.Max(v => v.Y);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }

        /// <summary>
        /// 射线法判断点是否在多边形内
        /// </summary>
        public bool Contains(Point point)
        {
            if (Vertices.Length < 3) return false;

            bool inside = false;
            int j = Vertices.Length - 1;
            for (int i = 0; i < Vertices.Length; j = i++)
            {
                if ((Vertices[i].Y > point.Y) != (Vertices[j].Y > point.Y) &&
                    point.X < (Vertices[j].X - Vertices[i].X) * (point.Y - Vertices[i].Y) /
                              (Vertices[j].Y - Vertices[i].Y) + Vertices[i].X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// 平移
        /// </summary>
        public Polygon2d Offset(double dx, double dy)
        {
            var newVerts = new Point[Vertices.Length];
            for (int i = 0; i < Vertices.Length; i++)
                newVerts[i] = new Point(Vertices[i].X + dx, Vertices[i].Y + dy);
            return new Polygon2d(newVerts, IsClosed);
        }

        /// <summary>
        /// 缩放
        /// </summary>
        public Polygon2d Scale(double factor)
        {
            var center = Center;
            var newVerts = new Point[Vertices.Length];
            for (int i = 0; i < Vertices.Length; i++)
                newVerts[i] = new Point(
                    center.X + (Vertices[i].X - center.X) * factor,
                    center.Y + (Vertices[i].Y - center.Y) * factor);
            return new Polygon2d(newVerts, IsClosed);
        }

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            var fields = new List<(string, object?)>(Vertices.Length + 1)
            {
                ("顶点数", Vertices.Length),
            };
            for (int i = 0; i < Vertices.Length; i++)
            {
                fields.Add(($"顶点[{i}]", Vertices[i]));
            }
            return fields;
        }
    }
}
