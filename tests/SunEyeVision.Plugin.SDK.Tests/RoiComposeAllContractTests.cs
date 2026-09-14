using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 几何参数订阅"向量化多实例拼装"契约测试（有几个解析几个）：
/// - 字段叶无索引（[] = 该类型全部图形）→ double[N]，ComposeAllByTemplate 逐颗拼装 N 颗；
/// - 常量/标量参数广播到每颗；N=1 等价单实例拼装；
/// - 多个数组长度不一致 / 非法值 → 显式报错（rule-015，禁止静默截断）。
/// 对应几何测量订阅验收 AC-3/4/5。
/// </summary>
public class RoiComposeAllContractTests
{
    [Fact]
    public void ComposeResolver_CoversEveryComposableShape()
    {
        // 守卫（"能选不能用"防线）：几何参数模式列出的每个形状，拼装器都必须能产出几何。
        // 历史缺陷：UI 列出 7 类形状而拼装器只认 4 类 → 用户选多边形/圆环/弧形，参数行能填、运行期拼不出几何。
        // 今后新增形状时本测试立即红灯，逼"参数行 + 拼装器"同步补齐。
        var availableShapes = new SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels.ParameterPanelViewModel().AvailableShapeTypes;
        foreach (var shape in availableShapes)
        {
            var geometry = RoiComposeResolver.ComposeByTemplate(shape, _ => null);
            Assert.True(geometry != null, $"形状 {shape} 已列入几何参数模式，但拼装器产不出几何（能选不能用）");
        }
    }

    private static Dictionary<string, object?> RotatedRectRaw(
        double[] cx, double[] cy, double[] w, double[] h, double[] angle)
        => new()
        {
            ["CenterX"] = cx,
            ["CenterY"] = cy,
            ["Width"] = w,
            ["Height"] = h,
            ["Angle"] = angle,
        };

    [Fact]
    public void ComposeAll_TwoShapes_YieldsTwoRotatedRects()
    {
        var raw = RotatedRectRaw(
            cx: new[] { 10.0, 50.0 },
            cy: new[] { 20.0, 60.0 },
            w: new[] { 30.0, 40.0 },
            h: new[] { 30.0, 40.0 },
            angle: new[] { 15.0, 25.0 });

        var geoms = RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle, raw);

        Assert.Equal(2, geoms.Count);
        var r0 = Assert.IsType<RotatedRect2d>(geoms[0]);
        Assert.Equal(10, r0.Center.X);
        Assert.Equal(20, r0.Center.Y);
        Assert.Equal(15, r0.Angle);
        var r1 = Assert.IsType<RotatedRect2d>(geoms[1]);
        Assert.Equal(50, r1.Center.X);
        Assert.Equal(60, r1.Center.Y);
        Assert.Equal(25, r1.Angle);
    }

    [Fact]
    public void ComposeAll_SingleArray_StillBroadcastsScalarParam()
    {
        // 中心X/宽/高是数组（N=2），角度是常量标量 → 广播到每颗
        var raw = RotatedRectRaw(
            cx: new[] { 10.0, 50.0 },
            cy: new[] { 20.0, 60.0 },
            w: new[] { 30.0, 40.0 },
            h: new[] { 30.0, 40.0 },
            angle: new[] { 15.0, 25.0 });
        raw["Angle"] = 90.0; // 常量标量广播

        var geoms = RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle, raw);

        Assert.Equal(2, geoms.Count);
        Assert.Equal(90.0, Assert.IsType<RotatedRect2d>(geoms[0]).Angle);
        Assert.Equal(90.0, Assert.IsType<RotatedRect2d>(geoms[1]).Angle);
    }

    [Fact]
    public void ComposeAll_Length1_EqualsSingleInstanceBehavior()
    {
        // N=1：与旧单实例拼装结果一致（存量不回归）
        var raw = RotatedRectRaw(
            cx: new[] { 10.0 },
            cy: new[] { 20.0 },
            w: new[] { 30.0 },
            h: new[] { 40.0 },
            angle: new[] { 15.0 });

        var geoms = RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle, raw);

        var single = Assert.Single(geoms);
        var rect = Assert.IsType<RotatedRect2d>(single);
        Assert.Equal(10, rect.Center.X);
        Assert.Equal(20, rect.Center.Y);
        Assert.Equal(30, rect.Width);
        Assert.Equal(40, rect.Height);
        Assert.Equal(15, rect.Angle);
    }

    [Fact]
    public void ComposeAll_MismatchedArrayLengths_Throws()
    {
        // 两个数组长度不一致（2 vs 3）→ 显式报错（rule-015，禁止静默截断）
        var raw = RotatedRectRaw(
            cx: new[] { 10.0, 50.0 },
            cy: new[] { 20.0, 60.0, 70.0 }, // 3 颗，与其余 2 颗不一致
            w: new[] { 30.0, 40.0 },
            h: new[] { 30.0, 40.0 },
            angle: new[] { 15.0, 25.0 });

        Assert.Throws<InvalidOperationException>(() =>
            RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle, raw));
    }

    [Fact]
    public void ComposeAll_MissingParam_AppliesTemplateDefault()
    {
        // 某参数未绑定/缺失 → 模板默认（宽高默认 100）
        var raw = RotatedRectRaw(
            cx: new[] { 10.0, 50.0 },
            cy: new[] { 20.0, 60.0 },
            w: new[] { 30.0, 40.0 },
            h: new[] { 30.0, 40.0 },
            angle: new[] { 15.0, 25.0 });
        raw.Remove("Angle"); // 缺失 → 每颗默认 0

        var geoms = RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle, raw);

        Assert.Equal(2, geoms.Count);
        Assert.Equal(0.0, Assert.IsType<RotatedRect2d>(geoms[0]).Angle);
    }

    [Fact]
    public void ComposeAll_UnsupportedValueType_Throws()
    {
        // 非数值/数值数组的绑定值（如字符串非数值）→ 显式报错
        var raw = RotatedRectRaw(
            cx: new[] { 10.0 },
            cy: new[] { 20.0 },
            w: new[] { 30.0 },
            h: new[] { 40.0 },
            angle: new[] { 15.0 });
        raw["Width"] = "not-a-number";

        Assert.Throws<InvalidOperationException>(() =>
            RoiComposeResolver.ComposeAllByTemplate(ShapeType.RotatedRectangle, raw));
    }
}
