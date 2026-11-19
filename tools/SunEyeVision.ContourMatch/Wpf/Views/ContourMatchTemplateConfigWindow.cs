using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 <c>FeaturepatMatchConfigPanalData.CreateModel</c> 创建的 900x534 模板配置窗口。
/// 视觉结构来自 HPPyramidFeaturepatMatchDialogUIStyle.xaml，模板生成仍由宿主回调执行。
/// </summary>
internal sealed class ContourMatchTemplateConfigWindow : Window
{
    private const string ResourceRoot = "/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/";
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly Action _createTemplate;
    private readonly Action _saveProject;
    private readonly Action _loadProject;
    private readonly List<DeclaredFieldRow> _fieldRows = new();
    private readonly List<NativeParameterRow> _parameterRows = new();
    private readonly ParamListView _configList = new();
    private readonly TextBlock _status = new() { Foreground = Brushes.DimGray, Margin = new Thickness(8, 0, 8, 0) };
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 8, 4) };

    public ContourMatchTemplateConfigWindow(
        IToolSettingsDraft draft,
        Action<FieldSettings>? acceptCandidate,
        Action createTemplate,
        Action saveProject,
        Action loadProject)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _createTemplate = createTemplate;
        _saveProject = saveProject;
        _loadProject = loadProject;

        Title = "模板配置";
        Width = 900;
        Height = 534;
        MinWidth = 900;
        MinHeight = 534;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(242, 243, 244));
        FontSize = 14;
        FontWeight = FontWeights.Normal;
        Content = BuildContent();
        Closed += (_, _) => DisposeRows();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(242, 243, 244)) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

        var body = new Border { Padding = new Thickness(10), Child = BuildBody() };
        Grid.SetRow(body, 0);
        root.Children.Add(body);

        var footer = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(230, 231, 233)) };
        var confirm = new Button { Content = "确定", Width = 66, Height = 24, Margin = new Thickness(0, 4, 8, 0) };
        confirm.Click += (_, _) => DialogResult = true;
        DockPanel.SetDock(confirm, Dock.Right);
        footer.Children.Add(confirm);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        return root;
    }

    private UIElement BuildBody()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition());

        var top = new StackPanel { Orientation = Orientation.Horizontal };
        top.Children.Add(TextButton("选择当前图像", (_, _) => { }, 95));
        top.Children.Add(TextButton("选择其他图像", (_, _) => { }, 95));
        top.Children.Add(new TextBlock { Text = "模板存图", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(10, 0, 0, 0) });
        top.Children.Add(new ToggleButton { IsChecked = true, Width = 42, Height = 24, Margin = new Thickness(-2, 0, 6, 0), Content = "开" });
        top.Children.Add(IconButton("ic_导出_normal.png", "保存模板工程", _saveProject));
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
        imageBorder.Child = BuildImageSurface();
        columns.Children.Add(imageBorder);

        var right = new Border { Background = Brushes.White, Margin = new Thickness(0, 5, 0, 0) };
        Grid.SetColumn(right, 1);
        right.Child = BuildRightPanel();
        columns.Children.Add(right);
        return root;
    }

    private UIElement BuildImageSurface()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition());
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Color.FromRgb(252, 252, 252)) };
        var tools = new[]
        {
            ("ic_箭头_normal.png", "移动图像"), ("ic_绘制扇形_normal.png", "创建扇形掩膜"),
            ("ic_绘制圆_normal.png", "创建扇圆形掩膜"), ("ic_绘制矩形_normal.png", "创建矩形掩膜"),
            ("ic_绘制多边形_normal.png", "创建多边形掩膜"), ("ic_中心定位_normal.png", "选择模板匹配中心")
        };
        foreach (var (imageName, tip) in tools) toolbar.Children.Add(IconToggle(imageName, tip));
        toolbar.Children.Add(IconButton("ic_生成学习模板_normal.png", "生成模板", _createTemplate));
        toolbar.Children.Add(IconToggle("ic_橡皮擦2_normal.png", "擦除轮廓点"));
        toolbar.Children.Add(IconToggle("ic_绘制坐标_normal.png", "绘制感兴趣区域"));
        toolbar.Children.Add(IconButton("ic_清空图片_normal.png", "清空", () => { }));
        toolbar.Children.Add(IconButton("ic_上一步_normal.png", "撤销", () => { }));
        toolbar.Children.Add(IconButton("ic_下一步_normal.png", "重做", () => { }));
        root.Children.Add(toolbar);

        var view = new Grid();
        var imageView = new SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.ImageViewBox { Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)) };
        view.Children.Add(imageView);
        var zoom = new UniformGrid { Columns = 4, Width = 150, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        zoom.Children.Add(IconButton("ic_放大_normal.png", "放大", () => { }));
        zoom.Children.Add(IconButton("ic_缩小_normal.png", "缩小", () => { }));
        zoom.Children.Add(new Button { Content = "1:1", Width = 34, Height = 34, ToolTip = "1:1" });
        zoom.Children.Add(new Button { Content = "全屏", Width = 34, Height = 34, ToolTip = "全屏" });
        view.Children.Add(zoom);
        Grid.SetRow(view, 1);
        root.Children.Add(view);
        return root;
    }

    private UIElement BuildRightPanel()
    {
        var root = new Grid { Background = Brushes.White };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(260) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.Children.Add(new TextBlock { Text = "列表", Margin = new Thickness(14, 10, 0, 9), FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(76, 76, 76)) });
        var list = new ListView { BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)), Margin = new Thickness(0, 0, 0, 0) };
        list.Items.Add(new TextBlock { Text = "轮廓模板", Height = 28, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(14, 0, 0, 0) });
        Grid.SetRow(list, 1);
        root.Children.Add(list);

        var group = new GroupBox { Header = "配置参数", BorderBrush = Brushes.Transparent, Padding = new Thickness(5) };
        BuildTemplateFields();
        _configList.SetRows(_parameterRows);
        group.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _configList };
        Grid.SetRow(group, 2);
        root.Children.Add(group);

        var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 4, 5, 0) };
        status.Children.Add(new TextBlock { Text = "匹配中心 X/Y", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray });
        status.Children.Add(new TextBlock { Text = "0 / 0", Margin = new Thickness(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray });
        status.Children.Add(new ToggleButton { Content = "锁定", Width = 48, Height = 24 });
        Grid.SetRow(status, 3);
        root.Children.Add(status);
        return root;
    }

    private void BuildTemplateFields()
    {
        var values = _draft.CaptureFields();
        var schema = ContourMatchFields.Schema;
        var group = new NativeParameterGroup("配置参数");
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

    private static Button TextButton(string text, RoutedEventHandler handler, double width)
    {
        var button = new Button { Content = text, Width = width, Height = 28, Margin = new Thickness(0, 7, 6, 5), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(255, 132, 0)) };
        button.Click += handler;
        return button;
    }

    private static ToggleButton IconToggle(string imageName, string tooltip)
    {
        var toggle = new ToggleButton { Width = 34, Height = 34, ToolTip = tooltip, Background = Brushes.Transparent, Content = Image(imageName) };
        return toggle;
    }

    private static Button IconButton(string imageName, string tooltip, Action action)
    {
        var button = new Button { Width = 34, Height = 34, ToolTip = tooltip, Padding = new Thickness(0), Background = Brushes.Transparent, Content = Image(imageName) };
        button.Click += (_, _) => action();
        return button;
    }

    private static Image Image(string imageName)
        => new() { Width = 24, Height = 24, Stretch = Stretch.None, Source = new BitmapImage(new Uri("pack://application:,,," + ResourceRoot + "Composition/ParameterPresentation/resources/" + imageName, UriKind.Absolute)) };

    private void DisposeRows()
    {
        foreach (var row in _parameterRows) row.Dispose();
        foreach (var field in _fieldRows) field.Dispose();
        _parameterRows.Clear();
        _fieldRows.Clear();
    }
}
