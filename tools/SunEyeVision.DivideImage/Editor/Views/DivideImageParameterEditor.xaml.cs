using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.DivideImage.Runtime;

namespace SunEyeVision.Tool.DivideImage.Wpf.Views;

/// <summary>设计器与运行时共用同一套参数组装链，不另造预览模型。</summary>
public partial class DivideImageParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private static readonly NativeStandardBasicPageSpec BasicPageSpec = new()
    {
        RoiFieldIds = DivideImageRoi.Ports.Select(port => port.Id).ToArray(),
        PositionCorrectionFieldId = DivideImageFields.PositionCorrectionEnabled,
        ComposeRoi = (inputs, attach, observe) => DivideImageRoi.Add(inputs, attach, observe),
        ComposeAssist = DivideImageAssist.Add,
        CreateRoiDocument = DivideImageLocalRoi.Create,
        RoiShapes = new[] { "Box" }
    };

    private bool _disposed;
    private long _generation;
    private NativeStandardBasicPage? _basicPage;

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
        var previousPageBuilt = options.PageBuilt;
        // 本工具有图像输入：输入区标题必须显式声明，否则宿主报 "An input content section is required."。
        Editor.Bind(draft, options with
        {
            InputSection = options.InputSection ?? "基本参数",
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>())
                .Concat(new[] { "基本参数", "运行参数" })
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            PageBuilt = (page, fields, input) =>
            {
                previousPageBuilt?.Invoke(page, fields, input);
                if (page != "基本参数" || input is not DeclaredInputFieldsView inputs) return;
                _basicPage?.Dispose();
                _basicPage = NativeStandardBasicPage.Compose(inputs, fields, options, BasicPageSpec);
            }
        });
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
        _basicPage?.Dispose();
        _basicPage = null;
        Editor.Dispose();
    }
}
