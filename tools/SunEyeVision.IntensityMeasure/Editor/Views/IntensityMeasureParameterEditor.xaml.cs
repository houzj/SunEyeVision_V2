using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.IntensityMeasure.Runtime;

namespace SunEyeVision.Tool.IntensityMeasure.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：两个页签（基本参数 / 结果显示）全部来自参数声明，
/// 「基本参数」页使用平台标准 ROI/Assist 装配，工具只声明字段身份；
/// 「结果显示」页的图形分组与文本行由 <see cref="IntensityMeasureDisplayUi"/> 组合声明。
/// 宿主仍独占执行、捕获与释放。
/// </summary>
public partial class IntensityMeasureParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private static readonly NativeStandardBasicPageSpec BasicPageSpec = new()
    {
        RoiFieldIds = IntensityMeasureRoi.Ports.Select(port => port.Id).ToArray(),
        PositionCorrectionFieldId = IntensityMeasureFields.PositionCorrectionEnabled,
        ComposeRoi = (inputs, attach, observe) => IntensityMeasureRoi.Add(inputs, attach, observe),
        ComposeAssist = IntensityMeasureAssist.Add,
        CreateRoiDocument = IntensityMeasureLocalRoi.Create
    };

    private bool _disposed;
    private NativeStandardBasicPage? _basicPage;

    public IntensityMeasureParameterEditor()
    {
        InitializeComponent();
    }

    public IntensityMeasureParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        // 旧方案没有新增的「位置修正」开关时补一个 false，避免行显示为“未设置/设置”。
        if (draft.CaptureFields().Read(IntensityMeasureFields.PositionCorrectionEnabled) is UnsetSetting)
            draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [IntensityMeasureFields.PositionCorrectionEnabled] = new ConstantSetting(false)
            }, _ => { });
        var previousPageBuilt = options.PageBuilt;
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            IntensityMeasureFields.RoiSectionAnchor
        };
        var composed = IntensityMeasureDisplayUi.Configure(options with { SuppressedFieldRows = suppressed }) with
        {
            InputSection = options.InputSection ?? "基本参数",
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>())
                .Concat(new[] { "基本参数", "结果显示" })
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            PageBuilt = (page, fields, input) =>
            {
                previousPageBuilt?.Invoke(page, fields, input);
                if (page != "基本参数" || input is not DeclaredInputFieldsView inputs) return;
                _basicPage?.Dispose();
                _basicPage = NativeStandardBasicPage.Compose(inputs, fields, options, BasicPageSpec);
            }
        };
        Editor.Bind(draft, composed);
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits;
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => Editor.ReadNumericModels(scope, fieldId);
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out FieldSettings fields, out string error) => Editor.TryCapture(out fields, out error);
    /// <summary>参数页就绪信号：两个页签在 Bind 内同步组装，无异步依赖。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _basicPage?.Dispose();
        _basicPage = null;
        Editor.Dispose();
    }
}
