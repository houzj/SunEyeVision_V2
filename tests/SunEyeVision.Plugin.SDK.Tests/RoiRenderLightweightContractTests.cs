using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;
using Xunit;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// ROI 轻量渲染契约测试（ROI 渲染轻量化方案防御机制）：
///   RoiVisualHost 增删/清除计数正确；WpfShapeRenderer 只对可见区域建 visual、
///   隐藏后移除、Clear 后清空——区域本体不再物化为 UIElement 对象树。
/// WPF UIElement 必须在 STA 线程创建，故所有用例经 STA 线程执行。
/// </summary>
public class RoiRenderLightweightContractTests
{
    [Fact]
    public void VisualHost_AddRemoveClear_CountCorrect()
        => RunSta(() =>
        {
            var host = new RoiVisualHost();
            var v1 = new DrawingVisual();

            host.Add(v1);
            host.Add(new DrawingVisual());
            Assert.Equal(2, host.Count);

            host.Remove(v1);
            Assert.Equal(1, host.Count);

            host.Clear();
            Assert.Equal(0, host.Count);
        });

    [Fact]
    public void Renderer_TracksOnlyVisibleRegions()
        => RunSta(() =>
        {
            var canvas = new Canvas();
            using var renderer = new WpfShapeRenderer(canvas);

            var regions = new List<RoiRenderContext>
            {
                MakeRect("R1", 0, 0),
                MakeCircle("R2", 0, 0),
                MakeLine("R3", 0, 0),
            };

            // 初始渲染：3 个可见区域 → host 内 3 个 visual
            renderer.Render(regions);
            Assert.Equal(3, HostVisualCount(canvas));

            // 不变更再渲染：无增删，host 计数不变
            renderer.Render(regions);
            Assert.Equal(3, HostVisualCount(canvas));

            // 隐藏一个 → 移除对应 visual
            regions[2].IsVisible = false;
            renderer.Render(regions);
            Assert.Equal(2, HostVisualCount(canvas));

            // Clear → 清空
            renderer.Clear();
            Assert.Equal(0, HostVisualCount(canvas));
        });

    private static int HostVisualCount(Canvas canvas)
    {
        var host = canvas.Children.OfType<RoiVisualHost>().SingleOrDefault();
        Assert.NotNull(host);
        return host!.Count;
    }

    /// <summary>在 STA 线程内执行测试动作，异常原样抛回调用线程。</summary>
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static RoiRenderContext MakeRect(string label, double x, double y) => new()
    {
        Id = Guid.NewGuid(),
        Label = label,
        ShapeType = ShapeType.RotatedRectangle,
        Center = new Point(x, y),
        Width = 100,
        Height = 60,
        IsVisible = true,
    };

    private static RoiRenderContext MakeCircle(string label, double x, double y) => new()
    {
        Id = Guid.NewGuid(),
        Label = label,
        ShapeType = ShapeType.Circle,
        Center = new Point(x, y),
        Radius = 30,
        IsVisible = true,
    };

    private static RoiRenderContext MakeLine(string label, double x, double y) => new()
    {
        Id = Guid.NewGuid(),
        Label = label,
        ShapeType = ShapeType.Line,
        LineStart = new Point(x, y),
        LineEnd = new Point(x + 50, y + 20),
        IsVisible = true,
    };
}
