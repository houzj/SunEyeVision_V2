using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

internal readonly record struct TemplateMaskResolution(
    byte[]? EraseMask,
    byte[]? PaintMask,
    int Width,
    int Height);

/// <summary>
/// Resolves the current template-window mask state against a stored template.
/// Each layer has its own specified flag so clearing one layer never falls back
/// to, or overwrites, the other layer.
/// </summary>
internal static class TemplateMaskResolver
{
    public static TemplateMaskResolution Resolve(TemplateMaskSnapshot current, ContourMatchTemplate? stored)
    {
        var storedErase = stored?.Mask;
        var storedPaint = stored?.Mask2;
        var storedWidth = stored?.MaskWidth ?? 0;
        var storedHeight = stored?.MaskHeight ?? 0;
        if (storedWidth <= 0 || storedHeight <= 0)
        {
            storedWidth = stored?.Mask2Width ?? 0;
            storedHeight = stored?.Mask2Height ?? 0;
        }

        return new(
            current.EraseMaskSpecified ? current.EraseMask : storedErase,
            current.PaintMaskSpecified ? current.PaintMask : storedPaint,
            current.Width > 0 ? current.Width : storedWidth,
            current.Height > 0 ? current.Height : storedHeight);
    }
}
