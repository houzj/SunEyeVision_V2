using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Icons;

/// <summary>Single scalable, monochrome renderer shared by toolbox, node and window title.</summary>
public sealed class ToolIcon : FrameworkElement
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(nameof(Key), typeof(string), typeof(ToolIcon), new FrameworkPropertyMetadata("generic", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(ToolIcon), new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Key { get => (string)GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    protected override Size MeasureOverride(Size availableSize) => new(24, 24);
    protected override void OnRender(DrawingContext drawingContext)
    {
        var scale = Math.Min(ActualWidth, ActualHeight) / 24;
        if (scale <= 0 || Foreground == null) return;
        var pen = new Pen(Foreground, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawingContext.PushTransform(new TranslateTransform((ActualWidth - 24 * scale) / 2, (ActualHeight - 24 * scale) / 2));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, pen, ToolIconGeometry.Get(Key));
        drawingContext.Pop(); drawingContext.Pop();
    }
}

public static class ToolIconGeometry
{
    private static readonly IReadOnlyDictionary<string, Geometry> Icons = Create();
    public static bool Contains(string key) => key != null && Icons.ContainsKey(key);
    public static Geometry Get(string key) => key != null && Icons.TryGetValue(key, out var geometry) ? geometry : Icons["generic"];
    private static IReadOnlyDictionary<string, Geometry> Create()
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["generic"] = "M4,4 L20,4 20,20 4,20 Z M8,12 L16,12 M12,8 L12,16",
            ["camera"] = "M3,7 L7,7 9,4 15,4 17,7 21,7 21,20 3,20 Z M16,13 A4,4 0 1 1 8,13 A4,4 0 1 1 16,13",
            ["image"] = "M3,3 L21,3 21,21 3,21 Z M3,17 L9,11 14,16 17,12 21,16 M17,7 A1,1 0 1 1 15,7 A1,1 0 1 1 17,7",
            ["sliders"] = "M3,6 L7,6 M11,6 L21,6 M7,3 L11,3 11,9 7,9 Z M3,12 L14,12 M18,12 L21,12 M14,9 L18,9 18,15 14,15 Z M3,18 L6,18 M10,18 L21,18 M6,15 L10,15 10,21 6,21 Z",
            ["locate"] = "M3,8 L3,3 8,3 M16,3 L21,3 21,8 M21,16 L21,21 16,21 M8,21 L3,21 3,16 M8,12 L16,12 M12,8 L12,16",
            ["region"] = "M3,6 L7,3 12,5 17,3 21,8 18,12 21,17 17,21 11,18 6,21 3,16 5,11 Z M8,8 L16,8 16,16 8,16 Z",
            ["ruler"] = "M3,5 L21,5 21,19 3,19 Z M7,5 L7,10 M12,5 L12,13 M17,5 L17,10",
            ["axes"] = "M12,3 L12,16 3,21 M12,16 L21,21 M9,6 L12,3 15,6 M3,17 L3,21 7,21 M17,21 L21,21 21,17",
            ["exchange"] = "M3,7 L21,7 17,3 M21,7 L17,11 M21,17 L3,17 7,13 M3,17 L7,21",
            ["branch"] = "M7,5 L7,17 M7,12 C7,8 17,12 17,7 M9,4 A2,2 0 1 1 5,4 A2,2 0 1 1 9,4 M9,19 A2,2 0 1 1 5,19 A2,2 0 1 1 9,19 M19,5 A2,2 0 1 1 15,5 A2,2 0 1 1 19,5",
            ["code"] = "M8,6 L2,12 8,18 M16,6 L22,12 16,18 M14,3 L10,21",
            ["receive"] = "M14,3 L21,3 21,21 14,21 M3,12 L16,12 M11,7 L16,12 11,17",
            ["send"] = "M10,3 L3,3 3,21 10,21 M8,12 L21,12 M16,7 L21,12 16,17",
            ["parse"] = "M12,21 L12,14 5,7 5,3 M5,3 L2,6 M5,3 L8,6 M12,14 L19,7 19,3 M19,3 L16,6 M19,3 L22,6",
            ["assemble"] = "M5,21 L5,17 12,10 19,17 19,21 M12,10 L12,3 M8,7 L12,3 16,7",
            ["circle"] = "M20,12 A8,8 0 1 1 4,12 A8,8 0 1 1 20,12 M10,12 L14,12 M12,10 L12,14",
            ["line"] = "M3,18 L21,6 M3,14 L3,18 7,18 M17,6 L21,6 21,10",
            ["match"] = "M3,8 L3,3 8,3 M16,3 L21,3 21,8 M3,16 L3,21 8,21 M16,21 L21,21 21,16 M12,6 L17,15 7,15 Z",
            ["threshold"] = "M20,12 A8,8 0 1 1 4,12 A8,8 0 1 1 20,12 M12,4 L12,20 M12,7 L18,7 M12,11 L20,11 M12,15 L19,15 M12,18 L17,18",
            ["binarize"] = "M3,3 L21,3 21,21 3,21 Z M3,12 L21,12 M12,3 L12,21 M4,4 L11,11 M4,7 L8,11 M16,13 L20,17 M13,13 L20,20 M13,17 L16,20",
            ["morphology"] = "M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M17,14 L22,21 12,21 Z",
            ["blob"] = "M3,7 C3,2 11,2 11,7 C11,12 3,12 3,7 Z M14,15 C12,9 22,8 22,15 C22,22 14,22 14,15 Z M7,16 L10,16 10,20 7,20 Z",
            ["point-point"] = "M6,12 A2,2 0 1 1 2,12 A2,2 0 1 1 6,12 M22,12 A2,2 0 1 1 18,12 A2,2 0 1 1 22,12 M7,12 L17,12 M9,10 L7,12 9,14 M15,10 L17,12 15,14",
            ["point-line"] = "M5,6 A2,2 0 1 1 1,6 A2,2 0 1 1 5,6 M3,9 L3,18 M2,21 L22,21 M3,17 L7,17 7,21 M6,10 L18,10 M14,7 L18,10 14,13",
            ["point-circle"] = "M22,12 A7,7 0 1 1 8,12 A7,7 0 1 1 22,12 M5,12 A2,2 0 1 1 1,12 A2,2 0 1 1 5,12 M6,12 L14,12 M11,9 L14,12 11,15",
            ["line-line"] = "M3,4 L21,4 M3,20 L21,20 M12,6 L12,18 M9,9 L12,6 15,9 M9,15 L12,18 15,15",
            ["line-circle"] = "M3,3 L3,21 M22,12 A6,6 0 1 1 10,12 A6,6 0 1 1 22,12 M5,12 L9,12 M7,10 L5,12 7,14",
            ["circle-circle"] = "M9,12 A4,4 0 1 1 1,12 A4,4 0 1 1 9,12 M23,12 A4,4 0 1 1 15,12 A4,4 0 1 1 23,12 M10,12 L14,12 M11,10 L10,12 11,14 M13,10 L14,12 13,14",
            ["verify"] = "M12,3 L20,6 20,14 12,21 4,14 4,6 Z M8,12 L11,15 16,9",
            ["subroutine"] = "M3,3 L9,3 9,9 3,9 Z M15,15 L21,15 21,21 15,21 Z M6,9 L6,18 15,18 M11,15 L15,18 11,21 M15,3 L21,3 21,9 15,9 Z M9,6 L15,6"
        };
        var result = new Dictionary<string, Geometry>(StringComparer.Ordinal);
        foreach (var pair in paths) { var geometry = Geometry.Parse(pair.Value); geometry.Freeze(); result.Add(pair.Key, geometry); }
        return result;
    }
}
