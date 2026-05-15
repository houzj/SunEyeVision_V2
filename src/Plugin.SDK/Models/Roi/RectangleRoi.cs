using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class RectangleRoi : RoiBase
    {
        private readonly Rect2d _rect;
        private readonly double _angleRadians;

        public RectangleRoi(double x, double y, double width, double height)
        {
            _rect = new Rect2d(x, y, width, height);
            _angleRadians = 0;
        }

        public RectangleRoi(double x, double y, double width, double height, double angleRadians)
        {
            _rect = new Rect2d(x, y, width, height);
            _angleRadians = angleRadians;
        }

        public RectangleRoi(Rect2d rect, double angleRadians = 0)
        {
            _rect = rect;
            _angleRadians = angleRadians;
        }

        public static RectangleRoi FromRotatedRect(Geometry.RotatedRect rotatedRect)
        {
            var boundingRect = rotatedRect.BoundingRect();
            return new RectangleRoi(boundingRect, rotatedRect.AngleRadians);
        }

        public static RectangleRoi FromCenter(Point2d center, double width, double height, double angleRadians = 0)
        {
            return new RectangleRoi(Rect2d.FromCenter(center, width, height), angleRadians);
        }

        public override RoiType Type => _angleRadians == 0 ? RoiType.Rectangle : RoiType.RotatedRectangle;
        public override bool IsValid => !_rect.IsEmpty;
        public override Rect2d BoundingBox => _rect;
        public override Point2d Center => _rect.Center;
        public override double Area => _rect.Area;

        public Rect2d Rect => _rect;
        public Geometry.RotatedRect RotatedRect => new Geometry.RotatedRect(Center, _rect.Width, _rect.Height, _angleRadians * 180 / Math.PI);
        public double X => _rect.X;
        public double Y => _rect.Y;
        public double Width => _rect.Width;
        public double Height => _rect.Height;
        public double Angle => _angleRadians;

        public override bool Contains(Point2d point)
        {
            if (_angleRadians == 0)
                return _rect.Contains(point);

            Point2d center = Center;
            Point2d localPoint = point - center;

            double cos = Math.Cos(-_angleRadians);
            double sin = Math.Sin(-_angleRadians);

            double rotatedX = localPoint.X * cos - localPoint.Y * sin;
            double rotatedY = localPoint.X * sin + localPoint.Y * cos;

            return Math.Abs(rotatedX) <= _rect.Width / 2 && Math.Abs(rotatedY) <= _rect.Height / 2;
        }

        public override Point2d[] GetBoundaryPoints()
        {
            if (_angleRadians == 0)
            {
                return new[] { _rect.TopLeft, _rect.TopRight, _rect.BottomRight, _rect.BottomLeft };
            }

            Point2d center = Center;
            Point2d[] corners = new[]
            {
                new Point2d(-_rect.Width / 2, -_rect.Height / 2),
                new Point2d(_rect.Width / 2, -_rect.Height / 2),
                new Point2d(_rect.Width / 2, _rect.Height / 2),
                new Point2d(-_rect.Width / 2, _rect.Height / 2)
            };

            double cos = Math.Cos(_angleRadians);
            double sin = Math.Sin(_angleRadians);

            for (int i = 0; i < corners.Length; i++)
            {
                double x = corners[i].X * cos - corners[i].Y * sin;
                double y = corners[i].X * sin + corners[i].Y * cos;
                corners[i] = new Point2d(x + center.X, y + center.Y);
            }

            return corners;
        }

        public override IRoi Offset(double dx, double dy)
        {
            return new RectangleRoi(_rect.Offset(dx, dy), _angleRadians);
        }

        public override IRoi Scale(double factor)
        {
            return new RectangleRoi(_rect.X * factor, _rect.Y * factor, _rect.Width * factor, _rect.Height * factor, _angleRadians);
        }

        public override IRoi Transform(double[] matrix)
        {
            if (matrix == null || matrix.Length < 9)
                return this;

            var corners = GetBoundaryPoints();
            Point2d[] transformedCorners = new Point2d[4];
            for (int i = 0; i < 4; i++)
                transformedCorners[i] = TransformPoint(corners[i], matrix);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in transformedCorners)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new RectangleRoi(minX, minY, maxX - minX, maxY - minY, _angleRadians);
        }

        private static Point2d TransformPoint(Point2d point, double[] matrix)
        {
            double x = point.X * matrix[0] + point.Y * matrix[1] + matrix[2];
            double y = point.X * matrix[3] + point.Y * matrix[4] + matrix[5];
            return new Point2d(x, y);
        }

        public override string ToString() =>
            _angleRadians == 0
                ? $"RectangleROI(X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1})"
                : $"RotatedRectROI(X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1}, theta={_angleRadians:F2})";
    }
}
