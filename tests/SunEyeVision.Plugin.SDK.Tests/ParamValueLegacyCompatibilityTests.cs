using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 参数值【旧格式兼容】契约测试：数组参数必须能读入老工程的**标量**写法（回归守卫）。
/// </summary>
/// <remarks>
/// 背景：点参数由 <c>ParamValue&lt;Point2d&gt;</c> 改为 <c>ParamValue&lt;Point2d[]&gt;</c> 后，
/// 老工程里 <c>P1Point.Value</c> 仍是标量对象 —— 若无兼容读取，加载解决方案会直接失败
/// （真实事故：`could not be converted to Point2d[]`）。本测试确保该形态永远可读。
/// </remarks>
public class ParamValueLegacyCompatibilityTests
{
    private static readonly JsonSerializerOptions Options = new();

    [Fact]
    public void ArrayParam_LegacyScalarJson_IsWrappedIntoLengthOneArray()
    {
        // 用真实序列化器产出"点"的标量 JSON 形状，避免手写形状与实现漂移
        var scalarPointJson = JsonSerializer.Serialize(new Point2d(7.5, 8.5), Options);
        var legacyJson = $"{{\"Value\":{scalarPointJson}}}";

        var back = JsonSerializer.Deserialize<ParamValue<Point2d[]>>(legacyJson, Options);

        Assert.NotNull(back);
        var pts = Assert.IsType<Point2d[]>(back!.Value);
        Assert.Single(pts);
        Assert.Equal(7.5, pts[0].X, 6);
        Assert.Equal(8.5, pts[0].Y, 6);
    }

    [Fact]
    public void ArrayParam_ArrayJson_IsReadAsIs()
    {
        // 新格式（数组）读取不受影响
        var arrayJson = $"{{\"Value\":{JsonSerializer.Serialize(new[] { new Point2d(1, 2), new Point2d(3, 4) }, Options)}}}";

        var back = JsonSerializer.Deserialize<ParamValue<Point2d[]>>(arrayJson, Options);

        Assert.NotNull(back);
        var pts = Assert.IsType<Point2d[]>(back!.Value);
        Assert.Equal(2, pts.Length);
        Assert.Equal(3, pts[1].X, 6);
    }

    [Fact]
    public void ArrayParam_Write_AlwaysEmitsArray()
    {
        // 写出统一为数组（单值 = 长度 1 数组，决策 5）
        var value = ParamValue<Point2d[]>.CreateConstant(new[] { new Point2d(5, 6) }, "P");

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Contains("[", json, System.StringComparison.Ordinal);
        Assert.Contains("5", json, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarParam_BehaviorUnchanged()
    {
        // 非数组参数（double）行为零变化（工厂对非数组委托默认转换器）
        var value = ParamValue<double>.CreateConstant(128.5, "T");

        var json = JsonSerializer.Serialize(value, Options);
        var back = JsonSerializer.Deserialize<ParamValue<double>>(json, Options);

        Assert.NotNull(back);
        Assert.Equal(128.5, back!.Value, 6);
    }
}
