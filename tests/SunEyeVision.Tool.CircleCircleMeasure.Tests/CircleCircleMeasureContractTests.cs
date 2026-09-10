using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using Xunit;

namespace SunEyeVision.Tool.CircleCircleMeasure.Tests;

/// <summary>
/// 圆-圆测量工具契约测试（v4 P2 全支持套模板）。
/// 覆盖：参数序列化往返（rule-010）/ 工具泛型契约 / 引导几何 Fail-Fast（rule-015）。
/// </summary>
public class CircleCircleMeasureContractTests
{
    private static JsonSerializerOptions Options()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(CircleCircleMeasureParameters), "CircleCircleMeasure");
        return ParameterTypeRegistry.SerializationOptions;
    }

    /// <summary>构造有效引导：两个圆（半径 50px / 80px）。</summary>
    private static CircleCircleMeasureParameters CreateValidGuideParameters()
    {
        var parameters = new CircleCircleMeasureParameters();
        parameters.E1CenterX.Value = 100;
        parameters.E1CenterY.Value = 100;
        parameters.E1Radius.Value = 50;
        parameters.E2CenterX.Value = 300;
        parameters.E2CenterY.Value = 100;
        parameters.E2Radius.Value = 80;
        return parameters;
    }

    #region 序列化往返（rule-010）

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesParamValuesAndJudgments()
    {
        var options = Options();
        var source = CreateValidGuideParameters();
        source.E1Threshold.Value = 42.5;
        source.E1NumMeasures.Value = 32;
        source.E2Transition.Value = EdgeTransition.Positive;
        source.Judgments.Add(new MeasureThreshold
        {
            Name = "两圆距",
            Enabled = true,
            Min = 100.0,
            Max = 300.0,
            Unit = "px",
        });

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<CircleCircleMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(100.0, back!.E1CenterX.Value, 6);
        Assert.Equal(50.0, back.E1Radius.Value, 6);
        Assert.Equal(42.5, back.E1Threshold.Value, 6);
        Assert.Equal(32, back.E1NumMeasures.Value);
        Assert.Equal(80.0, back.E2Radius.Value, 6);
        Assert.Equal(EdgeTransition.Positive, back.E2Transition.Value);

        // 默认判定项（圆心距/交点X/交点Y/半径1/半径2）保留 + 新增项完整还原
        Assert.Equal(source.Judgments.Count, back.Judgments.Count);
        var custom = back.Judgments.Single(j => j.Name == "两圆距");
        Assert.True(custom.Enabled);
        Assert.Equal(100.0, custom.Min, 6);
        Assert.Equal(300.0, custom.Max, 6);
    }

    [Fact]
    public void Parameters_PolymorphicRoundTrip_AsToolParameters_RestoresConcreteType()
    {
        var options = Options();
        var json = JsonSerializer.Serialize(CreateValidGuideParameters(), options);

        Assert.Contains("\"$type\": \"CircleCircleMeasure\"", json);

        var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

        Assert.IsType<CircleCircleMeasureParameters>(asBase);
    }

    #endregion

    #region 工具契约

    [Fact]
    public void Tool_ImplementsTypedPluginContract_ForItsOwnParameterAndResultTypes()
    {
        var contract = typeof(CircleCircleMeasureTool).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IToolPlugin<,>));

        Assert.Equal(typeof(CircleCircleMeasureParameters), contract.GetGenericArguments()[0]);
        Assert.Equal(typeof(CircleCircleMeasureResults), contract.GetGenericArguments()[1]);
    }

    #endregion

    #region Fail-Fast（rule-015）

    [Fact]
    public void Run_FirstGuideCircleNotSet_FailsFast_WithVisibleError()
    {
        var result = new CircleCircleMeasureTool().Run(new CircleCircleMeasureParameters());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("卡尺1", result.ErrorMessage);
        Assert.Contains("引导圆未设置或半径无效", result.ErrorMessage);
    }

    [Fact]
    public void Run_SecondGuideCircleNotSet_FailsFast_WithVisibleError()
    {
        var parameters = new CircleCircleMeasureParameters();
        parameters.E1CenterX.Value = 100;
        parameters.E1CenterY.Value = 100;
        parameters.E1Radius.Value = 50;

        var result = new CircleCircleMeasureTool().Run(parameters);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("卡尺2", result.ErrorMessage);
        Assert.Contains("引导圆未设置或半径无效", result.ErrorMessage);
    }

    #endregion
}
