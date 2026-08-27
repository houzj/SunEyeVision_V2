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
    public void UpdateLineEndpoints_ChangesDirection()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.FromTwoPoints(new Point(0, 0), new Point(10, 0)));

        // 拖动端点从 (10,0) → (10,10)，方向应旋转
        Assert.True(model.UpdateLineEndpoints(new Point(0, 0), new Point(10, 10)));
        Assert.True(model.EditingLine.HasValue);
        Assert.Equal(0, model.EditingLine.Value.Point.X, 3);
        Assert.Equal(0, model.EditingLine.Value.Point.Y, 3);
        Assert.True(Math.Abs(model.EditingLine.Value.Direction.X - 10 / Math.Sqrt(200)) < 1e-6);
    }

    [Fact]
    public void UpdateLineEndpoints_RejectsCoincident()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.FromTwoPoints(new Point(0, 0), new Point(10, 0)));

        // 端点重合 → 更新失败（防退化）
        Assert.False(model.UpdateLineEndpoints(new Point(5, 5), new Point(5, 5)));
        Assert.True(model.EditingLine.HasValue);
    }

    [Fact]
    public void UpdateCircleRadius_ChangesRadius()
    {
        var model = new MeasureInteractionModel();
        model.SetCircle(new Circle2d(10, 10, 5));

        Assert.True(model.UpdateCircleRadius(20));
        Assert.True(model.EditingCircle.HasValue);
        Assert.Equal(20, model.EditingCircle.Value.Radius);
        Assert.Equal(10, model.EditingCircle.Value.Center.X);
    }

    [Fact]
    public void UpdateCircleRadius_ClampsMin()
    {
        var model = new MeasureInteractionModel();
        model.SetCircle(new Circle2d(10, 10, 5));

        // 半径钳制最小 1（防退化）
        Assert.True(model.UpdateCircleRadius(0.1));
        Assert.True(model.EditingCircle.HasValue);
        Assert.True(model.EditingCircle.Value.Radius >= 1.0);
    }

    [Fact]
    public void SetArcFromEndpoints_GeneratesDefaultP3()
    {
        var model = new MeasureInteractionModel();
        // 默认 P3 = 弧中点（半径=弦长，60° 弧），且三点定圆经过 P1/P2
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));
        Assert.True(model.EditingArc.HasValue);
        Assert.True((model.ArcPoint3 - model.EditingArc.Value.GetPointOnArc(
            model.EditingArc.Value.StartAngle + model.EditingArc.Value.AngularSpan / 2)).Length < 1e-3);
    }

    [Fact]
    public void UpdateArcEndpoint_RecomputesCenter()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));
        var beforeCenter = model.EditingArc.Value.Center;

        // 拖 P2 到 (0,10)，P1/P3 不动，三点定圆重算圆心（圆心变）
        Assert.True(model.UpdateArcEndpoint(false, new Point(0, 10)));
        Assert.True(model.EditingArc.HasValue);
        Assert.Equal(new Point(0, 10), model.ArcPoint2);      // P2 = 鼠标位置
        Assert.Equal(new Point(0, 0), model.ArcPoint1);       // P1 不动
        Assert.NotEqual(beforeCenter, model.EditingArc.Value.Center); // 圆心派生变
    }

    [Fact]
    public void UpdateArcPoint3_KeepsEndpoints()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));

        // 拖 P3 到 (5, 20)：P1/P2 不动，三点定圆重算（半径变）
        Assert.True(model.UpdateArcPoint3(new Point(5, 20)));
        Assert.True(model.EditingArc.HasValue);
        Assert.Equal(new Point(0, 0), model.ArcPoint1);       // P1 不动
        Assert.Equal(new Point(10, 0), model.ArcPoint2);      // P2 不动
        Assert.Equal(new Point(5, 20), model.ArcPoint3);      // P3 = 鼠标位置
        Assert.True(model.EditingArc.Value.Radius > 10);      // 半径变大
    }

    [Fact]
    public void UpdateArcPoint3_RejectsCollinear()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));

        // P3 拖到与 P1/P2 共线（y=0）→ 更新失败（防退化）
        Assert.False(model.UpdateArcPoint3(new Point(5, 0)));
        Assert.True(model.EditingArc.HasValue);
    }

    [Fact]
    public void UpdateLineEndpoints_SyncsEndpointParams()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.FromTwoPoints(new Point(0, 0), new Point(10, 0)), new Point(0, 0), new Point(10, 0));

        // 拖端点后，LineStartPoint/LineEndPoint 应同步
        Assert.True(model.UpdateLineEndpoints(new Point(2, 3), new Point(12, 3)));
        Assert.Equal(new Point(2, 3), model.LineStartPoint);
        Assert.Equal(new Point(12, 3), model.LineEndPoint);
        Assert.True(model.EditingLine.HasValue);
    }

    [Fact]
    public void SetArcFromEndpoints_RejectsCoincident()
    {
        var model = new MeasureInteractionModel();
        // 两点重合 → 创建失败
        Assert.False(model.SetArcFromEndpoints(new Point(5, 5), new Point(5, 5)));
        Assert.Null(model.EditingArc);
    }

    [Fact]
    public void Deselect_ClearsSelected_KeepsGuide()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.Horizontal(1));
        model.IsSelected = true;

        model.Deselect();

        Assert.False(model.IsSelected);
        Assert.True(model.HasGuide); // 引导几何保留
    }

    [Fact]
    public void Clear_ResetsSelectedState()
    {
        var model = new MeasureInteractionModel();
        model.SetLine(Line2d.Horizontal(1));
        model.IsDrawing = true;
        model.IsSelected = true;

        model.Clear();

        Assert.False(model.HasGuide);
        Assert.False(model.IsDrawing);
        Assert.False(model.IsSelected);
        Assert.Null(model.EditingLine);
    }

    [Fact]
    public void EmptyArc_DoesNotCountAsGuide()
    {
        var model = new MeasureInteractionModel();
        model.SetArc(Arc2d.Empty);

        Assert.False(model.HasGuide);
    }
}
