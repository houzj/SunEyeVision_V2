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
}
