using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using Xunit;

namespace SunEyeVision.Tool.LineLineMeasure.Tests;

/// <summary>
/// 线-线测量工具契约测试（v4 P2 定范式工具）。
/// 覆盖三条防御线：
///   1. 参数序列化往返（rule-010）：ParamValue 值 / 绑定配置 / 判定规格 保存→加载不丢失；
///   2. 工具契约：IToolPlugin&lt;TParams, TResult&gt; 泛型实参必须等于本工具参数/结果类型；
///   3. Fail-Fast（rule-015）：引导直线未设置时直接报错可见，禁止静默出结果。
/// </summary>
public class LineLineMeasureContractTests
{
    private static JsonSerializerOptions Options()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(LineLineMeasureParameters), "LineLineMeasure");
        return ParameterTypeRegistry.SerializationOptions;
    }

    /// <summary>构造两条有效引导直线的参数（长度均 100px）。</summary>
    private static LineLineMeasureParameters CreateValidGuideParameters()
    {
        var parameters = new LineLineMeasureParameters();
        parameters.E1StartX = 10;
        parameters.E1StartY = 20;
        parameters.E1EndX = 110;
        parameters.E1EndY = 20;
        parameters.E2StartX = 10;
        parameters.E2StartY = 60;
        parameters.E2EndX = 110;
        parameters.E2EndY = 60;
        return parameters;
    }

    #region 序列化往返（rule-010）

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesParamValuesAndJudgments()
    {
        var options = Options();
        var source = CreateValidGuideParameters();
        source.E1Threshold = 45.5;
        source.E1NumMeasures = 12;
        source.E1Transition = EdgeTransition.Positive;
        source.E1Select = EdgeSelect.First;
        source.ResultPresentation.Judgments.Add(new JudgmentConfigItem
        {
            Key = "test-custom",
            Name = "夹角",
            Enabled = true,
            Min = -1.0,
            Max = 1.0,
            Unit = "°",
        });

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<LineLineMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(10.0, back!.E1StartX, 6);
        Assert.Equal(20.0, back.E1StartY, 6);
        Assert.Equal(110.0, back.E1EndX, 6);
        Assert.Equal(45.5, back.E1Threshold, 6);
        Assert.Equal(12, back.E1NumMeasures);
        Assert.Equal(EdgeTransition.Positive, back.E1Transition);
        Assert.Equal(EdgeSelect.First, back.E1Select);

        // 默认判定项（角度/距离/交点X/交点Y）保留 + 新增项完整还原
        Assert.Equal(source.ResultPresentation.Judgments.Count, back.ResultPresentation.Judgments.Count);
        var custom = back.ResultPresentation.Judgments.Single(j => j.Name == "夹角");
        Assert.True(custom.Enabled);
        Assert.Equal(-1.0, custom.Min, 6);
        Assert.Equal(1.0, custom.Max, 6);
        Assert.Equal("°", custom.Unit);
    }

    [Fact]
    public void Parameters_PolymorphicRoundTrip_AsToolParameters_RestoresConcreteType()
    {
        var options = Options();
        var json = JsonSerializer.Serialize(CreateValidGuideParameters(), options);

        Assert.Contains("\"$type\": \"LineLineMeasure\"", json);

        var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

        Assert.IsType<LineLineMeasureParameters>(asBase);
    }

    [Fact]
    public void Parameters_RoundTrip_PreservesPureClrValues()
    {
        var options = Options();
        var source = new LineLineMeasureParameters();
        source.E1StartX = 12.5;
        source.E1EndY = 48.25;

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<LineLineMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(12.5, back!.E1StartX);
        Assert.Equal(48.25, back.E1EndY);
    }

    #endregion

    #region 工具契约

    [Fact]
    public void Tool_ImplementsTypedPluginContract_ForItsOwnParameterAndResultTypes()
    {
        var contract = typeof(LineLineMeasureTool).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IToolPlugin<,>));

        Assert.Equal(typeof(LineLineMeasureParameters), contract.GetGenericArguments()[0]);
        Assert.Equal(typeof(LineLineMeasureResults), contract.GetGenericArguments()[1]);
    }

    #endregion

    #region Fail-Fast（rule-015）

    [Fact]
    public void Run_FirstGuideLineNotSet_FailsFast_WithVisibleError()
    {
        var result = new LineLineMeasureTool().Run(new LineLineMeasureParameters());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("卡尺1", result.ErrorMessage);
        Assert.Contains("引导直线未设置或长度无效", result.ErrorMessage);
    }

    [Fact]
    public void Run_SecondGuideLineNotSet_FailsFast_WithVisibleError()
    {
        var parameters = new LineLineMeasureParameters();
        parameters.E1StartX = 10;
        parameters.E1StartY = 20;
        parameters.E1EndX = 110;
        parameters.E1EndY = 20;

        var result = new LineLineMeasureTool().Run(parameters);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("卡尺2", result.ErrorMessage);
        Assert.Contains("引导直线未设置或长度无效", result.ErrorMessage);
    }

    #endregion
}
