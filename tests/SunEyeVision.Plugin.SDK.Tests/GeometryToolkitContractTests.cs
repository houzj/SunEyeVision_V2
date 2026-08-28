using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 通用几何工具集契约测试。
/// 对应方案防御机制 D1（几何正确性）与 D3（Fail-Forward）。
/// </summary>
public class GeometryToolkitContractTests
{
    #region DistanceToSegment

    [Fact]
    public void DistanceToSegment_Perpendicular_ReturnsExactDistance()
    {
        // 点 (0,3) 到线段 (0,0)-(0,5) 的距离 = 0（点在线上），改测点到中垂线
        var d = GeometryToolkit.DistanceToSegment(new Point(3, 0), new Point(0, -5), new Point(0, 5));
        Assert.Equal(3, d, 3);
    }

    [Fact]
    public void DistanceToSegment_PointOnLine_ReturnsZero()
    {
        var d = GeometryToolkit.DistanceToSegment(new Point(2, 3), new Point(0, 3), new Point(10, 3));
        Assert.Equal(0, d, 3);
    }

    [Fact]
    public void DistanceToSegment_BeyondEnd_ReturnsEndpointDistance()
    {
        // 点 (0,0) 到线段 (5,5)-(10,10)：垂足在线段外，应取到端点距离
        var d = GeometryToolkit.DistanceToSegment(new Point(0, 0), new Point(5, 5), new Point(10, 10));
        Assert.Equal(Math.Sqrt(50), d, 3);
    }

    [Fact]
    public void DistanceToSegment_DegenerateSegment_ReturnsPointDistance()
    {
        var d = GeometryToolkit.DistanceToSegment(new Point(3, 4), new Point(0, 0), new Point(0, 0));
        Assert.Equal(5, d, 3);
    }

    #endregion

    #region AngleInRange

    [Fact]
    public void AngleInRange_Inside_ReturnsTrue()
    {
        // 弧段从 0° 到 90°，45° 在内
        Assert.True(GeometryToolkit.AngleInRange(Math.PI / 4, 0, Math.PI / 2, 0.01));
    }

    [Fact]
    public void AngleInRange_Outside_ReturnsFalse()
    {
        // 弧段从 0° 到 90°，180° 在外
        Assert.False(GeometryToolkit.AngleInRange(Math.PI, 0, Math.PI / 2, 0.01));
    }

    [Fact]
    public void AngleInRange_CrossZeroWraparound_ReturnsTrue()
    {
        // 弧段从 300° 到 60°（跨 0），350° 在内
        double start = 300.0 * Math.PI / 180;
        double end = 60.0 * Math.PI / 180;
        Assert.True(GeometryToolkit.AngleInRange(350.0 * Math.PI / 180, start, end, 0.01));
        // 30° 也在内（跨 0 的另一端）
        Assert.True(GeometryToolkit.AngleInRange(30.0 * Math.PI / 180, start, end, 0.01));
        // 180° 在弧段外（跨 0 弧的对面）
        Assert.False(GeometryToolkit.AngleInRange(180.0 * Math.PI / 180, start, end, 0.01));
    }

    #endregion

    #region PointInQuad

    [Fact]
    public void PointInQuad_Inside_ReturnsTrue()
    {
        // 正方形 (0,0)-(10,0)-(10,10)-(0,10)，中心 (5,5) 在内
        Assert.True(GeometryToolkit.PointInQuad(
            new Point(5, 5),
            new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10)));
    }

    [Fact]
    public void PointInQuad_Outside_ReturnsFalse()
    {
        Assert.False(GeometryToolkit.PointInQuad(
            new Point(15, 15),
            new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10)));
    }

    [Fact]
    public void PointInQuad_OnBoundary_ReturnsTrue()
    {
        // 边界上视为包含
        Assert.True(GeometryToolkit.PointInQuad(
            new Point(10, 5),
            new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10)));
    }

    #endregion
}
