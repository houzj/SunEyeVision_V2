using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板配置窗口：外壳由框架层 BaseWindow 统一提供（深色标题栏/边框/最小化最大化关闭），
/// 内容由 TemplateConfigSurface 承载原厂 hppyramidfeaturepatmatchdialoguistyle.xaml 的 ControlTemplate。
/// 分层：框架壳(BaseWindow) → 内容控件(Surface) → 数据模型(TemplateConfigData)。
/// </summary>
public sealed partial class TemplateConfigWindow : BaseWindow
{
    private readonly TemplateConfigData _data;
    private readonly TemplateConfigSurface _surface;

    public TemplateConfigWindow(
        IToolSettingsDraft draft,
        Action<FieldSettings>? acceptCandidate,
        Action createTemplate,
        Action saveProject,
        Action loadProject,
        Action<RecordValue> acceptDocument,
        RecordValue initialDocument,
        NativeRoiFrameSource? frames,
        Func<string?>? drawBlockReason = null)
    {
        _ = acceptDocument ?? throw new ArgumentNullException(nameof(acceptDocument));
        _data = new TemplateConfigData(draft, acceptCandidate, createTemplate, saveProject,
            loadProject, initialDocument, frames);

        InitializeComponent();
        // 严格照原厂 FeaturepatMatchConfigPanalData.cs:244-265 的宿主结构：
        // BaseWindow(框架壳) + 内容控件(默认样式自动套用原厂 ControlTemplate) + 主题色字典。
        ShowInTaskbar = false;
        Height = 534; Width = 900;
        MinButtonVisibility = Visibility.Collapsed;
        MaxButtonVisibility = Visibility.Visible;

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Foundation/BaseWindow.xaml", UriKind.Relative)
        });
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        // 原厂 :245-246 本地化 + :259 主题色。
        Plugin.Wpf.Themes.Palettes.ColorThemesResourceDictionary.GetInstance(this);
        // 原厂内容控件的 ControlTemplate（品牌中性化迁移）。本方工具 DLL 无 generic.xaml 主题链，
        // DefaultStyleKey 不会向父级资源树查找，故显式把样式赋给 Surface（等价原厂默认样式效果）。
        var originalStyleResources = new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Tool.ContourMatch.Runtime;component/Editor/Views/TemplateConfigWindow.Resources.xaml", UriKind.Relative)
        };
        Resources.MergedDictionaries.Add(originalStyleResources);

        _surface = new TemplateConfigSurface(_data)
        {
            Style = (Style)originalStyleResources[typeof(TemplateConfigSurface)]
        };
        Content = _surface;   // 原厂 :265 baseWindow.Content = featurepatMatchDialogUI
        DataContext = _data;

        _surface.ApplyTemplate();
        var imageViewBox = _surface.Template?.FindName("imageViewBox", _surface) as ImageViewBox;
        if (imageViewBox is not null)
        {
            imageViewBox.DataContext = _data;
            _data.ViewportAction = action =>
            {
                if (action == "origin") imageViewBox.Init();
                else if (action == "full") imageViewBox.Enlarge();
            };
        }
    }
}
