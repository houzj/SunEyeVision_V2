using OpenCvSharp;

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
        public Mat TransformMatrix { get; }
        public double AxisLength { get; }
        public uint XAxisColor { get; }
        public uint YAxisColor { get; }

        public CoordinateSystemGeometry(Mat transformMatrix, double axisLength, uint xAxisColor, uint yAxisColor)
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
        public Point2d[] Points { get; }
        public bool IsClosed { get; }

        public ContourGeometry(Point2d[] points, bool isClosed)
        {
            Points = points;
            IsClosed = isClosed;
        }
    }
}
