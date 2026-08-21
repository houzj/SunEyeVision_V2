using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;

namespace CommonControl;

/// <summary>
/// 编译诊断标记着色器：在错误/警告 span 底部绘制波浪线（对齐 RoslynPad TextMarkerService）。
/// 采用 IBackgroundRenderer 绘制在文字下层，精准 span 不污染整行、不覆盖 xshd 语法颜色。
/// 分级着色：Error 红 / Warning 蓝，颜色随暗色主题。运行在 KnownLayer.Selection 层。
/// </summary>
internal sealed class ErrorMarkerColorizer : IBackgroundRenderer
{
    private readonly Func<IReadOnlyList<Diagnostic>> _getDiagnostics;
    private readonly bool _isDark;

    // 浅色主题：Error 红 / Warning 蓝
    private static readonly Brush ErrorBrush = FrozenBrush(0xFF, 0x00, 0x00);
    private static readonly Brush WarningBrush = FrozenBrush(0x1E, 0x90, 0xFF);
    // 暗色主题：Error 亮红 / Warning 亮蓝（深色底上更清晰）
    private static readonly Brush DarkErrorBrush = FrozenBrush(0xFF, 0x5F, 0x5F);
    private static readonly Brush DarkWarningBrush = FrozenBrush(0x64, 0xBE, 0xFF);

    private const double WaveOffset = 2.5;   // 波浪间距（像素）
    private const double WavePenWidth = 1.0; // 波浪线宽

    public ErrorMarkerColorizer(Func<IReadOnlyList<Diagnostic>> getDiagnostics, bool isDark)
    {
        _getDiagnostics = getDiagnostics;
        _isDark = isDark;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var diagnostics = _getDiagnostics();
        if (diagnostics.Count == 0 || !textView.VisualLinesValid) return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        // 可视区偏移范围：只画可见行的诊断，滚动时按需绘制
        var viewStart = visualLines.First().FirstDocumentLine.Offset;
        var viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

        foreach (var diag in diagnostics)
        {
            var span = diag.Location.SourceSpan;
            if (span.Start >= viewEnd || span.End <= viewStart) continue;

            var brush = GetSeverityBrush(diag.Severity);
            DrawWaveLine(drawingContext, textView, span.Start, span.Length, brush);
        }
    }

    private Brush GetSeverityBrush(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Warning => _isDark ? DarkWarningBrush : WarningBrush,
            _ => _isDark ? DarkErrorBrush : ErrorBrush
        };

    /// <summary>沿 span 文字底部画一排小三角折线（波浪线），对齐 RoslynPad TextMarkerService.CreatePoints。</summary>
    private void DrawWaveLine(DrawingContext dc, TextView textView, int startOffset, int length, Brush brush)
    {
        var segment = new TextSegment { StartOffset = startOffset, Length = length };
        var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, segment);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var rect in rects)
            {
                var bottomLeft = rect.BottomLeft;
                var count = Math.Max((int)((rect.BottomRight.X - bottomLeft.X) / WaveOffset) + 1, 4);
                ctx.BeginFigure(bottomLeft, isFilled: false, isClosed: false);
                ctx.PolyLineTo(CreateWavePoints(bottomLeft, count), isStroked: true, isSmoothJoin: false);
            }
        }
        geometry.Freeze();

        var pen = new Pen(brush, WavePenWidth);
        pen.Freeze();
        dc.DrawGeometry(Brushes.Transparent, pen, geometry);
    }

    /// <summary>自底向上生成波浪点序列：每格交替上浮 offset 与回落，形成锯齿。</summary>
    private static List<Point> CreateWavePoints(Point bottomLeft, int count)
    {
        var points = new List<Point>(count);
        for (var i = 0; i < count; i++)
            points.Add(new Point(bottomLeft.X + i * WaveOffset, bottomLeft.Y - ((i + 1) % 2 == 0 ? WaveOffset : 0)));
        return points;
    }

    private static Brush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }
}
