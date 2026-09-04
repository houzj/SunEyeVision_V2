using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 旋转矩形显示语义自洽契约：无论位置修正矩阵旋转角多大，
/// UI 渲染语义（中心=full(c)、宽高×s、角度=DisplayAngleDeg(full, refAngle)）
/// 产生的旋转矩形角点必须与"完整矩阵逐点变换参考角点"完全重合。
/// 这是"图形/手柄/算法三态不随角度增大而脱离"的数学前提。
/// </summary>
public class RotatedRectCorrectionContractTests
{
    private static AffineMatrix Full(double ox, double oy, double angleDeg, double scale)
        => AffineMatrix.FromCenterAngleScale(ox, oy, angleDeg * Math.PI / 180.0, scale);

    [Theory]
    [InlineData(37)]
    [InlineData(90)]
    [InlineData(150)]
    [InlineData(-60)]
    public void RenderedCorners_Equal_FullTransformedCorners(double thetaDeg)
    {
        const double refAngle = 23; // 参考系旋转矩形自身角度（屏幕度）
        var center = new Point(120, 90);
        const double w = 60, h = 30;
        var full = Full(500, 400, thetaDeg, 1.7);

        // 参考矩形局部轴（RotateTransform 语义：局部 x 轴 = (cos a, sin a)）
        double a = refAngle * Math.PI / 180;
        var u = new Vector(Math.Cos(a), Math.Sin(a));
        var v = new Vector(-Math.Sin(a), Math.Cos(a));
        var hw = w / 2;
        var hh = h / 2;
        var refCorners = new[]
        {
            center + u * hw + v * hh,
            center + u * hw - v * hh,
            center - u * hw - v * hh,
            center - u * hw + v * hh,
        };

        // 期望：参考角点经完整矩阵逐点变换（手柄/算法区域即按此）
        var expected = refCorners.Select(p => full.TransformWpfPoint(p)).ToArray();

        // 实际（UI 渲染语义）：中心 full 变换、宽高×s、显示角由矩阵对长轴作用反推
        double s = full.ScaleFactor();
        var cDisp = full.TransformWpfPoint(center);
        double dispRad = PositionCorrectionResolver.DisplayAngleDeg(full, refAngle) * Math.PI / 180;
        var U = new Vector(Math.Cos(dispRad), Math.Sin(dispRad));
        var V = new Vector(-Math.Sin(dispRad), Math.Cos(dispRad));
        var actual = new[]
        {
            cDisp + U * (w * s / 2) + V * (h * s / 2),
            cDisp + U * (w * s / 2) - V * (h * s / 2),
            cDisp - U * (w * s / 2) - V * (h * s / 2),
            cDisp - U * (w * s / 2) + V * (h * s / 2),
        };

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(expected[i].X, actual[i].X, 3);
            Assert.Equal(expected[i].Y, actual[i].Y, 3);
        }
    }
}
