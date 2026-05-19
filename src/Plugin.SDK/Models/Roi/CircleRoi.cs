using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class CircleRoi : RoiBase
    {
        private readonly Circle2d _circle;

        public CircleRoi(double centerX, double centerY, double radius)
            => _circle = new Circle2d(centerX, centerY, radius);

        public CircleRoi(Circle2d circle) => _circle = circle;

        public static CircleRoi FromCenter(Point center, double radius)
            => new CircleRoi(new Circle2d(center, radius));

        public override RoiType Type => RoiType.Circle;
        public override bool IsValid => _circle.Radius > 0;
        public override Rect BoundingBox => _circle.BoundingBox;
        public override Point Center => _circle.Center;
        public override double Area => _circle.Area;

        public Circle2d Circle => _circle;
        public double CenterX => _circle.Center.X;
        public double CenterY => _circle.Center.Y;
        public double Radius => _circle.Radius;
        public double Diameter => _circle.Diameter;
        public double Circumference => _circle.Circumference;

        public override bool Contains(Point point) => _circle.Contains(point);

        public override Point[] GetBoundaryPoints()
        {
            const int pointCount = 64;
            Point[] points = new Point[pointCount];
            double angleStep = 2 * Math.PI / pointCount;
            for (int i = 0; i < pointCount; i++)
                points[i] = _circle.GetPointOnCircle(i * angleStep);
            return points;
        }

        public override IRoi Offset(double dx, double dy)
            => new CircleRoi(_circle.Offset(dx, dy));

        public override IRoi Scale(double factor)
            => new CircleRoi(_circle.Scale(factor));

        public override IRoi Transform(Matrix transformMatrix)
        {
            if (transformMatrix.IsIdentity)
                return this;

            double scaleX = Math.Sqrt(
                transformMatrix.M11 * transformMatrix.M11 + transformMatrix.M21 * transformMatrix.M21);
            double scaleY = Math.Sqrt(
                transformMatrix.M12 * transformMatrix.M12 + transformMatrix.M22 * transformMatrix.M22);
            double avgScale = (scaleX + scaleY) / 2;

            var newCenter = transformMatrix.Transform(_circle.Center);
            return new CircleRoi(newCenter.X, newCenter.Y, _circle.Radius * avgScale);
        }

        public override string ToString()
            => $"CircleROI(Center=({CenterX:F1}, {CenterY:F1}), R={Radius:F1})";
    }
}
