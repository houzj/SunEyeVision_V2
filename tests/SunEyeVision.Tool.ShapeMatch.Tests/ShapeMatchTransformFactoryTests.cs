using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.ShapeMatch;
using Xunit;

namespace SunEyeVision.Tool.ShapeMatch.Tests;

/// <summary>
/// 矩阵构造工厂方向契约测试（方案 AC-2 / 防御机制：方向契约）。
/// 锁定"匹配姿态 → AffineMatrix"的唯一构造出口数学，防止回归到手拼 / 符号错位。
/// </summary>
public class ShapeMatchTransformFactoryTests
{
    private const double Deg2Rad = Math.PI / 180.0;
    private const double Epsilon = 1e-9;

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(30.0, 1.0)]
    [InlineData(-45.0, 1.1)]
    [InlineData(90.0, 0.8)]
    public void FromMatchPose_LinearPart_Matches_ScaleTimesRotation(double angleDeg, double scale)
    {
        // 姿态 → 矩阵线性部分必须等于 s·R(θ)（对齐 Halcon vector_angle_to_rigid）：
        //   M11=s·cosθ, M12=−s·sinθ, M21=+s·sinθ, M22=s·cosθ
        // 历史首版断言 M12=+sin / M21=−sin 与 Halcon 不兼容，把 bug 固化为期望值
        // ——本次根据 Halcon 官方公式（affine_trans_point + vector_angle_to_rigid）纠偏。
        double rad = angleDeg * Deg2Rad;
        var m = ShapeMatchTransformFactory.FromMatchPose(
            row: 200, col: 300, angleRad: rad, scale: scale);

        Assert.Equal(scale * Math.Cos(rad), m.M11, 9);
        Assert.Equal(-scale * Math.Sin(rad), m.M12, 9);
        Assert.Equal(+scale * Math.Sin(rad), m.M21, 9);
        Assert.Equal(scale * Math.Cos(rad), m.M22, 9);
    }

    [Fact]
    public void FromMatchPose_Translation_Is_MatchCenter()
    {
        var m = ShapeMatchTransformFactory.FromMatchPose(
            row: 123.5, col: 456.25, angleRad: 0.3, scale: 1.0);

        Assert.Equal(456.25, m.OffsetX, 9);
        Assert.Equal(123.5, m.OffsetY, 9);
    }

    [Fact]
    public void FromMatchPose_Identity_WhenZeroAngleUnitScale()
    {
        var m = ShapeMatchTransformFactory.FromMatchPose(
            row: 100, col: 200, angleRad: 0.0, scale: 1.0);

        Assert.Equal(1.0, m.M11, 9);
        Assert.Equal(0.0, m.M12, 9);
        Assert.Equal(0.0, m.M21, 9);
        Assert.Equal(1.0, m.M22, 9);
        Assert.Equal(200.0, m.OffsetX, 9);
        Assert.Equal(100.0, m.OffsetY, 9);
    }

    [Fact]
    public void ToPositionCorrection_Subtracts_ReferencePoint()
    {
        var match = ShapeMatchTransformFactory.FromMatchPose(
            row: 320, col: 150, angleRad: 30.0 * Deg2Rad, scale: 1.1);

        var corr = ShapeMatchTransformFactory.ToPositionCorrection(match, refX: 100, refY: 200);

        // 线性部分不变，平移 = 匹配点 − 参考点
        Assert.Equal(match.M11, corr.M11, 9);
        Assert.Equal(match.M22, corr.M22, 9);
        Assert.Equal(150 - 100, corr.OffsetX, 9);
        Assert.Equal(320 - 200, corr.OffsetY, 9);
    }

    [Fact]
    public void AffineMatrix_RoundTrip_ToHalconArray_IsConsistent()
    {
        // AffineMatrix ↔ ToHalconArray 往返（halcon 边界适配，方向契约不破坏）
        var m = ShapeMatchTransformFactory.FromMatchPose(
            row: 50, col: 60, angleRad: 15.0 * Deg2Rad, scale: 1.2);

        var h = m.ToHalconArray();
        // Halcon: r'=H0·r+H1·c+H2 ; c'=H3·r+H4·c+H5
        // 对照 WPF 行向量 → H0=M22, H1=M12, H2=OffsetY, H3=M21, H4=M11, H5=OffsetX
        Assert.Equal(m.M22, h[0], 9);
        Assert.Equal(m.M12, h[1], 9);
        Assert.Equal(m.OffsetY, h[2], 9);
        Assert.Equal(m.M21, h[3], 9);
        Assert.Equal(m.M11, h[4], 9);
        Assert.Equal(m.OffsetX, h[5], 9);
    }
}
