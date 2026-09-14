using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 「抠几何」契约测试（方案 P1 改动 1）：ROI 容器（<c>RoiShape[]</c>）按目标几何类型提取真几何数组。
/// </summary>
/// <remarks>
/// 守住的退化：把 <c>RoiShape[]</c> 原样交给几何值参数（下游还得自己取 <c>.Geometry</c>），
/// 或提取时"类型不符就静默跳过"（rule-015 要求报错优先）。
/// </remarks>
public class GeometryExtractionContractTests
{
    [Fact]
    public void TargetCircleArray_ExtractsOnlyCircleGeometries()
    {
        // 目标几何值（Circle2d[]）→ 抠出真几何，下游不必再取 .Geometry
        object? value = new RoiShape[]
        {
            new() { Geometry = new Circle2d(10, 20, 5) },
            new() { Geometry = new Circle2d(30, 40, 7) },
        };

        var handled = GeometryExtractor.TryExtract(value, typeof(Circle2d[]), out var extracted, out var error);

        Assert.True(handled);
        Assert.Null(error);
        var circles = Assert.IsType<Circle2d[]>(extracted);
        Assert.Equal(2, circles.Length);
        Assert.Equal(10, circles[0].Center.X, 6);
        Assert.Equal(7, circles[1].Radius, 6);
    }

    [Fact]
    public void TargetPointArray_ExtractsPointGeometries()
    {
        object? value = new RoiShape[] { new() { Geometry = new Point2d(3, 4) } };

        var handled = GeometryExtractor.TryExtract(value, typeof(Point2d[]), out var extracted, out var error);

        Assert.True(handled);
        Assert.Null(error);
        var points = Assert.IsType<Point2d[]>(extracted);
        Assert.Equal(3, points[0].X, 6);
        Assert.Equal(4, points[0].Y, 6);
    }

    [Fact]
    public void TargetRoiArray_NotHandled_KeepsContainerForRegionSubscription()
    {
        // 区域订阅：目标就是 RoiShape[]（词目 ROI）→ **不接管**，调用方保持容器原值
        object? value = new RoiShape[]
        {
            new() { Geometry = new Circle2d(10, 20, 5) },
            new() { Geometry = new RotatedRect2d(1, 2, 3, 4, 0) },
        };

        var handled = GeometryExtractor.TryExtract(value, typeof(RoiShape[]), out var extracted, out var error);

        Assert.False(handled);
        Assert.Null(extracted);
        Assert.Null(error);
    }

    [Fact]
    public void TargetNumericOrImage_NotHandled()
    {
        object? value = new RoiShape[] { new() { Geometry = new Circle2d(1, 2, 3) } };

        Assert.False(GeometryExtractor.TryExtract(value, typeof(double), out _, out _));
        Assert.False(GeometryExtractor.TryExtract(value, typeof(double[]), out _, out _));
    }

    [Fact]
    public void AlreadyGeometryArray_OrScalar_NotHandled()
    {
        // 值本就不是 RoiShape 集合（已是几何数组 / 标量 / null）→ 不接管
        Assert.False(GeometryExtractor.TryExtract(new Circle2d[] { new(1, 2, 3) }, typeof(Circle2d[]), out _, out _));
        Assert.False(GeometryExtractor.TryExtract(42.0, typeof(double), out _, out _));
        Assert.False(GeometryExtractor.TryExtract(null, typeof(Circle2d[]), out _, out _));
    }

    [Fact]
    public void MismatchedGeometry_ReportsError_NoSilentSkip()
    {
        // rule-015 报错优先：容器内含非目标几何时必须报错，不得静默跳过该元素
        object? value = new RoiShape[]
        {
            new() { Geometry = new Circle2d(10, 20, 5) },
            new() { Geometry = new RotatedRect2d(1, 2, 3, 4, 0) },
        };

        var handled = GeometryExtractor.TryExtract(value, typeof(Circle2d[]), out var extracted, out var error);

        Assert.True(handled);
        Assert.Null(extracted);
        Assert.NotNull(error);
        Assert.Contains("不符", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void NullGeometryElement_ReportsError()
    {
        object? value = new RoiShape[] { new() { Geometry = null } };

        var handled = GeometryExtractor.TryExtract(value, typeof(Circle2d[]), out _, out var error);

        Assert.True(handled);
        Assert.NotNull(error);
        Assert.Contains("没有几何值", error!, StringComparison.Ordinal);
    }
}
