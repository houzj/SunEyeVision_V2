using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class PolygonRoi : RoiBase
    {
        private readonly Point2d[] _vertices;

        public PolygonRoi(Point2d[] vertices)
        {
            _vertices = vertices ?? Array.Empty<Point2d>();
        }

        public static PolygonRoi FromRectangle(Rect2d rect)
        {
            return new PolygonRoi(new[] { rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft });
        }

        public static PolygonRoi CreateRegular(Point2d center, double radius, int sides, double startAngle = 0)
        {
            if (sides < 3)
                throw new ArgumentException("Regular polygon needs at least 3 sides", nameof(sides));

            Point2d[] vertices = new Point2d[sides];
            double angleStep = 2 * Math.PI / sides;
            for (int i = 0; i < sides; i++)
            {
                double angle = startAngle + i * angleStep;
                vertices[i] = new Point2d(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
            }
            return new PolygonRoi(vertices);
        }

        public override RoiType Type => RoiType.Polygon;
        public override bool IsValid => _vertices.Length >= 3;

        public override Rect2d BoundingBox
        {
            get
            {
                if (_vertices.Length == 0) return new Rect2d(0, 0, 0, 0);
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var p in _vertices)
                {
                    minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
                }
                return new Rect2d(minX, minY, maxX - minX, maxY - minY);
            }
        }

        public override Point2d Center
        {
            get
            {
                if (_vertices.Length == 0) return new Point2d(0, 0);
                double sumX = 0, sumY = 0;
                foreach (var p in _vertices) { sumX += p.X; sumY += p.Y; }
                return new Point2d(sumX / _vertices.Length, sumY / _vertices.Length);
            }
        }

        public override double Area
        {
            get
            {
                if (_vertices.Length < 3) return 0;
                double area = 0;
                int n = _vertices.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    area += _vertices[i].X * _vertices[j].Y;
                    area -= _vertices[j].X * _vertices[i].Y;
                }
                return Math.Abs(area) / 2;
            }
        }

        public int VertexCount => _vertices.Length;
        public Point2d[] Vertices => _vertices;

        public double Perimeter
        {
            get
            {
                if (_vertices.Length < 2) return 0;
                double perimeter = 0;
                int n = _vertices.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    perimeter += _vertices[i].DistanceTo(_vertices[j]);
                }
                return perimeter;
            }
        }

        public override bool Contains(Point2d point)
        {
            if (_vertices.Length < 3) return false;
            int n = _vertices.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((_vertices[i].Y > point.Y) != (_vertices[j].Y > point.Y)) &&
                    (point.X < (_vertices[j].X - _vertices[i].X) * (point.Y - _vertices[i].Y) /
                              (_vertices[j].Y - _vertices[i].Y) + _vertices[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public override Point2d[] GetBoundaryPoints() => _vertices;

        public override IRoi Offset(double dx, double dy)
        {
            Point2d[] newVertices = new Point2d[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
                newVertices[i] = new Point2d(_vertices[i].X + dx, _vertices[i].Y + dy);
            return new PolygonRoi(newVertices);
        }

        public override IRoi Scale(double factor)
        {
            Point2d[] newVertices = new Point2d[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
                newVertices[i] = new Point2d(_vertices[i].X * factor, _vertices[i].Y * factor);
            return new PolygonRoi(newVertices);
        }

        public override IRoi Transform(double[] matrix)
        {
            if (matrix == null || matrix.Length < 9)
                return this;

            Point2d[] newVertices = new Point2d[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
            {
                double x = _vertices[i].X * matrix[0] + _vertices[i].Y * matrix[1] + matrix[2];
                double y = _vertices[i].X * matrix[3] + _vertices[i].Y * matrix[4] + matrix[5];
                newVertices[i] = new Point2d(x, y);
            }
            return new PolygonRoi(newVertices);
        }

        public override string ToString() => $"PolygonROI({VertexCount} vertices)";
    }
}
