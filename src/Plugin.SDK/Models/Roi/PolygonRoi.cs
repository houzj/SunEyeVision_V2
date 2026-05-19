using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class PolygonRoi : RoiBase
    {
        private readonly Point[] _vertices;

        public PolygonRoi(Point[] vertices)
        {
            _vertices = vertices ?? Array.Empty<Point>();
        }

        public static PolygonRoi FromRectangle(Rect rect)
        {
            return new PolygonRoi(new[]
            {
                new Point(rect.X, rect.Y),
                new Point(rect.X + rect.Width, rect.Y),
                new Point(rect.X + rect.Width, rect.Y + rect.Height),
                new Point(rect.X, rect.Y + rect.Height)
            });
        }

        public static PolygonRoi CreateRegular(Point center, double radius, int sides, double startAngle = 0)
        {
            if (sides < 3)
                throw new ArgumentException("正多边形至少需要3条边", nameof(sides));

            Point[] vertices = new Point[sides];
            double angleStep = 2 * Math.PI / sides;

            for (int i = 0; i < sides; i++)
            {
                double angle = startAngle + i * angleStep;
                vertices[i] = new Point(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)
                );
            }

            return new PolygonRoi(vertices);
        }

        public override RoiType Type => RoiType.Polygon;
        public override bool IsValid => _vertices.Length >= 3;

        public override Rect BoundingBox
        {
            get
            {
                if (_vertices.Length == 0)
                    return new Rect(0, 0, 0, 0);

                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var p in _vertices)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }

                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }

        public override Point Center
        {
            get
            {
                if (_vertices.Length == 0)
                    return new Point(0, 0);

                double sumX = 0, sumY = 0;
                foreach (var p in _vertices) { sumX += p.X; sumY += p.Y; }
                return new Point(sumX / _vertices.Length, sumY / _vertices.Length);
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
        public Point[] Vertices => _vertices;

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
                    perimeter += (_vertices[i] - _vertices[j]).Length;
                }
                return perimeter;
            }
        }

        public override bool Contains(Point point)
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

        public override Point[] GetBoundaryPoints() => _vertices;

        public override IRoi Offset(double dx, double dy)
        {
            var v = new Vector(dx, dy);
            Point[] newVertices = new Point[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
                newVertices[i] = _vertices[i] + v;
            return new PolygonRoi(newVertices);
        }

        public override IRoi Scale(double factor)
        {
            Point[] newVertices = new Point[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
                newVertices[i] = new Point(_vertices[i].X * factor, _vertices[i].Y * factor);
            return new PolygonRoi(newVertices);
        }

        public override IRoi Transform(Matrix transformMatrix)
        {
            if (transformMatrix.IsIdentity)
                return this;

            Point[] newVertices = new Point[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
                newVertices[i] = transformMatrix.Transform(_vertices[i]);
            return new PolygonRoi(newVertices);
        }

        /// <summary>
        /// 简化多边形（Douglas-Peucker算法）
        /// </summary>
        public PolygonRoi Simplify(double tolerance)
        {
            if (_vertices.Length <= 3)
                return this;

            var simplified = DouglasPeuckerSimplify(_vertices, tolerance);
            return new PolygonRoi(simplified);
        }

        private static Point[] DouglasPeuckerSimplify(Point[] points, double tolerance)
        {
            if (points.Length <= 2)
                return points;

            int first = 0, last = points.Length - 1;
            var resultIndices = new System.Collections.Generic.List<int> { first, last };

            SimplifySegment(points, first, last, tolerance, resultIndices);

            resultIndices.Sort();
            var result = new Point[resultIndices.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = points[resultIndices[i]];

            return result;
        }

        private static void SimplifySegment(Point[] points, int first, int last, double tolerance,
            System.Collections.Generic.List<int> result)
        {
            double maxDist = 0;
            int maxIdx = 0;

            for (int i = first + 1; i < last; i++)
            {
                double dist = PointToSegmentDistance(points[i], points[first], points[last]);
                if (dist > maxDist) { maxDist = dist; maxIdx = i; }
            }

            if (maxDist > tolerance)
            {
                SimplifySegment(points, first, maxIdx, tolerance, result);
                result.Add(maxIdx);
                SimplifySegment(points, maxIdx, last, tolerance, result);
            }
        }

        private static double PointToSegmentDistance(Point p, Point a, Point b)
        {
            var ab = b - a;
            var ap = p - a;
            double t = Math.Max(0, Math.Min(1, (ap.X * ab.X + ap.Y * ab.Y) / (ab.X * ab.X + ab.Y * ab.Y)));
            double projX = a.X + t * ab.X;
            double projY = a.Y + t * ab.Y;
            return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
        }

        public override string ToString() => $"PolygonROI({VertexCount} vertices)";
    }
}
