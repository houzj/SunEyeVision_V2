using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
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
    [InlineData(typeof(IVisionRegion[]), "Region")]
    [InlineData(typeof(ShapeParameters), "RoI")]
    [InlineData(typeof(ShapeParameters[]), "RoI")]
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
    public void GetAllTypeOptions_OnlyEightCoreConcepts()
    {
        var options = SdkTypeVocabulary.GetAllTypeOptions();
        Assert.Equal(8, options.Count);
        var values = options.Select(o => o.Value).ToList();
        Assert.Contains("Integer", values);
        Assert.Contains("Double", values);
        Assert.Contains("String", values);
        Assert.Contains("Boolean", values);
        Assert.Contains("Image", values);
        Assert.Contains("Region", values);
        Assert.Contains("RoI", values);
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
    public void RoundTrip_IntScalar_NormalizedToLongArray()
    {
        // 强制 T[]（步骤7）：标量 int 序列化为 "Integer" 后重新加载为 long[]（长度 1 数组）。
        var result = RoundTrip(42);
        Assert.IsType<long[]>(result);
        Assert.Equal(new long[] { 42 }, (long[])result!);
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
    public void RoundTrip_LongScalar_PreservesValueAsArray()
    {
        // 强制 T[]：标量 long 重开为 long[]（长度 1）。
        var result = RoundTrip(12345678901L);
        Assert.IsType<long[]>(result);
        Assert.Equal(new long[] { 12345678901L }, (long[])result!);
    }

    [Fact]
    public void RoundTrip_DoubleScalar_PreservesValueAsArray()
    {
        // 强制 T[]：标量 double 重开为 double[]（长度 1），即 填 3.14 → [3.14]。
        var result = RoundTrip(3.14);
        Assert.IsType<double[]>(result);
        Assert.Equal(new double[] { 3.14 }, (double[])result!);
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
    public void RoundTrip_FloatScalar_NormalizedToDoubleArray()
    {
        // 强制 T[]：标量 float 重开为 double[]（长度 1）。
        var result = RoundTrip(2.5f);
        Assert.IsType<double[]>(result);
        Assert.Equal(new double[] { 2.5 }, (double[])result!);
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

    [Fact]
    public void SerializeValue_ScalarDouble_StoredAsArrayJson()
    {
        // 数据一致性验收 #3：标量 3.14 落盘为 [3.14]（强制 T[]）。
        var json = JsonSerializer.Serialize(new ValueHolder { Value = 3.14 });
        Assert.Contains("\"Value\":[3.14]", json);
    }

    [Fact]
    public void ParseLegacyScalarJson_LosesValueAfterStep7()
    {
        // 验收 #8（破坏性）：历史以标量形态存储的全局变量（如 "Value":3.14）
        // 在强制 T[] 后无法解析，ParseAdaptive 抛异常、转换器记录日志并置空值，
        // 需用户重新保存升级为 T[] 形态（而非静默保留错误数据）。
        const string legacyScalarJson = "{\"Value\":{\"Type\":\"Double\",\"Value\":3.14}}";
        var back = JsonSerializer.Deserialize<ValueHolder>(legacyScalarJson);
        Assert.NotNull(back);
        Assert.Null(back!.Value);
    }

    #endregion

    #region 契约：全局变量闸门（Region 禁止 / RoI 允许，方案步骤2 + 验收 #7）

    [Fact]
    public void GlobalVariable_RegionType_Invalid()
    {
        // Region 仅运行时，不可作为全局变量类型（闸门保留）。
        var gv = new GlobalVariable("r", null, "Region");
        var (isValid, _) = gv.Validate();
        Assert.False(isValid);
    }

    [Fact]
    public void GlobalVariable_RoIType_Valid()
    {
        // RoI（图形定义）可作为全局变量类型（步骤2 放开）。
        var gv = new GlobalVariable("roi", null, "RoI");
        var (isValid, _) = gv.Validate();
        Assert.True(isValid);
    }

    #endregion

    #region 契约：方案扩展（RoI 词目 + Region 数组 + tuple 原则）

    [Fact]
    public void ToSystemType_Region_ReturnsRegionArray()
    {
        // 连线根因修复：Region 反向解析为数组（与上游 IVisionRegion[] 输出 IsArray 一致）
        Assert.Equal(typeof(IVisionRegion[]), SdkTypeVocabulary.ToSystemType("Region"));
    }

    [Fact]
    public void ToSystemType_RoI_ReturnsShapeParametersArray()
    {
        Assert.Equal(typeof(ShapeParameters[]), SdkTypeVocabulary.ToSystemType("RoI"));
    }

    [Fact]
    public void ToTypeVocabulary_RegionArray_ReturnsRegion()
    {
        Assert.Equal("Region", SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionRegion[])));
    }

    [Fact]
    public void ToTypeVocabulary_RoIArray_ReturnsRoI()
    {
        Assert.Equal("RoI", SdkTypeVocabulary.ToTypeVocabulary(typeof(ShapeParameters[])));
    }

    [Fact]
    public void TupleSemantics_ScalarAndArray_ShareVocabulary()
    {
        // 〇 原则1：标量 = 长度 1 的 tuple，与数组共享同一词目
        Assert.Equal("Double", SdkTypeVocabulary.ToTypeVocabulary(typeof(double)));
        Assert.Equal("Double", SdkTypeVocabulary.ToTypeVocabulary(typeof(double[])));
        Assert.Equal("Region", SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionRegion)));
        Assert.Equal("Region", SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionRegion[])));
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
        Assert.True(dict.ContainsKey(typeof(IVisionRegion[])));
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

    #region 契约：几何多态 round-trip（bug ① 治本）

    [Fact]
    public void RoundTrip_RoIGlobal_CircleGeometry_Preserved()
    {
        // 手画圆保存重开：几何参数（中心/半径）精确还原，而非退化为 JsonElement
        var input = new ShapeParameters[] { new ShapeParameters { Geometry = new Circle2d(100, 80, 50) } };
        var result = RoundTrip(input);
        Assert.IsType<ShapeParameters[]>(result);
        var arr = (ShapeParameters[])result!;
        Assert.Single(arr);
        Assert.IsType<Circle2d>(arr[0].Geometry);
        var c = (Circle2d)arr[0].Geometry!;
        Assert.Equal(100, c.Center.X);
        Assert.Equal(80, c.Center.Y);
        Assert.Equal(50, c.Radius);
    }

    [Fact]
    public void RoundTrip_RoIGlobal_AllGeometryTypes_Preserved()
    {
        // 治本验证 bug ①：所有几何类型经 RoI 全局变量 round-trip 后参数精确还原
        var inputs = new ShapeParameters[]
        {
            new ShapeParameters { Geometry = new Circle2d(100, 80, 50) },
            new ShapeParameters { Geometry = new RotatedRect2d(10, 20, 30, 40, 15) },
            new ShapeParameters { Geometry = new Annulus2d(5, 6, 7, 8, 0.5, 2.0) },
            new ShapeParameters { Geometry = Line2d.FromTwoPoints(new System.Windows.Point(0, 0), new System.Windows.Point(10, 10)) },
            new ShapeParameters { Geometry = new Arc2d(1, 2, 3, 0.1, 1.2) },
            new ShapeParameters { Geometry = new Polygon2d(new[] { new System.Windows.Point(0, 0), new System.Windows.Point(10, 0), new System.Windows.Point(5, 10) }) },
            new ShapeParameters { Geometry = new System.Windows.Rect(1, 2, 3, 4) },
        };
        var result = RoundTrip(inputs);
        var arr = (ShapeParameters[])result!;
        Assert.Equal(7, arr.Length);

        var circle = (Circle2d)arr[0].Geometry!;
        Assert.Equal(100, circle.Center.X);
        Assert.Equal(50, circle.Radius);

        var rr = (RotatedRect2d)arr[1].Geometry!;
        Assert.Equal(10, rr.Center.X);
        Assert.Equal(30, rr.Width);
        Assert.Equal(15, rr.Angle);

        var an = (Annulus2d)arr[2].Geometry!;
        Assert.Equal(5, an.Center.X);
        Assert.Equal(7, an.InnerRadius);
        Assert.Equal(8, an.OuterRadius);

        var line = (Line2d)arr[3].Geometry!;
        Assert.Equal(0, line.Point.X);

        var arc = (Arc2d)arr[4].Geometry!;
        Assert.Equal(1, arc.Center.X);
        Assert.Equal(3, arc.Radius);

        var poly = (Polygon2d)arr[5].Geometry!;
        Assert.Equal(3, poly.Vertices.Length);

        var rect = (System.Windows.Rect)arr[6].Geometry!;
        Assert.Equal(1, rect.X);
        Assert.Equal(4, rect.Height);
    }

    [Fact]
    public void RoundTrip_RoIGlobal_RectangleGeometry_Preserved()
    {
        var input = new ShapeParameters[]
        {
            new ShapeParameters { Geometry = new System.Windows.Rect(10, 20, 30, 40) },
        };
        var result = RoundTrip(input);
        var arr = (ShapeParameters[])result!;
        Assert.IsType<System.Windows.Rect>(arr[0].Geometry);
        var r = (System.Windows.Rect)arr[0].Geometry!;
        Assert.Equal(10, r.X);
        Assert.Equal(20, r.Y);
        Assert.Equal(30, r.Width);
        Assert.Equal(40, r.Height);
    }

    #endregion
}
