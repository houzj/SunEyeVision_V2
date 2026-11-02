using System.Windows;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Services.Canvas;

/// <summary>Explicit conversion at the WPF canvas boundary.</summary>
internal static class CanvasPointProjection
{
    internal static Point ToWpf(this CanvasPoint point) => new(point.X, point.Y);
    internal static CanvasPoint ToCanvas(this Point point) => new(point.X, point.Y);
}
