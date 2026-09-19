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
        public IReadOnlyList<Point[]> Contours { get; }
        public bool IsClosed { get; }

        public BatchedContourGeometry(IReadOnlyList<Point[]> contours, bool isClosed = true)
        {
            Contours = contours;
            IsClosed = isClosed;
        }
    }
}
