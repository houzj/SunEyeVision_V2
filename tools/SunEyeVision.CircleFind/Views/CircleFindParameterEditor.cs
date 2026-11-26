using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.CircleFind.Runtime;

namespace SunEyeVision.Tool.CircleFind.Wpf.Views;

/// <summary>
/// 圆查找参数页：字段、默认值、页签（运行参数/结果显示）与显隐全部来自 Runtime 声明，
/// 本层只负责输入区落位与生命周期。三入口：双击编辑、运行时、设计独立样例共用本组装链。
/// 呈现层五项契约：ReadyTask / InputSection / 分组显式 / GraphicDisplays 完整消费 /
/// DecimalPlaces:null 文本坐标由声明式视图按 Runtime 的 DecimalPlaces 处理。
/// </summary>
public sealed class CircleFindParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private readonly DeclaredSettingsView _editor;
    private bool _disposed;

    public CircleFindParameterEditor()
    {
        _editor = new DeclaredSettingsView { SelectedPageIndex = 0 };
        Content = _editor;
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.CircleFindDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public CircleFindParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        // 输入区落在 schema 真实存在的页/组：图像输入属于「运行参数/边缘与卡尺」所在的检测输入，
        // 由宿主 InputSection 提供；不指定时使用运行参数页首组。
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
    /// <summary>参数页就绪信号：字段与输入区在 Bind 内同步构建完成。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.Dispose();
    }
}
