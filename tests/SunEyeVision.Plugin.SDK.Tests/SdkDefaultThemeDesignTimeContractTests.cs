using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using SunEyeVision.Plugin.SDK.UI.Controls;
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
/// 字典测试只能验证资源存在；本类还在 STA 线程中验证模板与页面背景像素。
/// 页面测试使用不执行工具构造函数的 XAML 投影，不能替代 Visual Studio 设计器验收。
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
        Assert.True(dictionary.Contains(SdkThemeKeys.CheckerboardBackgroundBrush),
            $"约定位置的默认主题缺少图像控件透明背景的兜底 key：{SdkThemeKeys.CheckerboardBackgroundBrush}");
    }

    [Fact]
    public void DesignTimeThemeDictionary_RemainsEmptyInHostedRuntime()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var dictionary = new DesignTimeThemeResourceDictionary();

            Assert.Null(dictionary.Source);
            Assert.Empty(dictionary.MergedDictionaries);
        });
    }

    [Fact]
    public void ToolDebugControlBase_UsesDesignScopeAndRuntimeDynamicResources()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "ToolDebugControlBase.cs"));

        Assert.Contains("DesignTimeThemeLoader.EnsureApplicationResources(this);", source);
        Assert.Contains("SetResourceReference(BackgroundProperty, SdkThemeKeys.PanelBackgroundBrush);", source);
        Assert.Contains("SetResourceReference(ForegroundProperty, SdkThemeKeys.TextPrimaryBrush);", source);
        Assert.DoesNotContain("Resources.MergedDictionaries", source);
        Assert.DoesNotContain("protected override void OnInitialized", source);
    }

    [Fact]
    public void ToolAndSdkControlXaml_DoNotHardCodeThemeColors()
    {
        var repositoryRoot = FindRepositoryRoot();
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls"),
            Path.Combine(repositoryRoot, "tools")
        };
        var themeProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "Background", "Foreground", "BorderBrush", "Fill", "Stroke"
        };
        var violations = new List<string>();

        foreach (var file in roots.SelectMany(root =>
                     Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var attribute in document.Root!.DescendantsAndSelf().Attributes()
                         .Where(attribute => themeProperties.Contains(attribute.Name.LocalName)))
            {
                var value = attribute.Value.Trim();
                if (value.StartsWith("{", StringComparison.Ordinal)
                    || value.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                    continue;

                var line = ((IXmlLineInfo)attribute).HasLineInfo()
                    ? ((IXmlLineInfo)attribute).LineNumber
                    : 0;
                violations.Add($"{Path.GetRelativePath(repositoryRoot, file)}:{line} {attribute.Name.LocalName}=\"{value}\"");
            }
        }

        Assert.True(violations.Count == 0,
            "工具与 SDK 控件的界面颜色必须使用语义主题资源；固定业务叠加色应通过 x:Static 常量表达："
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ToolAndSdkControlCodeBehind_DoesNotManuallyChooseUiThemeColors()
    {
        var repositoryRoot = FindRepositoryRoot();
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls"),
            Path.Combine(repositoryRoot, "tools")
        };
        var forbidden = new Regex(
            @"\.(Background|Foreground|BorderBrush)\s*=\s*(new\s+SolidColorBrush|(?:System\.Windows\.Media\.)?Brushes\.)|IsDarkTheme\s*\(",
            RegexOptions.Compiled);
        var violations = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.xaml.cs", SearchOption.AllDirectories))
            .Where(file => forbidden.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .ToArray();

        Assert.True(violations.Length == 0,
            "界面后台代码不得判断当前主题或直接写入固定 UI 色；应使用 SetResourceReference/动态资源："
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EverySdkSemanticKey_IsDefinedByLightAndDarkThemes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var light = File.ReadAllText(Path.Combine(repositoryRoot, "src", "UI", "Themes", "Light", "LightTheme.xaml"));
        var dark = File.ReadAllText(Path.Combine(repositoryRoot, "src", "UI", "Themes", "Dark", "DarkTheme.xaml"));
        var missing = new List<string>();

        foreach (var field in typeof(SdkThemeKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.IsLiteral && field.FieldType == typeof(string)))
        {
            var reference = $"ThemeKeys.{field.Name}}}";
            if (!light.Contains(reference, StringComparison.Ordinal))
                missing.Add($"Light:{field.Name}");
            if (!dark.Contains(reference, StringComparison.Ordinal))
                missing.Add($"Dark:{field.Name}");
        }

        Assert.True(missing.Count == 0,
            "SDK 语义主题键必须同时由宿主 Light/Dark 定义：" + string.Join(", ", missing));
    }

    [Fact]
    public void LateLoadedDesignerTheme_ReappliesTemplatesToAffectedControls()
    {
        var repositoryRoot = FindRepositoryRoot();
        var controlsRoot = Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls");
        var controls = new[]
        {
            "ParamBinding", "DispItemColorControl", "DispTextColorControl",
            "ConfigSetting", "NumericUpDown", "RangeInputControl"
        };

        foreach (var control in controls)
        {
            var source = File.ReadAllText(Path.Combine(controlsRoot, $"{control}.cs"));
            Assert.Contains($"SetResourceReference(StyleProperty, typeof({control}))", source);
        }
    }

    [Fact]
    public void AffectedControls_CreateVisualTemplatesWhenThemeArrivesAfterConstruction()
    {
        RunOnSta(() =>
        {
            var root = new Grid();
            var controls = new Control[]
            {
                new ParamBinding(),
                new DispItemColorControl(),
                new DispTextColorControl(),
                new ConfigSetting(),
                new NumericUpDown(),
                new RangeInputControl()
            };

            foreach (var control in controls)
                root.Children.Add(control);

            var theme = new ResourceDictionary
            {
                Source = new Uri(ConventionalThemeUri, UriKind.Absolute)
            };
            Assert.True(theme.Contains(typeof(ParamBinding)), "Generic.xaml 缺少 ParamBinding 样式键");
            Assert.True(theme.Contains(typeof(DispItemColorControl)), "Generic.xaml 缺少 DispItemColorControl 样式键");
            Assert.True(theme.Contains(typeof(DispTextColorControl)), "Generic.xaml 缺少 DispTextColorControl 样式键");
            root.Resources.MergedDictionaries.Add(theme);
            root.Measure(new Size(600, 600));
            root.Arrange(new Rect(0, 0, 600, 600));
            root.UpdateLayout();

            foreach (var control in controls)
            {
                Assert.IsType<Style>(control.TryFindResource(control.GetType()));
                Assert.NotNull(control.Style);
                control.ApplyTemplate();
                Assert.NotNull(control.Template);
                Assert.True(control.DesiredSize.Height >= 20,
                    $"{control.GetType().Name} 的设计时高度为 {control.DesiredSize.Height}");
                Assert.True(VisualTreeHelper.GetChildrenCount(control) > 0,
                    $"{control.GetType().Name} 未生成模板可视树");
            }
        });
    }

    [Fact]
    public void EveryToolThemeKey_HasAnSdkDesignTimeFallback()
    {
        EnsureApplication();
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(ConventionalThemeUri, UriKind.Absolute),
        };
        var repositoryRoot = FindRepositoryRoot();
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI"),
            Path.Combine(repositoryRoot, "tools")
        };
        var pattern = new Regex(
            @"\{DynamicResource\s+(Theme[A-Za-z0-9_]+)\}",
            RegexOptions.Compiled);
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in roots.SelectMany(root =>
                     Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)))
        {
            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[1].Value;
                if (!dictionary.Contains(key))
                    missing.Add($"{key} <- {Path.GetRelativePath(repositoryRoot, file)}");
            }
        }

        Assert.True(missing.Count == 0,
            "以下工具主题键没有 SDK 设计期回退资源："
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToolXamlSurface_PaintsThemeWithoutToolConstructor(bool dark)
    {
        // Designer surrogate: preserve the actual XAML surface properties, but deliberately
        // do not run ToolDebugControlBase's constructor. This is not a VS designer integration test.
        RunOnSta(() =>
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var expected = dark ? Color.FromRgb(36, 36, 36) : Colors.White;
            var foreground = dark ? Colors.White : Color.FromRgb(51, 51, 51);
            var failures = new List<string>();
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(FindRepositoryRoot(), "tools"), "*.xaml", SearchOption.AllDirectories))
            {
                var original = XDocument.Load(file).Root!;
                if (original.Name.LocalName != "ToolDebugControlBase")
                    continue;
                var layout = original.Elements().First(element => !element.Name.LocalName.Contains('.'));
                var surface = new XElement(wpf + "UserControl",
                    original.Attributes().Where(attribute => attribute.IsNamespaceDeclaration
                        || attribute.Name.LocalName is "Background" or "Foreground")
                        .Select(attribute => new XAttribute(attribute)),
                    new XElement(layout.Name, layout.Attributes()
                        .Where(attribute => attribute.Name.LocalName == "Background")
                        .Select(attribute => new XAttribute(attribute))));
                var view = (UserControl)System.Windows.Markup.XamlReader.Parse(surface.ToString());
                view.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(ConventionalThemeUri, UriKind.Absolute)
                });
                view.Resources[SdkThemeKeys.PanelBackgroundBrush] = new SolidColorBrush(expected);
                view.Resources[SdkThemeKeys.TextPrimaryBrush] = new SolidColorBrush(foreground);
                view.Measure(new Size(200, 200));
                view.Arrange(new Rect(0, 0, 200, 200));
                view.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    200, 200, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var pixel = new byte[4];
                bitmap.CopyPixels(new Int32Rect(100, 100, 1, 1), pixel, 4, 0);
                if (pixel[3] != 255 || pixel[2] != expected.R || pixel[1] != expected.G
                    || pixel[0] != expected.B)
                    failures.Add(Path.GetFileName(file) + ": surface remains transparent or has wrong theme");
                if (view.Foreground is not SolidColorBrush brush || brush.Color != foreground)
                    failures.Add(Path.GetFileName(file) + ": root text does not follow the theme");
            }
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        });
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))
                    && Directory.Exists(Path.Combine(directory.FullName, "tools")))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("无法定位 SunEyeVision 仓库根目录。");
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw error;
    }
}
