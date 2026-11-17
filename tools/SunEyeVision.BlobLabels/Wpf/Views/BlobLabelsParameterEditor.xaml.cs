using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
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
    private BlobLabelsThresholdAreaPanel? _thresholdPanel;
    private bool _disposed;

    public BlobLabelsParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.BlobLabelsDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
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
        var composed = BlobLabelsDisplayUi.Configure(options) with
        {
            InputSection = options.InputSection ?? Design.BlobLabelsDesignData.InputPage
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
        Editor.Dispose();
    }
}
