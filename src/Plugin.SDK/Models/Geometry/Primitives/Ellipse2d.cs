using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 椭圆结构 - 对齐 Halcon DispEllipse / HDrawingObject.ELLIPSE
    /// </summary>
    public readonly struct Ellipse2d : IEquatable<Ellipse2d>
    {
        /// <summary>椭圆中心</summary>
        public readonly Point2d Center;

        /// <summary>旋转角度（弧度）</summary>
        public readonly double Phi;

        /// <summary>主轴半径</summary>
        public readonly double Radius1;

        /// <summary>副轴半径</summary>
        public readonly double Radius2;

        public Ellipse2d(Point2d center, double phiRadians, double radius1, double radius2)
        {
            Center = center;
            Phi = phiRadians;
            Radius1 = radius1;
            Radius2 = radius2;
        }

        public Ellipse2d(double centerX, double centerY, double phiRadians, double radius1, double radius2)
            : this(new Point2d(centerX, centerY), phiRadians, radius1, radius2) { }

        /// <summary>面积</summary>
        public double Area => Math.PI * Radius1 * Radius2;

        /// <summary>周长（Ramanujan 近似）</summary>
        public double Circumference
        {
            get
            {
                double h = (Radius1 - Radius2) * (Radius1 - Radius2) /
                           ((Radius1 + Radius2) * (Radius1 + Radius2));
                return Math.PI * (Radius1 + Radius2) * (1 + (3 * h) / (10 + Math.Sqrt(4 - 3 * h)));
            }
        }

        /// <summary>偏移</summary>
        public Ellipse2d Offset(double dx, double dy) =>
            new Ellipse2d(Center.X + dx, Center.Y + dy, Phi, Radius1, Radius2);

        /// <summary>缩放</summary>
        public Ellipse2d Scale(double factor) =>
            new Ellipse2d(Center, Phi, Radius1 * factor, Radius2 * factor);

        public bool Equals(Ellipse2d other) =>
            Center.Equals(other.Center) && Phi.Equals(other.Phi) &&
            Radius1.Equals(other.Radius1) && Radius2.Equals(other.Radius2);

        public override bool Equals(object? obj) => obj is Ellipse2d other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Center, Phi, Radius1, Radius2);

        public override string ToString() =>
            $"Ellipse2d(Center={Center}, Phi={Phi:F4}, R1={Radius1:F2}, R2={Radius2:F2})";
    }
}
