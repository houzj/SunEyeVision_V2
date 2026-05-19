using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.Models.Roi
{
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
    /// ROI接口 — 纯几何定义，不依赖任何图像处理库
    /// </summary>
    public interface IRoi
    {
        RoiType Type { get; }
        bool IsValid { get; }
        Rect BoundingBox { get; }
        Point Center { get; }
        double Area { get; }
        bool Contains(Point point);
        Point[] GetBoundaryPoints();
        IRoi Offset(double dx, double dy);
        IRoi Scale(double factor);
        IRoi Transform(Matrix transformMatrix);
    }

    public abstract class RoiBase : IRoi
    {
        public abstract RoiType Type { get; }
        public abstract bool IsValid { get; }
        public abstract Rect BoundingBox { get; }
        public abstract Point Center { get; }
        public abstract double Area { get; }
        public abstract bool Contains(Point point);
        public abstract Point[] GetBoundaryPoints();
        public abstract IRoi Offset(double dx, double dy);
        public abstract IRoi Scale(double factor);
        public abstract IRoi Transform(Matrix transformMatrix);

        public override string ToString() => $"{Type} ROI";
    }
}
