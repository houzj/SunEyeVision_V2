using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 FeatureMatchConfigPanal 的轮廓匹配组装。列表、预览、创建/载入、编辑和删除操作
/// 使用原厂资源和控件尺寸，模板生成窗口由原厂 900x534 配置窗口承载。
/// </summary>
public sealed class ContourMatchTemplatePanel : UserControl, IDisposable
{
    private const string ResourceRoot = "/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Input/WorkspaceControls/resources/";
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly NativeRoiFrameSource? _frames;
    private readonly ListView _modelList = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Height = 120 };
    private readonly Image _preview = new() { Width = 98, Height = 98, Stretch = Stretch.Uniform };
    private readonly Button _editButton;
    private readonly Button _deleteAllButton;
    private readonly TextBlock _error = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, Margin = new Thickness(8, 4, 8, 0) };
    private bool _disposed;

    public ContourMatchTemplatePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, NativeRoiFrameSource? frames)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _frames = frames;
        _editButton = Button("编辑模板", OpenEdit, 98);
        _deleteAllButton = Button("删除所有模板", ClearTemplate, 98);
        _modelList.SelectionChanged += OnSelectionChanged;
        Content = BuildView();
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => UpdateSelection();

    private UIElement BuildView()
    {
        var border = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(234, 223, 222)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(Color.FromRgb(252, 252, 252)) };
        var root = new Grid { Margin = new Thickness(12, 0, 12, 0) };
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(33) });
        root.RowDefinitions.Add(new RowDefinition());

        var actions = new UniformGrid { Columns = 2 };
        actions.Children.Add(TextIconButton("创建", "fm_ic_creat_normal.png", OpenCreate));
        actions.Children.Add(TextIconButton("载入", "fm_ic_load_normal.png", ImportTemplate));
        Grid.SetColumnSpan(actions, 2);
        root.Children.Add(actions);

        var listBorder = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(234, 223, 222)), BorderThickness = new Thickness(0, 1, 0, 0) };
        listBorder.Child = _modelList;
        Grid.SetRow(listBorder, 1);
        root.Children.Add(listBorder);

        var side = new StackPanel { Margin = new Thickness(7, 0, 0, 0) };
        _preview.Source = Resource("特征模板_image.png");
        side.Children.Add(_preview);
        side.Children.Add(_editButton);
        side.Children.Add(_deleteAllButton);
        Grid.SetColumn(side, 1);
        Grid.SetRowSpan(side, 2);
        root.Children.Add(side);

        var content = new DockPanel();
        content.Children.Add(root);
        content.Children.Add(_error);
        border.Child = content;
        return border;
    }

    private void OpenCreate() { _error.Text = ""; ShowConfigWindow(CreateTemplate); }
    private void OpenEdit() { _error.Text = ""; ShowConfigWindow(CreateTemplate); }

    private void ShowConfigWindow(Action createTemplate)
    {
        var window = new ContourMatchTemplateConfigWindow(_draft, _acceptCandidate, createTemplate, ExportTemplate, ImportTemplate) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void CreateTemplate()
    {
        _error.Text = "";
        if (_frames is null) { _error.Text = "参数页尚未接入调试输入：先对节点执行一次单步调试后再新建模型。"; return; }
        ContourMatchSettings settings;
        try { settings = ReadSettings(); } catch (Exception failure) { _error.Text = failure.Message; return; }
        NativeRoiInputLease? lease = null;
        try
        {
            lease = _frames.CaptureInputs(new[] { ContourMatchSchema.ImageInput, ContourMatchSchema.RoiInput });
            if (lease is null || !lease.IsCurrent) { _error.Text = "请选择与输入一致的已发布图像后再新建模型。"; return; }
            var image = lease.Inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput);
            if (image is null) { _error.Text = "缺少输入图像，无法建立模型。"; return; }
            var roi = lease.Inputs.Contains(ContourMatchSchema.RoiInput) ? lease.Inputs.Get<IReadOnlyRegion?>(ContourMatchSchema.RoiInput) : null;
            if (roi is null) { _error.Text = "请先连接搜索区域 ROI（原件必须框选区域）后再新建模型。"; return; }
            Apply(ContourMatchTemplateBuilder.Create(image, roi, settings));
        }
        catch (ContourMatchCapabilityException failure) { _error.Text = failure.Message; }
        catch (Exception failure) { _error.Text = "模型创建失败：" + failure.Message; }
        finally { lease?.Dispose(); }
    }

    private ContourMatchSettings ReadSettings()
    {
        var fields = _draft.CaptureFields();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in ContourMatchFields.All)
        {
            if (spec.Name == nameof(ContourMatchSettings.ModelData)) continue;
            values[spec.Id] = fields.Read(spec.Id) switch
            {
                ConstantSetting constant => constant.Value,
                UnsetSetting => throw new InvalidOperationException("模板参数“" + spec.Label + "”尚未赋值。"),
                _ => throw new InvalidOperationException("模板参数“" + spec.Label + "”必须是固定值，不能绑定引用或表达式。")
            };
        }
        return ContourMatchFields.Materialize(new InputValues(ContourMatchFields.Schema.Inputs, values));
    }

    private void ClearTemplate() { _error.Text = ""; Apply(""); }

    private void ExportTemplate()
    {
        var payload = CurrentPayload();
        if (payload.Length == 0) { _error.Text = "尚未创建模型，无法导出。"; return; }
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "轮廓匹配模型(*" + ContourMatchTemplate.FileExtension + ")|*" + ContourMatchTemplate.FileExtension, DefaultExt = ContourMatchTemplate.FileExtension, FileName = "ContourMatchTemplate" + ContourMatchTemplate.FileExtension };
        if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, payload, new UTF8Encoding(false));
    }

    private void ImportTemplate()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "轮廓匹配模型(*" + ContourMatchTemplate.FileExtension + ")|*" + ContourMatchTemplate.FileExtension };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var payload = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            if (!ContourMatchTemplate.TryDecode(payload, out _, out var error)) { _error.Text = "模型文件无效：" + error; return; }
            Apply(payload);
        }
        catch (Exception failure) { _error.Text = "读取模型文件失败：" + failure.Message; }
    }

    private string CurrentPayload() => _draft.CaptureFields().Read(ContourMatchFields.ModelData) is ConstantSetting { Value: string text } ? text : "";

    private void Apply(string payload)
    {
        try
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting> { [ContourMatchFields.ModelData] = new ConstantSetting(payload) }, candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (ArgumentException failure) { _error.Text = failure.Message; }
        finally { Reload(); }
    }

    private void Reload()
    {
        _modelList.Items.Clear();
        if (CurrentPayload().Length != 0) _modelList.Items.Add(ModelRow("轮廓模板"));
        UpdateSelection();
    }

    private Grid ModelRow(string name)
    {
        var row = new Grid { Height = 30, Background = Brushes.White };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = "1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Foreground = Brushes.DimGray });
        var nameBox = new TextBox { Text = name, BorderThickness = new Thickness(0), MinHeight = 20, Foreground = Brushes.DimGray, VerticalContentAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nameBox, 1);
        row.Children.Add(nameBox);
        var ops = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ops.Children.Add(IconButton("特征模板_ic_排序上_normal.png", "上移", () => { }));
        ops.Children.Add(IconButton("特征模板_ic_排序下_normal.png", "下移", () => { }));
        ops.Children.Add(IconButton("delete_normal.png", "删除", ClearTemplate));
        ops.Children.Add(new CheckBox { Width = 24, Height = 24, ToolTip = "加入搜索列表", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(ops, 2);
        row.Children.Add(ops);
        return row;
    }

    private void UpdateSelection()
    {
        var has = _modelList.SelectedItem is not null || CurrentPayload().Length != 0;
        _editButton.IsEnabled = has;
        _deleteAllButton.IsEnabled = has;
    }

    private static Button Button(string text, Action action, double width)
    {
        var button = new Button { Content = text, Width = width, Height = 24, Margin = new Thickness(7), Padding = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button TextIconButton(string text, string icon, Action action)
    {
        var button = Button(text, action, 96);
        button.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Image { Width = 20, Height = 20, Source = Resource(icon), Stretch = Stretch.None }, new TextBlock { Text = text, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray } } };
        return button;
    }

    private static Button IconButton(string icon, string tooltip, Action action)
    {
        var button = new Button { Width = 24, Height = 24, ToolTip = tooltip, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Content = new Image { Width = 20, Height = 20, Stretch = Stretch.None, Source = Resource(icon) } };
        button.Click += (_, _) => action();
        return button;
    }

    private static BitmapImage Resource(string imageName) => new(new Uri("pack://application:,,," + ResourceRoot + imageName, UriKind.Absolute));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _modelList.SelectionChanged -= OnSelectionChanged;
        Content = null;
    }
}
