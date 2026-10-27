using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
// 解决 System.Windows.Media.LineSegment 与 SDK LineSegment 的命名冲突
using SdkLineSegment = SunEyeVision.Plugin.SDK.Models.Visualization.LineSegment;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 契约测试：DisplayCollector 新增 AddCircle/AddLine 必须产出正确的 GraphicsType 与几何数据，
/// 使子程序显示项能按真实类型画"真圆/真直线"（而非折线轮廓）。
/// </summary>
public class DisplayCollectorGeometryContractTests
{
    private static GraphicsStyle MakeStyle() => new()
    {
        Name = "测试样式",
        LineWidth = 2,
        OkColor = Color.FromRgb(0, 255, 0)
    };

    [Fact]
    public void AddCircle_ProducesCircleGraphicsType_WithCircle2dGeometry()
    {
        var collector = new DisplayCollector();
        var style = MakeStyle();
        var circle = new Circle2d(new Point(10, 20), 30);

        collector.AddCircle(style, circle);

        var item = Assert.Single(collector.Items.OfType<GraphicsDisplayItem>());
        Assert.Equal(GraphicsType.Circle, item.Type);
        Assert.Same(style, item.Style);

        var geom = Assert.IsType<Circle2d>(Assert.Single(item.Geometries));
        Assert.Equal(10, geom.Center.X);
        Assert.Equal(20, geom.Center.Y);
        Assert.Equal(30, geom.Radius);
    }

    [Fact]
    public void AddLine_ProducesLineGraphicsType_WithLineSegmentGeometry()
    {
        var collector = new DisplayCollector();
        var style = MakeStyle();

        collector.AddLine(style, new Point(0, 0), new Point(100, 50));

        var item = Assert.Single(collector.Items.OfType<GraphicsDisplayItem>());
        Assert.Equal(GraphicsType.Line, item.Type);

        var geom = Assert.IsType<SdkLineSegment>(Assert.Single(item.Geometries));
        Assert.Equal(0, geom.Start.X);
        Assert.Equal(0, geom.Start.Y);
        Assert.Equal(100, geom.End.X);
        Assert.Equal(50, geom.End.Y);
    }

    [Fact]
    public void DistinctStyles_ProduceSeparateGraphicsItems()
    {
        var collector = new DisplayCollector();

        // 子程序每个显示项有独立 GraphicsStyle 实例，故不同显示项 → 独立 GraphicsDisplayItem
        collector.AddCircle(MakeStyle(), new Circle2d(0, 0, 5));
        collector.AddRect(MakeStyle(), new Rect(0, 0, 10, 10));

        Assert.Equal(2, collector.Items.OfType<GraphicsDisplayItem>().Count());
    }
    [Fact]
    public void Shared_style_keeps_geometry_types_separate_across_accumulation_and_freeze()
    {
        var style = MakeStyle(); var first = new DisplayCollector();
        first.AddCircle(style, new Circle2d(1, 2, 3), true);
        first.AddLine(style, new Point(1, 2), new Point(3, 4), false);
        Assert.Equal(2, first.Items.Count); // Freezes the first round before another round is merged.
        var second = new DisplayCollector();
        second.AddCircle(style, new Circle2d(5, 6, 7), false);
        second.AddLine(style, new Point(5, 6), new Point(7, 8), true);
        first.Merge(second);
        var items = first.Items.OfType<GraphicsDisplayItem>().ToArray(); Assert.Equal(2, items.Length);
        var circles = Assert.Single(items, item => item.Type == GraphicsType.Circle);
        var lines = Assert.Single(items, item => item.Type == GraphicsType.Line);
        Assert.Same(style, circles.Style); Assert.Same(style, lines.Style);
        Assert.Equal(2, circles.Geometries.Length); Assert.All(circles.Geometries, value => Assert.IsType<Circle2d>(value));
        Assert.Equal(2, lines.Geometries.Length); Assert.All(lines.Geometries, value => Assert.IsType<SdkLineSegment>(value));
        Assert.Equal(new[] { true, false }, circles.IsPassArray); Assert.Equal(new[] { false, true }, lines.IsPassArray);
        first.Clear(); first.AddLine(style, new Point(), new Point(1, 1));
        Assert.Equal(GraphicsType.Line, Assert.IsType<GraphicsDisplayItem>(Assert.Single(first.Items)).Type);
    }

    [Fact]
    public void Shared_style_circle_and_line_both_render_and_follow_live_style_color()
    {
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var style = new GraphicsStyle { OkColor = Colors.Lime, LineWidth = 3 };
                var collector = new DisplayCollector(); collector.AddCircle(style, new Circle2d(35, 45, 14));
                collector.AddLine(style, new Point(90, 25), new Point(130, 65));
                var layer = new SunEyeVision.Plugin.SDK.UI.Controls.ResultRenderLayer();
                var surface = new System.Windows.Controls.Grid { Width = 160, Height = 100, Background = Brushes.Black };
                surface.Children.Add(layer); layer.SetDisplayCollector(collector);
                surface.Measure(new Size(160, 100)); surface.Arrange(new Rect(0, 0, 160, 100)); surface.UpdateLayout();
                void Verify(string name, bool magenta)
                {
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,
                        new Action(() => frame.Continue = false));
                    System.Windows.Threading.Dispatcher.PushFrame(frame); surface.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(160, 100, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(surface); var pixels = new byte[160 * 100 * 4]; bitmap.CopyPixels(pixels, 160 * 4, 0);
                    int Count(int left, int top, int right, int bottom)
                    {
                        var count = 0;
                        for (var y = top; y < bottom; y++) for (var x = left; x < right; x++)
                        {
                            var index = (y * 160 + x) * 4;
                            if (magenta ? pixels[index] > 180 && pixels[index + 2] > 180 && pixels[index + 1] < 40
                                : pixels[index + 1] > 180 && pixels[index] < 40 && pixels[index + 2] < 40) count++;
                        }
                        return count;
                    }
                    Assert.True(Count(15, 25, 55, 65) > 20, "Circle must be visible in the configured color.");
                    Assert.True(Count(85, 20, 136, 71) > 20, "Line must be visible in the configured color.");
                    if (Environment.GetEnvironmentVariable("S2_CASE_ROOT") is {} path)
                    {
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var file = System.IO.File.Create(System.IO.Path.Combine(path, name)); encoder.Save(file);
                    }
                }
                Verify("group-shared-style-green.png", false);
                style.OkColor = Colors.Magenta; Verify("group-shared-style-magenta.png", true);
                layer.SetDisplayCollector(null);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

}
