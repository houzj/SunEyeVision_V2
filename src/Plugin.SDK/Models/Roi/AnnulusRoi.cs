using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    public sealed class AnnulusRoi : RoiBase
    {
        private readonly Annulus2d _annulus;

        public AnnulusRoi(double centerX, double centerY, double innerRadius, double outerRadius)
        {
            _annulus = new Annulus2d(centerX, centerY, innerRadius, outerRadius);
        }

        public AnnulusRoi(double centerX, double centerY, double innerRadius, double outerRadius,
                          double startAngle, double endAngle)
        {
            _annulus = new Annulus2d(centerX, centerY, innerRadius, outerRadius, startAngle, endAngle);
        }

        public AnnulusRoi(Annulus2d annulus)
        {
            _annulus = annulus;
        }

        public static AnnulusRoi FromCenter(Point2d center, double innerRadius, double outerRadius)
        {
            return new AnnulusRoi(new Annulus2d(center, innerRadius, outerRadius));
        }

        public override RoiType Type => RoiType.Annulus;
        public override bool IsValid => _annulus.Width > 0;
        public override Rect2d BoundingBox => _annulus.BoundingBox;
        public override Point2d Center => _annulus.Center;
        public override double Area => _annulus.Area;

        public Annulus2d Annulus => _annulus;
        public double CenterX => _annulus.Center.X;
        public double CenterY => _annulus.Center.Y;
        public double InnerRadius => _annulus.InnerRadius;
        public double OuterRadius => _annulus.OuterRadius;
        public double StartAngle => _annulus.StartAngle;
        public double EndAngle => _annulus.EndAngle;
        public bool IsFullCircle => _annulus.IsFullCircle;

        public override bool Contains(Point2d point) => _annulus.Contains(point);

        public override Point2d[] GetBoundaryPoints()
        {
            const int pointsPerCircle = 32;
            int totalPoints = IsFullCircle ? pointsPerCircle * 2 : pointsPerCircle * 2 + 4;
            Point2d[] points = new Point2d[totalPoints];
            int index = 0;

            if (IsFullCircle)
            {
                double angleStep = 2 * Math.PI / pointsPerCircle;
                for (int i = 0; i < pointsPerCircle; i++)
                    points[index++] = _annulus.InnerCircle.GetPointOnCircle(i * angleStep);
                for (int i = 0; i < pointsPerCircle; i++)
                    points[index++] = _annulus.OuterCircle.GetPointOnCircle(i * angleStep);
            }
            else
            {
                Point2d[] samplePoints = _annulus.GetSamplePoints(pointsPerCircle);
                foreach (var p in samplePoints)
                    points[index++] = p;

                for (int i = pointsPerCircle - 1; i >= 0; i--)
                {
                    double angle = StartAngle + (EndAngle - StartAngle) * i / pointsPerCircle;
                    points[index++] = new Point2d(CenterX + InnerRadius * Math.Cos(angle),
                                                  CenterY + InnerRadius * Math.Sin(angle));
                }
            }

            return points;
        }

        public override IRoi Offset(double dx, double dy) => new AnnulusRoi(_annulus.Offset(dx, dy));

        public override IRoi Scale(double factor) => new AnnulusRoi(_annulus.Scale(factor));

        public override IRoi Transform(double[] matrix)
        {
            if (matrix == null || matrix.Length < 9)
                return this;

            double scaleX = Math.Sqrt(matrix[0] * matrix[0] + matrix[3] * matrix[3]);
            double scaleY = Math.Sqrt(matrix[1] * matrix[1] + matrix[4] * matrix[4]);
            double avgScale = (scaleX + scaleY) / 2;

            double newCenterX = _annulus.Center.X * matrix[0] + _annulus.Center.Y * matrix[1] + matrix[2];
            double newCenterY = _annulus.Center.X * matrix[3] + _annulus.Center.Y * matrix[4] + matrix[5];

            double rotation = Math.Atan2(matrix[3], matrix[0]);

            return new AnnulusRoi(newCenterX, newCenterY,
                _annulus.InnerRadius * avgScale, _annulus.OuterRadius * avgScale,
                _annulus.StartAngle + rotation, _annulus.EndAngle + rotation);
        }

        public override string ToString() =>
            IsFullCircle
                ? $"AnnulusROI(Center=({CenterX:F1}, {CenterY:F1}), R=[{InnerRadius:F1}, {OuterRadius:F1}])"
                : $"AnnulusROI(Center=({CenterX:F1}, {CenterY:F1}), R=[{InnerRadius:F1}, {OuterRadius:F1}], theta=[{StartAngle:F2}, {EndAngle:F2}])";
    }
}
