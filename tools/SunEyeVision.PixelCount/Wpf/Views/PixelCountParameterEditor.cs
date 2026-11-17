using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.PixelCount.Wpf.Views;

/// <summary>
/// 像素统计参数页：字段、默认值与显隐全部来自 Runtime 声明，本层只负责输入区落位与生命周期。
/// 输入区必须落在本工具 schema 真实存在的分组上，否则声明式视图会拒绝组装。
/// </summary>
public sealed class PixelCountParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private const string InputSection = "阈值范围";

    private readonly DeclaredSettingsView _editor;
    private bool _disposed;

    public PixelCountParameterEditor()
    {
        _editor = new DeclaredSettingsView { SelectedPageIndex = 0 };
        Content = _editor;
    }

    public PixelCountParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        _editor.Bind(draft, options with { InputSection = options.InputSection ?? InputSection });
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

    /// <summary>参数页就绪信号：字段与输入区在 Bind 内同步构建完成。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.Dispose();
    }
}
