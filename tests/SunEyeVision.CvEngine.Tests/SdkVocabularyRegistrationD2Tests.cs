using OpenCvSharp;
using SunEyeVision.CvAdapter;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.CvEngine.Tests;

/// <summary>
/// D2 注册契约测试（对称架构装配收口）：
/// CvAdapter 装配启动调 RegisterSdkVocabulary() 后，词汇表 Mat→Image 往返可用——
/// 契约 JSON 主映射 IVisionImage→"Image" 不变，引擎具体类型 Mat 由注册供给
/// （恢复 S1 删除静态 Mat 映射后的运行期能力，GlobalVariableValueConverter 序列化裸 Mat 依赖）。
/// </summary>
public class SdkVocabularyRegistrationD2Tests
{
    [Fact]
    public void BeforeRegistration_Mat_NotMapped()
    {
        // 未装配注册前，typeof(Mat) 不应命中词目（rule-015：不静默回退）
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(Mat)));
    }

    [Fact]
    public void AfterCvAdapterStartup_Mat_MapsToImage()
    {
        // 模拟主程序装配调用
        CvAdapterStartup.RegisterSdkVocabulary();

        Assert.Equal("Image", SdkTypeVocabulary.ToTypeVocabulary(typeof(Mat)));
        // 契约主映射保持（下游 ToSystemType("Image") 仍为抽象接口，绑定/过滤用）
        Assert.Equal(typeof(IVisionImage), SdkTypeVocabulary.ToSystemType("Image"));
    }

    [Fact]
    public void RegisterSdkVocabulary_IsIdempotent()
    {
        // 重复装配调用幂等（主程序多次初始化安全）
        CvAdapterStartup.RegisterSdkVocabulary();
        CvAdapterStartup.RegisterSdkVocabulary();

        Assert.Equal("Image", SdkTypeVocabulary.ToTypeVocabulary(typeof(Mat)));
    }
}
