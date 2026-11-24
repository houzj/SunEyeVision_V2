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
        // 原厂 :165 列表勾选列用 NormalCheckBoxStyle（CheckBoxStyle.xaml），
        // 与底栏锁定用的 CheckBoxStyle1 不是同一套，必须单独合并。
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/SharedStyles/checkboxstyle/checkboxstyle.xaml", UriKind.Relative)
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

    private void OpenCreate()
    {
        _error.Text = "";
        // 原厂新建窗口在整个对话期间拥有一个固定的 ModelItem 槽位：
        // 首次“生成模型”追加到该槽位，后续再次生成只更新它，不能每次点击都追加副本。
        _editIndex = _entries.Count;
        ShowConfigWindow(requireStoredImage: false);
    }

    private void OpenEdit()
    {
        _error.Text = "";
        _editIndex = _modelList.SelectedIndex >= 0 ? _modelList.SelectedIndex
            : (_entries.Count == 1 ? 0 : -1);
        if (_editIndex < 0)
        {
            _error.Text = "请先在列表中选择要编辑的模板。";
            return;
        }
        ShowConfigWindow(requireStoredImage: true);
    }

    private void ShowConfigWindow(bool requireStoredImage)
    {
        EnsureDeclaredDefaults();
        // 原厂语义（decompiled FeaturepatMatchConfigPanalData.cs:313-318）：编辑已有模板必须有存档原图，
        // 否则弹「模型不包含原图，不允许编辑」并且不打开窗口。
        if (requireStoredImage)
        {
            if (_editIndex < 0 || _editIndex >= _entries.Count)
            {
                _error.Text = "尚未创建模板：请先点“创建”生成模板。";
                return;
            }
            if (_entries[_editIndex].Template.Image is not { Length: > 0 })
            {
                _error.Text = "模型不包含原图，不允许编辑。请在创建模板时保持“模板存图”开启。";
                return;
            }
        }
        // 每模板独立样本：编辑已有模板用该模板存档的样本集；新建时样本为空（不继承其他模板）。
        var initial = _editIndex >= 0 && _editIndex < _entries.Count
            ? TemplateTrainingCodec.Decode(_entries[_editIndex].Training)
            : SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty;
        // 样本编辑复用平台 NativeRoiEditHost，必须拿到调试帧来源；无帧来源时明确提示，不降级为空画布。
        if (_frames is null)
        {
            _error.Text = "参数页尚未接入调试输入：先对节点执行一次单步调试后再编辑模板样本。";
            return;
        }
        // 按原厂 xaml 逐字重写的新窗口（TemplateConfigWindow.xaml），替换原手写近似壳。
        var window = new TemplateConfigWindow(
            _draft, _acceptCandidate, () => { }, ExportTemplate, ImportTemplate,
            AcceptTrainingRois, initial, _frames, templateIndex: Math.Max(_editIndex, 0),
             matchPointLock: requireStoredImage,
             createTemplateWithMasks: CreateTemplateFromWindow,
             commitTemplate: CommitTemplateFromWindow)
        { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private RecordValue CurrentTrainingRois()
        => _draft.CaptureFields().Read(ContourMatchFields.TrainingRois) is ConstantSetting { Value: RecordValue document }
            ? document
            : SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty;

    /// <summary>
    /// 样本回写：写入当前编辑模板自己的样本集（原厂每个 ModelItem 有独立 modelData.ItemsData），
    /// 不再写全局字段，避免多模板共用同一批样本。
    /// </summary>
    private void AcceptTrainingRois(RecordValue document)
    {
        if (_editIndex >= 0 && _editIndex < _entries.Count)
        {
            _entries[_editIndex] = _entries[_editIndex] with { Training = TemplateTrainingCodec.Encode(document) };
            WriteEntries();
            return;
        }
        // 新建模板尚未进入 ModelItem 列表；原厂不会把会话内 ItemsData
        // 提前写入宿主参数。Confirm 时由 CommitTemplateFromWindow 一次提交。
    }

    /// <summary>窗口内"生成模型"：样本已由窗口回写草稿，这里用草稿声明（含 TrainingRois）建模板。</summary>
    private string? CreateTemplateFromWindow(TemplateImageSnapshot? selectedImage, RecordValue trainingDocument,
        TemplateMaskSnapshot maskSnapshot)
    {
        _error.Text = "";
        if (_frames is null && selectedImage is null) throw new InvalidOperationException("缺少调试帧来源，无法生成模型。");
        ContourMatchSettings settings;
        try { settings = ReadSettings(); } catch (Exception failure) { throw new InvalidOperationException(failure.Message, failure); }
        NativeRoiInputLease? lease = null;
        try
        {
            IReadOnlyImage? image = null;
            if (selectedImage is null)
            {
                lease = _frames!.CaptureInputs(new[] { ContourMatchSchema.ImageInput });
                if (lease is null || !lease.IsCurrent) throw new InvalidOperationException("请选择与输入一致的已发布图像后再生成模型。");
                image = lease.Inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput)
                    ?? throw new InvalidOperationException("缺少输入图像，无法建立模型。");
            }
            // 样本取当前模板自己的样本集（原厂每 ModelItem 独立）；无样本时 Create 明确报错，不退化全图。
            // 原厂新建模板的 ItemsData 属于当前编辑会话；窗口确认前会先把它回写到
            // TrainingRois。新建模板不能继续取空集合，否则“画了 ROI 后生成模型”会
            // 退化成无训练域，随后模板列表再打开也看不到刚画的样本。
            var training = trainingDocument;
            // 擦除掩膜（原厂 otherMask）与屏蔽掩膜（otherMask2）由窗口会话分别决定。
            // 未修改的层沿用存档值；明确清空的层以 null 为当前值，不能用 null-coalescing
            // 回退到旧载荷，否则会出现“画布已清空、建模仍使用旧擦除区域”。
            var currentEntries = ReadEntries();
            var currentTemplate = _editIndex >= 0 && _editIndex < currentEntries.Count
                ? currentEntries[_editIndex].Template : null;
            var resolvedMasks = TemplateMaskResolver.Resolve(maskSnapshot, currentTemplate);
            var effectiveWidth = resolvedMasks.Width;
            var effectiveHeight = resolvedMasks.Height;
            var effectiveEraseMask = resolvedMasks.EraseMask;
            var effectivePaintMask = resolvedMasks.PaintMask;
            // 擦除层与屏蔽层方向相反（原厂 otherMask 扣除 / otherMask2 并入），
            // 必须分别传给建模端，不能先并集成一个掩膜。
            var built = selectedImage is not null
                ? ContourMatchTemplateBuilder.CreateGray8(selectedImage.GrayPixels, selectedImage.Width, selectedImage.Height,
                    settings with { TrainingRois = training },
                    effectiveEraseMask, effectiveWidth, effectiveHeight,
                    effectivePaintMask, effectiveWidth, effectiveHeight)
                : ContourMatchTemplateBuilder.Create(image!, null, settings with { TrainingRois = training },
                    effectiveEraseMask, effectiveWidth, effectiveHeight,
                    effectivePaintMask, effectiveWidth, effectiveHeight);
            // 建模时使用合并后的有效训练域，但持久化仍保留原厂的 two-mask 状态，
            // 这样再次打开窗口时 fun/fun2 可以分别继续编辑。
            var builtTemplate = ContourMatchTemplate.Decode(built)
                .WithMasks(effectiveEraseMask, effectivePaintMask, effectiveWidth, effectiveHeight);
            var encodedTemplate = builtTemplate.Encode();
            // 原厂在窗口内生成模型只更新当前会话 ModelData；只有“确定”后
            // AddModelToModuleCommandExecute 才把 ModelItem 放入模板列表。
            return encodedTemplate;
        }
        catch (ContourMatchCapabilityException failure) { _error.Text = failure.Message; throw; }
        catch (Exception failure) { _error.Text = "模型创建失败：" + failure.Message; throw; }
        finally { lease?.Dispose(); }
    }

    private void CommitTemplateFromWindow(string payload, string training)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidOperationException("模板数据为空，不能提交到模板列表。");
        try
        {
            // 配置窗口的会话载荷已经是完整模板集合；原厂此时才执行
            // AddModelToModuleCommandExecute，把 ModelItem 一次性提交到列表。
            var entries = ContourMatchTemplateSet.DecodeEntries(payload).ToList();
            if (_editIndex >= 0 && _editIndex < entries.Count)
                entries[_editIndex] = entries[_editIndex] with { Training = training };
            _entries = entries;
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting>
                {
                    [ContourMatchFields.ModelData] = new ConstantSetting(ContourMatchTemplateSet.Encode(entries))
                },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
            Reload();
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            // 导入/旧调用仍可能传入单模型载荷，保留原有单项提交路径。
            Apply(payload, training, throwOnError: true);
        }
    }

    private void CreateTemplate()
    {
        _error.Text = "";
        if (_frames is null) { _error.Text = "参数页尚未接入调试输入：先对节点执行一次单步调试后再新建模型。"; return; }
        ContourMatchSettings settings;
        try { settings = ReadSettings(); } catch (Exception failure) { throw new InvalidOperationException(failure.Message, failure); }
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
        catch (ContourMatchCapabilityException failure) { _error.Text = failure.Message; throw; }
        catch (Exception failure) { _error.Text = "模型创建失败：" + failure.Message; throw; }
        finally { lease?.Dispose(); }
    }

    private void EnsureDeclaredDefaults()
    {
        var fields = _draft.CaptureFields();
        var changes = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
        foreach (var spec in ContourMatchFields.All)
        {
            if (spec.DefaultPresence != FieldDefaultPresence.UseDeclaredDefault) continue;
            if (fields.Read(spec.Id) is UnsetSetting)
                changes[spec.Id] = new ConstantSetting(spec.Default);
        }
        if (changes.Count != 0)
            _draft.ApplyFields(changes, candidate => _acceptCandidate?.Invoke(candidate));
    }
    private ContourMatchSettings ReadSettings()
    {
        var fields = _draft.CaptureFields();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in ContourMatchFields.All)
        {
            // TrainingRois 由当前模板会话单独传给建模入口，不参与匹配参数读取。
            // ModelData 仍是声明必需的字符串端口；新建模时用空载荷占位，生成后的模板由面板写回。
            if (spec.Name == nameof(ContourMatchSettings.TrainingRois))
            {
                values[spec.Id] = CurrentTrainingRois();
                continue;
            }
            if (spec.Name == nameof(ContourMatchSettings.ModelData))
            {
                values[spec.Id] = string.Empty;
                continue;
            }
            values[spec.Id] = fields.Read(spec.Id) switch
            {
                ConstantSetting constant => constant.Value,
                UnsetSetting => throw new InvalidOperationException("模板参数“" + spec.Label + "”尚未赋值。"),
                _ => throw new InvalidOperationException("模板参数“" + spec.Label + "”必须是固定值，不能绑定引用或表达式。")
            };
        }
        return ContourMatchFields.Materialize(new InputValues(ContourMatchFields.Schema.Inputs, values));
    }

    private void ClearTemplate() { _error.Text = ""; _editIndex = -1; Apply(""); }

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

    /// <summary>当前编辑的模板序号；-1 表示新建（追加）。原厂 AddModelToModule 追加、编辑写回选中项。</summary>
    private int _editIndex = -1;

    private void Apply(string payload, string? training = null, bool throwOnError = false)
    {
        try
        {
            var entries = ReadEntries();
            if (payload.Length == 0)
            {
                // 删除所有模板
                entries.Clear();
            }
            else
            {
                var template = ContourMatchTemplate.Decode(payload);
                if (_editIndex >= 0 && _editIndex < entries.Count)
                    entries[_editIndex] = entries[_editIndex] with
                    {
                        Template = template,
                        Training = training ?? entries[_editIndex].Training
                    };
                else
                {
                    _editIndex = entries.Count;
                    entries.Add(new ContourMatchTemplateSet.Entry(template,
                        ContourMatchTemplateSet.NewTemplateName(entries.Count), true, training ?? ""));
                }
            }
            _entries = entries;
            var encoded = entries.Count == 0 ? "" : ContourMatchTemplateSet.Encode(entries);
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting> { [ContourMatchFields.ModelData] = new ConstantSetting(encoded) },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (Exception failure) when (failure is ArgumentException or ContourMatchCapabilityException)
        {
            _error.Text = failure.Message;
            if (throwOnError) throw;
        }
        finally
        {
            // 保留当前窗口的槽位，保证重复 Match/Confirm 都更新同一个 ModelItem。
            // 面板关闭后下一次 OpenCreate/OpenEdit 会重新设定它。
            Reload();
        }
    }

    /// <summary>模板集合缓存；列表操作都作用在它上面，再整体写回 ModelData。</summary>
    private List<ContourMatchTemplateSet.Entry> _entries = new();

    private void Reload()
    {
        var previous = _modelList.SelectedIndex;
        _modelList.Items.Clear();
        _entries = ReadEntries();
        for (var index = 0; index < _entries.Count; index++)
            _modelList.Items.Add(ModelRow(_entries[index], index));
        if (_modelList.Items.Count > 0)
            _modelList.SelectedIndex = Math.Clamp(previous, 0, _modelList.Items.Count - 1);
        UpdateSelection();
    }

    private List<ContourMatchTemplateSet.Entry> ReadEntries()
    {
        var payload = CurrentPayload();
        if (payload.Length == 0) return new();
        try
        {
            return ContourMatchTemplateSet.DecodeEntries(payload).ToList();
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            _error.Text = "模板数据无法解析：" + error.Message;
            return new();
        }
    }

    /// <summary>
    /// 把当前模板集合写回 ModelData；空集合写空串（等价"删除所有模板"）。
    /// <paramref name="reload"/> 为 false 时只持久化、不重建列表行——
    /// 勾选这类不改变行内容的操作按原厂数据绑定语义不应重建整表。
    /// </summary>
    private void WriteEntries(bool reload = true)
    {
        try
        {
            var payload = _entries.Count == 0 ? "" : ContourMatchTemplateSet.Encode(_entries);
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting> { [ContourMatchFields.ModelData] = new ConstantSetting(payload) },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            _error.Text = "写入模板失败：" + error.Message;
        }
        finally
        {
            if (reload) Reload();
        }
    }

    private Grid ModelRow(ContourMatchTemplateSet.Entry entry, int index)
    {
        // 背景保持透明：行底色由 ListItemContainerStyle 的 Border 提供，
        // 行内再铺白底会把选中态 #FFF2F4F7 盖掉。
        var row = new Grid { Height = 30, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        // 原厂 GridViewColumn 三列固定宽 28 / 92 / 106，操作列不随窗口拉伸。
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
        // 原厂序号绑定 ModelItem.Index（0 基），第一行显示 0。
        row.Children.Add(new TextBlock { Text = index.ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Foreground = Brushes.DimGray });
        var name = string.IsNullOrWhiteSpace(entry.Name) ? ContourMatchTemplateSet.NewTemplateName(index) : entry.Name;
        var nameBox = new TextBox { Text = name, BorderThickness = new Thickness(0), MinWidth = 80, MinHeight = 20, MaxLength = 63, Foreground = Brushes.DimGray, VerticalContentAlignment = VerticalAlignment.Center };
        // 名称编辑写回集合（原厂 ModelItem.Name + ModelNameTextChangedCommand）。
        nameBox.LostFocus += (_, _) =>
        {
            var text = nameBox.Text.Trim();
            if (text.Length == 0 || text == _entries[index].Name) return;
            _entries[index] = _entries[index] with { Name = text };
            WriteEntries();
        };
        // 原厂 ModelItem.ModelNameGotFocusCommandExcute（ModelItem.cs:479-489）：
        // 名称框获得焦点就把所在行设为 ListView 选中项，右侧预览随之切换。
        nameBox.GotFocus += (_, _) =>
        {
            if (_modelList.SelectedIndex != index) _modelList.SelectedIndex = index;
        };
        Grid.SetColumn(nameBox, 1);
        row.Children.Add(nameBox);
        // 原厂 :137-168：操作列是 8 列 Grid(1,*,1,*,1,*,1,*)，上移/下移/删除/勾选依次占 1/3/5/7。
        var ops = new Grid();
        foreach (var width in new[]
                 {
                     new GridLength(1), new GridLength(1, GridUnitType.Star),
                     new GridLength(1), new GridLength(1, GridUnitType.Star),
                     new GridLength(1), new GridLength(1, GridUnitType.Star),
                     new GridLength(1), new GridLength(1, GridUnitType.Star)
                 })
            ops.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var up = IconButton("特征模板_ic_排序上_normal.png", "上移", () =>
        {
            if (index <= 0) return;
            (_entries[index - 1], _entries[index]) = (_entries[index], _entries[index - 1]);
            WriteEntries();
        });
        Grid.SetColumn(up, 1);
        up.IsEnabled = index > 0;
        ops.Children.Add(up);
        var down = IconButton("特征模板_ic_排序下_normal.png", "下移", () =>
        {
            if (index >= _entries.Count - 1) return;
            (_entries[index + 1], _entries[index]) = (_entries[index], _entries[index + 1]);
            WriteEntries();
        });
        Grid.SetColumn(down, 3);
        down.IsEnabled = index < _entries.Count - 1;
        ops.Children.Add(down);
        var delete = IconButton("delete_normal.png", "删除", () =>
        {
            if (index >= _entries.Count) return;
            _entries.RemoveAt(index);
            WriteEntries();
        });
        Grid.SetColumn(delete, 5);
        ops.Children.Add(delete);
        // 原厂"加入搜索列表"勾选列：控制该模板是否参与搜索（UseMatchAllMode 关闭时生效）。
        var select = new CheckBox
        {
            ToolTip = "加入搜索列表",
            IsChecked = entry.Selected,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Style = Resources["NormalCheckBoxStyle"] as Style
        };
        select.Checked += (_, _) => SetSelected(index, true);
        select.Unchecked += (_, _) => SetSelected(index, false);
        // 原厂勾选列位于第 8 列（索引 7），与上移/下移/删除同属操作列。
        Grid.SetColumn(select, 7);
        ops.Children.Add(select);
        Grid.SetColumn(ops, 2);
        row.Children.Add(ops);
        return row;
    }

    private void SetSelected(int index, bool selected)
    {
        if (index >= _entries.Count || _entries[index].Selected == selected) return;
        _entries[index] = _entries[index] with { Selected = selected };
        // 勾选不改变行内容，只持久化，不重建列表（原厂 SelectedCommand 也不重建 ModelList）。
        WriteEntries(reload: false);
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
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
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
        // 原厂选中行只有底色变化；默认 ListViewItem 的焦点虚线框会出现一圈虚线，与原件不符。
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        return style;
    }

    private void UpdateSelection()
    {
        // 原厂 :218-225 的 DataTrigger：SelectedItem 为空时「编辑模板」禁用；
        // 「删除所有模板」在原厂没有禁用触发器，始终可用。
        _editButton.IsEnabled = _modelList.SelectedIndex >= 0;
        _deleteAllButton.IsEnabled = true;
        UpdatePreview();
    }

    /// <summary>
    /// 预览图：显示选中模板的训练原图缩略图（原厂右侧 98×98 预览）。
    /// 无选中项或无存档原图时回到占位图，不臆造预览。
    /// </summary>
    private void UpdatePreview()
    {
        var index = _modelList.SelectedIndex;
        if (index < 0 || index >= _entries.Count)
        {
            _preview.Source = Resource("特征模板_image.png");
            return;
        }
        var entry = _entries[index];
        var template = entry.Template;
        if (template.Image is not { Length: > 0 } pixels || template.ImageWidth <= 0 || template.ImageHeight <= 0)
        {
            _preview.Source = Resource("特征模板_image.png");
            return;
        }
        try
        {
            _preview.Source = BuildTemplatePreview(template, pixels,
                TrainingCenter(entry.Training, template.ImageWidth, template.ImageHeight));
        }
        catch (ArgumentException)
        {
            _preview.Source = Resource("特征模板_image.png");
        }
    }

    /// <summary>
    /// 原厂面板的 ImageSource 来自 ModelImageData，而不是裸的 CurImageDataA：
    /// RefreshModelImageData 后模型轮廓已经叠加到缩略图。这里用同一份训练图像
    /// 和 HALCON 模型轮廓生成只读缩略图，避免“列表已更新但右侧仍是旧图/空图”。
    /// </summary>
    private static Point TrainingCenter(string trainingPayload, int width, int height)
    {
        try
        {
            var document = TemplateTrainingCodec.Decode(trainingPayload);
            var centers = RoiDocumentValue.Items(document, RoiDocumentValue.Inspection)
                .Concat(RoiDocumentValue.Items(document, RoiDocumentValue.Mask))
                .Select(item => item.Get<RecordValue>("shape"))
                .Select(shape =>
                {
                    if (shape.Get<string>("kind") == "polygon")
                    {
                        var vertices = shape.Get<SequenceValue>("vertices").Cast<RecordValue>()
                            .Select(p => new Point(p.Get<double>("x"), p.Get<double>("y"))).ToArray();
                        return vertices.Length == 0
                            ? new Point(width / 2d, height / 2d)
                            : new Point((vertices.Min(p => p.X) + vertices.Max(p => p.X)) / 2d,
                                (vertices.Min(p => p.Y) + vertices.Max(p => p.Y)) / 2d);
                    }
                    var center = shape.Get<RecordValue>("center");
                    return new Point(center.Get<double>("x"), center.Get<double>("y"));
                }).ToArray();
            return centers.Length == 0
                ? new Point(width / 2d, height / 2d)
                : new Point(centers.Average(p => p.X), centers.Average(p => p.Y));
        }
        catch (Exception)
        {
            return new Point(width / 2d, height / 2d);
        }
    }

    private static BitmapSource BuildTemplatePreview(ContourMatchTemplate template, byte[] pixels, Point fallbackCenter)
    {
        var width = template.ImageWidth;
        var height = template.ImageHeight;
        var source = BitmapSource.Create(width, height, 96, 96,
            PixelFormats.Gray8, null, pixels, width);
        source.Freeze();
        var points = template.ReadOutlinePoints();
        var pose = template.PreviewPose;
        var column = pose?.Column ?? fallbackCenter.X;
        var row = pose?.Row ?? fallbackCenter.Y;
        var angle = pose?.Angle ?? 0d;
        var sx = pose?.ScaleX ?? 1d;
        var sy = pose?.ScaleY ?? 1d;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        Point Map(Point2 p) => new(
            column + cos * sx * p.X - sin * sy * p.Y,
            row + sin * sx * p.X + cos * sy * p.Y);
        var mapped = points.Select(Map).ToArray();
        var minX = mapped.Length == 0 ? 0 : mapped.Min(p => p.X);
        var minY = mapped.Length == 0 ? 0 : mapped.Min(p => p.Y);
        var maxX = mapped.Length == 0 ? width : mapped.Max(p => p.X);
        var maxY = mapped.Length == 0 ? height : mapped.Max(p => p.Y);
        var padding = Math.Max(4d, Math.Max(maxX - minX, maxY - minY) * 0.08d);
        var cropX = Math.Clamp((int)Math.Floor(minX - padding), 0, Math.Max(0, width - 1));
        var cropY = Math.Clamp((int)Math.Floor(minY - padding), 0, Math.Max(0, height - 1));
        var cropRight = Math.Clamp((int)Math.Ceiling(maxX + padding), cropX + 1, width);
        var cropBottom = Math.Clamp((int)Math.Ceiling(maxY + padding), cropY + 1, height);
        var cropWidth = Math.Max(1, cropRight - cropX);
        var cropHeight = Math.Max(1, cropBottom - cropY);
        var cropped = new CroppedBitmap(source, new Int32Rect(cropX, cropY, cropWidth, cropHeight));
        cropped.Freeze();
        // 原厂 RefreshModelImageData（decompiled OCRLocationModelPage.xaml.cs:1170-1215）：
        // 先按 98 长边等比算出缩略图尺寸并采样，再在缩略图坐标里逐点写绿点。
        // 若先画点再整体缩小，点会被一起缩到看不见；先缩图再画点才与原厂预览一致。
        var ratio = Math.Min(1d, PreviewEdge / (double)Math.Max(cropWidth, cropHeight));
        var targetWidth = Math.Max(1, (int)Math.Round(cropWidth * ratio));
        var targetHeight = Math.Max(1, (int)Math.Round(cropHeight * ratio));
        var scaled = new TransformedBitmap(cropped,
            new ScaleTransform((double)targetWidth / cropWidth, (double)targetHeight / cropHeight));
        scaled.Freeze();
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(scaled, new Rect(0, 0, targetWidth, targetHeight));
            // 模板窗口的 MultiPointShape 按原厂 RefreshPoints 逐点绘制 MicroPoint，
            // 不把相邻点重新连成折线。缩略图若连线，会比窗口显示出更多细节，
            // 造成“预览轮廓点更多”的视觉差异。
            var brush = new SolidColorBrush(Color.FromRgb(0x66, 0xFF, 0x00));
            brush.Freeze();
            foreach (var point in mapped)
            {
                var x = (point.X - cropX) * targetWidth / cropWidth;
                var y = (point.Y - cropY) * targetHeight / cropHeight;
                dc.DrawRectangle(brush, null, new Rect(x, y, 2, 2));
            }
        }
        var bitmap = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>原厂右侧预览图边长（featurematchconfigpanal.xaml:210 Width/Height=98）。</summary>
    private const int PreviewEdge = 98;

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













