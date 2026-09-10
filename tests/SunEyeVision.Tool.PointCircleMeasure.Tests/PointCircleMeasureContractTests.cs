using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using Xunit;

namespace SunEyeVision.Tool.PointCircleMeasure.Tests;

/// <summary>
/// 点-圆测量工具契约测试（v4 P2：点元素仅参数订阅，无绘制入口）。
/// 覆盖：
///   1. 参数序列化往返（rule-010），含 Point2d 常量参数（验证 ParamSetting 常量类型还原）；
///   2. 工具泛型契约；
///   3. Fail-Fast（rule-015）：点元素「按图形」未绑定上游时必须报错可见。
/// </summary>
public class PointCircleMeasureContractTests
{
    private static JsonSerializerOptions Options()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(PointCircleMeasureParameters), "PointCircleMeasure");
        return ParameterTypeRegistry.SerializationOptions;
    }

    private static PointCircleMeasureParameters CreateByCoordinateParameters()
    {
        var parameters = new PointCircleMeasureParameters
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
        source.E2NumMeasures.Value = 28;
        source.E2Transition.Value = EdgeTransition.Positive;
        source.E2Select.Value = EdgeSelect.First;
        source.Judgments.Add(new MeasureThreshold
        {
            Name = "点圆距",
            Enabled = true,
            Min = 0.0,
            Max = 300.0,
            Unit = "px",
        });

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<PointCircleMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(PointInputMode.ByCoordinate, back!.P1InputMode);
        Assert.Equal(150.0, back.P1X.Value, 6);
        Assert.Equal(180.0, back.P1Y.Value, 6);
        Assert.Equal(42.5, back.E2Threshold.Value, 6);
        Assert.Equal(28, back.E2NumMeasures.Value);
        Assert.Equal(EdgeTransition.Positive, back.E2Transition.Value);
        Assert.Equal(EdgeSelect.First, back.E2Select.Value);

        // 默认判定项（圆心距/圆周距/半径/最近点X/最近点Y）保留 + 新增项完整还原
        Assert.Equal(source.Judgments.Count, back.Judgments.Count);
        var custom = back.Judgments.Single(j => j.Name == "点圆距");
        Assert.True(custom.Enabled);
        Assert.Equal(0.0, custom.Min, 6);
        Assert.Equal(300.0, custom.Max, 6);
    }

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesPoint2dConstant()
    {
        var options = Options();
        var source = new PointCircleMeasureParameters
        {
            P1InputMode = PointInputMode.ByShape,
            P1Point = ParamValue<Point2d[]>.CreateConstant(new[] { new Point2d(12.5, 34.5) }, "P1Point"),
        };

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<PointCircleMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(PointInputMode.ByShape, back!.P1InputMode);
        Assert.Single(back.P1Point.Value);
        Assert.Equal(12.5, back.P1Point.Value[0].X, 6);
        Assert.Equal(34.5, back.P1Point.Value[0].Y, 6);
        Assert.False(back.P1Point.IsBinding);
    }

    [Fact]
    public void Parameters_PolymorphicRoundTrip_AsToolParameters_RestoresConcreteType()
    {
        var options = Options();
        var json = JsonSerializer.Serialize(CreateByCoordinateParameters(), options);

        Assert.Contains("\"$type\": \"PointCircleMeasure\"", json);

        var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

        Assert.IsType<PointCircleMeasureParameters>(asBase);
    }

    #endregion

    #region 工具契约

    [Fact]
    public void Tool_ImplementsTypedPluginContract_ForItsOwnParameterAndResultTypes()
    {
        var contract = typeof(PointCircleMeasureTool).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IToolPlugin<,>));

        Assert.Equal(typeof(PointCircleMeasureParameters), contract.GetGenericArguments()[0]);
        Assert.Equal(typeof(PointCircleMeasureResults), contract.GetGenericArguments()[1]);
    }

    #endregion

    #region Fail-Fast（rule-015）

    [Fact]
    public void Run_PointByShapeNotBound_FailsFast_WithActionableError()
    {
        // 默认 P1InputMode = ByShape，且未绑定上游点图形 → 必须直接报错，禁止静默出结果
        var result = new PointCircleMeasureTool().Run(new PointCircleMeasureParameters());

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
        var result = new PointCircleMeasureTool().Run(CreateByCoordinateParameters());

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
