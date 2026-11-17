using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.GrayMatch.Runtime;

namespace SunEyeVision.Tool.GrayMatch.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：页签（模板参数 / 运行参数 / 结果显示）来自参数声明，
/// 模型面板（新建/清空/导出/导入 + 模型摘要）挂在「金字塔层数标记」之前，
/// 对应原件训练对话框 <c>GrayMatchPat</c>（GrayMatchVAConfigPanalData）的位置。
/// 宿主仍独占执行、捕获与释放。
/// </summary>
public partial class GrayMatchParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private GrayMatchTemplatePanel? _templatePanel;
    private bool _disposed;

    public GrayMatchParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.GrayMatchDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public GrayMatchParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var panel = new GrayMatchTemplatePanel(draft, options.AcceptCandidate, options.RoiFrames);
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            GrayMatchFields.ModelData
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(GrayMatchFields.PyramidMode, panel, InsertBefore: true));
        var composed = GrayMatchDisplayUi.Configure(options) with
        {
            // 输入区必须落在已声明的分组上（否则 DeclaredFieldsView.AddToGroup 直接抛“未知分组”）：
            // 原件把输入/ROI 放在基本参数页顶部，本工具对应第一页的「模板参数」组。
            InputSection = options.InputSection ?? GrayMatchFields.TemplateSection,
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
