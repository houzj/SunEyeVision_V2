using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using Xunit;

namespace SunEyeVision.Tool.Subroutine.Tests;

/// <summary>
/// 契约测试：SubroutineTool.Run 中显示项必须按解析值的真实类型分发（AC-02），
/// 绑定失败/类型不支持必须抛异常而非静默跳过（AC-05，rule-015）。
/// </summary>
public class SubroutineToolDisplayContractTests
{
    static SubroutineToolDisplayContractTests()
    {
        // 子程序工具是 IsToolPlugin 插件，输出部署到 dev/Debug/plugins/<Name>/，
        // 测试输出目录不包含工具 DLL。运行时从插件目录显式加载（工具插件测试的通用解法）。
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new System.Reflection.AssemblyName(args.Name).Name;
            var pluginDll = Path.Combine(AppContext.BaseDirectory, "plugins", "SunEyeVision.Tool.Subroutine", $"{name}.dll");
            return File.Exists(pluginDll) ? System.Reflection.Assembly.LoadFrom(pluginDll) : null;
        };
    }

    private static SubroutineParameters MakeParams(Action<SubroutineParameters> configure, GenericToolResults internalResult)
    {
        // 源节点必须标记为执行成功，ParameterResolver 才会返回其属性值（IsSuccess 检查）
        internalResult.Status = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success;

        var parameters = new SubroutineParameters
        {
            SubroutineId = "sub-workflow-1",
            SubroutineName = "测试子程序",
            ExecuteSubWorkflowAsync = (_, _, _) => Task.FromResult(new ExecutionResult
            {
                Success = true,
                NodeResults = new Dictionary<string, NodeExecutionResult>
                {
                    ["internalNode"] = new NodeExecutionResult { Result = internalResult }
                }
            })
        };
        // 满足前置：至少 1 输入 + 1 输出（均不绑定，只用于计数）
        parameters.Inputs.Add(new SubroutineInputConfig { Name = "in1" });
        parameters.Outputs.Add(new SubroutineOutputConfig { Name = "out1" });
        configure(parameters);
        return parameters;
    }

    private static SubroutineResults Run(SubroutineParameters parameters)
    {
        var tool = new SubroutineTool();
        return tool.Run(parameters);
    }

    private static SubroutineDisplayItem CircleDisplayItem()
    {
        var style = new GraphicsStyle { Name = "圆样式" };
        return new SubroutineDisplayItem
        {
            DisplayName = "拟合圆",
            DisplayType = SubroutineDisplayType.Circle,
            BindingSource = "internalNode.Circle",
            GraphicsStyle = style
        };
    }

    [Fact]
    public void CircleOutput_IsRenderedAsTrueCircle_NotContour()
    {
        var internalResult = new GenericToolResults();
        internalResult.AddResultItem("Circle", new Circle2d(new Point(10, 20), 30));

        var parameters = MakeParams(p => p.DisplayConfigs.Add(CircleDisplayItem()), internalResult);
        var result = Run(parameters);

        // 诊断：若执行异常被 Run 吞掉，先暴露 ErrorMessage 以便定位
        Assert.True(string.IsNullOrEmpty(result.ErrorMessage), $"Run 返回异常: {result.ErrorMessage}");

        // 真圆以 GraphicsType.Circle 渲染，而非 Point[] 折线轮廓（AC-02）
        var circleItem = result.Display.Items.OfType<GraphicsDisplayItem>()
            .SingleOrDefault(i => i.Type == GraphicsType.Circle);
        Assert.NotNull(circleItem);
        var circle = Assert.IsType<Circle2d>(Assert.Single(circleItem.Geometries));
        Assert.Equal(10, circle.Center.X);
        Assert.Equal(20, circle.Center.Y);
        Assert.Equal(30, circle.Radius);
    }

    [Fact]
    public void MissingBinding_Throws_NotSilentlySkips()
    {
        var internalResult = new GenericToolResults();
        internalResult.AddResultItem("Circle", new Circle2d(0, 0, 5));

        var displayItem = CircleDisplayItem();
        displayItem.BindingSource = "internalNode.DoesNotExist";

        var parameters = MakeParams(p => p.DisplayConfigs.Add(displayItem), internalResult);

        // rule-015：绑定解析失败必须显式报错（Fail-Forward 可见性），而非 LogWarning 后静默跳过（AC-05）。
        // SubroutineTool.Run 的 catch 会把 BuildDisplayItemsFromConfig 的异常转为 ErrorMessage + Failed 状态，
        // 使主窗口能显示错误信息（如"无输出变量 'DoesNotExist'"）；绝不允许静默返回空 Display。
        var result = Run(parameters);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage), "绑定解析失败必须记录 ErrorMessage，不得静默跳过");
        Assert.Contains("internalNode", result.ErrorMessage);
    }

    [Fact]
    public void RoiContainer_TypeFiltered_IsRenderedAsTrueCircle()
    {
        // Blob 检测区图形：以 RoI（ShapeParameters[]）输出，子程序"圆形"显示项绑定 [Type=Circle] 下钻。
        // 运行期解析出 ShapeParameters[]，必须按 Geometry 真实类型画真圆（AC-02）。
        var internalResult = new GenericToolResults();
        internalResult.AddResultItem("检测区图形", new ShapeParameters[]
        {
            new ShapeParameters { Geometry = new Circle2d(new Point(50, 60), 25) },
        });

        var style = new GraphicsStyle { Name = "圆样式" };
        var displayItem = new SubroutineDisplayItem
        {
            DisplayName = "检测区圆",
            DisplayType = SubroutineDisplayType.Circle,
            BindingSource = "internalNode.检测区图形[Type=Circle]",
            GraphicsStyle = style
        };

        var parameters = MakeParams(p => p.DisplayConfigs.Add(displayItem), internalResult);
        var result = Run(parameters);

        Assert.True(string.IsNullOrEmpty(result.ErrorMessage), $"Run 返回异常: {result.ErrorMessage}");

        var circleItem = result.Display.Items.OfType<GraphicsDisplayItem>()
            .SingleOrDefault(i => i.Type == GraphicsType.Circle);
        Assert.NotNull(circleItem);
        var circle = Assert.IsType<Circle2d>(Assert.Single(circleItem.Geometries));
        Assert.Equal(50, circle.Center.X);
        Assert.Equal(60, circle.Center.Y);
        Assert.Equal(25, circle.Radius);
    }

    [Fact]
    public void RoiContainer_AllShapeTypes_AreRenderedByGeometry()
    {
        // Blob 检测区域含 2 圆 + 1 矩形 + 1 旋转矩形：不同显示项按 [Type=X] 各自下钻绘制。
        // 圆→真圆、矩形→Rect、旋转矩形→四角轮廓。
        var internalResult = new GenericToolResults();
        internalResult.AddResultItem("检测区图形", new ShapeParameters[]
        {
            new ShapeParameters { Geometry = new Circle2d(new Point(10, 20), 5) },
            new ShapeParameters { Geometry = new Circle2d(new Point(30, 40), 7) },
            new ShapeParameters { Geometry = new System.Windows.Rect(50, 60, 10, 12) },
            new ShapeParameters { Geometry = new RotatedRect2d(100, 110, 20, 30, 15) },
        });

        var circleItem = new SubroutineDisplayItem
        {
            DisplayName = "圆",
            DisplayType = SubroutineDisplayType.Circle,
            BindingSource = "internalNode.检测区图形[Type=Circle]",
            GraphicsStyle = new GraphicsStyle { Name = "圆" }
        };
        var rectItem = new SubroutineDisplayItem
        {
            DisplayName = "矩形",
            DisplayType = SubroutineDisplayType.Rectangle,
            BindingSource = "internalNode.检测区图形[Type=Rectangle]",
            GraphicsStyle = new GraphicsStyle { Name = "矩形" }
        };
        var rrItem = new SubroutineDisplayItem
        {
            DisplayName = "旋转矩形",
            DisplayType = SubroutineDisplayType.Rectangle,
            BindingSource = "internalNode.检测区图形[Type=RotatedRectangle]",
            GraphicsStyle = new GraphicsStyle { Name = "旋转矩形" }
        };

        var parameters = MakeParams(p =>
        {
            p.DisplayConfigs.Add(circleItem);
            p.DisplayConfigs.Add(rectItem);
            p.DisplayConfigs.Add(rrItem);
        }, internalResult);
        var result = Run(parameters);

        Assert.True(string.IsNullOrEmpty(result.ErrorMessage), $"Run 返回异常: {result.ErrorMessage}");

        // 2 个圆 → 真圆
        var circleGraphics = result.Display.Items.OfType<GraphicsDisplayItem>()
            .Where(i => i.Type == GraphicsType.Circle);
        Assert.Equal(2, circleGraphics.SelectMany(i => i.Geometries).OfType<Circle2d>().Count());

        // 1 个矩形 → Rect
        var rectGraphics = result.Display.Items.OfType<GraphicsDisplayItem>()
            .Where(i => i.Type == GraphicsType.Rectangle);
        Assert.Single(rectGraphics.SelectMany(i => i.Geometries).OfType<Rect>());

        // 1 个旋转矩形 → 四角轮廓（Contour 闭合）
        var rrContours = result.Display.Items.OfType<GraphicsDisplayItem>()
            .Where(i => i.Type == GraphicsType.Contour);
        Assert.Single(rrContours.SelectMany(i => i.Geometries).OfType<ContourGeometry>());
    }
}
