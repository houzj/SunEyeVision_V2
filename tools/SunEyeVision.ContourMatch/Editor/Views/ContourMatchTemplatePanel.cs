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
    private readonly ListView _modelList = new()
    {
        // 原厂 :96：FontSize=14、Foreground=#FF666666、BorderThickness=0、背景透明、铺满 Row1=*。
        FocusVisualStyle = null, // 去掉系统默认焦点虚线框（原厂 ListViewStyle1 模板无虚线）。
        FontSize = 14,
        Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
        BorderThickness = new Thickness(0), Background = Brushes.Transparent,
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
    };
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
        // 原厂编辑/删除按钮用 BottomButtonStyle（BottomCommands.xaml），需合并到本控件资源树。
        FocusVisualStyle = null; // 去掉宿主给整个面板的焦点虚线框（原厂面板无虚线）。
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Commands/BottomCommands.xaml", UriKind.Relative)
        });
        // 原厂 :96 ListView 显式 BorderThickness=0 + Background=透明（覆盖 ListViewStyle1 的边框），
        // 本方裸 ListView + 字段初始化时已设 BorderThickness=0/背景透明，等价原厂效果，不引入带依赖的样式。
        _editButton = Button("编辑模板", OpenEdit, 98);
        _deleteAllButton = Button("删除所有模板", ClearTemplate, 98);
        _modelList.SelectionChanged += OnSelectionChanged;
        _modelList.ItemContainerStyle = ListItemContainerStyle(); // 原厂 :180-204 行容器模板
        Content = BuildView();
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => UpdateSelection();

    private UIElement BuildView()
    {
        // 严格按原厂 featurematchconfigpanal.xaml:41-214 嵌套结构：
        // Border(#EADFDE,1) → Grid(列 * / 120)
        //   列0: Border(0,1,0,0,#EADFDE) → Grid(Margin=12,0,12,0) → [Row0=33 工具条 / Row1=* 列表]
        //   列1: StackPanel（无 Margin）→ Image(11,11,11,0 98x98) + Button(98x24 Margin7) ×2
        var border = new Border
        {
            // 原厂控件无自身宽高（featurematchconfigpanal.xaml:36-42），由页内容区 */120 列
            // 自适应铺满。本方经框架整页内容挂载到"特征模板"Tab，同样不设 Width，直接 Stretch。
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xDE)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(1),
            Background = new SolidColorBrush(Color.FromRgb(0xFC, 0xFC, 0xFC))
        };
        var outer = new Grid();
        outer.ColumnDefinitions.Add(new ColumnDefinition());          // 列0 = *
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 列1 = 120（原厂 :43）

        // ── 列0：原厂 :43 Border(0,0,1,0) 右竖线分隔列0/列1 ──
        var listColumnBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xDE)),
            BorderThickness = new Thickness(0, 0, 1, 0), UseLayoutRounding = true, SnapsToDevicePixels = true
        };
        var root = new Grid { Margin = new Thickness(12, 0, 12, 0) }; // 原厂 :44 Grid Column=0 Margin
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(33) }); // 原厂 :46
        root.RowDefinitions.Add(new RowDefinition());                              // 原厂 :47 *

        // 原厂 :49-94 UniformGrid Columns=2，每 cell 内 StackPanel(Horizontal) 包 SuperImageButton。
        var actions = new UniformGrid { Columns = 2 };
        actions.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, // 原厂 :50 无 VerticalAlignment（居中）
            Children = { SuperIconButton("创建", "fm_ic_creat", OpenCreate) }
        });
        actions.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { SuperIconButton("载入", "fm_ic_load", ImportTemplate) }
        });
        root.Children.Add(actions);
        // 原厂 :95：列表外层 Border(0,1,0,0,#FFEADFDE) 提供列表顶部横线（列头隐藏后
        // 该横线与 Row0 按钮条共同构成原厂截图上的"列表头带"视觉）。
        var listBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xDE)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _modelList
        };
        Grid.SetRow(listBorder, 1);
        root.Children.Add(listBorder);
        listColumnBorder.Child = root;
        outer.Children.Add(listColumnBorder);

        // ── 列1：原厂 :209-213 StackPanel（无 Margin），Image Margin=11,11,11,0，Button Margin=7 ──
        var side = new StackPanel();
        _preview.Source = Resource("特征模板_image.png");
        _preview.Margin = new Thickness(11, 11, 11, 0); // 原厂 :210
        side.Children.Add(_preview);
        side.Children.Add(_editButton);   // 原厂 :211 Margin=7 由 Button() 设置
        side.Children.Add(_deleteAllButton); // 原厂 :212
        Grid.SetColumn(side, 1);
        outer.Children.Add(side);

        border.Child = outer;

        // 原厂 :44 Grid Column=0 Margin=12,0,12,0 直接铺满，无 DockPanel（DockPanel 会在固定宽度外
        // 露出外层 #FCFCFC 背景形成浅灰块/横线）。错误提示作为覆盖层放 root 内，不参与布局。
        Grid.SetRow(_error, 1);
        _error.VerticalAlignment = VerticalAlignment.Bottom;
        _error.Margin = new Thickness(8, 0, 8, 4);
        _error.IsHitTestVisible = false;
        root.Children.Add(_error);
        return border;
    }

    private void OpenCreate() { _error.Text = ""; ShowConfigWindow(); }
    private void OpenEdit() { _error.Text = ""; ShowConfigWindow(); }

    private void ShowConfigWindow()
    {
        var initial = CurrentTrainingRois();
        // 样本编辑复用平台 NativeRoiEditHost，必须拿到调试帧来源；无帧来源时明确提示，不降级为空画布。
        if (_frames is null)
        {
            _error.Text = "参数页尚未接入调试输入：先对节点执行一次单步调试后再编辑模板样本。";
            return;
        }
        // 按原厂 xaml 逐字重写的新窗口（TemplateConfigWindow.xaml），替换原手写近似壳。
        var window = new TemplateConfigWindow(
            _draft, _acceptCandidate, CreateTemplateFromWindow, ExportTemplate, ImportTemplate,
            AcceptTrainingRois, initial, _frames)
        { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private RecordValue CurrentTrainingRois()
        => _draft.CaptureFields().Read(ContourMatchFields.TrainingRois) is ConstantSetting { Value: RecordValue document }
            ? document
            : SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty;

    private void AcceptTrainingRois(RecordValue document)
    {
        try
        {
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting> { [ContourMatchFields.TrainingRois] = new ConstantSetting(document) },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (ArgumentException failure) { _error.Text = failure.Message; }
    }

    /// <summary>窗口内"生成模型"：样本已由窗口回写草稿，这里用草稿声明（含 TrainingRois）建模板。</summary>
    private void CreateTemplateFromWindow()
    {
        _error.Text = "";
        if (_frames is null) { _error.Text = "缺少调试帧来源，无法生成模型。"; return; }
        ContourMatchSettings settings;
        try { settings = ReadSettings(); } catch (Exception failure) { _error.Text = failure.Message; return; }
        NativeRoiInputLease? lease = null;
        try
        {
            lease = _frames.CaptureInputs(new[] { ContourMatchSchema.ImageInput });
            if (lease is null || !lease.IsCurrent) { _error.Text = "请选择与输入一致的已发布图像后再生成模型。"; return; }
            var image = lease.Inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput)
                ?? throw new InvalidOperationException("缺少输入图像，无法建立模型。");
            // 样本来自 settings.TrainingRois（窗口绘制并回写）；无样本时 ContourMatchTemplate.Create 明确报错。
            Apply(ContourMatchTemplateBuilder.Create(image, null, settings));
        }
        catch (ContourMatchCapabilityException failure) { _error.Text = failure.Message; }
        catch (Exception failure) { _error.Text = "模型创建失败：" + failure.Message; }
        finally { lease?.Dispose(); }
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
        var nameBox = new TextBox { Text = name, BorderThickness = new Thickness(0), MinWidth = 80, MinHeight = 20, MaxLength = 63, Foreground = Brushes.DimGray, VerticalContentAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nameBox, 1);
        row.Children.Add(nameBox);
        // 原厂 :137-168：操作列是 7 列 Grid(1,*,1,*,1,*,1)，上移/下移/删除/勾选各占一个 * 列，均 Height=24。
        var ops = new Grid();
        foreach (var width in new[] { new GridLength(1), new GridLength(1, GridUnitType.Star), new GridLength(1), new GridLength(1, GridUnitType.Star), new GridLength(1), new GridLength(1, GridUnitType.Star), new GridLength(1) })
            ops.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var up = IconButton("特征模板_ic_排序上_normal.png", "上移", () => { });
        Grid.SetColumn(up, 1);
        ops.Children.Add(up);
        var down = IconButton("特征模板_ic_排序下_normal.png", "下移", () => { });
        Grid.SetColumn(down, 3);
        ops.Children.Add(down);
        var delete = IconButton("delete_normal.png", "删除", ClearTemplate);
        Grid.SetColumn(delete, 5);
        ops.Children.Add(delete);
        var select = new CheckBox { ToolTip = "加入搜索列表", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(select, 7);
        ops.Children.Add(select);
        Grid.SetColumn(ops, 2);
        row.Children.Add(ops);
        return row;
    }

    /// <summary>
    /// 原厂 featurematchconfigpanal.xaml:180-204 行容器：Border Height=30 白底，
    /// IsSelected=true 时背景 #FFF2F4F7。本方单模型现状不含 IsCorrect=false 红框触发。
    /// </summary>
    private static Style ListItemContainerStyle()
    {
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.BackgroundProperty, Brushes.White);
        border.SetValue(Border.HeightProperty, 30d);
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(ListViewItem)) { VisualTree = border };
        template.Triggers.Add(new Trigger
        {
            Property = ListBoxItem.IsSelectedProperty,
            Value = true,
            Setters = { new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7)), "bd") }
        });
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private void UpdateSelection()
    {
        var has = _modelList.SelectedItem is not null || CurrentPayload().Length != 0;
        _editButton.IsEnabled = has;
        _deleteAllButton.IsEnabled = has;
    }

    /// <summary>
    /// 原厂 featurematchconfigpanal.xaml:211-212：编辑模板/删除所有模板 =
    /// Height=24 Width=98 Margin=7 VerticalAlignment=Top Style=BottomButtonStyle。
    /// </summary>
    private Button Button(string text, Action action, double width)
    {
        var button = new Button
        {
            Content = text, Width = width, Height = 24, Margin = new Thickness(7),
            VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left,
            Style = Resources["BottomButtonStyle"] as Style
        };
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>
    /// 原厂特征模板面板创建/载入按钮：commomcontrol:SuperImageButton 三态
    /// （featurematchconfigpanal.xaml:51-92），Height=20，图标+文字 Padding=6,0,0,0，Foreground=#808080。
    /// 用平台已迁移控件而非手写 Button 近似（避免装配层视觉差异）。
    /// </summary>
    private static System.Windows.Controls.Control SuperIconButton(string text, string iconBase, Action action)
    {
        FrameworkElement State(string icon) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new Image { Source = Resource(icon), Stretch = Stretch.None },
                new TextBlock { Text = text, Padding = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)) }
            }
        };
        var button = new SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Commom.SuperImageButton
        {
            Height = 20, // 原厂 :51 SuperImageButton 只设 Height，宽度由 StackPanel 内容决定
            NormalConnent = State(iconBase + "_normal.png"),
            PressedConnent = State(iconBase + "_focus.png"),
            MOverConnent = State(iconBase + "_focus.png")
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button IconButton(string icon, string tooltip, Action action)
    {
        // 原厂 :147-164 ImageButtonStyle 按钮：Height=24、透明背景、无边框，宽度填满所在 * 列。
        var button = new Button { Height = 24, ToolTip = tooltip, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Content = new Image { Width = 20, Height = 20, Stretch = Stretch.None, Source = Resource(icon) } };
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
