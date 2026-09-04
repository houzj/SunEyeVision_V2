using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 测量带（卡尺）Halcon 对齐契约测试。
/// 规则（Halcon 惯例）：
/// - 直线/弧：首末带"外边缘"贴端点，中心向内侧偏移半个测量带长度
///   （首带中心 = 起点 + dir×halfBand，末带中心 = 终点 − dir×halfBand）。
/// - 圆（闭环）：N 条带把整圆等分 N 段，带中心落在覆盖段中点 = (i+0.5)×(2π/N)（相位半格）。
/// - 弧：末条基于连续扫掠终点 Start+AngularSpan（跨 2π 正确），顺/逆时针由 direction=sign(span) 处理。
/// 覆盖顺/逆 × 小/大弧四组合。
/// </summary>
public class MeasureBandAlignmentContractTests
{
    private static Point BandCenter(Point[] quad) =>
        new((quad[0].X + quad[1].X + quad[2].X + quad[3].X) / 4,
            (quad[0].Y + quad[1].Y + quad[2].Y + quad[3].Y) / 4);

    /// <summary>
    /// 弧四组合通用断言：首/末带中心 = 端点沿弧方向内偏移半带处
    /// </summary>
    private static void AssertArcHalconAlignment(Arc2d arc, double bandWidth, double bandLength, int measureCount)
    {
        var quads = MeasureBandOverlay.BuildArcBands(arc, bandWidth, bandLength, measureCount).ToList();
        Assert.True(quads.Count == measureCount, $"应生成 {measureCount} 条带，实际 {quads.Count}");

        double span = arc.AngularSpan;
        double halfBandAngle = (bandLength / 2) / arc.Radius;
        double direction = span >= 0 ? 1.0 : -1.0;
        double sweepEnd = arc.StartAngle + span;

        var expectedFirst = arc.GetPointOnArc(arc.StartAngle + direction * halfBandAngle);
        var expectedLast = arc.GetPointOnArc(sweepEnd - direction * halfBandAngle);

        var first = BandCenter(quads[0]);
        var last = BandCenter(quads[quads.Count - 1]);

        Assert.True((first - expectedFirst).Length < 1e-6,
            $"首带中心应=弧起点内侧半带处，偏差 {(first - expectedFirst).Length}");
        Assert.True((last - expectedLast).Length < 1e-6,
            $"末带中心应=弧终点内侧半带处，偏差 {(last - expectedLast).Length}");

        // 步长：HALCON 实测（get_metrology_object_measures 回读）相邻带中心角差
        //   = (|span| - 2×halfBandAngle)/(N-1) —— 即"首末带边贴端点"的分布，
        //   与整圆的 2π/N 等分不同（整圆闭合，无端点约束）。
        if (measureCount > 1)
        {
            double expectedStep = (Math.Abs(span) - 2 * halfBandAngle) / (measureCount - 1);
            for (int i = 1; i < quads.Count; i++)
            {
                double actualStep = AngleBetween(
                    BandCenter(quads[i - 1]), BandCenter(quads[i]), arc.Center);
                Assert.True(Math.Abs(actualStep - expectedStep) < 1e-6,
                    $"第 {i} 条与第 {i - 1} 条的角差应为 {expectedStep:F6}，实际 {actualStep:F6}");
            }
        }
    }

    /// <summary>两点相对同一圆心的夹角（取 [0, π] 的最小夹角）</summary>
    private static double AngleBetween(Point a, Point b, Point center)
    {
        double a1 = Math.Atan2(a.Y - center.Y, a.X - center.X);
        double a2 = Math.Atan2(b.Y - center.Y, b.X - center.X);
        double d = ((a2 - a1) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
        return d > Math.PI ? 2 * Math.PI - d : d;
    }

    [Fact]
    public void BuildLineBands_FirstEdgeOnStart_LastEdgeOnEnd()
    {
        var start = new Point(10, 20);
        var end = new Point(210, 120);
        double bandLength = 10;
        var halfBand = bandLength / 2;

        var quads = MeasureBandOverlay.BuildLineBands(start, end, 40, bandLength, 6).ToList();
        var dir = (end - start) / (end - start).Length;

        var first = BandCenter(quads[0]);
        var last = BandCenter(quads[quads.Count - 1]);

        // 首带中心 = 起点沿方向偏移半带（带起点边精确落在起点）
        var expectedFirst = start + dir * halfBand;
        var expectedLast = end - dir * halfBand;

        Assert.True((first - expectedFirst).Length < 1e-6, $"首带中心偏差 {(first - expectedFirst).Length}");
        Assert.True((last - expectedLast).Length < 1e-6, $"末带中心偏差 {(last - expectedLast).Length}");
    }

    [Fact]
    public void BuildCircleBands_FullCircle_EqualDivision_GridPhase()
    {
        // 整圆闭合：N 条带等分整圈，第 i 条中心 = start + i×(2π/N)（格点，首条在 start=0 处）。
        // 实测依据（HALCON get_metrology_object_measures）：相邻带角差恒为 2π/N，首条落在 start_phi。
        var circle = new Circle2d(new Point(0, 0), 100);
        int n = 8;
        var quads = MeasureBandOverlay.BuildCircleBands(circle, 40, 10, n).ToList();

        var stepAngle = Math.PI * 2 / n;
        for (int i = 0; i < n; i++)
        {
            var expected = new Point(100 * Math.Cos(i * stepAngle), 100 * Math.Sin(i * stepAngle));
            var actual = BandCenter(quads[i]);
            Assert.True((actual - expected).Length < 1e-6,
                $"第 {i} 条中心应在格点 {i}×2π/{n}，偏差 {(actual - expected).Length}");
        }
    }

    [Fact]
    public void BuildCircleBands_StartAngle_RotatesGridPhase()
    {
        // 整圆卡尺阵列起始相位可平移（位置修正旋转时首条落在矩阵旋转角处，均分间距不变）
        var circle = new Circle2d(new Point(0, 0), 100);
        int n = 8;
        double startAngle = Math.PI / 6;   // 30°
        var quads = MeasureBandOverlay.BuildCircleBands(circle, 40, 10, n, startAngle).ToList();

        var stepAngle = Math.PI * 2 / n;
        for (int i = 0; i < n; i++)
        {
            var expected = new Point(100 * Math.Cos(startAngle + i * stepAngle), 100 * Math.Sin(startAngle + i * stepAngle));
            var actual = BandCenter(quads[i]);
            Assert.True((actual - expected).Length < 1e-6,
                $"第 {i} 条中心应在格点 startAngle + {i}×2π/{n}，偏差 {(actual - expected).Length}");
        }
    }

    [Fact]
    public void BuildArcBands_CcwMinor_HalconEdgeAligned()
    {
        // 逆时针（span>0）小弧
        var arc = Arc2d.FromTwoPointsDefault(new Point(0, 0), new Point(200, 0), clockwise: false);
        Assert.True(arc.AngularSpan > 0);
        AssertArcHalconAlignment(arc, 40, 10, 6);
    }

    [Fact]
    public void BuildArcBands_CwMinor_HalconEdgeAligned()
    {
        // 顺时针（span<0）小弧
        var arc = Arc2d.FromTwoPointsDefault(new Point(0, 0), new Point(200, 0), clockwise: true);
        Assert.True(arc.AngularSpan < 0);
        AssertArcHalconAlignment(arc, 40, 10, 6);
    }

    [Fact]
    public void BuildArcBands_CwMajorAcross2Pi_HalconEdgeAligned()
    {
        // 顺时针大弧：跨 2π（EndAngle 与 Start+span 差整周期——回归"卡尺整排反向"缺陷）
        var arc = new Arc2d(new Point(0, 0), 200, 0.3, 1.0, isMajorArc: true);   // delta=0.7
        var span = arc.AngularSpan;
        Assert.True(span < 0 && Math.Abs(span) > Math.PI, $"应为跨 2π 顺时针大弧，实际 span={span}");
        AssertArcHalconAlignment(arc, 40, 10, 6);
    }

    [Fact]
    public void BuildArcBands_CcwMajorAcross2Pi_HalconEdgeAligned()
    {
        // 逆时针大弧：跨 2π（span 正、|span|>π）
        var arc = new Arc2d(new Point(0, 0), 200, 1.0, 0.3, isMajorArc: true);   // delta=-0.7
        var span = arc.AngularSpan;
        Assert.True(span > 0 && span > Math.PI, $"应为跨 2π 逆时针大弧，实际 span={span}");
        AssertArcHalconAlignment(arc, 40, 10, 6);
    }
}
