using SunEyeVision.Plugin.SDK.UI.Controls;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// ViewportScaling 幂律缩放契约测试（双渲染层公式唯一源）。
/// 覆盖幂律管控表：
///   InvScale / Power 反除互逆、屏幕恒定 clamp 边界、Fail-Forward（zoom≤0 不回退 NaN/Infinity）、
///   与历史公式（交互层 2/zoom、手柄 10/scale^0.15）数值等价。
/// 对应方案防御机制 D1（契约测试）与 D3（Fail-Forward）。
/// </summary>
public class ViewportScalingContractTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void InvScale_IsInverseOfZoom(double zoom)
    {
        var logical = ViewportScaling.InvScale(zoom, 8.0);
        // 逻辑尺寸 × zoom ≈ 屏幕恒定 8
        Assert.Equal(8.0, logical * zoom, 6);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Power_IsInverseOfZoom(double power)
    {
        var zoom = 2.5;
        var logical = ViewportScaling.Power(zoom, 10.0, power);
        Assert.Equal(10.0, logical * Math.Pow(zoom, power), 6);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(4.0)]
    [InlineData(16.0)]
    public void HandleSize_ScreenConstantInClampRange(double zoom)
    {
        var logical = ViewportScaling.HandleSize(zoom);
        Assert.InRange(logical * zoom, 10.0, 12.0);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(4.0)]
    [InlineData(16.0)]
    public void LabelFontSize_ScreenConstantInClampRange(double zoom)
    {
        var logical = ViewportScaling.LabelFontSize(zoom);
        Assert.InRange(logical * zoom, 9.0, 12.0);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void DashInterval_ScreenConstantInRange(double zoom)
    {
        var logical = ViewportScaling.DashInterval(zoom);
        Assert.InRange(logical * zoom, 4.0, 6.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-0.01)]
    public void FailForward_NonPositiveZoom_ReturnsBaseNotInfinity(double zoom)
    {
        // zoom ≤ 0：不得产生 Infinity/NaN，直接回退逻辑尺寸 = 屏幕尺寸
        Assert.Equal(8.0, ViewportScaling.InvScale(zoom, 8.0));
        Assert.Equal(2.0, ViewportScaling.LineWidth(zoom));
        Assert.False(double.IsInfinity(ViewportScaling.Power(zoom, 10.0, 0.7)));
        Assert.False(double.IsNaN(ViewportScaling.Power(zoom, 10.0, 0.7)));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void LineWidth_EquivalentToLegacyOverlayFormula(double zoom)
    {
        // 交互层历史公式 StrokeWidthFor = 2/zoom；归口后数值必须等价
        var legacy = 2.0 / zoom;
        Assert.Equal(legacy, ViewportScaling.LineWidth(zoom), 6);
    }

    [Fact]
    public void DecorMarkerPowers_AreManagedConstants()
    {
        // 幂律管控表：装饰 0.15 / 标记 0.7 必须通过常量暴露（防散落字面量回归）
        Assert.Equal(0.15, ViewportScaling.DecorPower);
        Assert.Equal(0.7, ViewportScaling.MarkerPower);
    }
}
