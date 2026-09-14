using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Themes;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 判定 + 守卫：SDK 兜底样式能否在**无宿主注入**的环境（XAML 设计器 / 独立开发环境）自动生效。
/// </summary>
/// <remarks>
/// 背景（问题定位）：
/// - SDK 已提供控件样式库 <c>UI/Themes/Generic.xaml</c> 与兜底语义 token <c>UI/Themes/DefaultTheme.xaml</c>；
/// - 但两者的**装配**目前由宿主完成（<c>src/UI/Shell/Resources/AppResources.xaml</c> 显式合入）；
/// - 设计器里没有宿主 ⇒ 无人装配 ⇒ SDK 控件（依赖 DefaultStyleKey 找默认样式 + DynamicResource 找语义 key）渲染为空。
///
/// 期望（本测试锁定）：SDK 兜底样式落在 WPF 约定的"程序集默认主题"资源路径
/// （<c>themes/generic.xaml</c>）—— 控件找不到样式/token 时由 WPF 自动回退，无需任何注入；
/// 宿主注入的亮/暗主题仍在 Application 级覆盖它（优先级更高），主题切换不受影响。
///
/// 说明：本类**不实例化控件**（WPF 控件构造要求 STA 线程，本测试工程不具备），
/// 只做"约定路径 + 兜底 token 是否存在"的字典级判定——足以回答"设计器能否自动拿到兜底"。
/// </remarks>
[Collection("ThemeResourceResolution")]
public class SdkDefaultThemeDesignTimeContractTests
{
    /// <summary>WPF 约定的程序集默认主题资源路径（全小写 themes/generic.xaml）。</summary>
    private const string ConventionalThemeUri =
        "pack://application:,,,/SunEyeVision.Plugin.SDK;component/themes/generic.xaml";

    /// <summary>当前实际路径（宿主 AppResources.xaml 与既有测试引用的路径）。</summary>
    private const string CurrentThemeUri =
        "pack://application:,,,/SunEyeVision.Plugin.SDK;component/UI/Themes/Generic.xaml";

    private static void EnsureApplication()
    {
        // 必须先有 Application：pack:// 方案在 WPF 初始化后才会注册，否则 URI 解析报错
        if (Application.Current == null)
            _ = new Application();
    }

    /// <summary>
    /// 判定 ①：SDK 是否已提供 WPF 约定的程序集默认主题（<c>themes/generic.xaml</c>）。
    /// 这是"设计器/独立环境无需宿主注入即可渲染 SDK 控件"的**唯一**机制。
    /// </summary>
    [Fact]
    public void ConventionalThemePath_IsLoadable()
    {
        EnsureApplication();

        var exception = Record.Exception(() => new ResourceDictionary
        {
            Source = new Uri(ConventionalThemeUri, UriKind.Absolute),
        });

        Assert.True(exception == null,
            $"SDK 未提供 WPF 约定的程序集默认主题（{ConventionalThemeUri}）：{exception?.Message}。"
            + "无此兜底时，XAML 设计器与独立开发环境看不到 SDK 控件（宿主不参与装配）。"
            + $"当前样式库实际位于 {CurrentThemeUri}（资源路径含 UI/，不在约定位置）。");
    }

    /// <summary>
    /// 判定 ②：约定位置的默认主题必须真的含**兜底语义 key**（否则控件仍是无样式空壳）。
    /// </summary>
    [Fact]
    public void ConventionalTheme_ContainsFallbackSemanticKeys()
    {
        EnsureApplication();

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(ConventionalThemeUri, UriKind.Absolute),
        };

        Assert.True(dictionary.Contains(SdkThemeKeys.ToolBarBackgroundBrush),
            $"约定位置的默认主题缺少兜底语义 key：{SdkThemeKeys.ToolBarBackgroundBrush}");
    }
}
