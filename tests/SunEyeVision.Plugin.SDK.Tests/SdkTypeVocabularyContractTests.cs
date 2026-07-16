using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 全局变量类型映射 + 序列化往返的契约测试。
/// 覆盖方案验收标准：数值归并（int/float → long/double）、
/// 7 核心概念映射、AffineMatrix→Transform、IVisionRegion→Region、
/// 以及已删除类型（Byte/几何）的 fail-fast 行为。
/// </summary>
public class SdkTypeVocabularyContractTests
{
    #region 契约：单值与数组返回同一类型名

    [Theory]
    [InlineData(typeof(int), "Integer")]
    [InlineData(typeof(int[]), "Integer")]
    [InlineData(typeof(long), "Integer")]
    [InlineData(typeof(long[]), "Integer")]
    [InlineData(typeof(float), "Double")]
    [InlineData(typeof(float[]), "Double")]
    [InlineData(typeof(double), "Double")]
    [InlineData(typeof(double[]), "Double")]
    [InlineData(typeof(string), "String")]
    [InlineData(typeof(string[]), "String")]
    [InlineData(typeof(bool), "Boolean")]
    [InlineData(typeof(bool[]), "Boolean")]
    [InlineData(typeof(AffineMatrix), "Transform")]
    [InlineData(typeof(AffineMatrix[]), "Transform")]
    [InlineData(typeof(IVisionImage), "Image")]
    [InlineData(typeof(IVisionRegion), "Region")]
    public void ToTypeVocabulary_SingleAndArray_ReturnSameName(Type type, string expected)
    {
        var actual = SdkTypeVocabulary.ToTypeVocabulary(type);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToTypeVocabulary_Null_ReturnsNull()
    {
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(null));
    }

    #endregion

    #region 契约：已删除类型 fail-fast（rule-015 不静默）

    [Theory]
    [InlineData("Byte")]
    [InlineData("Point")]
    [InlineData("Size")]
    [InlineData("Circle")]
    [InlineData("Line")]
    [InlineData("Annulus")]
    [InlineData("RotatedRect")]
    [InlineData("Polygon")]
    [InlineData("Arc")]
    public void ToSystemType_DeletedTypes_ReturnNull(string deletedType)
    {
        // 已删除的几何/Byte 类型反向解析返回 null → IsValidType 判定无效，加载时 fail-fast 可见。
        Assert.Null(SdkTypeVocabulary.ToSystemType(deletedType));
    }

    [Fact]
    public void GetAllTypeOptions_OnlySevenCoreConcepts()
    {
        var options = SdkTypeVocabulary.GetAllTypeOptions();
        Assert.Equal(7, options.Count);
        var values = options.Select(o => o.Value).ToList();
        Assert.Contains("Integer", values);
        Assert.Contains("Double", values);
        Assert.Contains("String", values);
        Assert.Contains("Boolean", values);
        Assert.Contains("Image", values);
        Assert.Contains("Region", values);
        Assert.Contains("Transform", values);
    }

    #endregion

    #region 数据一致性：序列化/反序列化往返一致

    private class ValueHolder
    {
        [JsonConverter(typeof(GlobalVariableValueConverter))]
        public object? Value { get; set; }
    }

    private static object? RoundTrip(object? value)
    {
        var options = new JsonSerializerOptions();
        var json = JsonSerializer.Serialize(new ValueHolder { Value = value }, options);
        var back = JsonSerializer.Deserialize<ValueHolder>(json, options);
        Assert.NotNull(back);
        return back!.Value;
    }

    [Fact]
    public void RoundTrip_IntScalar_NormalizedToLong()
    {
        // int 值序列化为 "Integer" 后重新加载为 long（数值归并）。
        var result = RoundTrip(42);
        Assert.IsType<long>(result);
        Assert.Equal(42L, (long)result!);
    }

    [Fact]
    public void RoundTrip_IntArray_NormalizedToLongArray()
    {
        var input = new int[] { 10, 20, 30 };
        var result = RoundTrip(input);
        Assert.IsType<long[]>(result);
        Assert.Equal(new long[] { 10, 20, 30 }, (long[])result!);
    }

    [Fact]
    public void RoundTrip_LongScalar_PreservesValue()
    {
        var result = RoundTrip(12345678901L);
        Assert.IsType<long>(result);
        Assert.Equal(12345678901L, (long)result!);
    }

    [Fact]
    public void RoundTrip_DoubleScalar_PreservesValue()
    {
        var result = RoundTrip(3.14);
        Assert.IsType<double>(result);
        Assert.Equal(3.14, (double)result!);
    }

    [Fact]
    public void RoundTrip_DoubleArray_PreservesArray()
    {
        // 回归验证：改动后 "Double" 类型名同时承载标量/数组。
        var input = new double[] { 1.0, 2.0, 3.5 };
        var result = RoundTrip(input);
        Assert.IsType<double[]>(result);
        Assert.Equal(input, (double[])result!);
    }

    [Fact]
    public void RoundTrip_FloatScalar_NormalizedToDouble()
    {
        var result = RoundTrip(2.5f);
        Assert.IsType<double>(result);
        Assert.Equal(2.5, (double)result!);
    }

    [Fact]
    public void RoundTrip_AffineMatrix_PreservesValue()
    {
        var input = new AffineMatrix(1, 0, 0, 1, 10.5, -20.25);
        var result = RoundTrip(input);
        Assert.IsType<AffineMatrix>(result);
        var m = (AffineMatrix)result!;
        Assert.Equal(1.0, m.M11);
        Assert.Equal(10.5, m.OffsetX);
        Assert.Equal(-20.25, m.OffsetY);
    }

    [Fact]
    public void RoundTrip_LegacyDoubleArrayName_PreservesArray()
    {
        // 存量兼容：历史 JSON 使用 "Double[]" 类型名，反序列化不应静默丢失。
        const string legacyJson = "{\"Value\":{\"Type\":\"Double[]\",\"Value\":[1.0,2.0,3.0]}}";
        var back = JsonSerializer.Deserialize<ValueHolder>(legacyJson);
        Assert.NotNull(back);
        Assert.IsType<double[]>(back!.Value);
        Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, (double[])back.Value!);
    }

    #endregion
}
