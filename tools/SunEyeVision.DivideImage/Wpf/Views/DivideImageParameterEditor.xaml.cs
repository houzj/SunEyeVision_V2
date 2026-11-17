using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.DivideImage.Wpf.Views;

/// <summary>设计器与运行时共用同一套参数组装链，不另造预览模型。</summary>
public partial class DivideImageParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private bool _disposed;
    private long _generation;

    public DivideImageParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.DivideImageDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public DivideImageParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var generation = ++_generation;
        // 本工具有图像输入：输入区标题必须显式声明，否则宿主报 "An input content section is required."。
        Editor.Bind(draft, options with { InputSection = options.InputSection ?? "基本参数" });
        if (generation != _generation || _disposed) throw new InvalidOperationException("划片拆分参数组合已重新绑定或关闭。");
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields) => Editor.HasPendingEditsOutside(excludedFields);
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out FieldSettings fields, out string error) => Editor.TryCapture(out fields, out error);
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Editor.ReadNumericModels(scope, fieldId);
    }

    /// <summary>参数页就绪信号：字段组合在构造内同步完成。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        Editor.Dispose();
    }
}
