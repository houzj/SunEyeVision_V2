using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using Xunit;

namespace SunEyeVision.Tool.PointLineMeasure.Tests;

/// <summary>
/// 点-线测量工具契约测试（v4 P2：点元素仅参数订阅，无绘制入口）。
/// 覆盖：
///   1. 参数序列化往返（rule-010）；
///   2. 工具泛型契约；
///   3. Fail-Fast（rule-015）：点元素「按图形」未绑定上游时必须报错可见（禁止静默出结果）。
/// </summary>
public class PointLineMeasureContractTests
{
    private static JsonSerializerOptions Options()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(PointLineMeasureParameters), "PointLineMeasure");
        return ParameterTypeRegistry.SerializationOptions;
    }

    private static PointLineMeasureParameters CreateByCoordinateParameters()
    {
        var parameters = new PointLineMeasureParameters
        {
            P1InputMode = PointInputMode.ByCoordinate,
        };
        parameters.P1X.Value = 150;
        parameters.P1Y.Value = 180;
        return parameters;
    }

    #region 序列化往返（rule-010）

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesParamValuesAndJudgments()
    {
        var options = Options();
        var source = CreateByCoordinateParameters();
        source.E2Threshold.Value = 42.5;
        source.E2NumMeasures.Value = 20;
        source.E2Transition.Value = EdgeTransition.Negative;
        source.E2Select.Value = EdgeSelect.Last;
        source.Judgments.Add(new MeasureThreshold
        {
            Name = "点线距",
            Enabled = true,
            Min = 0.0,
            Max = 200.0,
            Unit = "px",
        });

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<PointLineMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(PointInputMode.ByCoordinate, back!.P1InputMode);
        Assert.Equal(150.0, back.P1X.Value, 6);
        Assert.Equal(180.0, back.P1Y.Value, 6);
        Assert.Equal(42.5, back.E2Threshold.Value, 6);
        Assert.Equal(20, back.E2NumMeasures.Value);
        Assert.Equal(EdgeTransition.Negative, back.E2Transition.Value);
        Assert.Equal(EdgeSelect.Last, back.E2Select.Value);

        // 默认判定项（距离/垂足X/垂足Y）保留 + 新增项完整还原
        Assert.Equal(source.Judgments.Count, back.Judgments.Count);
        var custom = back.Judgments.Single(j => j.Name == "点线距");
        Assert.True(custom.Enabled);
        Assert.Equal(0.0, custom.Min, 6);
        Assert.Equal(200.0, custom.Max, 6);
    }

    [Fact]
    public void Parameters_PolymorphicRoundTrip_AsToolParameters_RestoresConcreteType()
    {
        var options = Options();
        var json = JsonSerializer.Serialize(CreateByCoordinateParameters(), options);

        Assert.Contains("\"$type\": \"PointLineMeasure\"", json);

        var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

        Assert.IsType<PointLineMeasureParameters>(asBase);
    }

    #endregion

    #region 工具契约

    [Fact]
    public void Tool_ImplementsTypedPluginContract_ForItsOwnParameterAndResultTypes()
    {
        var contract = typeof(PointLineMeasureTool).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IToolPlugin<,>));

        Assert.Equal(typeof(PointLineMeasureParameters), contract.GetGenericArguments()[0]);
        Assert.Equal(typeof(PointLineMeasureResults), contract.GetGenericArguments()[1]);
    }

    #endregion

    #region Fail-Fast（rule-015）

    [Fact]
    public void Run_PointByShapeNotBound_FailsFast_WithActionableError()
    {
        // 默认 P1InputMode = ByShape，且未绑定上游点图形 → 必须直接报错，禁止静默出结果
        var result = new PointLineMeasureTool().Run(new PointLineMeasureParameters());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("点1", result.ErrorMessage);
        Assert.Contains("未绑定上游点图形输出", result.ErrorMessage);
        Assert.Contains("切换为「按参数」", result.ErrorMessage);
    }

    [Fact]
    public void Run_PointByCoordinate_ThenImageMissing_FailsFast_WithImageError()
    {
        // 点坐标解析通过后，图像缺失必须先于 measure 报错（校验顺序契约）
        var result = new PointLineMeasureTool().Run(CreateByCoordinateParameters());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.NotNull(result.ErrorMessage);
        // 核心契约：图像缺失必须 Fail-Fast（报错可见、不出结果）。
        // 缺陷记录 P2-1：HalconToolHelpers.GetHalconImage 在图像为 null 时先抛英文
        // "Input is not Halcon HImage, type: null"，使工具内「输入图像为空或无效」守卫成为不可达死代码。
        Assert.Contains("测量失败:", result.ErrorMessage);
    }

    #endregion
}
