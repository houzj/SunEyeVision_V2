using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// S1 注册契约测试（方案 D1/AC-5 前置）：
/// 验证 SdkTypeVocabulary.RegisterType 注册机制 —— 引擎具体类型须注册后方可命中词目，
/// 未注册返回 null（rule-015 不静默），非法词目禁止注册，重复注册幂等；
/// 且注册不污染 SDK 静态契约表与主映射（IVisionImage→"Image" 保持）。
/// 使用嵌套测试类型做注册载体，避免污染其它契约断言的查询结果。
/// </summary>
public class SdkTypeVocabularyRegistrationContractTests
{
    // 专用探针类型：仅"未注册返回 null"断言使用，避免与其它注册断言共享静态注册表互相干扰。
    private sealed class NeverRegisteredProbe { }
    private sealed class EngineLikeImageType { }
    private sealed class SecondEngineLikeType { }

    #region 注册前/后行为（rule-015：未注册不静默）

    [Fact]
    public void ToTypeVocabulary_UnregisteredType_ReturnsNull()
    {
        // 未注册的引擎具体类型不命中任何词目 → 返回 null（不静默回退）
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(NeverRegisteredProbe)));
    }

    [Fact]
    public void RegisterType_ThenToTypeVocabulary_HitsRegisteredWord()
    {
        // RegisterType(typeof(X), "Image") 后，ToTypeVocabulary(typeof(X)) == "Image"
        SdkTypeVocabulary.RegisterType(typeof(EngineLikeImageType), "Image");
        Assert.Equal("Image", SdkTypeVocabulary.ToTypeVocabulary(typeof(EngineLikeImageType)));
    }

    [Fact]
    public void RegisterType_ArrayType_RecursiveElementHit()
    {
        // 注册单值类型后，其数组形态经元素递归命中同一词目（对齐单值=长度1数组语义）
        SdkTypeVocabulary.RegisterType(typeof(EngineLikeImageType), "Image");
        Assert.Equal("Image", SdkTypeVocabulary.ToTypeVocabulary(typeof(EngineLikeImageType[])));
    }

    [Fact]
    public void RegisterType_ListOfType_UnboxesToArrayHit()
    {
        // List<X> → X[] 拆箱递归命中注册词目
        SdkTypeVocabulary.RegisterType(typeof(EngineLikeImageType), "Image");
        Assert.Equal("Image",
            SdkTypeVocabulary.ToTypeVocabulary(typeof(System.Collections.Generic.List<EngineLikeImageType>)));
    }

    #endregion

    #region 注册约束（rule-015：禁止不对称词目、禁止篡改契约）

    [Fact]
    public void RegisterType_ToUnlistedVocabulary_Throws()
    {
        // 词目必须已收录在契约反向表；禁止经注册表静默引入"正向能解析、反向不能"的词目
        var ex = Assert.Throws<ArgumentException>(() =>
            SdkTypeVocabulary.RegisterType(typeof(SecondEngineLikeType), "NotInContract"));
        Assert.Contains("NotInContract", ex.Message);
    }

    [Fact]
    public void RegisterType_NullType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SdkTypeVocabulary.RegisterType(null!, "Image"));
    }

    [Fact]
    public void RegisterType_BlankName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SdkTypeVocabulary.RegisterType(typeof(SecondEngineLikeType), " "));
    }

    [Fact]
    public void RegisterType_RepeatSameType_IsIdempotent()
    {
        // 重复注册同类型同词目：幂等覆盖不抛异常
        SdkTypeVocabulary.RegisterType(typeof(SecondEngineLikeType), "Double");
        SdkTypeVocabulary.RegisterType(typeof(SecondEngineLikeType), "Double");
        Assert.Equal("Double", SdkTypeVocabulary.ToTypeVocabulary(typeof(SecondEngineLikeType)));
    }

    #endregion

    #region 契约主映射与静态表不变（S1 过渡保障）

    [Fact]
    public void ImageContract_IVisionImageStillMapsToImage()
    {
        // 删除 Mat 硬映射后，契约主映射 IVisionImage→"Image" 保持不变（契约 JSON 不变）
        Assert.Equal("Image", SdkTypeVocabulary.ToTypeVocabulary(typeof(IVisionImage)));
        Assert.Equal(typeof(IVisionImage), SdkTypeVocabulary.ToSystemType("Image"));
    }

    [Fact]
    public void RegisterType_DoesNotMutateStaticContractTable()
    {
        // 注册只进动态注册表，静态 TypeToString 契约表键集不被污染
        SdkTypeVocabulary.RegisterType(typeof(EngineLikeImageType), "Image");

        var dict = (System.Collections.Generic.Dictionary<Type, string>)
            typeof(SdkTypeVocabulary)
                .GetField("TypeToString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;

        // 静态表仍是契约基准：IVisionImage 主映射在、引擎具体类型（模拟 Mat 角色）不在
        Assert.True(dict.ContainsKey(typeof(IVisionImage)));
        Assert.False(dict.ContainsKey(typeof(EngineLikeImageType)));
    }

    #endregion
}
