using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class FullImageRoi : RoiBase
    {
        private readonly int _width;
        private readonly int _height;

        public FullImageRoi(int width, int height) { _width = width; _height = height; }
        public FullImageRoi() { _width = 0; _height = 0; }

        public override RoiType Type => RoiType.FullImage;
        public override bool IsValid => true;

        public override Rect BoundingBox => _width > 0 && _height > 0
            ? new Rect(0, 0, _width, _height) : new Rect(0, 0, 0, 0);

        public override Point Center => _width > 0 && _height > 0
            ? new Point(_width / 2.0, _height / 2.0) : new Point(0, 0);

        public override double Area => _width * _height;
        public int Width => _width;
        public int Height => _height;

        public override bool Contains(Point point)
        {
            if (_width <= 0 || _height <= 0) return true;
            return point.X >= 0 && point.X < _width && point.Y >= 0 && point.Y < _height;
        }

        public override Point[] GetBoundaryPoints()
        {
            if (_width <= 0 || _height <= 0)
                return Array.Empty<Point>();
            return new Point[] { new Point(0, 0), new Point(_width, 0), new Point(_width, _height), new Point(0, _height) };
        }

        public override IRoi Offset(double dx, double dy) => this;
        public override IRoi Scale(double factor) => new FullImageRoi((int)(_width * factor), (int)(_height * factor));
        public override IRoi Transform(Matrix transformMatrix) => this;

        public override string ToString() => _width > 0 && _height > 0
            ? $"FullImageROI({Width}x{Height})" : "FullImageROI";
    }
}
