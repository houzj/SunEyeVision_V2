using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobLabels.Runtime;

namespace SunEyeVision.Tool.BlobLabels.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：三个页签（基本参数 / 运行参数 / 结果显示）来自参数声明，
/// 阈值区域列表按原件 <c>ThresholdAreaPanel</c> 行为挂在「标签面积使能」之后。
/// 宿主仍独占执行、捕获与释放。
/// </summary>
public partial class BlobLabelsParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private static readonly NativeStandardBasicPageSpec BasicPageSpec = new()
    {
        RoiFieldIds = new[]
        {
            BlobLabelsSchema.MaskInput,
            BlobLabelsSchema.InspectionRoiInput,
            BlobLabelsSchema.AnnulusRoiInput,
            BlobLabelsSchema.MaskRegionInput,
            BlobLabelsRoi.LocalDocumentId
        }
        .Concat(BlobLabelsRoi.Ports.Select(port => port.Id))
        .Concat(BlobLabelsAssist.Ports.Select(port => port.Id))
        .Append(BlobLabelsSchema.FixtureInput)
        .Distinct(StringComparer.Ordinal)
        .ToArray(),
        PositionCorrectionFieldId = BlobLabelsFields.PositionCorrectionEnabled,
        ComposeRoi = (inputs, attach, observe) => BlobLabelsRoi.Add(inputs, attach, observe),
        ComposeAssist = BlobLabelsAssist.Add,
        CreateRoiDocument = BlobLabelsLocalRoi.Create
    };

    private BlobLabelsThresholdAreaPanel? _thresholdPanel;
    private NativeStandardBasicPage? _basicPage;
    private bool _disposed;

    public BlobLabelsParameterEditor()
    {
        InitializeComponent();
    }

    public BlobLabelsParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var panel = new BlobLabelsThresholdAreaPanel(draft, options.AcceptCandidate);
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            BlobLabelsFields.ThresholdArea
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(BlobLabelsFields.LabelWiseAreaSelectEnabled, panel));
        var displayOptions = BlobLabelsDisplayUi.Configure(options);
        var composed = displayOptions with
        {
            InputSection = options.InputSection ?? "鍩烘湰鍙傛暟",
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>())
                .Concat(new[] { "基本参数", "运行参数", "结果显示" })
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            PageBuilt = (page, fields, input) =>
            {
                options.PageBuilt?.Invoke(page, fields, input);
                if (page != "基本参数" || input is not DeclaredInputFieldsView inputs) return;
                _basicPage?.Dispose();
                _basicPage = NativeStandardBasicPage.Compose(inputs, fields, displayOptions, BasicPageSpec);
            }
        };
        Editor.Bind(draft, composed with { SuppressedFieldRows = suppressed, AdditionalAnchoredContents = anchored });
        _thresholdPanel = panel;
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits || _thresholdPanel?.HasPendingEdits == true;
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => Editor.ReadNumericModels(scope, fieldId);
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out FieldSettings fields, out string error) => Editor.TryCapture(out fields, out error);
    /// <summary>参数页就绪信号：阈值区域面板在 Bind 内同步构建完成，无异步依赖。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _thresholdPanel = null;
        _basicPage?.Dispose();
        _basicPage = null;
        Editor.Dispose();
    }
}
