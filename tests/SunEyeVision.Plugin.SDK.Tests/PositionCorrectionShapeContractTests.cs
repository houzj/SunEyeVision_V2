using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 位置修正矩阵形状语义契约测试（无向形状：中心完整修正 + 朝向不旋转；有向形状：完整跟随）。
/// 契约：
///   ① 无向形状（轴对齐矩形/圆/圆环）矩阵无旋转分量（M12=M21=0）；
///   ② 无向形状中心被完整矩阵修正：M'(中心) == M(中心)（含旋转分量造成的位移，定位准确）；
///   ③ 有向形状（旋转矩形/多边形）→ 完整矩阵（跟随旋转）；
///   ④ StripRotation 输出无旋转分量且尺度=ScaleFactor；
///   ⑤ IsConformal 区分相似变换与非均匀缩放/斜切。
/// </summary>
public class PositionCorrectionShapeContractTests
{
    private static AffineMatrix Rotated(double angleDeg, double scale = 1.0)
        => AffineMatrix.FromCenterAngleScale(0, 0, angleDeg * Math.PI / 180.0, scale);

    [Fact]
    public void StripRotation_RemovesRotation_KeepsUniformScaleAndTranslation()
    {
        var m = AffineMatrix.FromCenterAngleScale(37, 41, 30 * Math.PI / 180.0, 2.0);

        var stripped = m.StripRotation();

        Assert.Equal(2.0, stripped.M11, 6);
        Assert.Equal(0.0, stripped.M12, 6);
        Assert.Equal(0.0, stripped.M21, 6);
        Assert.Equal(2.0, stripped.M22, 6);
        Assert.Equal(37, stripped.OffsetX, 6);
        Assert.Equal(41, stripped.OffsetY, 6);
    }

    [Fact]
    public void StripRotation_PureTranslation_IsIdentity()
    {
        var m = new AffineMatrix(1, 0, 0, 1, 5, 7);

        var stripped = m.StripRotation();

        Assert.Equal(1.0, stripped.M11, 6);
        Assert.Equal(0.0, stripped.M12, 6);
        Assert.Equal(5.0, stripped.OffsetX, 6);
        Assert.Equal(7.0, stripped.OffsetY, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(-123)]
    public void IsConformal_RotationMatrix_IsTrue(double angleDeg)
    {
        Assert.True(Rotated(angleDeg).IsConformal());
    }

    [Fact]
    public void IsConformal_NonUniformScale_IsFalse()
    {
        var m = new AffineMatrix(2, 0, 0, 1, 0, 0); // x2 / y1
        Assert.False(m.IsConformal());
    }

    [Fact]
    public void IsConformal_Shear_IsFalse()
    {
        var m = new AffineMatrix(1, 0.5, 0, 1, 0, 0); // 斜切
        Assert.False(m.IsConformal());
    }

    [Fact]
    public void ResolveForShape_NullMatrix_ReturnsNull()
    {
        Assert.Null(PositionCorrectionResolver.ResolveForShape(new Circle2d(0, 0, 10), null));
        Assert.Null(PositionCorrectionResolver.ResolveForShape(new RotatedRect2d(0, 0, 10, 10, 45), null));
    }

    [Fact]
    public void ResolveForShape_Circle_CenterFullyCorrected_NoRotation()
    {
        var full = Rotated(45, 2);
        var circle = new Circle2d(100, 60, 10);
        var resolved = PositionCorrectionResolver.ResolveForShape(circle, full);

        Assert.NotNull(resolved);
        Assert.Equal(0.0, resolved!.M12, 6);
        Assert.Equal(0.0, resolved.M21, 6);
        Assert.Equal(2.0, resolved.M11, 6);

        // 中心必须完整修正（等于完整矩阵作用结果，含旋转造成的位移）
        var fullCenter = full.TransformWpfPoint(new Point(100, 60));
        var resolvedCenter = resolved.TransformWpfPoint(new Point(100, 60));
        Assert.Equal(fullCenter.X, resolvedCenter.X, 6);
        Assert.Equal(fullCenter.Y, resolvedCenter.Y, 6);
    }

    [Fact]
    public void ResolveForShape_AxisRect_CenterFullyCorrected_NoRotation()
    {
        var full = Rotated(90, 1.5);
        var rect = new Rect(100, 100, 50, 40);
        var centerRef = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        var resolved = PositionCorrectionResolver.ResolveForShape(rect, full);

        Assert.NotNull(resolved);
        Assert.Equal(0.0, resolved!.M12, 6);
        Assert.Equal(0.0, resolved.M21, 6);
        Assert.Equal(1.5, resolved.M11, 6);

        var fullCenter = full.TransformWpfPoint(centerRef);
        var resolvedCenter = resolved.TransformWpfPoint(centerRef);
        Assert.Equal(fullCenter.X, resolvedCenter.X, 6);
        Assert.Equal(fullCenter.Y, resolvedCenter.Y, 6);
    }

    [Fact]
    public void ResolveForShape_Annulus_CenterFullyCorrected_NoRotation()
    {
        var full = Rotated(30, 1.25);
        var annulus = new Annulus2d(new Point(40, 80), 5, 10);
        var resolved = PositionCorrectionResolver.ResolveForShape(annulus, full);

        Assert.NotNull(resolved);
        Assert.Equal(0.0, resolved!.M12, 6);
        Assert.Equal(1.25, resolved.M11, 6);

        var fullCenter = full.TransformWpfPoint(new Point(40, 80));
        var resolvedCenter = resolved.TransformWpfPoint(new Point(40, 80));
        Assert.Equal(fullCenter.X, resolvedCenter.X, 6);
        Assert.Equal(fullCenter.Y, resolvedCenter.Y, 6);
    }

    [Fact]
    public void ResolveForShape_RotatedRect_KeepsFullMatrix()
    {
        var full = Rotated(30, 1.5);
        var resolved = PositionCorrectionResolver.ResolveForShape(new RotatedRect2d(0, 0, 10, 10, 45), full);

        Assert.Same(full, resolved);
    }

    [Fact]
    public void ResolveForShape_Polygon_KeepsFullMatrix()
    {
        var full = Rotated(30, 1.5);
        var resolved = PositionCorrectionResolver.ResolveForShape(
            new Polygon2d(new[] { new Point(0, 0), new Point(10, 0), new Point(10, 10) }), full);

        Assert.Same(full, resolved);
    }

    [Fact]
    public void DisplayAngleDeg_RotatesReferenceAngleByMatrix()
    {
        // FromCenterAngleScale 角度语义：显示角 = 参考角 − 矩阵构造角（验证公式与矩阵自洽）
        var m = Rotated(30);
        var displayed = PositionCorrectionResolver.DisplayAngleDeg(m, 45);

        Assert.Equal(15.0, displayed, 6);
    }

    [Fact]
    public void DisplayAngleDeg_NoRotation_KeepsAngle()
    {
        var m = AffineMatrix.Identity;
        Assert.Equal(0.0, PositionCorrectionResolver.DisplayAngleDeg(m, 0), 6);
        Assert.Equal(90.0, PositionCorrectionResolver.DisplayAngleDeg(m, 90), 6);
    }
}
