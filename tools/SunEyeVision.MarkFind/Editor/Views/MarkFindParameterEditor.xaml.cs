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
public partial class MarkFindParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private MarkFindTemplatePanel? _templatePanel;
    private bool _disposed;

    public MarkFindParameterEditor()
    {
        InitializeComponent();
    }

    public MarkFindParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
        => Bind(draft, options);

    public void Bind(IToolSettingsDraft draft, ToolSettingsViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        // 结果页显示声明（原厂 Display.xml 的 ImageViewConfig 集合）：六个图形组 + 文本组。
        options = MarkFindDisplayUi.Configure(options);
        var panel = new MarkFindTemplatePanel(draft, options.AcceptCandidate, options.RoiFrames);
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            MarkFindFields.ModelData
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        anchored.Add(new AnchoredSettingsContent(MarkFindFields.PyramidScaleMode, panel, InsertBefore: true));
        var composed = options with
        {
            InputSection = options.InputSection ?? Runtime.MarkFindFields.BasicPage,
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>())
                .Concat(new[] { "基本参数", "特征模型", "运行参数", "结果显示" })
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            SuppressedFieldRows = suppressed,
            AdditionalAnchoredContents = anchored
        };
        Editor.Bind(draft, composed);
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
    /// <summary>参数页就绪信号：模板面板在 Bind 内同步构建。</summary>
    public Task ReadyTask => Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _templatePanel = null;
        Editor.Dispose();
    }
}
