using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>线段几何</summary>
    public sealed class LineSegment
    {
        public Point Start { get; }
        public Point End { get; }

        public LineSegment(Point start, Point end)
        {
            Start = start;
            End = end;
        }

        public double Length => (End - Start).Length;
    }

    /// <summary>圆弧几何</summary>
    public sealed class ArcGeometry
    {
        public Point Center { get; }
        public double Radius { get; }
        public double StartAngle { get; }
        public double EndAngle { get; }

        public ArcGeometry(Point center, double radius, double startAngle, double endAngle)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }
    }

    /// <summary>坐标系几何</summary>
    public sealed class CoordinateSystemGeometry
    {
        public Matrix TransformMatrix { get; }
        public double AxisLength { get; }
        public uint XAxisColor { get; }
        public uint YAxisColor { get; }

        public CoordinateSystemGeometry(Matrix transformMatrix, double axisLength, uint xAxisColor, uint yAxisColor)
        {
            TransformMatrix = transformMatrix;
            AxisLength = axisLength;
            XAxisColor = xAxisColor;
            YAxisColor = yAxisColor;
        }
    }

    /// <summary>轮廓几何</summary>
    public sealed class ContourGeometry
    {
        public Point[] Points { get; }
        public bool IsClosed { get; }
        public Matrix? Transform { get; }

        public ContourGeometry(Point[] points, bool isClosed)
            : this(points, isClosed, null)
        {
        }

        public ContourGeometry(Point[] points, bool isClosed, Matrix? transform)
        {
            Points = points;
            IsClosed = isClosed;
            Transform = transform;
        }
    }

    /// <summary>
    /// 批量轮廓几何。一个显示对象承载多条轮廓，避免为每个区域创建独立的
    /// ContourGeometry 和 WPF 绘制对象；区域状态由所属批次携带。
    /// </summary>
    public sealed class BatchedContourGeometry
    {
        /// <summary>Frozen outer list; the point arrays retain the existing borrowed geometry semantics.</summary>
        public IReadOnlyList<Point[]> Contours { get; }
        public bool IsClosed { get; }
        /// <summary>Explicit per-contour sources, or null when the producer supplied no mapping.</summary>
        public IReadOnlyList<DeclaredElementAddress>? SourceElements { get; }

        public BatchedContourGeometry(IReadOnlyList<Point[]> contours, bool isClosed = true,
            IReadOnlyList<DeclaredElementAddress>? sourceElements = null)
        {
            ArgumentNullException.ThrowIfNull(contours);
            var frozenContours = contours.ToArray();
            if (sourceElements != null)
            {
                var addresses = sourceElements.ToArray();
                if (addresses.Length != frozenContours.Length)
                    throw new ArgumentException("Each contour requires exactly one explicit source address.", nameof(sourceElements));
                if (addresses.Any(address => address == null))
                    throw new ArgumentException("Source addresses cannot contain null entries.", nameof(sourceElements));
                SourceElements = Array.AsReadOnly(addresses);
            }
            Contours = Array.AsReadOnly(frozenContours);
            IsClosed = isClosed;
        }
    }
}
