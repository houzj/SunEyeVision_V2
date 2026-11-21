using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：两个页签（模板参数 / 运行参数）来自参数声明，
/// 模型面板（新建/清空/导出/导入 + 模型摘要）挂在模板参数页「金字塔比例」之前，
/// 对应原件训练对话框 <c>HPPyramidMatchPat</c>（PyramidFeaturepatMatchConfigPanalData）的位置。
/// 宿主仍独占执行、捕获与释放。
/// </summary>
public partial class ContourMatchParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private ContourMatchTemplatePanel? _templatePanel;
    private NativeRoiEditHost? _roiHost;
    private INativeRoiEditDocument? _roiDocument;
    private NativeAssistUiDraftControls? _fixtureAssist;
    private DeclaredFieldRow? _fixtureSwitch;
    private bool _disposed;

    public ContourMatchParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.ContourMatchDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public ContourMatchParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var panel = new ContourMatchTemplatePanel(draft, options.AcceptCandidate, options.RoiFrames);
        // 原厂特征模板页只有模板列表/预览/创建/载入/编辑/删除；训练参数（尺度模式/速度尺度/特征尺度/
        // 阈值模式/边缘阈值/链长模式/最小链长/建模类型）只在 900x534 模板配置窗口右侧，不在参数页 Tab。
        // 证据：hppyramidfeaturepatmatchdialoguistyle.xaml:879-906 + IMVSContourMatchModuAlgorithmTab.xml。
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            ContourMatchFields.ModelData,
            // 训练参数只在模板配置窗口出现（原厂 hppyramid...xaml:879-906）。
            ContourMatchFields.PyramidMode, ContourMatchFields.PyramidScaleLevel, ContourMatchFields.PyramidScaleRLevel,
            ContourMatchFields.EdgeThresholdMode, ContourMatchFields.EdgeThreshold,
            ContourMatchFields.ChainMode, ContourMatchFields.MinChainLen, ContourMatchFields.MaskModel
            // 注意：input.roi 是执行输入呈现字段，不属于 settings presentation，不能加入 SuppressedFieldRows。
        };
        // 原厂 FeatureMatchConfigPanal 是 DataTemplate 根、独占"特征模板"Tab 内容区，无自身宽高：
        // docs/07-工具逆向/decompiled/ModuleControls/IMVSHPFeatureMatchModuControl/datatemplate/paramusercontroldatatemplate.xaml:7-9。
        // 因此走框架整页内容挂载，不再当参数行锚点插入（行宿主无法 Stretch 铺满全页）。
        var pageContents = (options.PageContents ?? new Dictionary<string, FrameworkElement>(StringComparer.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (pageContents.TryGetValue(ContourMatchFields.TemplatePage, out var existing) && !ReferenceEquals(existing, panel))
            throw new InvalidOperationException("特征模板页已声明其他整页内容，不能重复挂载。");
        pageContents[ContourMatchFields.TemplatePage] = panel;
        var previousPageBuilt = options.PageBuilt;
        var composed = ContourMatchDisplayUi.Configure(options) with
        {
            // 输入区必须落在已声明的分组上。ModelData 是隐藏的基本页锚点，承载原厂图像输入和 ROI 区域。
            InputSection = options.InputSection ?? ContourMatchFields.ImageInputSection,
            SuppressedFieldRows = suppressed,
            PageContents = pageContents,
            PageBuilt = (page, fields, input) =>
            {
                previousPageBuilt?.Invoke(page, fields, input);
                if (page == ContourMatchFields.BasicPage && input is DeclaredInputFieldsView inputs && options.RoiFrames is not null)
                {
                    // 原厂 Tab_ROI Area 单组：ROI 控件 + AssistGroup（位置修正）放进同一容器，一次插入，只生成一个组头。
                    var roiArea = new StackPanel();
                    // 原厂顺序：ROI 组合（绘制/继承/形状/掩膜）在前。
                    var roi = ContourMatchRoiComposer.Add(inputs, (fieldId, content, group) => roiArea.Children.Add(content));
                    _roiDocument = ContourMatchRoiComposer.CreateDocument(inputs);
                    // 原厂 IMVSContourMatchModuAlgorithmTab.xml:29 <ROISelection>Box Polygon</ROISelection>：
                    // 轮廓匹配只支持矩形与多边形，按此声明收敛形状按钮（平台默认是 Box/SectorRing/Sector/Polygon 四种）。
                    _roiHost = new NativeRoiEditHost(inputs, roi, options.RoiFrames, _roiDocument,
                        options.RoiDrawBlockReason, new[] { "Box", "Polygon" });
                    // AssistGroup（位置修正）在 ROI 区域内末尾：开关行 + 选择方式/输入行（原厂四状态截图）。
                    _fixtureSwitch = ContourMatchFixtureComposer.ComposeSwitch(inputs);
                    roiArea.Children.Add(_fixtureSwitch);
                    _fixtureAssist = ContourMatchFixtureComposer.Add(inputs, content => roiArea.Children.Add(content));
                    _fixtureSwitch.Changed += OnFixtureSwitchChanged;
                    ApplyFixtureEnabled();
                    fields.AddAfterField(ContourMatchSchema.ImageInput, roiArea, groupOverride: ContourMatchFields.RoiInputSection);
                }
            }
        };
        Editor.Bind(draft, composed);
        _templatePanel = panel;
    }

    private void OnFixtureSwitchChanged() => ApplyFixtureEnabled();

    private void ApplyFixtureEnabled()
    {
        if (_fixtureAssist is null || _fixtureSwitch is null) return;
        var enabled = _fixtureSwitch.Setting is ConstantSetting { Value: bool value } && value;
        _fixtureAssist.SetPositionCorrectionEnabled(enabled);
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits || _templatePanel?.HasPendingEdits == true;
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => Editor.ReadNumericModels(scope, fieldId);
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out FieldSettings fields, out string error) => Editor.TryCapture(out fields, out error);
    /// <summary>参数页就绪信号：模型面板在 Bind 内同步构建。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _roiHost?.Dispose();
        _roiHost = null;
        if (_fixtureSwitch is not null) _fixtureSwitch.Changed -= OnFixtureSwitchChanged;
        _fixtureSwitch = null;
        _fixtureAssist?.Dispose();
        _fixtureAssist = null;
        (_roiDocument as IDisposable)?.Dispose();
        _roiDocument = null;
        _templatePanel = null;
        Editor.Dispose();
    }
}

