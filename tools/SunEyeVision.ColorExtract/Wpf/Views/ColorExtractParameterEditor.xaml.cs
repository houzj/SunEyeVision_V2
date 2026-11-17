using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ColorExtract.Wpf.Design;

namespace SunEyeVision.Tool.ColorExtract.Wpf.Views;

/// <summary>设计器与运行时共用同一套参数组装链，不另造预览模型。</summary>
public partial class ColorExtractParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private event Action? ViewChanged;
    private bool _disposed;
    private long _generation;
    private ColorExtractRangePanel? _rangePanel;

    public ColorExtractParameterEditor()
    {
        InitializeComponent();
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var context = ColorExtractDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public ColorExtractParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        InitializeComponent();
        Bind(draft, options);
    }

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var generation = ++_generation;
        Editor.Changed -= OnFieldsChanged;
        var panel = new ColorExtractRangePanel(draft, options.AcceptCandidate);
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            Runtime.ColorExtractFields.ColorRange
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(Runtime.ColorExtractFields.ColorRange, panel, InsertBefore: true));
        // 有图像输入就必须声明输入区落位；颜色卡片是本工具已声明的首个分组。
        Editor.Bind(draft, options with
        {
            InputSection = options.InputSection ?? Runtime.ColorExtractFields.RangeSection,
            SuppressedFieldRows = suppressed,
            AdditionalAnchoredContents = anchored
        });
        if (generation != _generation || _disposed) throw new InvalidOperationException("色彩提取参数组合已重新绑定或关闭。");
        Editor.Changed += OnFieldsChanged;
        _rangePanel = panel;
    }

    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
    { ObjectDisposedException.ThrowIf(_disposed, this); return Editor.ReadNumericModels(scope, fieldId); }
    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits || _rangePanel?.HasPendingEdits == true;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields) => Editor.HasPendingEditsOutside(excludedFields);
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public event Action? Changed { add => ViewChanged += value; remove => ViewChanged -= value; }
    private void OnFieldsChanged() => ViewChanged?.Invoke();
    public bool TryCapture(out FieldSettings fields, out string error)
        => Editor.TryCapture(out fields, out error);
    /// <summary>参数页就绪信号：字段组合在构造内同步完成。</summary>
    public Task ReadyTask => Task.CompletedTask;
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _generation++;
        Editor.Changed -= OnFieldsChanged;
        ViewChanged = null; _rangePanel = null; Editor.Dispose();
    }
}
