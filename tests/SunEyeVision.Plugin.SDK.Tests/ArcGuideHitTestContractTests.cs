using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 弧引导线命中契约测试（AC-04）。
/// 背景：旧实现把 offset 折叠到 (-π, π]，而 AngularSpan 可为大弧（|span| > π），
///       导致大弧时下界判定恒成立 —— 半圆以上任意位置都被误判为命中。
/// 契约：弧上点命中；该弧的"补弧"中点不命中；整圆任意点命中。
/// </summary>
public class ArcGuideHitTestContractTests
{
    private const double Tolerance = 5.0;

    private static bool Hit(Arc2d arc, Point point)
    {
        var model = new MeasureInteractionModel();
        model.SetArc(arc);
        return new ArcGuideInteraction().HitTestGuide(model, point, Tolerance);
    }

    private static Point MidPointOfArc(Arc2d arc) =>
        arc.GetPointOnArc(arc.StartAngle + arc.AngularSpan / 2);

    private static Point MidPointOfComplement(Arc2d arc) =>
        arc.GetPointOnArc(arc.StartAngle + arc.AngularSpan / 2 + Math.PI);

    [Fact]
    public void HitTestGuide_MajorArc_MidPointHits()
    {
        // 顺时针大弧（跨 2π，|span| ≈ 5.58 > π）
        var arc = new Arc2d(new Point(0, 0), 200, 0.3, 1.0, isMajorArc: true);
        Assert.True(Math.Abs(arc.AngularSpan) > Math.PI);

        Assert.True(Hit(arc, MidPointOfArc(arc)), "大弧中点应命中");
        Assert.False(Hit(arc, MidPointOfComplement(arc)), "大弧的补弧中点不应命中（旧实现会误命中）");
    }

    [Fact]
    public void HitTestGuide_CounterClockwiseMajorArc_MidPointHits()
    {
        // 逆时针大弧（span 正、> π）
        var arc = new Arc2d(new Point(0, 0), 200, 1.0, 0.3, isMajorArc: true);
        Assert.True(arc.AngularSpan > Math.PI);

        Assert.True(Hit(arc, MidPointOfArc(arc)), "大弧中点应命中");
        Assert.False(Hit(arc, MidPointOfComplement(arc)), "大弧的补弧中点不应命中");
    }

    [Fact]
    public void HitTestGuide_MinorArc_ComplementMisses()
    {
        // 小弧：弧上命中、补弧不命中（回归保护）
        var arc = Arc2d.FromTwoPointsDefault(new Point(0, 0), new Point(200, 0), clockwise: false);
        Assert.True(Math.Abs(arc.AngularSpan) <= Math.PI);

        Assert.True(Hit(arc, MidPointOfArc(arc)), "小弧中点应命中");
        Assert.False(Hit(arc, MidPointOfComplement(arc)), "小弧的补弧中点不应命中");
    }
}
