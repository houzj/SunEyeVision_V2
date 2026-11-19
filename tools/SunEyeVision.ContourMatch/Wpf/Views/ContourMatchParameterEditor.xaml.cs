using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：两个页签（模板参数 / 运行参数）来自参数声明，
/// 模型面板（新建/清空/导出/导入 + 模型摘要）挂在模板参数页「金字塔比例」之前，
/// 对应原件训练对话框 <c>HPPyramidMatchPat</c>（PyramidFeaturepatMatchConfigPanalData）的位置。
/// 宿主仍独占执行、捕获与释放。
/// </summary>
public partial class ContourMatchParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private ContourMatchTemplatePanel? _templatePanel;
    private bool _disposed;

    public ContourMatchParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.ContourMatchDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public ContourMatchParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var panel = new ContourMatchTemplatePanel(draft, options.AcceptCandidate, options.RoiFrames);
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            ContourMatchFields.ModelData
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(ContourMatchFields.PyramidMode, panel, InsertBefore: true));
        var composed = ContourMatchDisplayUi.Configure(options) with
        {
            // 输入区必须落在已声明的分组上。ModelData 是隐藏的基本页锚点，承载原厂图像输入和 ROI 区域。
            InputSection = options.InputSection ?? ContourMatchFields.ImageInputSection,
            SuppressedFieldRows = suppressed,
            AdditionalAnchoredContents = anchored
        };
        Editor.Bind(draft, composed);
        _templatePanel = panel;
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits || _templatePanel?.HasPendingEdits == true;
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => Editor.ReadNumericModels(scope, fieldId);
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out FieldSettings fields, out string error) => Editor.TryCapture(out fields, out error);
    /// <summary>参数页就绪信号：模型面板在 Bind 内同步构建。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _templatePanel = null;
        Editor.Dispose();
    }
}

