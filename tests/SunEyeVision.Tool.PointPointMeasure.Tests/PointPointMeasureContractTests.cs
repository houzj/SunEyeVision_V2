using System;
using System.Linq;
using System.Text.Json;
using NSubstitute;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Tool.PointPointMeasure.Tests;

/// <summary>
/// 点-点测量工具契约测试（v4 P2：纯订阅双点 + 纯几何直算，最后落地）。
/// 覆盖：
///   1. 参数序列化往返（rule-010），含 Point2d 常量参数；
///   2. 工具泛型契约；
///   3. 双点坐标直算（点距 / ΔX / ΔY / 连线角度）；
///   4. 判定 OK/NG（MeasureJudge 聚合，rule-015：无值即 NG）；
///   5. Fail-Fast：点元素「按图形」未绑定上游时必须报错可见。
/// </summary>
public class PointPointMeasureContractTests
{
    private static JsonSerializerOptions Options()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(PointPointMeasureParameters), "PointPointMeasure");
        return ParameterTypeRegistry.SerializationOptions;
    }

    /// <summary>构造非空图像桩（点-点工具的图像校验在几何计算之前）。</summary>
    private static IVisionImage CreateNonEmptyImage()
    {
        var image = Substitute.For<IVisionImage>();
        image.IsEmpty.Returns(false);
        return image;
    }

    private static PointPointMeasureParameters CreateByCoordinateParameters(double x2, double y2)
    {
        var parameters = new PointPointMeasureParameters
        {
            P1InputMode = PointInputMode.ByCoordinate,
            P2InputMode = PointInputMode.ByCoordinate,
            ResolvedInputImage = CreateNonEmptyImage(),
        };
        parameters.P1X.Value = 0;
        parameters.P1Y.Value = 0;
        parameters.P2X.Value = x2;
        parameters.P2Y.Value = y2;
        return parameters;
    }

    /// <summary>改写默认「点距」判定项的上下限（默认已启用，避免重复追加同名项）。</summary>
    private static void SetPointDistanceTolerance(PointPointMeasureParameters parameters, double min, double max)
    {
        var pointDistance = parameters.ResultPresentation.Judgments.Single(j => j.Name == "点距");
        pointDistance.Enabled = true;
        pointDistance.Min = min;
        pointDistance.Max = max;
    }

    #region 序列化往返（rule-010）

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesParamValuesAndJudgments()
    {
        var options = Options();
        var source = CreateByCoordinateParameters(3, 4);
        source.ResultPresentation.Judgments.Add(new JudgmentConfigItem
        {
            Key = "test-custom",
            Name = "点距校验",
            Enabled = true,
            Min = 4.9,
            Max = 5.1,
            Unit = "px",
        });

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<PointPointMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(PointInputMode.ByCoordinate, back!.P1InputMode);
        Assert.Equal(PointInputMode.ByCoordinate, back.P2InputMode);
        Assert.Equal(0.0, back.P1X.Value, 6);
        Assert.Equal(3.0, back.P2X.Value, 6);
        Assert.Equal(4.0, back.P2Y.Value, 6);

        // 默认判定项（点距/ΔX/ΔY/连线角度）保留 + 新增项完整还原
        Assert.Equal(source.ResultPresentation.Judgments.Count, back.ResultPresentation.Judgments.Count);
        var custom = back.ResultPresentation.Judgments.Single(j => j.Name == "点距校验");
        Assert.True(custom.Enabled);
        Assert.Equal(4.9, custom.Min, 6);
        Assert.Equal(5.1, custom.Max, 6);
        Assert.Equal("px", custom.Unit);
    }

    [Fact]
    public void Parameters_JsonRoundTrip_PreservesPoint2dArrayConstant()
    {
        // 6b：点参数为**数组口子**（决策 4）——常量往返须保真（长度与元素不丢）
        var options = Options();
        var source = new PointPointMeasureParameters
        {
            P1InputMode = PointInputMode.ByShape,
            P1Point = ParamValue<Point2d[]>.CreateConstant(new[] { new Point2d(7.5, 8.5) }, "P1Point"),
        };

        var json = JsonSerializer.Serialize(source, options);
        var back = JsonSerializer.Deserialize<PointPointMeasureParameters>(json, options);

        Assert.NotNull(back);
        Assert.Equal(PointInputMode.ByShape, back!.P1InputMode);
        var pts = Assert.IsType<Point2d[]>(back.P1Point.Value);
        Assert.Single(pts);
        Assert.Equal(7.5, pts[0].X, 6);
        Assert.Equal(8.5, pts[0].Y, 6);
        Assert.False(back.P1Point.IsBinding);
    }

    [Fact]
    public void Parameters_PolymorphicRoundTrip_AsToolParameters_RestoresConcreteType()
    {
        var options = Options();
        var json = JsonSerializer.Serialize(CreateByCoordinateParameters(3, 4), options);

        Assert.Contains("\"$type\": \"PointPointMeasure\"", json);

        var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

        Assert.IsType<PointPointMeasureParameters>(asBase);
    }

    #endregion

    #region 工具契约

    [Fact]
    public void Tool_ImplementsTypedPluginContract_ForItsOwnParameterAndResultTypes()
    {
        var contract = typeof(PointPointMeasureTool).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IToolPlugin<,>));

        Assert.Equal(typeof(PointPointMeasureParameters), contract.GetGenericArguments()[0]);
        Assert.Equal(typeof(PointPointMeasureResults), contract.GetGenericArguments()[1]);
    }

    #endregion

    #region 几何直算 + 判定（rule-015）

    [Fact]
    public void Run_ByCoordinate_ComputesDistanceDeltasAndAngle_WithinTolerance_Passes()
    {
        var parameters = CreateByCoordinateParameters(3, 4);
        SetPointDistanceTolerance(parameters, 4.9, 5.1);

        var result = new PointPointMeasureTool().Run(parameters);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.0, result.Point1X, 6);
        Assert.Equal(0.0, result.Point1Y, 6);
        Assert.Equal(3.0, result.Point2X, 6);
        Assert.Equal(4.0, result.Point2Y, 6);
        Assert.Equal(5.0, result.Distance, 6);
        Assert.Equal(3.0, result.DeltaX, 6);
        Assert.Equal(4.0, result.DeltaY, 6);
        Assert.Equal(53.130102, result.AngleDeg, 4);
        Assert.True(result.IsPass);
        Assert.Equal(parameters.ResultPresentation.Judgments.Count, result.JudgmentDetails.Length);
    }

    [Fact]
    public void Run_ByCoordinate_OutOfTolerance_ExecutesButJudgesNg()
    {
        var parameters = CreateByCoordinateParameters(3, 4);
        SetPointDistanceTolerance(parameters, 5.5, 6.5);

        var result = new PointPointMeasureTool().Run(parameters);

        Assert.True(result.IsSuccess);
        Assert.Equal(5.0, result.Distance, 6);
        Assert.False(result.IsPass);
        Assert.Equal(parameters.ResultPresentation.Judgments.Count, result.JudgmentDetails.Length);
    }

    [Fact]
    public void Run_JudgmentWithoutMeasuredValue_JudgesNg()
    {
        // rule-015：判定项启用但无对应实测值 → NG（禁止静默跳过）
        var parameters = CreateByCoordinateParameters(3, 4);
        parameters.ResultPresentation.Judgments.Add(new JudgmentConfigItem
        {
            Key = "test-custom",
            Name = "未知几何量",
            Enabled = true,
            Min = 0.0,
            Max = 100.0,
        });

        var result = new PointPointMeasureTool().Run(parameters);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.Equal(parameters.ResultPresentation.Judgments.Count, result.JudgmentDetails.Length);
    }

    #endregion

    #region Fail-Fast（rule-015）

    [Fact]
    public void Run_FirstPointByShapeNotBound_FailsFast_WithActionableError()
    {
        var parameters = new PointPointMeasureParameters
        {
            ResolvedInputImage = CreateNonEmptyImage(),
        };

        var result = new PointPointMeasureTool().Run(parameters);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPass);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("点1", result.ErrorMessage);
        Assert.Contains("未绑定上游点图形输出", result.ErrorMessage);
        Assert.Contains("切换为「按参数」", result.ErrorMessage);
    }

    [Fact]
    public void Run_SecondPointByShapeNotBound_FailsFast_WithActionableError()
    {
        var parameters = CreateByCoordinateParameters(3, 4);
        parameters.P2InputMode = PointInputMode.ByShape;

        var result = new PointPointMeasureTool().Run(parameters);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("点2", result.ErrorMessage);
        Assert.Contains("未绑定上游点图形输出", result.ErrorMessage);
    }

    [Fact]
    public void Run_ImageMissing_FailsFast_WithVisibleError()
    {
        var parameters = CreateByCoordinateParameters(3, 4);
        parameters.ResolvedInputImage = null;

        var result = new PointPointMeasureTool().Run(parameters);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        // 核心契约：图像缺失必须 Fail-Fast（报错可见、不出结果）。
        // 缺陷记录 P2-1：HalconToolHelpers.GetHalconImage 在图像为 null 时先抛英文
        // "Input is not Halcon HImage, type: null"，使工具内「输入图像为空或无效」守卫成为不可达死代码。
        Assert.Contains("测量失败:", result.ErrorMessage);
    }

    [Fact]
    public async System.Threading.Tasks.Task ExecuteAsync_ReadsImageFromRequest_WithoutMutatingParameters()
    {
        var parameters = CreateByCoordinateParameters(3, 4);
        parameters.ResolvedInputImage = null;
        var request = new ToolExecutionRequest(parameters)
        {
            ResolvedInputImage = CreateNonEmptyImage(),
        };

        var result = await new PointPointMeasureTool().ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(parameters.ResolvedInputImage);
        Assert.Equal(5.0, Assert.IsType<PointPointMeasureResults>(result).Distance, 6);
    }

    #endregion
}
