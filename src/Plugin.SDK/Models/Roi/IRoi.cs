using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
    /// <summary>
    /// ROI类型枚举
    /// </summary>
    public enum RoiType
    {
        FullImage = 0,
        Rectangle = 1,
        RotatedRectangle = 2,
        Annulus = 3,
        Polygon = 4,
        LineCaliper = 5,
        ArcCaliper = 6,
        Circle = 7,
        Ellipse = 8,
        Custom = 99
    }

    /// <summary>
    /// ROI接口
    /// </summary>
    public interface IRoi
    {
        RoiType Type { get; }
        bool IsValid { get; }
        Rect2d BoundingBox { get; }
        RectI BoundingRect { get; }
        Point2d Center { get; }
        double Area { get; }
        bool Contains(Point2d point);
        Point2d[] GetBoundaryPoints();
        IRoi Offset(double dx, double dy);
        IRoi Scale(double factor);
        IRoi Transform(double[] transformMatrix);
    }

    /// <summary>
    /// ROI基类
    /// </summary>
    public abstract class RoiBase : IRoi
    {
        public abstract RoiType Type { get; }
        public abstract bool IsValid { get; }
        public abstract Rect2d BoundingBox { get; }
        public abstract Point2d Center { get; }
        public abstract double Area { get; }

        public abstract bool Contains(Point2d point);
        public abstract Point2d[] GetBoundaryPoints();

        public virtual RectI BoundingRect => new RectI(
            (int)Math.Floor(BoundingBox.X),
            (int)Math.Floor(BoundingBox.Y),
            (int)Math.Ceiling(BoundingBox.Width),
            (int)Math.Ceiling(BoundingBox.Height));

        public abstract IRoi Offset(double dx, double dy);
        public abstract IRoi Scale(double factor);
        public abstract IRoi Transform(double[] transformMatrix);

        public override string ToString() => $"{Type} ROI";
    }
}
