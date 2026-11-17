using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ColorExtract.Wpf.Design;

namespace SunEyeVision.Tool.ColorExtract.Wpf.Views;

/// <summary>设计器与运行时共用同一套参数组装链，不另造预览模型。</summary>
public partial class ColorExtractParameterEditor : UserControl, IToolSettingsView
{
    private event Action? ViewChanged;
    private bool _disposed;
    private long _generation;

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
        Editor.Bind(draft, options with { InputSection = null });
        if (generation != _generation || _disposed) throw new InvalidOperationException("色彩提取参数组合已重新绑定或关闭。");
        Editor.Changed += OnFieldsChanged;
    }

    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
    { ObjectDisposedException.ThrowIf(_disposed, this); return Editor.ReadNumericModels(scope, fieldId); }
    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields) => Editor.HasPendingEditsOutside(excludedFields);
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public event Action? Changed { add => ViewChanged += value; remove => ViewChanged -= value; }
    private void OnFieldsChanged() => ViewChanged?.Invoke();
    public bool TryCapture(out FieldSettings fields, out string error)
        => Editor.TryCapture(out fields, out error);
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _generation++;
        Editor.Changed -= OnFieldsChanged;
        ViewChanged = null; Editor.Dispose();
    }
}
