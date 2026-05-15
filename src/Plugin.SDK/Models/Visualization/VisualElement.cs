using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>线段几何</summary>
    public sealed class LineSegment
    {
        public Point2d Start { get; }
        public Point2d End { get; }

        public LineSegment(Point2d start, Point2d end)
        {
            Start = start;
            End = end;
        }

        public double Length => Start.DistanceTo(End);
    }

    /// <summary>圆弧几何</summary>
    public sealed class ArcGeometry
    {
        public Point2d Center { get; }
        public double Radius { get; }
        public double StartAngle { get; }
        public double EndAngle { get; }

        public ArcGeometry(Point2d center, double radius, double startAngle, double endAngle)
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
        /// <summary>3x3 变换矩阵（列主序，9 个元素）</summary>
        public double[] TransformMatrix { get; }
        public double AxisLength { get; }
        public uint XAxisColor { get; }
        public uint YAxisColor { get; }

        public CoordinateSystemGeometry(double[] transformMatrix, double axisLength, uint xAxisColor, uint yAxisColor)
        {
            TransformMatrix = transformMatrix;
            AxisLength = axisLength;
            XAxisColor = xAxisColor;
            YAxisColor = yAxisColor;
        }
    }

    /// <summary>箭头几何</summary>
    public sealed class ArrowGeometry
    {
        public Point2d Start { get; }
        public Point2d End { get; }
        public double HeadSize { get; }

        public ArrowGeometry(Point2d start, Point2d end, double headSize)
        {
            Start = start;
            End = end;
            HeadSize = headSize;
        }
    }

    /// <summary>轮廓几何</summary>
    public sealed class ContourGeometry
    {
        public Point2d[] Points { get; }
        public bool IsClosed { get; }

        public ContourGeometry(Point2d[] points, bool isClosed)
        {
            Points = points;
            IsClosed = isClosed;
        }
    }
}
