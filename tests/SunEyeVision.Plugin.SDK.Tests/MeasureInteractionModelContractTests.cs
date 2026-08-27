using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 测量交互模型契约测试：引导几何三态互斥 + 平移 + 编辑保持类型。
/// 对应方案防御机制 D1（契约测试，防"工具栏/几何状态漂移"）与 D2（空状态防护）。
/// </summary>
public class MeasureInteractionModelContractTests
{
    [Fact]
    public void SetLine_ClearsCircleAndArc()
    {
        var model = new MeasureInteractionModel();
        model.SetCircle(new Circle2d(10, 10, 5));
        model.SetArc(new Arc2d(0, 0, 5, 0, Math.PI));

        model.SetLine(Line2d.Horizontal(1));

        Assert.True(model.EditingLine.HasValue);
        Assert.Null(model.EditingCircle);
        Assert.Null(model.EditingArc);
        Assert.True(model.HasGuide);
    }

    [Fact]
    public void SetCircle_ClearsLine()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.Horizontal(1));

        model.SetCircle(new Circle2d(10, 10, 5));

        Assert.True(model.EditingCircle.HasValue);
        Assert.Null(model.EditingLine);
        Assert.True(model.HasGuide);
    }

    [Fact]
    public void Translate_MovesLinePoint()
    {
        var model = new MeasureInteractionModel();
        var line = Line2d.FromTwoPoints(new Point(0, 0), new Point(10, 0));
        model.SetLine(line);

        model.Translate(new Vector(5, 3));

        Assert.True(model.EditingLine.HasValue);
        Assert.Equal(5, model.EditingLine.Value.Point.X);
        Assert.Equal(3, model.EditingLine.Value.Point.Y);
    }

    [Fact]
    public void Translate_MovesCircleCenter()
    {
        var model = new MeasureInteractionModel();
        model.SetCircle(new Circle2d(10, 10, 5));

        model.Translate(new Vector(-2, 4));

        Assert.True(model.EditingCircle.HasValue);
        Assert.Equal(8, model.EditingCircle.Value.Center.X);
        Assert.Equal(14, model.EditingCircle.Value.Center.Y);
        Assert.Equal(5, model.EditingCircle.Value.Radius);
    }

    [Fact]
    public void TryUpdateLine_KeepsType_WhenNoLine()
    {
        var model = new MeasureInteractionModel();

        // 无引导直线时，更新应失败且不产生副作用（Fail-Forward 契约）
        Assert.False(model.TryUpdateLine(Line2d.Horizontal(1)));
        Assert.Null(model.EditingLine);
    }

    [Fact]
    public void TryUpdateArc_KeepsArc_WhenArcSet()
    {
        var model = new MeasureInteractionModel();
        var arc = new Arc2d(0, 0, 5, 0, Math.PI);
        model.SetArc(arc);

        Assert.True(model.TryUpdateArc(new Arc2d(1, 1, 6, 0, Math.PI)));
        Assert.True(model.EditingArc.HasValue);
        Assert.Equal(6, model.EditingArc.Value.Radius);
    }

    [Fact]
    public void Clear_ResetsAllGuides()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.Horizontal(1));
        model.IsDrawing = true;

        model.Clear();

        Assert.False(model.HasGuide);
        Assert.False(model.IsDrawing);
        Assert.Null(model.EditingLine);
        Assert.Null(model.EditingCircle);
        Assert.Null(model.EditingArc);
    }

    [Fact]
    public void EmptyArc_DoesNotCountAsGuide()
    {
        var model = new MeasureInteractionModel();
        model.SetArc(Arc2d.Empty);

        Assert.False(model.HasGuide);
    }
}
