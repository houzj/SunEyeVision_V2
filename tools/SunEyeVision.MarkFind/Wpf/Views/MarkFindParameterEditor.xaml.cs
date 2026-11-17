using System.ComponentModel;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.MarkFind.Runtime;

namespace SunEyeVision.Tool.MarkFind.Wpf.Views;

/// <summary>
/// 原件参数面板的迁移：两个页签（模板参数 / 运行参数）来自参数声明，
/// 模板面板（新建/清空/导出/导入 + 模型摘要）挂在模板参数页顶部，
/// 对应原件 <c>MarkFindConfigPanal</c> 的位置。宿主仍独占执行、捕获与释放。
/// </summary>
public partial class MarkFindParameterEditor : UserControl, IToolSettingsView
{
    private MarkFindTemplatePanel? _templatePanel;
    private bool _disposed;

    public MarkFindParameterEditor()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var context = Design.MarkFindDesignData.CreateParameterContext();
            Bind(context.Draft, context.Options);
        }
    }

    public MarkFindParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        var panel = new MarkFindTemplatePanel(draft, options.AcceptCandidate, options.RoiFrames);
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            MarkFindFields.ModelData
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(MarkFindFields.PyramidScaleMode, panel, InsertBefore: true));
        Editor.Bind(draft, options with { SuppressedFieldRows = suppressed, AdditionalAnchoredContents = anchored });
        _templatePanel = panel;
    }

    public System.Windows.FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public bool HasPendingEdits => Editor.HasPendingEdits || _templatePanel?.HasPendingEdits == true;
    public long AcceptedRevision => Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder => Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields() => Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => Editor.ReadNumericModels(scope, fieldId);
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out FieldSettings fields, out string error) => Editor.TryCapture(out fields, out error);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _templatePanel = null;
        Editor.Dispose();
    }
}
