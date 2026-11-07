using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Editing.Wpf.Models;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Results;
using SunEyeVision.Plugin.Contracts.Vision;
using SunEyeVision.Plugin.Editing.Wpf.Models.Geometry;
using Xunit;

using SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;
namespace SunEyeVision.Plugin.Editing.Wpf.Tests;

/// <summary>
/// 全局变量类型映射 + 序列化往返的契约测试。
/// 覆盖方案验收标准：数值归并（int/float → long/double）、
/// 7 核心概念映射（Region 已移出词汇表，禁止进入工作流契约）、AffineMatrix→Transform、
/// 以及已删除类型（Byte/几何/Region）的 fail-fast 行为。
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
    [InlineData(typeof(RoiShape), "ROI")]
    [InlineData(typeof(RoiShape[]), "ROI")]
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

    #region 契约：未注册类型 fail-fast（rule-015 不静默）

    [Theory]
    [InlineData("Byte")]
    [InlineData("Size")]
    public void ToSystemType_DeletedTypes_ReturnNull(string deletedType)
    {
        // 已删除/未注册的类型（Byte/Size/Region）反向解析返回 null
        // → IsValidType 判定无效，加载时 fail-fast 可见。
        // 注意：Arc 已随 Arc2d 恢复进契约（正/反向映射均含），不属于删除类型，故不在此列。
        Assert.Null(SdkTypeVocabulary.ToSystemType(deletedType));
    }

    [Fact]
    public void GetAllTypeOptions_AllRegisteredConcepts()
    {
        // 7个基础概念 + 7个几何图形 + 独立Binary载荷；旧Byte标量不恢复。
        var options = SdkTypeVocabulary.GetAllTypeOptions();
        Assert.Equal(15, options.Count);
        var values = options.Select(o => o.Value).ToList();
        Assert.Contains("Integer", values);
        Assert.Contains("Double", values);
        Assert.Contains("String", values);
        Assert.Contains("Boolean", values);
        Assert.Contains("Binary", values);
        Assert.Contains("Image", values);
        Assert.Contains("ROI", values);
        Assert.Contains("Transform", values);
        Assert.Contains("Circle", values);
        Assert.Contains("RotatedRect", values);
        Assert.Contains("Point", values);
        Assert.Contains("Polygon", values);
        Assert.Contains("Line", values);
        Assert.Contains("Annulus", values);
        Assert.Contains("Arc", values);
        Assert.DoesNotContain("Region", values);
    }

    #endregion

    #region 数据一致性：序列化/反序列化往返一致

    private class ValueHolder
    {
        public object? Value { get; set; }
    }

    private static object? RoundTrip(object? value)
    {
        using var codec = new VariableCodecScope(typeof(ValueHolder));
        var options = codec.Options;
        var json = JsonSerializer.Serialize(new ValueHolder { Value = value }, options);
        var back = JsonSerializer.Deserialize<ValueHolder>(json, options);
        Assert.NotNull(back);
        return back!.Value;
    }

    [Fact]
    public void RoundTrip_IntScalar_NormalizedToLongScalar()
    {
        var result = RoundTrip(42);
        Assert.Equal(42L, Assert.IsType<long>(result));
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
    public void RoundTrip_LongScalar_PreservesScalar()
    {
        var result = RoundTrip(12345678901L);
        Assert.Equal(12345678901L, Assert.IsType<long>(result));
    }

    [Fact]
    public void RoundTrip_DoubleScalar_PreservesScalar()
    {
        var result = RoundTrip(3.14);
        Assert.Equal(3.14, Assert.IsType<double>(result));
    }

    [Fact]
    public void RoundTrip_DoubleArray_PreservesArray()
    {
        // 回归验证：数组形态原样保留。
        var input = new double[] { 1.0, 2.0, 3.5 };
        var result = RoundTrip(input);
        Assert.IsType<double[]>(result);
        Assert.Equal(input, (double[])result!);
    }

    [Fact]
    public void RoundTrip_FloatScalar_NormalizedToDoubleScalar()
    {
        var result = RoundTrip(2.5f);
        Assert.Equal(2.5, Assert.IsType<double>(result));
    }

    [Fact]
    public void SerializeValue_ScalarDouble_StoredAsScalarJson()
    {
        using var codec = new VariableCodecScope(typeof(ValueHolder));
        var json = JsonSerializer.Serialize(new ValueHolder { Value = 3.14 }, codec.Options);
        Assert.Contains("\"Value\":3.14", json);
    }

    [Fact]
    public void Declared_scalar_json_preserves_its_value_and_shape()
    {
        const string scalarJson = "{\"Value\":{\"Type\":\"Double\",\"Value\":3.14}}";
        using var codec = new VariableCodecScope(typeof(ValueHolder));
        var restored=JsonSerializer.Deserialize<ValueHolder>(scalarJson, codec.Options)!;
        Assert.Equal(3.14,Assert.IsType<double>(restored.Value));
    }

    #endregion

    #region 契约：全局变量闸门（Region 禁止 / RoI 允许，方案步骤2 + 验收 #7）

    #endregion

    #region 契约：方案扩展（RoI 词目 + Region 数组 + tuple 原则）

    [Fact]
    public void ToSystemType_Region_RemovedFromVocabulary()
    {
        // 方案 D8：Region 已移出词汇表，反向解析返回 null（禁止进入工作流契约）
        Assert.Null(SdkTypeVocabulary.ToSystemType("Region"));
    }

    [Fact]
    public void ToSystemType_RoI_ReturnsRoiShapeArray()
    {
        Assert.Equal(typeof(RoiShape[]), SdkTypeVocabulary.ToSystemType("ROI"));
    }

    [Fact]
    public void ToTypeVocabulary_RegionArray_ReturnsNull()
    {
        // 方案 D8：Region 已移出词汇表，正向解析返回 null
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionRegion[])));
    }

    [Fact]
    public void ToTypeVocabulary_RoIArray_ReturnsRoI()
    {
        Assert.Equal("ROI", SdkTypeVocabulary.ToTypeVocabulary(typeof(RoiShape[])));
    }

    [Fact]
    public void TupleSemantics_ScalarAndArray_ShareVocabulary()
    {
        // 〇 原则1：标量 = 长度 1 的 tuple，与数组共享同一词目
        Assert.Equal("Double", SdkTypeVocabulary.ToTypeVocabulary(typeof(double)));
        Assert.Equal("Double", SdkTypeVocabulary.ToTypeVocabulary(typeof(double[])));
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionRegion)));
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionRegion[])));
    }

    [Fact]
    public void ForwardTypeToString_HasNoScalarDoubleKey()
    {
        // 〇 原则1：正向字典仅数组(tuple)词目，无独立单值 double 键
        var dict = (System.Collections.Generic.Dictionary<Type, string>)
            typeof(SdkTypeVocabulary)
                .GetField("TypeToString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;
        Assert.False(dict.ContainsKey(typeof(double)));
        Assert.True(dict.ContainsKey(typeof(double[])));
        Assert.False(dict.ContainsKey(typeof(IVisionRegion[])));
    }

    [Fact]
    public void ToSystemType_Double_ScalarAndArray()
    {
        Assert.Equal(typeof(double), SdkTypeVocabulary.ToSystemType("Double"));
        Assert.Equal(typeof(double[]), SdkTypeVocabulary.ToSystemType("Double[]"));
    }

    [Fact]
    public void ToSystemType_Rectangle_Deleted()
    {
        // 零兼容：遗留 Rectangle 映射已删除
        Assert.Null(SdkTypeVocabulary.ToSystemType("Rectangle"));
    }

    #endregion


}

