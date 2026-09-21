using System;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Core;
using Xunit;

[assembly: ToolContractVersionAttribute(2, 0)]

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 守卫契约测试：工具插件契约版本（方案 §十七 R4「多人协作」的"证件"，防御机制 #1）。
/// </summary>
/// <remarks>
/// 覆盖三类很容易被改坏、且改坏后**表面看不出来**的退化：
/// <list type="bullet">
/// <item>主版本判定放宽（不同主版本也放行）⇒ 宿主装上不兼容插件后行为诡异、无从诊断；</item>
/// <item>改为"插件自行声明版本"⇒ 声明可写错/过期（本实现用程序集元数据，不会说谎）；</item>
/// <item>报错文案丢失版本信息 ⇒ 开发者拿到"插件没生效"却不知道要换哪个开发包。</item>
/// </list>
/// </remarks>
public class ToolContractVersionContractTests
{
    [Fact]
    public void Version_IsMajorDotMinor()
    {
        Assert.Equal($"{ToolContract.Major}.{ToolContract.Minor}", ToolContract.Version);
        Assert.Equal("SunEyeVision.Plugin.SDK", ToolContract.AssemblyName);
    }

    [Fact]
    public void IsCompatible_SameMajorAndSupportedMinor_IsCompatible()
    {
        // 主版本相同且插件次版本不高于宿主 ⇒ 兼容。
        Assert.True(ToolContract.IsCompatible(new Version(ToolContract.Major, ToolContract.Minor)));
        Assert.True(ToolContract.IsCompatible(new Version(ToolContract.Major, 0)));
        Assert.False(ToolContract.IsCompatible(new Version(ToolContract.Major, ToolContract.Minor + 1)));
    }

    [Fact]
    public void IsCompatible_DifferentMajor_IsIncompatible()
    {
        // 主版本不同 ⇒ 不兼容（这是"证件核对"的核心判定）
        Assert.False(ToolContract.IsCompatible(new Version(ToolContract.Major + 1, 0, 0, 0)));
        Assert.False(ToolContract.IsCompatible(null));
    }

    [Fact]
    public void GetPluginContractVersion_ReadsExplicitAssemblyMetadata()
    {
        // 判定依据是插件程序集显式声明的契约元数据，而不是 SDK 的 AssemblyVersion。
        var executing = Assembly.GetExecutingAssembly();
        var referenced = ToolContract.GetPluginContractVersion(executing);
        Assert.Equal(new Version(2, 0), referenced);
        Assert.True(ToolContract.IsCompatible(referenced));
    }

    [Fact]
    public void GetPluginContractVersion_AssemblyWithoutSdkReference_ReturnsNull()
    {
        // 未引用 SDK 的程序集 ⇒ null（宿主据此判定"不兼容"，不静默放行）
        var noSdkReference = typeof(Uri).Assembly;

        Assert.Null(ToolContract.GetPluginContractVersion(noSdkReference));
        Assert.False(ToolContract.IsCompatible(null));
    }

    [Fact]
    public void Describe_CarriesVersionsForBothCases()
    {
        // 兼容与不兼容两种文案都必须带版本信息，否则开发者无法自助修复
        var compatible = ToolContract.Describe(new Version(ToolContract.Major, ToolContract.Minor));
        Assert.Contains(ToolContract.Version, compatible);
        Assert.Contains("兼容", compatible);

        var incompatible = ToolContract.Describe(new Version(ToolContract.Major + 1, 0, 0, 0));
        Assert.Contains(ToolContract.Version, incompatible);
        Assert.Contains("不兼容", incompatible);
        Assert.Contains((ToolContract.Major + 1).ToString(), incompatible);
    }

    [Fact]
    public void GetPluginContractVersion_NullAssembly_Throws()
    {
        // 报错优先：不静默兜底（rule-008 / rule-015）
        Assert.Throws<ArgumentNullException>(() => ToolContract.GetPluginContractVersion(null!));
    }
}
