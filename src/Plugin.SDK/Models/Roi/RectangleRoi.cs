using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class RectangleRoi : RoiBase
    {
        private readonly Rect _rect;
        private readonly double _angleRadians;

        public RectangleRoi(double x, double y, double width, double height)
        {
            _rect = new Rect(x, y, width, height);
            _angleRadians = 0;
        }

        public RectangleRoi(double x, double y, double width, double height, double angleRadians)
        {
            _rect = new Rect(x, y, width, height);
            _angleRadians = angleRadians;
        }

        public RectangleRoi(Rect rect, double angleRadians = 0)
        {
            _rect = rect;
            _angleRadians = angleRadians;
        }

        public static RectangleRoi FromCenter(Point center, double width, double height, double angleRadians = 0)
        {
            return new RectangleRoi(
                new Rect(center.X - width / 2, center.Y - height / 2, width, height),
                angleRadians
            );
        }

        public override RoiType Type => _angleRadians == 0 ? RoiType.Rectangle : RoiType.RotatedRectangle;
        public override bool IsValid => _rect.Width > 0 && _rect.Height > 0;
        public override Rect BoundingBox => _rect;
        public override Point Center => new Point(_rect.X + _rect.Width / 2, _rect.Y + _rect.Height / 2);
        public override double Area => _rect.Width * _rect.Height;

        public Rect Rect => _rect;
        public double X => _rect.X;
        public double Y => _rect.Y;
        public double Width => _rect.Width;
        public double Height => _rect.Height;
        public double Angle => _angleRadians;

        public override bool Contains(Point point)
        {
            if (_angleRadians == 0)
                return _rect.Contains(point);

            Point center = Center;
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;

            double cos = Math.Cos(-_angleRadians);
            double sin = Math.Sin(-_angleRadians);

            double rotatedX = dx * cos - dy * sin;
            double rotatedY = dx * sin + dy * cos;

            return Math.Abs(rotatedX) <= _rect.Width / 2 && Math.Abs(rotatedY) <= _rect.Height / 2;
        }

        public override Point[] GetBoundaryPoints()
        {
            if (_angleRadians == 0)
            {
                return new[]
                {
                    new Point(_rect.X, _rect.Y),
                    new Point(_rect.X + _rect.Width, _rect.Y),
                    new Point(_rect.X + _rect.Width, _rect.Y + _rect.Height),
                    new Point(_rect.X, _rect.Y + _rect.Height)
                };
            }

            Point center = Center;
            double halfW = _rect.Width / 2;
            double halfH = _rect.Height / 2;

            double cos = Math.Cos(_angleRadians);
            double sin = Math.Sin(_angleRadians);

            var corners = new[]
            {
                new Point(-halfW, -halfH),
                new Point(halfW, -halfH),
                new Point(halfW, halfH),
                new Point(-halfW, halfH)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                double x = corners[i].X * cos - corners[i].Y * sin + center.X;
                double y = corners[i].X * sin + corners[i].Y * cos + center.Y;
                corners[i] = new Point(x, y);
            }

            return corners;
        }

        public override IRoi Offset(double dx, double dy)
        {
            return new RectangleRoi(new Rect(_rect.X + dx, _rect.Y + dy, _rect.Width, _rect.Height), _angleRadians);
        }

        public override IRoi Scale(double factor)
        {
            return new RectangleRoi(
                _rect.X * factor, _rect.Y * factor,
                _rect.Width * factor, _rect.Height * factor,
                _angleRadians
            );
        }

        public override IRoi Transform(Matrix transformMatrix)
        {
            if (transformMatrix.IsIdentity)
                return this;

            var corners = GetBoundaryPoints();
            var transformedCorners = new Point[4];

            for (int i = 0; i < 4; i++)
                transformedCorners[i] = transformMatrix.Transform(corners[i]);

            // 计算最小外接矩形
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in transformedCorners)
            {
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
            }
            double angle = Math.Atan2(
                transformedCorners[1].Y - transformedCorners[0].Y,
                transformedCorners[1].X - transformedCorners[0].X);

            return new RectangleRoi(minX, minY, maxX - minX, maxY - minY, angle);
        }

        public override string ToString() =>
            _angleRadians == 0
                ? $"RectangleROI(X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1})"
                : $"RotatedRectROI(X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1}, θ={_angleRadians:F2})";
    }
}
