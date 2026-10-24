using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.UI.Services.Rendering;

/// <summary>Projects immutable image-pixel contract geometry into display coordinates.</summary>
internal static class ContractGeometryDisplayProjection
{
    /// <summary>Returns the original frame and fresh corners in local TL, TR, BR, BL order.</summary>
    internal static (CoordinateFrame Frame, ContourGeometry Contour) Project(OrientedRectangleGeometry rectangle)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        if (rectangle.Frame.Domain != CoordinateDomain.Image || rectangle.Frame.Unit != CoordinateUnit.Pixel)
            throw new NotSupportedException("Rectangle display projection requires an Image/Pixel coordinate frame; calibration is not applied.");

        // Contract angles are radians in image coordinates: +X right, +Y down.
        var cos = Math.Cos(rectangle.AngleRadians);
        var sin = Math.Sin(rectangle.AngleRadians);
        var halfWidth = rectangle.Width / 2;
        var halfHeight = rectangle.Height / 2;
        Point Corner(double x, double y)
        {
            var point = new Point(rectangle.Center.X + x * cos - y * sin,
                                  rectangle.Center.Y + x * sin + y * cos);
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                throw new ArgumentOutOfRangeException(nameof(rectangle), "Projected coordinates must be finite.");
            return point;
        }
        return (rectangle.Frame, new ContourGeometry(new[]
        {
            Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight),
            Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight)
        }, isClosed: true));
    }
}
