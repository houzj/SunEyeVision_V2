using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers;

/// <summary>Display fitted lines as one continuous line clipped to the execution image.</summary>
public static class FittedLineDisplay
{
    public static void RemoveLegacyExtensions(ResultPresentationConfig config, params string[] keys)
    {
        foreach (var item in config.Graphics.Where(item => keys.Contains(item.Key)).ToArray())
            config.Graphics.Remove(item);
    }

    public static void AddLine(DisplayCollector display, GraphicsStyle style, Point start, Point end,
        int imageWidth, int imageHeight, bool isOk)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return;
        var d = end - start;
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) ||
            !double.IsFinite(d.X) || !double.IsFinite(d.Y) || d.LengthSquared < 1e-18) return;
        double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
        bool Clip(double origin, double direction, double limit)
        {
            if (Math.Abs(direction) < 1e-12) return origin >= 0 && origin <= limit;
            var t0 = -origin / direction;
            var t1 = (limit - origin) / direction;
            if (t0 > t1) (t0, t1) = (t1, t0);
            lo = Math.Max(lo, t0);
            hi = Math.Min(hi, t1);
            return lo <= hi;
        }
        if (!Clip(start.X, d.X, imageWidth - 1) || !Clip(start.Y, d.Y, imageHeight - 1)) return;
        if (hi - lo > 1e-9)
            display.AddLine(style, start + d * lo, start + d * hi, isOk);
    }
}
