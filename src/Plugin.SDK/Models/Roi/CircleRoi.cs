using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class CircleRoi : RoiBase
    {
        private readonly Circle2d _circle;

        public CircleRoi(double centerX, double centerY, double radius)
        {
            _circle = new Circle2d(centerX, centerY, radius);
        }

        public CircleRoi(Circle2d circle)
        {
            _circle = circle;
        }

        public static CircleRoi FromCenter(Point2d center, double radius)
        {
            return new CircleRoi(new Circle2d(center, radius));
        }

        public override RoiType Type => RoiType.Circle;
        public override bool IsValid => _circle.Radius > 0;
        public override Rect2d BoundingBox => _circle.BoundingBox;
        public override Point2d Center => _circle.Center;
        public override double Area => _circle.Area;

        public Circle2d Circle => _circle;
        public double CenterX => _circle.Center.X;
        public double CenterY => _circle.Center.Y;
        public double Radius => _circle.Radius;
        public double Diameter => _circle.Diameter;
        public double Circumference => _circle.Circumference;

        public override bool Contains(Point2d point) => _circle.Contains(point);

        public override Point2d[] GetBoundaryPoints()
        {
            const int pointCount = 64;
            Point2d[] points = new Point2d[pointCount];
            double angleStep = 2 * Math.PI / pointCount;
            for (int i = 0; i < pointCount; i++)
                points[i] = _circle.GetPointOnCircle(i * angleStep);
            return points;
        }

        public override IRoi Offset(double dx, double dy) => new CircleRoi(_circle.Offset(dx, dy));

        public override IRoi Scale(double factor) => new CircleRoi(_circle.Scale(factor));

        public override IRoi Transform(double[] matrix)
        {
            if (matrix == null || matrix.Length < 9)
                return this;

            double scaleX = Math.Sqrt(matrix[0] * matrix[0] + matrix[3] * matrix[3]);
            double scaleY = Math.Sqrt(matrix[1] * matrix[1] + matrix[4] * matrix[4]);
            double avgScale = (scaleX + scaleY) / 2;

            double newCenterX = _circle.Center.X * matrix[0] + _circle.Center.Y * matrix[1] + matrix[2];
            double newCenterY = _circle.Center.X * matrix[3] + _circle.Center.Y * matrix[4] + matrix[5];

            return new CircleRoi(newCenterX, newCenterY, _circle.Radius * avgScale);
        }

        public override string ToString() => $"CircleROI(Center=({CenterX:F1}, {CenterY:F1}), R={Radius:F1})";
    }
}
