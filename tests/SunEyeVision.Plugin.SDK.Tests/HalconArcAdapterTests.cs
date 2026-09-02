using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// HalconArcAdapter 契约测试（WPF 弧/圆 ↔ HALCON metrology 角度参数的唯一转换出口）。
/// 契约（依据 HALCON 24.05 Operator Reference — add_metrology_object_circle_measure）:
///   ① start_phi / end_phi 落在 HALCON 区间 [0, 2π]
///   ② (end - start) mod 2π 沿 point_order 方向 == |AngularSpan|（两端各自 ±2πk 不改变该模差）
///   ③ 整圆恒为 (0, 2π, "positive")
///   ④ 非法输入直接抛错（rule-015 禁止兜底回退）
/// </summary>
public class HalconArcAdapterTests
{
    private const double TwoPi = 2.0 * Math.PI;

    private static double Mod2Pi(double value)
    {
        double t = value % TwoPi;
        return t < 0 ? t + TwoPi : t;
    }

    /// <summary>弧转换的三条不变量</summary>
    private static void AssertHalconContract(Arc2d arc)
    {
        var (startPhi, endPhi, pointOrder) = HalconArcAdapter.FromArc(arc);

        Assert.InRange(startPhi, 0.0, TwoPi);
        Assert.InRange(endPhi, 0.0, TwoPi);

        double span = arc.AngularSpan;
        double diff = Mod2Pi(endPhi - startPhi);
        double expected = pointOrder == "positive" ? Math.Abs(span) : TwoPi - Math.Abs(span);
        Assert.True(Math.Abs(diff - expected) < 1e-9,
            $"模差应沿 {pointOrder} 方向等于 |span|={Math.Abs(span):F6}，实际 {diff:F6}");
    }

    [Fact]
    public void FromFullCircle_ReturnsZeroToTwoPiPositive()
    {
        var (startPhi, endPhi, pointOrder) = HalconArcAdapter.FromFullCircle();

        Assert.Equal(0.0, startPhi);
        Assert.Equal(TwoPi, endPhi, 9);
        Assert.Equal("positive", pointOrder);
    }

    [Fact]
    public void FromArc_CounterClockwiseMinor_SatisfiesContract()
    {
        var arc = Arc2d.FromTwoPointsDefault(new Point(0, 0), new Point(200, 0), clockwise: false);
        Assert.True(arc.AngularSpan > 0);
        AssertHalconContract(arc);
    }

    [Fact]
    public void FromArc_ClockwiseMinor_SatisfiesContract()
    {
        var arc = Arc2d.FromTwoPointsDefault(new Point(0, 0), new Point(200, 0), clockwise: true);
        Assert.True(arc.AngularSpan < 0);
        AssertHalconContract(arc);
    }

    [Fact]
    public void FromArc_ClockwiseMajorAcross2Pi_SatisfiesContract()
    {
        // 顺时针大弧：跨 2π（EndAngle 与 Start+span 差整周期）
        var arc = new Arc2d(new Point(0, 0), 200, 0.3, 1.0, isMajorArc: true);
        Assert.True(arc.AngularSpan < 0 && Math.Abs(arc.AngularSpan) > Math.PI);
        AssertHalconContract(arc);
    }

    [Fact]
    public void FromArc_CounterClockwiseMajorAcross2Pi_SatisfiesContract()
    {
        // 逆时针大弧：跨 2π
        var arc = new Arc2d(new Point(0, 0), 200, 1.0, 0.3, isMajorArc: true);
        Assert.True(arc.AngularSpan > 0 && arc.AngularSpan > Math.PI);
        AssertHalconContract(arc);
    }

    [Fact]
    public void FromArc_CrossZeroBoundary_KeepsArcLength()
    {
        // 起点在 0° 附近、顺时针跨越 0° 边界：两端各自 ±2π 后仍保持 |span|
        var arc = new Arc2d(new Point(0, 0), 200, 0.2, -0.8, isMajorArc: false);
        Assert.True(arc.AngularSpan < 0);
        AssertHalconContract(arc);
    }

    [Fact]
    public void FromArc_EmptyArc_Throws()
    {
        Assert.Throws<ArgumentException>(() => HalconArcAdapter.FromArc(Arc2d.Empty));
    }

    [Fact]
    public void FromArc_NonPositiveRadius_Throws()
    {
        var arc = new Arc2d(new Point(0, 0), 0, 0, 1.0, false);
        Assert.Throws<ArgumentException>(() => HalconArcAdapter.FromArc(arc));
    }

    [Fact]
    public void ToWpfAngle_IsNegationOfPhi()
    {
        Assert.Equal(-0.75, HalconArcAdapter.ToWpfAngle(0.75), 9);
    }
}
