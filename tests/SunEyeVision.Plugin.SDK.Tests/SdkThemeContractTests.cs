using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Themes;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// SDK 主题收官方案的防御机制：契约校验 + XAML 冒烟。
///
/// 契约校验：反射 SdkThemeKeys 全部 const string 常量，断言 SDK 兜底主题 DefaultTheme.xaml 定义了每个 key。
/// 防止 SDK 新增语义 key（如 ScrollBar 4 组）但漏写 DefaultTheme 兜底值，导致无 UI 宿主时静默回退（rule-015）。
///
/// XAML 冒烟：加载 Generic.xaml 成功，防新增的标准控件隐式样式 / 图标 Path 触发 XamlParseException。
/// </summary>
// 与 ThemeResourceResolutionTests 共用同一 collection，避免并行测试各自 new Application() 冲突
// （WPF Application 进程级单例，并行创建会抛 InvalidOperationException）。
[Collection("ThemeResourceResolution")]
public class SdkThemeContractTests
{
    private static readonly string DefaultThemeUri =
        "pack://application:,,,/SunEyeVision.Plugin.SDK;component/UI/Themes/DefaultTheme.xaml";

    private static readonly string SdkGenericUri =
        "pack://application:,,,/SunEyeVision.Plugin.SDK;component/Themes/Generic.xaml";

    /// <summary>
    /// 契约校验：遍历 SdkThemeKeys 全部常量，每个 key 都必须在 DefaultTheme.xaml 中有定义。
    /// 新增 key 后若漏写兜底值，此测试即失败（报错优先，不静默）。
    /// </summary>
    [Fact]
    public void DefaultTheme_DefinesEverySdkThemeKey()
    {
        EnsureApplication();

        var keys = typeof(SdkThemeKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(keys);

        var dict = new ResourceDictionary { Source = new Uri(DefaultThemeUri, UriKind.Absolute) };

        var missing = keys.Where(key => !dict.Contains(key)).ToArray();
        Assert.True(missing.Length == 0,
            $"DefaultTheme.xaml 缺失 {missing.Length} 个 SDK 语义 key: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// XAML 冒烟：Generic.xaml 含新增标准控件隐式样式 + 图标 Path，必须能成功加载（防 XamlParseException）。
    /// </summary>
    [Fact]
    public void GenericTheme_LoadsWithoutXamlParseException()
    {
        EnsureApplication();

        var dict = new ResourceDictionary { Source = new Uri(SdkGenericUri, UriKind.Absolute) };

        // 显式解析几个新增关键资源，确认隐式样式与图标资源真实存在
        Assert.True(dict.Contains("LinkIconPath"), "Generic.xaml 应定义 LinkIconPath（绑定图标矢量）");
        Assert.True(dict.Contains("SelectableShapeButtonStyle"), "Generic.xaml 应定义可选中形状按钮样式");
        Assert.NotNull(dict["TreeExpanderPath"]);
    }

    /// <summary>
    /// 契约校验：DefaultTheme.xaml 补齐的 ScrollBar 4 个 Brush key 必须与 SdkThemeKeys 一致。
    /// 直接断言 key 存在，确保滚动条 token 激活闭环完整。
    /// </summary>
    [Fact]
    public void ScrollBarBrushKeys_AreDefinedInDefaultTheme()
    {
        EnsureApplication();

        var dict = new ResourceDictionary { Source = new Uri(DefaultThemeUri, UriKind.Absolute) };

        Assert.True(dict.Contains(SdkThemeKeys.ScrollBarBackgroundBrush));
        Assert.True(dict.Contains(SdkThemeKeys.ScrollBarThumbBrush));
        Assert.True(dict.Contains(SdkThemeKeys.ScrollBarThumbHoverBrush));
        Assert.True(dict.Contains(SdkThemeKeys.ScrollBarThumbPressedBrush));
    }

    /// <summary>
    /// 确保 Application 存在，以注册 WPF pack URI scheme（pack://application:,,,），
    /// 否则直接 new Uri(packString) 会因 authority "application:,,," 被误解析为端口而抛 UriFormatException。
    /// </summary>
    private static void EnsureApplication()
    {
        if (Application.Current != null)
            return;
        _ = new Application();
    }
}
