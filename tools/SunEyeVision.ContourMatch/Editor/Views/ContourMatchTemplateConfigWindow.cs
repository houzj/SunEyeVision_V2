using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 <c>FeaturepatMatchConfigPanalData.CreateModel</c> 创建的 900x534 模板配置窗口。
/// 视觉结构与命令来自 HPPyramidFeaturepatMatchDialogUIStyle.xaml；样本区域编辑复用平台
/// NativeRoiEditHost（与 Blob/PixelCount 同一体系），绘制/列表/可见/删除/撤销重做由平台提供。
/// </summary>
internal sealed class ContourMatchTemplateConfigWindow : Window, IDisposable
{
    private const string ResourceRoot = "/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/";
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly Action _createTemplate;
    private readonly Action _saveProject;
    private readonly Action _loadProject;
    private readonly Action<RecordValue> _acceptDocument;
    private readonly NativeRoiFrameSource? _frames;
    private readonly List<DeclaredFieldRow> _fieldRows = new();
    private readonly List<NativeParameterRow> _parameterRows = new();
    private readonly ParamListView _configList = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 8, 4) };
    private ContourMatchTrainingRoiEditor? _roiEditor;
    private StackPanel? _roiHost;
    private bool _disposed;

    public ContourMatchTemplateConfigWindow(
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
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _acceptCandidate = acceptCandidate;
        _createTemplate = createTemplate;
        _saveProject = saveProject;
        _loadProject = loadProject;
        _acceptDocument = acceptDocument ?? throw new ArgumentNullException(nameof(acceptDocument));
        _frames = frames;

        Title = "模板配置";
        Width = 900;
        Height = 534;
        MinWidth = 900;
        MinHeight = 534;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(242, 243, 244));
        FontSize = 14;
        FontWeight = FontWeights.Normal;
        // ROI 组合器生成的参数行用模板选择器在本窗口资源树里找 RadioSelecter 等原参数行模板。
        // 这些模板在 GlobalResources.xaml 链里（ParamListView/ParamUserControl 各自合并它）；
        // 独立 ShowDialog 窗口不继承宿主的参数行资源，必须自行合并，否则单选行应用模板时抛错。
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/ParameterPresentation/ResourceDictionaries/GlobalResources.xaml", UriKind.Relative)
        });
        // 底栏 NativeMatchCenterBar 用 CheckBoxStyle1/BottomButtonStyle（原生 hpfeaturepatmatchdialoguibasestyle.xaml:16-45），
        // 定义在 BottomCommands.xaml；独立窗口不继承宿主资源，必须合并，否则 StaticResource 异常。
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Commands/BottomCommands.xaml", UriKind.Relative)
        });
        Content = BuildContent(initialDocument, drawBlockReason);
        Closed += (_, _) => Dispose();
    }

    private UIElement BuildContent(RecordValue initialDocument, Func<string?>? drawBlockReason)
    {
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(242, 243, 244)) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

        var body = new Border { Padding = new Thickness(10), Child = BuildBody(initialDocument, drawBlockReason) };
        Grid.SetRow(body, 0);
        root.Children.Add(body);

        // 底栏按原厂 hpfeaturepatmatchdialoguibasestyle.xaml:16-45 迁移（匹配中心 FixX/FixY + 锁定 + 确定）。
        var footer = new NativeMatchCenterBar
        {
            ConfirmCommand = new DelegateCommand(() => Confirm_OnClick(this, new RoutedEventArgs()))
        };
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        return root;
    }

    private UIElement BuildBody(RecordValue initialDocument, Func<string?>? drawBlockReason)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition());

        var top = new StackPanel { Orientation = Orientation.Horizontal };
        // 原厂 HPPyramidFeaturepatMatchDialogUI.cs:897-916 SelectCurImage：取上游当前图像后 InitData+DoClear。
        // 本方 NativeRoiFrameSource 只能借宿主当前帧（契约不支持选择图像），故"当前图像"=重挂当前输入帧。
        top.Children.Add(TextButton("选择当前图像", (_, _) => SelectCurrentImage(), 95));
        // 原厂 :918-944 SelectOtherImage 弹文件对话框选图——本方 ROI 编辑器只认宿主帧，不支持外部图像注入，
        // 明确提示而非假装可用（框架缺口，见对照表）。
        top.Children.Add(TextButton("选择其他图像", (_, _) =>
            _error.Text = "本方模板编辑器只使用当前输入图像；请在主界面切换图像来源。", 95));
        top.Children.Add(new TextBlock { Text = "模板存图", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(10, 0, 0, 0) });
        top.Children.Add(new ToggleButton { IsChecked = true, Width = 42, Height = 24, Margin = new Thickness(-2, 0, 6, 0), Content = "开" });
        top.Children.Add(IconButton("ic_导出_normal.png", "保存模板工程", () => _saveProject()));
        top.Children.Add(new TextBlock { Text = "全部屏蔽", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(48, 0, 0, 0) });
        top.Children.Add(new ToggleButton { Width = 42, Height = 24, Margin = new Thickness(-2, 0, 0, 0), Content = "关" });
        Grid.SetRow(top, 0);
        root.Children.Add(top);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star) });
        Grid.SetRow(columns, 1);
        root.Children.Add(columns);

        var imageBorder = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 5, 5, 0) };
        _roiHost = new StackPanel();
        _roiEditor = new ContourMatchTrainingRoiEditor(initialDocument, _frames, drawBlockReason, null,
            element => _roiHost.Children.Add(element));
        // 生成模型按钮：平台 ROI 编辑器在图像区，生成命令放在面板工具条由主面板承载；
        // 窗口内保留一个显式生成按钮，先提交样本再触发训练。
        var generate = TextButton("生成模型", (_, _) =>
        {
            if (!TryCaptureDocument(out _)) return;
            _createTemplate();
        }, 95);
        _roiHost.Children.Add(generate);
        _roiHost.Children.Add(_error);
        imageBorder.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _roiHost };
        columns.Children.Add(imageBorder);

        var right = new Border { Background = Brushes.White, Margin = new Thickness(0, 5, 0, 0), Child = BuildRightPanel() };
        Grid.SetColumn(right, 1);
        columns.Children.Add(right);
        return root;
    }

    private UIElement BuildRightPanel()
    {
        var root = new Grid { Background = Brushes.White };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "列表", Margin = new Thickness(14, 10, 0, 9), FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(76, 76, 76)) });
        BuildTemplateFields();
        _configList.SetRows(_parameterRows);
        // 原厂 HPPyramidFeaturepatMatchDialogUIStyle.xaml:857-860 分组标题=配置参数（Tab_Match Params 是资源键）。
        var group = new GroupBox { Header = "配置参数", BorderBrush = Brushes.Transparent, Padding = new Thickness(5), Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _configList } };
        Grid.SetRow(group, 1);
        root.Children.Add(group);
        return root;
    }

    private void BuildTemplateFields()
    {
        var values = _draft.CaptureFields();
        var schema = ContourMatchFields.Schema;
        var group = new NativeParameterGroup("Tab_Match Params");
        foreach (var presentation in schema.Presentation!.Fields.Where(field => field.Layout?.Page == ContourMatchFields.TemplatePage).OrderBy(field => field.Order))
        {
            var port = schema.Inputs.Single(item => item.Id == presentation.FieldId);
            var editor = new DeclaredFieldRow(port, presentation.DisplayName, values.Read(port.Id), setting =>
            {
                _draft.ApplyFields(new Dictionary<string, ParameterSetting> { [port.Id] = setting }, candidate => _acceptCandidate?.Invoke(candidate));
            }, ReferenceFieldOptions.Empty, presentation: presentation, showClearButton: false);
            _fieldRows.Add(editor);
            _parameterRows.Add(new NativeParameterRow(group, editor));
        }
    }

    private bool TryCaptureDocument(out RecordValue document)
    {
        document = SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty;
        if (_roiEditor is null)
        {
            _error.Text = "区域编辑器未初始化。";
            return false;
        }
        if (!_roiEditor.TryCapture(out document, out var error))
        {
            _error.Text = string.IsNullOrEmpty(error) ? "请先完成当前区域绘制。" : error;
            return false;
        }
        _error.Text = "";
        return true;
    }

    /// <summary>借宿主当前输入帧（原厂 GetCurImage 的语义对应），重挂 ROI 编辑器。</summary>
    private void SelectCurrentImage()
    {
        _error.Text = "";
        if (_frames is null) { _error.Text = "当前没有可用图像来源。"; return; }
        using var lease = _frames.Borrow(ContourMatchSchema.ImageInput, interactive: false);
        if (lease is null || !lease.IsCurrent) { _error.Text = "当前输入图像尚未发布，请先运行图像来源。"; return; }
        _frames.Refresh(); // 让 NativeRoiEditHost 丢弃旧回执、重挂当前帧（原厂 InitData+DoClear 语义）
    }

    private void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryCaptureDocument(out var document)) return;
        _acceptDocument(document);
        DialogResult = true;
    }

    private static Button TextButton(string text, RoutedEventHandler handler, double width)
        => new()
        {
            Content = text, Width = width, Height = 28, Margin = new Thickness(0, 7, 6, 5),
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(255, 132, 0))
        };

    private static Button IconButton(string name, string tooltip, Action action)
    {
        var button = new Button
        {
            Width = 34, Height = 34, ToolTip = tooltip, Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Content = new Image { Width = 24, Height = 24, Stretch = Stretch.None, Source = new BitmapImage(new Uri(ResourceRoot + "Composition/ParameterPresentation/resources/" + name, UriKind.Relative)) }
        };
        button.Click += (_, _) => action();
        return button;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _roiEditor?.Dispose();
        _roiEditor = null;
        foreach (var row in _parameterRows) row.Dispose();
        foreach (var field in _fieldRows) field.Dispose();
        _parameterRows.Clear();
        _fieldRows.Clear();
    }
}
