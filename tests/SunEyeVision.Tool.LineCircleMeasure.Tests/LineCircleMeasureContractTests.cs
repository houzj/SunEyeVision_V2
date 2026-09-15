using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using Xunit;

namespace SunEyeVision.Tool.LineCircleMeasure.Tests;

/// <summary>
/// 线-圆测量工具契约测试（v4 P2 全支持套模板）。
/// 覆盖：参数序列化往返（rule-010）/ 工具泛型契约 / 引导几何 Fail-Fast（rule-015）。
/// </summary>
public class LineCircleMeasureContractTests
{
    private static JsonSerializerOptions Options()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(LineCircleMeasureParameters), "LineCircleMeasure");
        return ParameterTypeRegistry.SerializationOptions;
    }

    /// <summary>构造有效引导：元素1 直线（长度 100px）+ 元素2 圆（半径 50px）。</summary>
    private static LineCircleMeasureParameters CreateValidGuideParameters()
    {
        var parameters = new LineCircleMeasureParameters();
        parameters.E1StartX.Value = 10;
        parameters.E1StartY.Value = 20;
        parameters.E1EndX.Value = 110;
        parameters.E1EndY.Value = 20;
        parameters.E2CenterX.Value = 200;
        parameters.E2CenterY.Value = 200;
        parameters.E2Radius.Value = 50;
        return parameters;
    }

    #region 序列化往返（rule-010）

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesParamValuesAndJudgments()
    {
        var options = Options();
        var source = CreateValidGuideParameters();
        source.E1Threshold.Value = 45.5;
        source.E2NumMeasures.Value = 24;
        source.E2Transition.Value = EdgeTransition.Negative;
        source.E2Select.Value = EdgeSelect.Last;
        source.ResultPresentation.Judgments.Add(new JudgmentConfigItem
        {
            Key = "test-custom",
            Name = "线圆距",
            Enabled = true,
            Min = 0.0,
            Max = 500.0,
            Unit = "px",
        });

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<LineCircleMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(110.0, back!.E1EndX.Value, 6);
        Assert.Equal(45.5, back.E1Threshold.Value, 6);
        Assert.Equal(200.0, back.E2CenterX.Value, 6);
        Assert.Equal(50.0, back.E2Radius.Value, 6);
        Assert.Equal(24, back.E2NumMeasures.Value);
        Assert.Equal(EdgeTransition.Negative, back.E2Transition.Value);
        Assert.Equal(EdgeSelect.Last, back.E2Select.Value);

        // 默认判定项（距离/交点X/交点Y/半径/圆心距）保留 + 新增项完整还原
        Assert.Equal(source.ResultPresentation.Judgments.Count, back.ResultPresentation.Judgments.Count);
        var custom = back.ResultPresentation.Judgments.Single(j => j.Name == "线圆距");
        Assert.True(custom.Enabled);
        Assert.Equal(0.0, custom.Min, 6);
        Assert.Equal(500.0, custom.Max, 6);
    }

    [Fact]
    public void Parameters_PolymorphicRoundTrip_AsToolParameters_RestoresConcreteType()
    {
        var options = Options();
        var json = JsonSerializer.Serialize(CreateValidGuideParameters(), options);

        Assert.Contains("\"$type\": \"LineCircleMeasure\"", json);

        var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

        Assert.IsType<LineCircleMeasureParameters>(asBase);
    }

    #endregion

    #region 工具契约

    [Fact]
    public void Tool_ImplementsTypedPluginContract_ForItsOwnParameterAndResultTypes()
    {
        var contract = typeof(LineCircleMeasureTool).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IToolPlugin<,>));

        Assert.Equal(typeof(LineCircleMeasureParameters), contract.GetGenericArguments()[0]);
        Assert.Equal(typeof(LineCircleMeasureResults), contract.GetGenericArguments()[1]);
    }

    #endregion

    #region Fail-Fast（rule-015）

    [Fact]
    public void Run_GuideLineNotSet_FailsFast_WithVisibleError()
    {
        var result = new LineCircleMeasureTool().Run(new LineCircleMeasureParameters());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("卡尺1", result.ErrorMessage);
        Assert.Contains("引导直线未设置或长度无效", result.ErrorMessage);
    }

    [Fact]
    public void Run_GuideCircleRadiusInvalid_FailsFast_WithVisibleError()
    {
        var parameters = new LineCircleMeasureParameters();
        parameters.E1StartX.Value = 10;
        parameters.E1StartY.Value = 20;
        parameters.E1EndX.Value = 110;
        parameters.E1EndY.Value = 20;

        var result = new LineCircleMeasureTool().Run(parameters);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("卡尺2", result.ErrorMessage);
        Assert.Contains("引导圆未设置或半径无效", result.ErrorMessage);
    }

    #endregion
}
