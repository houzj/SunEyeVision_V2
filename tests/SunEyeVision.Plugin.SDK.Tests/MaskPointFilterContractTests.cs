using System;
using System.Linq;
using System.Windows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 屏蔽点集过滤契约测试（ToolRegionHelper.FilterPointsOutOfMaskRois）。
/// 契约（VisionMaster 屏蔽语义）:
///   ① 落在任意启用屏蔽区内的点被剔除，不参与后续拟合
///   ② 无屏蔽区 / 全部禁用 → 全量保留（快路径）
///   ③ 位置修正矩阵作用到屏蔽几何后再判定（参考坐标 → 图像坐标）
///   ④ 非直接绘制（绑定/计算）模式 → 显式报错（rule-015）
/// </summary>
public class MaskPointFilterContractTests
{
    private static Roi MakeMask(object geometry, bool enabled = true)
    {
        var roi = Roi.CreateLocal("test-mask", geometry);
        roi.IsEnabled = enabled;
        return roi;
    }

    private static Point2d[] Points(params (double X, double Y)[] list)
        => list.Select(p => new Point2d(p.X, p.Y)).ToArray();

    private static double[] Xs(Point2d[] pts) => pts.Select(p => p.X).ToArray();
    private static double[] Ys(Point2d[] pts) => pts.Select(p => p.Y).ToArray();

    [Fact]
    public void NoMask_KeepsAll()
    {
        var pts = Points((0, 0), (100, 100), (50, 50));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(pts, null, null);

        Assert.Equal(3, kept.Length);
        Assert.Empty(excluded);
    }

    [Fact]
    public void CircleMask_RemovesInsideAndBoundaryPoints()
    {
        var pts = Points((100, 100), (0, 0), (110, 100));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Circle2d(100, 100, 10)) }, null);

        Assert.Equal(new double[] { 0 }, Xs(kept));
        Assert.Equal(new double[] { 0 }, Ys(kept));
        Assert.Equal(2, excluded.Length); // 圆心点 + 边界点（<= 半径均剔除）
    }

    [Fact]
    public void RectMask_RemovesInsidePoints()
    {
        var pts = Points((90, 90), (50, 50), (119, 119));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Rect(80, 80, 40, 40)) }, null);

        Assert.Single(kept);
        Assert.Equal(50, kept[0].X);
        Assert.Equal(2, excluded.Length);
    }

    [Fact]
    public void PolygonMask_RemovesInsidePoints()
    {
        var polygon = new Polygon2d(new[]
        {
            new Point(80, 80), new Point(120, 80), new Point(120, 120), new Point(80, 120),
        });
        var pts = Points((100, 100), (10, 10), (85, 115));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(polygon) }, null);

        Assert.Single(kept);
        Assert.Equal(10, kept[0].X);
        Assert.Equal(2, excluded.Length);
    }

    [Fact]
    public void RotatedRectMask_RemovesCenterPoint()
    {
        var pts = Points((100, 100), (0, 0));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new RotatedRect2d(100, 100, 40, 40, 45)) }, null);

        Assert.Single(kept);
        Assert.Equal(0, kept[0].X);
        Assert.Single(excluded);
    }

    [Fact]
    public void AnnulusMask_RemovesRingOnly()
    {
        // 中心(100,100) 内径10 外径20：距离 15 的点被剔，距离 5 与 30 的点保留
        var pts = Points((115, 100), (105, 100), (130, 100));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Annulus2d(new Point(100, 100), 10, 20)) }, null);

        Assert.Equal(2, kept.Length);
        Assert.Single(excluded);
        Assert.Equal(115, excluded[0].X);
    }

    [Fact]
    public void DisabledMask_IsIgnored()
    {
        var pts = Points((100, 100));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Circle2d(100, 100, 10), enabled: false) }, null);

        Assert.Single(kept);
        Assert.Empty(excluded);
    }

    [Fact]
    public void FullCoverage_RemovesAll()
    {
        var pts = Points((0, 0), (100, 100), (-500, -500));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Circle2d(0, 0, 1000)) }, null);

        Assert.Empty(kept);
        Assert.Equal(3, excluded.Length);
    }

    [Fact]
    public void TranslationMatrix_AppliesToMaskGeometry()
    {
        // 屏蔽圆参考坐标中心 (100,100) r=5；矩阵平移 (+200,0) → 图像坐标中心 (300,100)
        var matrix = new AffineMatrix(1, 0, 0, 1, 200, 0);
        var pts = Points((300, 100), (100, 100));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Circle2d(100, 100, 5)) }, matrix);

        Assert.Single(kept);
        Assert.Equal(100, kept[0].X); // 未平移前的图像坐标点不在屏蔽区
        Assert.Single(excluded);
        Assert.Equal(300, excluded[0].X);
    }

    [Fact]
    public void ScaleMatrix_ScalesMaskRadius()
    {
        // 屏蔽圆中心 (0,0) r=5，矩阵 2 倍缩放 → 图像半径 10：距离 8 的点被剔，距离 12 的点保留
        var matrix = new AffineMatrix(2, 0, 0, 2, 0, 0);
        var pts = Points((8, 0), (12, 0));

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(
            pts, new[] { MakeMask(new Circle2d(0, 0, 5)) }, matrix);

        Assert.Single(kept);
        Assert.Equal(12, kept[0].X);
        Assert.Single(excluded);
        Assert.Equal(8, excluded[0].X);
    }

    [Fact]
    public void MultipleMasks_AnyHitRemovesPoint()
    {
        var pts = Points((50, 50), (150, 50), (200, 200));
        var masks = new[]
        {
            MakeMask(new Circle2d(50, 50, 5)),
            MakeMask(new Circle2d(150, 50, 5)),
        };

        var (kept, excluded) = ToolRegionHelper.FilterPointsOutOfMaskRois(pts, masks, null);

        Assert.Single(kept);
        Assert.Equal(200, kept[0].X);
        Assert.Equal(2, excluded.Length);
    }

    [Fact]
    public void NonShapeParametersMask_ThrowsNotSupported()
    {
        var pts = Points((100, 100));
        var computed = Roi.CreateComposed("computed", ShapeType.Rectangle);

        Assert.Throws<NotSupportedException>(() =>
            ToolRegionHelper.FilterPointsOutOfMaskRois(pts, new[] { computed }, null));
    }

    [Fact]
    public void UnsupportedGeometry_ThrowsNotSupported()
    {
        // Line2d 无面积，不能作为屏蔽区 → 显式报错
        var line = Line2d.FromTwoPoints(new Point(0, 0), new Point(100, 100));
        var pts = Points((50, 50));

        Assert.Throws<NotSupportedException>(() =>
            ToolRegionHelper.FilterPointsOutOfMaskRois(pts, new[] { MakeMask(line) }, null));
    }
}
