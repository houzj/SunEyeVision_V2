using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>Per-execution evidence, populated before fitting so failures retain their input.</summary>
public sealed class EdgeFitDiagnostics
{
    public string Stage { get; set; } = "未开始";
    public string Error { get; set; } = "";
    public Point2d[] RawPoints { get; set; } = Array.Empty<Point2d>();
    public Point2d[] CandidatePoints { get; set; } = Array.Empty<Point2d>();
    public Point2d[] ExcludedPoints { get; set; } = Array.Empty<Point2d>();
    public Point2d[] Inliers { get; set; } = Array.Empty<Point2d>();
    public Point2d[] Outliers { get; set; } = Array.Empty<Point2d>();
    public Point[][] Bands { get; set; } = Array.Empty<Point[]>();
    public int DuplicateCount { get; set; }
    public double DistanceThreshold { get; set; } = double.NaN;
    public double NormalX { get; set; } = double.NaN;
    public double NormalY { get; set; } = double.NaN;
    public double AnchorX { get; set; } = double.NaN;
    public double AnchorY { get; set; } = double.NaN;
    public double CircleCenterX { get; set; } = double.NaN;
    public double CircleCenterY { get; set; } = double.NaN;
    public double CircleRadius { get; set; } = double.NaN;
    public string Summary => $"{Stage}；原始点={RawPoints.Length}，候选点={CandidatePoints.Length}，" +
        $"屏蔽点={ExcludedPoints.Length}，重复点={DuplicateCount}，内点={Inliers.Length}，离群点={Outliers.Length}" +
        (double.IsFinite(DistanceThreshold) ? $"；距离阈值={DistanceThreshold:G6}px" : "") +
        (double.IsFinite(NormalX) ? $"；法向=({NormalX:G6},{NormalY:G6})，锚点=({AnchorX:G6},{AnchorY:G6})" : "") +
        (double.IsFinite(CircleRadius) ? $"；圆心=({CircleCenterX:G6},{CircleCenterY:G6})，半径={CircleRadius:G6}px" : "") +
        (Error.Length == 0 ? "" : $"；原因：{Error}");

    public static void EnsurePresentation(ResultPresentationConfig config)
    {
        if (config.Graphics.Any(item => item.Key == "fit-diagnostics")) return;
        config.Graphics.Add(new() { Key = "fit-diagnostics", Name = "失败诊断点及测量带", Group = "诊断", Order = 900,
            Style = new() { Name = "失败诊断点及测量带", IsVisible = true,
                OkColor = System.Windows.Media.Colors.Cyan, NgColor = System.Windows.Media.Colors.Cyan,
                CrossSize = 4.0, LineWidth = 1.0 } });
    }

    /// <summary>Only measurement evidence, never fabricated fitted geometry or result annotations.</summary>
    public static void FillFailureDisplay(DisplayCollector display, ResultPresentationConfig config,
        params EdgeFitDiagnostics[] diagnostics)
    {
        display.Clear();
        EnsurePresentation(config);
        var style = config.GetGraphicsStyle("fit-diagnostics");
        foreach (var diagnostic in diagnostics)
        {
            var points = diagnostic.RawPoints.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y))
                .Select(p => new Point(p.X, p.Y)).ToArray();
            if (points.Length > 0) display.AddCrosses(style, points);
            if (diagnostic.Bands.Length > 0) display.AddContours(style, diagnostic.Bands, isClosed: true);
        }
    }
}
