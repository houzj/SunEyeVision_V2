using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.UI.Themes;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 追溯 SDK 控件（ImageControl 工具栏/状态栏）主题资源解析问题：
/// 当 Application.Resources 中同时存在 "SDK DefaultTheme 兜底固定值" 与 "主题字典（Light/Dark）" 时，
/// 语义 key（如 ThemeToolBarBackgroundBrush）应解析为哪个值。
///
/// 背景：SDK Generic.xaml 合并了 DefaultTheme.xaml（固定亮色兜底），又经 AppResources 合入 Application.Resources。
/// 若主题字典优先级低于 DefaultTheme，则 ImageControl 工具栏/状态栏永远命中兜底亮色，黑白切换不生效。
///
/// 本测试精确复刻该资源查找链，验证"主题字典放在 MergedDictionaries 末尾"时能否覆盖 DefaultTheme 兜底值。
/// 隔离：本类独占集合，避免并行测试互相重置 Application.Resources。
/// </summary>
[Collection("ThemeResourceResolution")]
public class ThemeResourceResolutionTests
{
    private static readonly string SdkGenericUri =
        "pack://application:,,,/SunEyeVision.Plugin.SDK;component/UI/Themes/Generic.xaml";

    private static readonly string DefaultThemeUri =
        "pack://application:,,,/SunEyeVision.Plugin.SDK;component/UI/Themes/DefaultTheme.xaml";

    /// <summary>
    /// 构造与真实场景一致的 Application.Resources：
    /// 先合入 SDK Generic.xaml（内含 DefaultTheme 兜底，ToolBar/StatusBar/Input 均为固定浅色），
    /// 再追加一个"主题字典"（与真实 UI Dark 同 key 同值，ToolBar/StatusBar 为深色、Input 为深灰）。
    /// 验证主题字典在末尾时，语义 key 解析为深色（即主题生效），而非兜底浅色。
    /// </summary>
    [Fact]
    public void ThemeDictionary_AtEnd_OverridesSdkDefaultTheme()
    {
        var app = EnsureApplication();

        // 清理既有主题字典，构造干净场景
        CleanThemeDictionaries(app);

        // 1) 合入 SDK Generic.xaml（含 DefaultTheme 兜底固定值）
        app.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = new Uri(SdkGenericUri, UriKind.Absolute) });

        // 2) 追加一个模拟 Dark 主题字典（与真实 UI DarkTheme.xaml 的 key/值保持一致）
        app.Resources.MergedDictionaries.Add(BuildDarkThemeDictionary());

        // 3) 语义 key 应命中主题字典（末尾，最高优先级），而非 DefaultTheme 兜底
        var toolbarBrush = (SolidColorBrush)app.Resources["ThemeToolBarBackgroundBrush"];
        var statusBrush = (SolidColorBrush)app.Resources["ThemeStatusBarBackgroundBrush"];
        var inputBrush = (SolidColorBrush)app.Resources["ThemeInputBackgroundBrush"];

        Assert.NotNull(toolbarBrush);
        Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), toolbarBrush.Color);   // Dark 深色（主题字典）
        Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), statusBrush.Color);   // Dark 深色
        Assert.Equal(Color.FromRgb(0x2D, 0x2D, 0x2D), inputBrush.Color);    // Dark 深灰
    }

    /// <summary>
    /// 对照组：兜底字典若被放到了最后（DefaultTheme 在末尾），是否会被它遮蔽主题字典深色。
    /// 用于证明"主题字典必须放到末尾（最高优先级）"这一修复方向。
    ///
    /// 说明：DefaultTheme 已从 Generic.xaml 拆出，由宿主 Application 级（AppResources）显式合入，
    /// 故此处显式加载 DefaultTheme.xaml 作为兜底字典，而非依赖 Generic.xaml 内含。
    /// </summary>
    [Fact]
    public void ThemeDictionary_AtFront_IsShadowedBySdkDefaultTheme()
    {
        var app = EnsureApplication();
        CleanThemeDictionaries(app);

        // 主题字典放最前（模拟旧 Insert(0)）
        app.Resources.MergedDictionaries.Add(BuildDarkThemeDictionary());
        // DefaultTheme 兜底放最后 → 遮蔽主题字典（验证"必须把主题字典放末尾"）
        app.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = new Uri(DefaultThemeUri, UriKind.Absolute) });

        var toolbarBrush = (SolidColorBrush)app.Resources["ThemeToolBarBackgroundBrush"];

        Assert.NotNull(toolbarBrush);
        // DefaultTheme 兜底浅色（#F0F0F0）优先于主题字典深色 —— 这正是"黑白一样"的根因
        Assert.Equal(Color.FromRgb(0xF0, 0xF0, 0xF0), toolbarBrush.Color);
    }

    private static Application EnsureApplication()
    {
        if (Application.Current != null)
            return Application.Current;
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary());
        return app;
    }

    /// <summary>
    /// 移除所有 MergedDictionaries，构造干净场景。
    /// </summary>
    private static void CleanThemeDictionaries(Application app)
    {
        app.Resources.MergedDictionaries.Clear();
    }

    /// <summary>
    /// 构造模拟 Dark 主题字典，key 与真实 UI DarkTheme.xaml 一致。
    /// </summary>
    private static ResourceDictionary BuildDarkThemeDictionary()
    {
        var dict = new ResourceDictionary();
        dict[SdkThemeKeys.ToolBarBackgroundBrush] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        dict[SdkThemeKeys.StatusBarBackgroundBrush] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        dict[SdkThemeKeys.InputBackgroundBrush] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
        return dict;
    }
}

/// <summary>
/// 主题资源解析测试独占集合，防止并行测试互相重置 Application.Resources。
/// </summary>
[CollectionDefinition("ThemeResourceResolution")]
public sealed class ThemeResourceResolutionCollectionDefinition
{
}
