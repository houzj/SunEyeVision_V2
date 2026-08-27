using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 圆弧两点+半径参数化契约测试（对齐 VisionMaster 模型）。
/// 对应方案防御机制 D1（几何正确性）与 D2（Fail-Forward）。
/// </summary>
public class Arc2dContractTests
{
    [Fact]
    public void FromTwoPointsDefault_RadiusEqualsChord()
    {
        // 默认半径 = 弦长（VM 实测验证：两组数据均吻合）
        var arc = Arc2d.FromTwoPointsDefault(new Point(0, 0), new Point(10, 0));

        Assert.False(arc.IsEmpty);
        Assert.Equal(10, arc.Radius, 3);
        Assert.Equal(Math.PI / 3, Math.Abs(arc.AngularSpan), 3); // 60° 张角
    }

    [Fact]
    public void FromTwoPointsDefault_PassesThroughEndpoints()
    {
        var p1 = new Point(0, 0);
        var p2 = new Point(10, 0);
        var arc = Arc2d.FromTwoPointsDefault(p1, p2);

        // 起点/终点应落在弧上（用弧端点反查验证）
        var startPt = arc.GetPointOnArc(arc.StartAngle);
        var endPt = arc.GetPointOnArc(arc.EndAngle);
        Assert.True((startPt - p1).Length < 1e-3);
        Assert.True((endPt - p2).Length < 1e-3);
    }

    [Fact]
    public void FromEndpointsAndRadius_CenterOnPerpendicularBisector()
    {
        // 弦 (0,0)-(10,0)，弦中点 (5,0)，圆心距弦中点 = sqrt(100-25) = 8.66
        var arc = Arc2d.FromEndpointsAndRadius(new Point(0, 0), new Point(10, 0), 10, false);

        Assert.False(arc.IsEmpty);
        Assert.Equal(10, arc.Radius, 3);
        // 圆心在弦中垂线上：x=弦中点 x=5，沿 y 偏移 8.66（逆时针侧 y 为负）
        Assert.Equal(5, arc.Center.X, 3);
        Assert.True(Math.Abs(Math.Abs(arc.Center.Y) - 8.66) < 0.01);
    }

    [Fact]
    public void ClockwiseAndCounterClockwise_AreMirrored()
    {
        var p1 = new Point(0, 0);
        var p2 = new Point(10, 0);
        var ccw = Arc2d.FromEndpointsAndRadius(p1, p2, 10, false);
        var cw = Arc2d.FromEndpointsAndRadius(p1, p2, 10, true);

        Assert.False(ccw.IsEmpty);
        Assert.False(cw.IsEmpty);
        // 圆心镜像：x 相同，y 互为相反数
        Assert.Equal(ccw.Center.X, cw.Center.X, 3);
        Assert.Equal(-ccw.Center.Y, cw.Center.Y, 3);
        Assert.True(ccw.AngularSpan > 0);  // 逆时针正
        Assert.True(cw.AngularSpan < 0);   // 顺时针负
    }

    [Fact]
    public void FromEndpointsAndRadius_RadiusTooSmall_ReturnsEmpty()
    {
        // 半径 3 < 弦长/2=5 → 无解
        var arc = Arc2d.FromEndpointsAndRadius(new Point(0, 0), new Point(10, 0), 3, false);

        Assert.True(arc.IsEmpty);
    }

    [Fact]
    public void FromTwoPointsDefault_Coincident_ReturnsEmpty()
    {
        var arc = Arc2d.FromTwoPointsDefault(new Point(5, 5), new Point(5, 5));

        Assert.True(arc.IsEmpty);
    }
}
