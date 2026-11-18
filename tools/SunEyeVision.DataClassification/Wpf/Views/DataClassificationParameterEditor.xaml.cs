using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.DataClassification.Wpf.Views;

/// <summary>
/// 数据分类参数页：字段、默认值、两页签与显隐全部来自 Runtime 声明，本层只负责输入区落位与生命周期。
/// 输入区落在已声明的「输入源」分组（原厂基本参数页首个分组），避免参数页组装失败。
/// </summary>
public sealed partial class DataClassificationParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private const string InputSection = "输入源";
    private readonly DeclaredSettingsView _editor;
    private bool _disposed;

    public DataClassificationParameterEditor()
    {
        InitializeComponent();
        _editor = Editor;
    }

    public DataClassificationParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
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
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields) => _editor.HasPendingEditsOutside(excludedFields);
    public long AcceptedRevision => _editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => _editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => _editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => _editor.ReadNumericModels(scope, fieldId);
    public bool TryCapture(out FieldSettings fields, out string error) => _editor.TryCapture(out fields, out error);
    public Task ReadyTask => Task.CompletedTask;
    public event Action? Changed { add => _editor.Changed += value; remove => _editor.Changed -= value; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.Dispose();
    }
}
