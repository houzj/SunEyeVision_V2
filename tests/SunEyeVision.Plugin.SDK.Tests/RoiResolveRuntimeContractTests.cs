using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// Roi 统一运行时解析出口契约测试：本地 / 整对象订阅 / 参数拼装三态归一为 RoiShape[]。
/// 覆盖方案防御机制"契约一致性测试"：订阅结果 == 上游 InspectionGeometries 值，
/// 且消费方（Extract*/结果层/Cv）全部经此出口 —— 保证算法/结果树/结果层几何一致。
/// </summary>
public class RoiResolveRuntimeContractTests
{
    private sealed class FakeResults : ToolResults
    {
        private readonly IReadOnlyList<OutputVariable> _vars;
        public FakeResults(IReadOnlyList<OutputVariable> vars) => _vars = vars;
        public override IReadOnlyList<OutputVariable> GetOutputVariables() => _vars;
    }

    private static IDictionary<string, ToolResults> MakeResults(RoiShape[] shapes)
    {
        var variables = new List<OutputVariable>
        {
            new OutputVariable
            {
                Name = "检测区域",
                VariableType = "ROI",
                DisplayName = "检测区域",
                RuntimeValue = shapes,
            }
        };
        return new Dictionary<string, ToolResults> { ["Node1"] = new FakeResults(variables) };
    }

    private static IParameterContext MakeContext(RoiShape[] shapes)
        => new NodeResultsParameterContext(MakeResults(shapes));

    [Fact]
    public void LocalRoi_ReturnsLocalGeometry()
    {
        var roi = Roi.CreateLocal("本地矩形", new System.Windows.Rect(0, 0, 100, 80));

        var resolved = roi.ResolveRuntime(null);

        Assert.Single(resolved);
        Assert.Same(roi.LocalGeometry, resolved[0]);
    }

    [Fact]
    public void SubscribedRoi_ResolvesUpstreamShapes()
    {
        var upstream = new RoiShape[]
        {
            new RoiShape { Geometry = new RotatedRect2d(10, 20, 30, 40, 15) },
            new RoiShape { Geometry = new Circle2d(100, 80, 50) },
        };
        var roi = Roi.CreateSubscribed("订阅检测区", "Node1", "检测区域");

        var resolved = roi.ResolveRuntime(MakeContext(upstream));

        // 订阅出口结果 == 上游 InspectionGeometries 值（契约一致性：结果树/结果层/算法同源）
        Assert.Equal(2, resolved.Count);
        Assert.Equal(ShapeType.RotatedRectangle, resolved[0].ShapeType);
        Assert.Equal(ShapeType.Circle, resolved[1].ShapeType);
    }

    [Fact]
    public void SubscribedRoi_NoContext_Throws()
    {
        var roi = Roi.CreateSubscribed("订阅检测区", "Node1", "检测区域");

        // rule-015 报错优先：订阅态缺上下文必须抛异常，禁止静默回退本地
        Assert.Throws<InvalidOperationException>(() => roi.ResolveRuntime(null));
    }

    [Fact]
    public void SubscribedRoi_MissingUpstreamNode_Throws()
    {
        var roi = Roi.CreateSubscribed("订阅检测区", "MissingNode", "检测区域");
        var context = MakeContext(Array.Empty<RoiShape>());

        Assert.Throws<InvalidOperationException>(() => roi.ResolveRuntime(context));
    }

    [Fact]
    public void ComposedRoi_ResolvesParameterTemplate()
    {
        var roi = Roi.CreateComposed("拼装矩形", ShapeType.RotatedRectangle,
            new Dictionary<string, ParamSetting>
            {
                ["CenterX"] = ParamSetting.CreateBinding("CenterX", "Node1", "X"),
                ["CenterY"] = ParamSetting.CreateBinding("CenterY", "Node1", "Y"),
            });

        var variables = new List<OutputVariable>
        {
            new OutputVariable { Name = "X", VariableType = "数值", DisplayName = "X", RuntimeValue = 200.0 },
            new OutputVariable { Name = "Y", VariableType = "数值", DisplayName = "Y", RuntimeValue = 150.0 },
        };
        var context = new NodeResultsParameterContext(
            new Dictionary<string, ToolResults> { ["Node1"] = new FakeResults(variables) });

        var resolved = roi.ResolveRuntime(context);

        // 拼装模板默认宽高 100×100 于 (200,150) 中心 → Rect(150,100,100,100)
        Assert.Single(resolved);
        var rect = Assert.IsType<System.Windows.Rect>(resolved[0].Geometry);
        Assert.Equal(150, rect.X);
        Assert.Equal(100, rect.Y);
    }

    [Fact]
    public void ComposedRoi_NoContext_Throws()
    {
        var roi = Roi.CreateComposed("拼装矩形", ShapeType.RotatedRectangle,
            new Dictionary<string, ParamSetting>
            {
                ["CenterX"] = ParamSetting.CreateBinding("CenterX", "Node1", "X"),
            });

        Assert.Throws<InvalidOperationException>(() => roi.ResolveRuntime(null));
    }
}
