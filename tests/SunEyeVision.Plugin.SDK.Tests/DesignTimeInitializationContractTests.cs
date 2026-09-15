using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 防止设计模式判断再次放到 InitializeComponent 之前，导致设计器只有空白控件。
/// </summary>
public class DesignTimeInitializationContractTests
{
    [Fact]
    public void ControlsWithDesignModeGuards_InitializeTheirVisualTreeFirst()
    {
        var repositoryRoot = FindRepositoryRoot();
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI"),
            Path.Combine(repositoryRoot, "tools")
        };

        var violations = new List<string>();
        foreach (var file in roots.SelectMany(root =>
                     Directory.EnumerateFiles(root, "*.xaml.cs", SearchOption.AllDirectories)))
        {
            var source = File.ReadAllText(file);
            var designModeIndex = FirstDesignModeGuardIndex(source);
            if (designModeIndex < 0)
                continue;

            var initializeIndex = source.IndexOf("InitializeComponent();", StringComparison.Ordinal);
            if (initializeIndex < 0 || initializeIndex > designModeIndex)
                violations.Add(Path.GetRelativePath(repositoryRoot, file));
        }

        Assert.True(violations.Count == 0,
            "以下控件在创建可视树前退出设计模式，设计器会显示空白："
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void RoiEditor_InitializesShapeToolbarBeforeDesignModeExit()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "Roi", "Views",
            "RoiEditorControl.xaml.cs"));

        var constructorIndex = source.IndexOf("public RoiEditorControl()", StringComparison.Ordinal);
        var initializeToolsIndex = source.IndexOf("InitializeShapeTools();", constructorIndex, StringComparison.Ordinal);
        var designModeIndex = source.IndexOf("DesignerProperties.GetIsInDesignMode(this)", constructorIndex, StringComparison.Ordinal);

        Assert.True(constructorIndex >= 0 && initializeToolsIndex > constructorIndex && initializeToolsIndex < designModeIndex,
            "RoiEditorControl 必须在设计模式退出前初始化 ShapeTools，否则设计器只显示空工具栏。");
    }

    [Fact]
    public void AlgorithmTools_DirectlyComposeRequiredDataDrivenResultModules()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tools = new[]
        {
            (Project: "SunEyeVision.BlobAnalysis", Type: "BlobAnalysis"),
            (Project: "SunEyeVision.Tool.PointPointMeasure", Type: "PointPointMeasure"),
            (Project: "SunEyeVision.Tool.PointLineMeasure", Type: "PointLineMeasure"),
            (Project: "SunEyeVision.Tool.PointCircleMeasure", Type: "PointCircleMeasure"),
            (Project: "SunEyeVision.Tool.LineLineMeasure", Type: "LineLineMeasure"),
            (Project: "SunEyeVision.Tool.LineCircleMeasure", Type: "LineCircleMeasure"),
            (Project: "SunEyeVision.Tool.CircleCircleMeasure", Type: "CircleCircleMeasure"),
            (Project: "SunEyeVision.Tool.LineDetect", Type: "LineDetect"),
            (Project: "SunEyeVision.Tool.CircleDetect", Type: "CircleDetect"),
            (Project: "SunEyeVision.Tool.ShapeMatch", Type: "ShapeMatch"),
            (Project: "SunEyeVision.Tool.Threshold", Type: "Threshold"),
            (Project: "SunEyeVision.Tool.ImageThreshold", Type: "ImageThreshold"),
            (Project: "SunEyeVision.Tool.ImageFilter", Type: "ImageFilter"),
            (Project: "SunEyeVision.Tool.Morphology", Type: "Morphology")
        };

        foreach (var tool in tools)
        {
            var projectPath = Path.Combine(repositoryRoot, "tools", tool.Project);
            var parameterPath = Path.Combine(projectPath, $"{tool.Type}Parameters.cs");
            var xamlPath = Path.Combine(projectPath, "Views", $"{tool.Type}ToolDebugControl.xaml");
            var parameters = File.ReadAllText(parameterPath);
            var xaml = File.ReadAllText(xamlPath);

            Assert.Contains("ResultPresentationConfig ResultPresentation", parameters);
            Assert.Contains("<presentation:JudgmentEditorControl", xaml);
            Assert.DoesNotContain("ResultPresentationControl", xaml);
            Assert.DoesNotContain("ResultConfig", xaml);
            Assert.Contains("xmlns:designParameters=", xaml);
            Assert.Contains(
                $"d:DataContext=\"{{d:DesignInstance Type=designParameters:{tool.Type}Parameters, IsDesignTimeCreatable=True}}\"",
                xaml);

            var judgmentOnly = tool.Type is "Threshold" or "ImageThreshold" or "ImageFilter" or "Morphology";
            Assert.Equal(!judgmentOnly, xaml.Contains("<presentation:GraphicsDisplayEditorControl"));
            Assert.Equal(!judgmentOnly, xaml.Contains("<presentation:TextDisplayEditorControl"));
        }
    }

    [Fact]
    public void ResultModuleControls_DoNotOverrideToolDataWithDesignerPlaceholders()
    {
        var repositoryRoot = FindRepositoryRoot();
        var controlRoot = Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "ResultPresentation");

        foreach (var file in Directory.EnumerateFiles(controlRoot, "*EditorControl.xaml"))
            Assert.DoesNotContain("d:SampleData", File.ReadAllText(file));
    }

    [Fact]
    public void JudgmentRanges_RemainVisibleInTheDesigner()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "ResultPresentation",
            "JudgmentEditorControl.xaml"));

        Assert.Contains("Label=\"范围\" Margin=\"0,2,0,0\" d:Visibility=\"Visible\"", xaml);
    }

    [Fact]
    public void PointLineMeasure_HasOnlyOneBasicParametersTab()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot, "tools", "SunEyeVision.Tool.PointLineMeasure", "Views",
            "PointLineMeasureToolDebugControl.xaml"));

        Assert.Equal(1, CountOccurrences(xaml, "<TabItem Header=\"基本参数\""));
        Assert.Equal(1, CountOccurrences(xaml, "<TabItem Header=\"运行参数\""));
        Assert.Equal(1, CountOccurrences(xaml, "<TabItem Header=\"结果显示\""));
    }

    [Fact]
    public void MeasureTools_HaveExactlyTheirDeclaredTopLevelTabs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var cases = new Dictionary<string, string[]>
        {
            ["SunEyeVision.Tool.PointPointMeasure"] = new[] { "基本参数", "结果显示" },
            ["SunEyeVision.Tool.PointLineMeasure"] = new[] { "基本参数", "运行参数", "结果显示" },
            ["SunEyeVision.Tool.PointCircleMeasure"] = new[] { "基本参数", "运行参数", "结果显示" },
            ["SunEyeVision.Tool.LineLineMeasure"] = new[] { "基本参数", "运行参数", "结果显示" },
            ["SunEyeVision.Tool.LineCircleMeasure"] = new[] { "基本参数", "运行参数", "结果显示" },
            ["SunEyeVision.Tool.CircleCircleMeasure"] = new[] { "基本参数", "运行参数", "结果显示" }
        };
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        foreach (var item in cases)
        {
            var typeName = item.Key["SunEyeVision.Tool.".Length..];
            var xamlPath = Path.Combine(repositoryRoot, "tools", item.Key, "Views",
                $"{typeName}ToolDebugControl.xaml");
            var document = XDocument.Load(xamlPath);
            var tabControl = Assert.Single(document.Root!.Elements(presentation + "TabControl"));
            var headers = tabControl.Elements(presentation + "TabItem")
                .Select(tab => (string?)tab.Attribute("Header"))
                .ToArray();

            Assert.Equal(item.Value, headers);
        }
    }

    [Fact]
    public void ReusableSdkControls_DoNotMergeGenericThemeAtElementScope()
    {
        var repositoryRoot = FindRepositoryRoot();
        var controlsRoot = Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls");
        const string localThemeMerge =
            "pack://application:,,,/SunEyeVision.Plugin.SDK;component/Themes/Generic.xaml";

        var offenders = Directory.EnumerateFiles(controlsRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains(localThemeMerge, StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "SDK 可复用控件不得在元素级合并 Generic.xaml；局部 DefaultTheme 会覆盖宿主 Light/Dark 主题："
            + string.Join(", ", offenders));
    }

    [Fact]
    public void ThemeAssembly_HasOneGlobalCompositionPathAndNoLegacyDividerKey()
    {
        var repositoryRoot = FindRepositoryRoot();
        var generic = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "Plugin.SDK", "Themes", "Generic.xaml"));
        var appResources = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "UI", "Shell", "Resources", "AppResources.xaml"));

        Assert.Contains("component/UI/Themes/DefaultTheme.xaml", generic);
        Assert.DoesNotContain("component/UI/Themes/DefaultTheme.xaml", appResources);
        Assert.Equal(1, CountOccurrences(appResources,
            "component/Themes/Generic.xaml"));

        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src"),
            Path.Combine(repositoryRoot, "tools")
        };
        var offenders = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(file => File.ReadAllText(file).Contains("ThemeDividerBrushBrush", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "旧分隔线资源 ThemeDividerBrushBrush 不得残留：" + string.Join(", ", offenders));
    }

    [Fact]
    public void ReusableSdkControls_DoNotUseParseTimeReferencesToGlobalStyles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var controlsRoot = Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls");
        var forbidden = new[] { "LabelStyle", "ShapeButtonStyle", "SegmentedToggleStyle" };

        var offenders = Directory.EnumerateFiles(controlsRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(file => forbidden
                .Where(key => File.ReadAllText(file).Contains($"StaticResource {key}", StringComparison.Ordinal))
                .Select(key => $"{Path.GetRelativePath(repositoryRoot, file)}:{key}"))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "UserControl 不得在解析期静态依赖 Generic 的跨字典样式：" + string.Join(", ", offenders));
    }

    [Fact]
    public void DesignTheme_IsAddedToDesignerApplicationBeforeVisualTreeCreation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var cases = new[]
        {
            (Path: Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "ToolDebugControlBase.cs"),
                Constructor: "protected ToolDebugControlBase()", Initialize: "_availableDataSources ="),
            (Path: Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "Roi", "Views", "RoiEditorControl.xaml.cs"),
                Constructor: "public RoiEditorControl()", Initialize: "InitializeComponent();"),
            (Path: Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Controls", "Common", "Views", "ToolBarControl.xaml.cs"),
                Constructor: "public ToolBarControl()", Initialize: "InitializeComponent();")
        };

        foreach (var item in cases)
        {
            var source = File.ReadAllText(item.Path);
            var constructorIndex = source.IndexOf(item.Constructor, StringComparison.Ordinal);
            var themeIndex = source.IndexOf(
                "DesignTimeThemeLoader.EnsureApplicationResources(this);", constructorIndex,
                StringComparison.Ordinal);
            var initializeIndex = source.IndexOf(item.Initialize, constructorIndex, StringComparison.Ordinal);

            Assert.True(constructorIndex >= 0 && themeIndex > constructorIndex && themeIndex < initializeIndex,
                $"{Path.GetFileName(item.Path)} 必须在创建可视树之前向设计器 Application 装配 SDK Generic。");
        }

        var loader = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI", "Themes",
            "DesignTimeThemeLoader.cs"));
        Assert.Contains("DesignerProperties.GetIsInDesignMode(owner)", loader);
        Assert.Contains("LicenseManager.UsageMode == LicenseUsageMode.Designtime", loader);
        Assert.Contains("pendingElement.Initialized += OnOwnerInitialized", loader);
        Assert.Contains("EnsureApplicationResources(frameworkElement);", loader);
        Assert.Contains("processName.Contains(\"WpfSurface\"", loader);
        Assert.Contains("processName.Contains(\"DesignToolsServer\"", loader);
        Assert.Contains("frameworkElement.Resources", loader);
        Assert.Contains("Application.Current?.Resources", loader);
        Assert.Contains("MergedDictionaries.Add", loader);
        Assert.Contains("MergedDictionaries.Remove(existing)", loader);
    }

    [Fact]
    public void EveryToolDebugXaml_DeclaresDesignOnlyThemeDictionary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var offenders = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "tools"), "*.xaml", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("<controls:ToolDebugControlBase", StringComparison.Ordinal))
            .Where(file => !File.ReadAllText(file).Contains(
                "<themes:DesignTimeThemeResourceDictionary", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "每个工具调试根必须显式声明只在设计时生效的 SDK 主题字典："
            + string.Join(", ", offenders));

        var dictionarySource = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Plugin.SDK", "UI",
            "Themes", "DesignTimeThemeResourceDictionary.cs"));
        Assert.Contains("if (Application.Current == null)", dictionarySource);
        Assert.Contains("processName.Contains(\"devenv\"", dictionarySource);
        Assert.Contains("Source = new Uri(DesignTimeThemeLoader.GenericThemeUri", dictionarySource);
    }

    private static int FirstDesignModeGuardIndex(string source)
    {
        var candidates = new[]
        {
            source.IndexOf("GetIsInDesignMode", StringComparison.Ordinal),
            source.IndexOf("IsInDesignMode", StringComparison.Ordinal)
        };

        return candidates.Where(index => index >= 0).DefaultIfEmpty(-1).Min();
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Plugin.SDK")))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("无法定位 SunEyeVision 仓库根目录。");
    }
}
