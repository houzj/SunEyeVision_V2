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
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using Xunit;

namespace SunEyeVision.Tool.Subroutine.Tests;

/// <summary>
/// 契约测试：SubroutineTool.Run 中显示项必须按解析值的真实类型分发（AC-02），
/// 绑定失败/类型不支持必须抛异常而非静默跳过（AC-05，rule-015）。
/// </summary>
public class SubroutineToolDisplayContractTests
{
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Annular_sector_preserves_both_radii_and_angles_for_scalar_array_and_roi(bool array, bool roi)
    {
        var shape = new Annulus2d(new Point(70, 80), 12, 28, 5.5, 8.2);
        object value = roi ? (array ? new[] { new RoiShape { Geometry = shape }, new RoiShape { Geometry = shape } } : (object)new RoiShape { Geometry = shape })
            : array ? new[] { shape, shape } : (object)shape;
        var source = new GenericToolResults(); source.AddResultItem("Sectors", value);
        var parameters = MakeParams(p => p.DisplayConfigs.Add(new() { DisplayName = "环形扇区", DisplayType = SubroutineDisplayType.AnnularSector, BindingSource = "internalNode.Sectors[]" }), source);
        var result = Run(parameters);
        try
        {
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var item = Assert.IsType<GraphicsDisplayItem>(Assert.Single(result.Display.Items));
            Assert.Equal(GraphicsType.Annulus, item.Type);
            Assert.Equal(array ? 2 : 1, item.Geometries.Length);
            foreach (var geometry in item.Geometries) Assert.Equal(shape, Assert.IsType<Annulus2d>(geometry));
            Assert.Same(parameters.DisplayConfigs[0].GraphicsStyle, item.Style);
        }
        finally { result.Resources?.Dispose(); }
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
        // Blob 检测区图形：以 RoI（RoiShape[]）输出，子程序"圆形"显示项绑定 [Type=Circle] 下钻。
        // 运行期解析出 RoiShape[]，必须按 Geometry 真实类型画真圆（AC-02）。
        var internalResult = new GenericToolResults();
        internalResult.AddResultItem("检测区图形", new RoiShape[]
        {
            new RoiShape { Geometry = new Circle2d(new Point(50, 60), 25) },
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
    public void RoiContainer_CircleAndRotatedRect_AreRenderedByGeometry()
    {
        // Blob 检测区域含 2 圆 + 1 旋转矩形：不同显示项按 [Type=X] 各自下钻绘制
        //（圆→真圆、旋转矩形→四角轮廓）。
        // 注：轴对齐矩形（WPF Rect / [Type=Rectangle]）已随 P0-2 并入旋转矩形，此处不再覆盖。
        var internalResult = new GenericToolResults();
        internalResult.AddResultItem("检测区图形", new RoiShape[]
        {
            new RoiShape { Geometry = new Circle2d(new Point(10, 20), 5) },
            new RoiShape { Geometry = new Circle2d(new Point(30, 40), 7) },
            new RoiShape { Geometry = new RotatedRect2d(100, 110, 20, 30, 15) },
        });

        var circleItem = new SubroutineDisplayItem
        {
            DisplayName = "圆",
            DisplayType = SubroutineDisplayType.Circle,
            BindingSource = "internalNode.检测区图形[Type=Circle]",
            GraphicsStyle = new GraphicsStyle { Name = "圆" }
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
            p.DisplayConfigs.Add(rrItem);
        }, internalResult);
        var result = Run(parameters);

        Assert.True(string.IsNullOrEmpty(result.ErrorMessage), $"Run 返回异常: {result.ErrorMessage}");

        // 2 个圆 → 真圆
        var circleGraphics = result.Display.Items.OfType<GraphicsDisplayItem>()
            .Where(i => i.Type == GraphicsType.Circle);
        Assert.Equal(2, circleGraphics.SelectMany(i => i.Geometries).OfType<Circle2d>().Count());

        // 1 个旋转矩形 → 四角轮廓（Contour 闭合）
        var rrContours = result.Display.Items.OfType<GraphicsDisplayItem>()
            .Where(i => i.Type == GraphicsType.Contour);
        Assert.Single(rrContours.SelectMany(i => i.Geometries).OfType<ContourGeometry>());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Arc_output_preserves_true_geometry_and_major_arc_flag(bool array)
    {
        var arc=new Arc2d(new Point(13,17),9,0.2,1.1,true);
        var source=new GenericToolResults();source.AddResultItem("Arcs",array?new[]{arc,arc}:(object)arc);
        var parameters=MakeParams(p=>p.DisplayConfigs.Add(new(){DisplayName="圆弧",DisplayType=SubroutineDisplayType.Arc,BindingSource="internalNode.Arcs[]"}),source);
        var result=Run(parameters);
        try
        {
            Assert.True(result.IsSuccess,result.ErrorMessage);
            var item=Assert.IsType<GraphicsDisplayItem>(Assert.Single(result.Display.Items));
            Assert.Equal(GraphicsType.Arc,item.Type);
            Assert.Equal(array?2:1,item.Geometries.Length);
            foreach(var geometry in item.Geometries)Assert.Equal(arc,Assert.IsType<Arc2d>(geometry));
        }
        finally{result.Resources?.Dispose();}
    }

    [Fact]
    public void Arc_ROI_is_rendered_as_arc_without_polygon_approximation()
    {
        var arc=new Arc2d(new Point(13,17),9,0.2,1.1,true);
        var source=new GenericToolResults();source.AddResultItem("Shapes",new[]{new RoiShape{Geometry=arc}});
        var parameters=MakeParams(p=>p.DisplayConfigs.Add(new(){DisplayName="圆弧",DisplayType=SubroutineDisplayType.Arc,BindingSource="internalNode.Shapes[]"}),source);
        var result=Run(parameters);
        try{Assert.True(result.IsSuccess,result.ErrorMessage);var item=Assert.IsType<GraphicsDisplayItem>(Assert.Single(result.Display.Items));Assert.Equal(GraphicsType.Arc,item.Type);Assert.Equal(arc,Assert.IsType<Arc2d>(Assert.Single(item.Geometries)));}
        finally{result.Resources?.Dispose();}
    }

    public static IEnumerable<object[]> TextArrayCases()
    {
        yield return new object[] { new string?[] { "首行", null, "", "  ", "末行" }, new[] { "首行", "", "", "  ", "末行" } };
        yield return new object[] { new[] { 1, -2, 3 }, new[] { "1", "-2", "3" } };
        yield return new object[] { new[] { true, false }, new[] { "True", "False" } };
        yield return new object[] { Array.Empty<string>(), Array.Empty<string>() };
    }
    [Theory]
    [MemberData(nameof(TextArrayCases))]
    public void Text_arrays_create_one_display_entry_per_content_element(object value, string[] expected)
    {
        var source = new GenericToolResults(); source.AddResultItem("Content", value);
        var style = new TextStyle { PositionX = 12, PositionY = 34 };
        var parameters = MakeParams(p => p.DisplayConfigs.Add(new()
        { DisplayName = "文本", DisplayType = SubroutineDisplayType.Text,
          BindingSource = "internalNode.Content[]", TextStyle = style }), source);
        var result = Run(parameters);
        try
        {
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var items = result.Display.Items.OfType<TextDisplayItem>().ToArray();
            var texts = items.SelectMany(item => item.Texts).ToArray();
            Assert.Equal(expected, texts.Select(text => text.Content));
            Assert.All(items, item => Assert.Same(style, item.Style));
            Assert.All(texts, text => { Assert.Equal(12, text.X); Assert.Equal(34, text.Y); });
        }
        finally { result.Resources?.Dispose(); }
    }
    [Theory]
    [InlineData("en-US", "1.5")]
    [InlineData("fr-FR", "1,5")]
    public void Numeric_text_elements_use_current_culture_without_array_type_names(string culture, string expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        try
        {
            var source = new GenericToolResults(); source.AddResultItem("Content", new[] { 1.5 });
            var parameters = MakeParams(p => p.DisplayConfigs.Add(new()
            { DisplayName = "文本", DisplayType = SubroutineDisplayType.Text,
              BindingSource = "internalNode.Content[]", TextStyle = new TextStyle() }), source);
            var result = Run(parameters);
            try
            {
                Assert.True(result.IsSuccess, result.ErrorMessage);
                Assert.Equal(expected, Assert.Single(Assert.IsType<TextDisplayItem>(Assert.Single(result.Display.Items)).Texts).Content);
            }
            finally { result.Resources?.Dispose(); }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void Scalar_string_remains_a_single_text_instead_of_characters()
    {
        var source = new GenericToolResults(); source.AddResultItem("Content", "完整文本");
        var parameters = MakeParams(p => p.DisplayConfigs.Add(new()
        { DisplayName = "文本", DisplayType = SubroutineDisplayType.Text,
          BindingSource = "internalNode.Content", TextStyle = new TextStyle() }), source);
        var result = Run(parameters);
        try
        {
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal("完整文本", Assert.Single(Assert.IsType<TextDisplayItem>(Assert.Single(result.Display.Items)).Texts).Content);
        }
        finally { result.Resources?.Dispose(); }
    }

    [Fact]
    public void Loop_text_arrays_accumulate_elements_while_ordinary_display_keeps_last_round()
    {
        var count = 0;
        var parent = new GenericToolResults { Status = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success };
        parent.AddResultItem("Start", 0); parent.AddResultItem("End", 3);
        var accumulatedStyle = new TextStyle { PositionX = 10 };
        var lastStyle = new TextStyle { PositionX = 20 };
        var parameters = new SubroutineParameters
        {
            SubroutineId = "child", SubroutineName = "循环文本",
            NodeResults = new Dictionary<string, ToolResults> { ["parent"] = parent },
            LoopConfig = new() { EnableLoop = true, EnableBreak = false,
                StartValueBinding = "parent.Start", EndValueBinding = "parent.End" },
            ExecuteSubWorkflowAsync = (_, _, _) =>
            {
                count++;
                var values = new GenericToolResults { Status = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success };
                values.AddResultItem("Content", new[] { count + "a", count + "b" });
                return Task.FromResult(new ExecutionResult { Success = true,
                    NodeResults = new Dictionary<string, NodeExecutionResult> { ["internalNode"] = new() { Result = values } } });
            }
        };
        parameters.Inputs.Add(new() { Name = "input" }); parameters.Outputs.Add(new() { Name = "output" });
        parameters.DisplayConfigs.Add(new() { DisplayName = "累积", DisplayType = SubroutineDisplayType.Text,
            BindingSource = "internalNode.Content[]", TextStyle = accumulatedStyle, IsAccumulated = true });
        parameters.DisplayConfigs.Add(new() { DisplayName = "最后", DisplayType = SubroutineDisplayType.Text,
            BindingSource = "internalNode.Content[]", TextStyle = lastStyle });
        var result = Run(parameters);
        try
        {
            Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(3, count);
            var items = result.Display.Items.OfType<TextDisplayItem>().ToArray();
            Assert.Equal(new[] { "1a", "1b", "2a", "2b", "3a", "3b" },
                items.Where(item => ReferenceEquals(item.Style, accumulatedStyle)).SelectMany(item => item.Texts).Select(text => text.Content));
            Assert.Equal(new[] { "3a", "3b" },
                items.Where(item => ReferenceEquals(item.Style, lastStyle)).SelectMany(item => item.Texts).Select(text => text.Content));
        }
        finally { result.Resources?.Dispose(); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Accumulated_geometry_keeps_configured_style_without_per_iteration_replacement(int rounds)
    {
        var count = 0;
        var parent = new GenericToolResults { Status = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success };
        parent.AddResultItem("Start", 0); parent.AddResultItem("End", rounds);
        var style = new GraphicsStyle { Name = "自定义", OkColor = System.Windows.Media.Color.FromArgb(143, 203, 21, 95),
            NgColor = System.Windows.Media.Colors.Purple, LineWidth = 4, Opacity = 0.65, CrossSize = 13 };
        var accumulated = new SubroutineDisplayItem { DisplayName = "累积圆", DisplayType = SubroutineDisplayType.Circle,
            BindingSource = "internalNode.Circles[]", GraphicsStyle = style, IsAccumulated = true };
        var notifications = new List<string?>(); accumulated.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        var parameters = new SubroutineParameters
        {
            SubroutineId = "child", SubroutineName = "累积样式",
            NodeResults = new Dictionary<string, ToolResults> { ["parent"] = parent },
            LoopConfig = new() { EnableLoop = true, EnableBreak = false,
                StartValueBinding = "parent.Start", EndValueBinding = "parent.End" },
            ExecuteSubWorkflowAsync = (_, _, _) =>
            {
                count++;
                var values = new GenericToolResults { Status = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success };
                values.AddResultItem("Circles", new[] { new Circle2d(count, 10, 5), new Circle2d(count, 20, 7) });
                values.AddResultItem("Lines", new[] { new Line2d(new Point(count, 30), new Vector(1, 0)) });
                return Task.FromResult(new ExecutionResult { Success = true,
                    NodeResults = new Dictionary<string, NodeExecutionResult> { ["internalNode"] = new() { Result = values } } });
            }
        };
        parameters.Inputs.Add(new() { Name = "input" }); parameters.Outputs.Add(new() { Name = "output" });
        parameters.DisplayConfigs.Add(accumulated);
        parameters.DisplayConfigs.Add(new() { DisplayName = "累积线", DisplayType = SubroutineDisplayType.Line,
            BindingSource = "internalNode.Lines[]", GraphicsStyle = style, IsAccumulated = true });
        var result = Run(parameters);
        try
        {
            Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(rounds, count);
            var items = result.Display.Items.OfType<GraphicsDisplayItem>().ToArray(); Assert.Equal(2, items.Length);
            var item = Assert.Single(items, geometry => geometry.Type == GraphicsType.Circle);
            var lines = Assert.Single(items, geometry => geometry.Type == GraphicsType.Line);
            Assert.Same(style, lines.Style); Assert.Equal(rounds, lines.Geometries.Length);
            Assert.All(lines.Geometries, geometry => Assert.IsType<SunEyeVision.Plugin.SDK.Models.Visualization.LineSegment>(geometry));
            Assert.Same(style, item.Style); Assert.Equal(rounds * 2, item.Geometries.Length);
            Assert.Equal(Enumerable.Range(1, rounds).SelectMany(i => new[] { (double)i, i }),
                item.Geometries.Cast<Circle2d>().Select(circle => circle.Center.X));
            Assert.Equal(System.Windows.Media.Color.FromArgb(143, 203, 21, 95), item.Style.OkColor);
            Assert.Equal(System.Windows.Media.Colors.Purple, item.Style.NgColor);
            Assert.Equal(4, item.Style.LineWidth); Assert.Equal(0.65, item.Style.Opacity); Assert.Equal(13, item.Style.CrossSize);
            Assert.Same(style, accumulated.GraphicsStyle); Assert.Empty(notifications);
        }
        finally { result.Resources?.Dispose(); }
    }

}
