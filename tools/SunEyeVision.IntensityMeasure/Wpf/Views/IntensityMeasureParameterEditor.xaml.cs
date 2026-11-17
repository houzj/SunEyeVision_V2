using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.IntensityMeasure.Runtime;

namespace SunEyeVision.Tool.IntensityMeasure.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：两个页签（基本参数 / 结果显示）全部来自参数声明，
/// 「结果显示」页的图形分组与文本七行由 <see cref="IntensityMeasureDisplayUi"/> 组合声明。
/// 宿主仍独占执行、捕获与释放。
/// </summary>
public partial class IntensityMeasureParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private bool _disposed;

    public IntensityMeasureParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.IntensityMeasureDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public IntensityMeasureParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        // 输入区必须有明确且真实存在的分区：宿主传入的输入区页优先，缺省落到本工具自己的
        // 「结果显示」页（本工具全部设置行都在该页）。
        // 不能写「基本参数」：原件该页只有 ROI/掩膜/位置修正输入组，本方以输入端口承载（与像素统计一致），
        // 声明里没有该页，引用它会让参数组合抛 "Unknown declared presentation group"；
        // 传空则抛 "An input content section is required."（Release 冒烟已实测两者）。
        var composed = IntensityMeasureDisplayUi.Configure(options) with
        {
            InputSection = options.InputSection ?? IntensityMeasureFields.ResultPage
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
        Editor.Dispose();
    }
}
