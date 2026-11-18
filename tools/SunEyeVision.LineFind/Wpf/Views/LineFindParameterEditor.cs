using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.LineFind.Runtime;

namespace SunEyeVision.Tool.LineFind.Wpf.Views;

/// <summary>
/// 直线查找参数页：字段、默认值、页签（运行参数/结果显示）与显隐全部来自 Runtime 声明，
/// 本层只负责输入区落位与生命周期。三入口：双击编辑、运行时、设计独立样例共用本组装链。
/// </summary>
public sealed class LineFindParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private readonly DeclaredSettingsView _editor;
    private bool _disposed;

    public LineFindParameterEditor()
    {
        _editor = new DeclaredSettingsView { SelectedPageIndex = 0 };
        Content = _editor;
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.LineFindDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public LineFindParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        _editor.Bind(draft, options with { InputSection = options.InputSection ?? "边缘与卡尺" });
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => _editor.HasInvalidInput;
    public bool HasPendingEdits => _editor.HasPendingEdits;
    public long AcceptedRevision => _editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => _editor.AcceptedFieldOrder;
    public event Action? Changed { add => _editor.Changed += value; remove => _editor.Changed -= value; }
    public FieldSettings CaptureAcceptedFields() => _editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => _editor.ReadNumericModels(scope, fieldId);
    public bool TryCapture(out FieldSettings fields, out string error) => _editor.TryCapture(out fields, out error);
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.Dispose();
    }
}
