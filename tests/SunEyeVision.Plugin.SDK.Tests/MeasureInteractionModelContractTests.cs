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
    public void SetArcFromEndpoints_DefaultRadiusEqualsChord()
    {
        var model = new MeasureInteractionModel();
        // 默认半径 = 弦长（60° 弧，对齐 VM）
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));
        Assert.True(model.EditingArc.HasValue);
        Assert.Equal(10, model.ArcRadius, 3);
        Assert.Equal(10, model.EditingArc.Value.Radius, 3);
        Assert.Equal(new Point(0, 0), model.ArcPoint1);
        Assert.Equal(new Point(10, 0), model.ArcPoint2);
    }

    [Fact]
    public void UpdateArcEndpoint_AnchorsMidpoint_RadiusRecomputes()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));
        Assert.True(model.EditingArc.HasValue);

        // 锚点 P3 = 当前弧中点（拖端点时保持不动，三点定圆保证弧始终过它）
        var arcBefore = model.EditingArc.Value;
        var anchor = arcBefore.GetPointOnArc(arcBefore.StartAngle + arcBefore.AngularSpan / 2);

        // 拖 P2 到 (0,10)：P2 = 鼠标位置、P1 不动，半径/方向由三点几何自适应（不再锁死旧半径）
        Assert.True(model.UpdateArcEndpoint(false, new Point(0, 10)));
        Assert.True(model.EditingArc.HasValue);
        Assert.Equal(new Point(0, 0), model.ArcPoint1);   // P1 不动
        Assert.Equal(new Point(0, 10), model.ArcPoint2);  // P2 = 鼠标位置

        var arcAfter = model.EditingArc.Value;
        // 弧仍经过锚点 P3（连续，无跳变）
        Assert.True(Math.Abs((anchor - arcAfter.Center).Length - arcAfter.Radius) < 1e-3);
        // 模型派生参数与几何一致
        Assert.Equal(arcAfter.Radius, model.ArcRadius, 3);
    }

    [Fact]
    public void UpdateArcRadiusFromPoint_RecomputesArc()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));

        // 拖 P3 到 (5, 20)：三点定圆保证弧过鼠标，提取新半径
        Assert.True(model.UpdateArcRadiusFromPoint(new Point(5, 20)));
        Assert.True(model.EditingArc.HasValue);
        Assert.Equal(new Point(0, 0), model.ArcPoint1);       // P1 不动
        Assert.Equal(new Point(10, 0), model.ArcPoint2);      // P2 不动
        Assert.True(model.EditingArc.Value.Radius > 10);      // 半径变大
        // 鼠标点 (5,20) 在弧上（弧经过鼠标）
        Assert.True(Math.Abs((new Point(5, 20) - model.EditingArc.Value.Center).Length
            - model.EditingArc.Value.Radius) < 1e-3);
    }

    [Fact]
    public void UpdateArcRadiusFromPoint_Collinear_UsesTransitionArc()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));

        // 鼠标拖到与 P1/P2 共线（y=0）：三点定圆退化，不拒绝也不崩溃，
        // 以"接近直线"的大半径过渡弧平滑衔接，弧状态保持连续
        Assert.True(model.UpdateArcRadiusFromPoint(new Point(5, 0)));
        Assert.True(model.EditingArc.HasValue);
        Assert.False(model.EditingArc.Value.IsEmpty);
        Assert.True(model.EditingArc.Value.Radius > 1e5);   // 过渡弧为大半径（近直线），防卡顿跳变
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
    public void SetLine_WithExplicitEndpoints_PreservesPanelEndpoints()
    {
        var model = new MeasureInteractionModel();
        var start = new Point(450.98, 312.87);
        var end = new Point(865.81, 288.76);

        model.SetLine(Line2d.FromTwoPoints(start, end), start, end);

        Assert.Equal(start, model.LineStartPoint);
        Assert.Equal(end, model.LineEndPoint);
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

    [Fact]
    public void FromEndpointsAndRadius_MajorArc_KeepsSpanGreaterThanPi()
    {
        // 弦长 10（P1=(0,0), P2=(10,0)），半径 10，小弧 60°
        var minor = Arc2d.FromEndpointsAndRadius(new Point(0, 0), new Point(10, 0), 10, false, false);
        Assert.False(minor.IsEmpty);
        Assert.True(Math.Abs(minor.AngularSpan) <= Math.PI);

        // 相同参数 + majorArc=true → 大弧，张角补到 2π-60°=300°
        var major = Arc2d.FromEndpointsAndRadius(new Point(0, 0), new Point(10, 0), 10, false, true);
        Assert.False(major.IsEmpty);
        Assert.True(Math.Abs(major.AngularSpan) > Math.PI);
        Assert.Equal(2 * Math.PI - Math.Abs(minor.AngularSpan), Math.Abs(major.AngularSpan), 3);
    }

    [Fact]
    public void UpdateArcRadiusFromPoint_DraggingHighAbove_FlipsToMajorArc()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));
        Assert.False(model.ArcClockwise);   // 初始逆时针小弧
        Assert.False(model.ArcMajorArc);

        // 拖 P3 到弦上方远处 (5, 100)：三点定圆弧段选择优弧 → 顺时针大弧（方向+大小弧同步变化）
        Assert.True(model.UpdateArcRadiusFromPoint(new Point(5, 100)));
        Assert.True(model.ArcClockwise);
        Assert.True(model.ArcMajorArc);
        var arc = model.EditingArc;
        Assert.True(arc.HasValue && Math.Abs(arc.Value.AngularSpan) > Math.PI);
    }

    [Fact]
    public void UpdateArcEndpoint_KeepsMajorArcState()
    {
        var model = new MeasureInteractionModel();
        Assert.True(model.SetArcFromEndpoints(new Point(0, 0), new Point(10, 0)));

        // 拖 P3 到 (5, 100) 制造大弧（顺时针大弧）
        Assert.True(model.UpdateArcRadiusFromPoint(new Point(5, 100)));
        Assert.True(model.ArcMajorArc, "前置条件：应已变为大弧");
        Assert.True(model.ArcClockwise);
        var arcBefore = model.EditingArc;
        Assert.True(arcBefore.HasValue && Math.Abs(arcBefore.Value.AngularSpan) > Math.PI);

        // 拖 P2 到 (8, 2)：大弧状态应保持，张角仍 > π
        Assert.True(model.UpdateArcEndpoint(false, new Point(8, 2)));
        Assert.True(model.ArcMajorArc, "拖 P2 后大弧状态应保持");
        Assert.True(model.ArcClockwise, "拖 P2 后方向应保持");
        var arcAfter = model.EditingArc;
        Assert.True(arcAfter.HasValue && Math.Abs(arcAfter.Value.AngularSpan) > Math.PI);
    }
}
