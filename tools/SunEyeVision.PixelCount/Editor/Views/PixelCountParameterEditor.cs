using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.PixelCount.Runtime;

namespace SunEyeVision.Tool.PixelCount.Wpf.Views;

/// <summary>
/// 像素统计参数页：字段、默认值与显隐全部来自 Runtime 声明，本层只负责输入区落位与生命周期。
/// 输入区必须落在本工具 schema 真实存在的分组上，否则声明式视图会拒绝组装。
/// </summary>
public sealed class PixelCountParameterEditor : UserControl, IToolSettingsView, IToolSettingsViewReady
{
    private const string InputSection = "阈值范围";

    private static readonly NativeStandardBasicPageSpec BasicPageSpec = new()
    {
        RoiFieldIds = PixelCountRoi.Ports.Select(port => port.Id).ToArray(),
        PositionCorrectionFieldId = PixelCountFields.PositionCorrectionEnabled,
        ComposeRoi = (inputs, attach, observe) => PixelCountRoi.Add(inputs, attach, observe),
        ComposeAssist = PixelCountAssist.Add,
        CreateRoiDocument = PixelCountLocalRoi.Create
    };

    private readonly DeclaredSettingsView _editor;
    private PixelCountRangePanel? _rangePanel;
    private NativeStandardBasicPage? _basicPage;
    private DeclaredFieldsView? _settingsFields;
    private IToolSettingsDraft? _draft;
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
        var panel = new PixelCountRangePanel(draft, options.AcceptCandidate, RefreshRangeVisibility);
        _draft = draft;
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            PixelCountFields.RangeCount,
            PixelCountFields.RoiAreaType,
            PixelCountFields.OutputMaskEnabled
        };
        var anchored = (options.AdditionalAnchoredContents ?? Array.Empty<AnchoredSettingsContent>()).ToList();
        // 原厂命令条在区间行下方；锚定最后一个槽位，并保持自身可见（末行折叠时仍位于底部）。
        anchored.Add(new AnchoredSettingsContent(
            PixelCountFields.RangeMaximum(PixelCountRanges.MaximumCount - 1), panel,
            FollowAnchorVisibility: false));
        var previousPageBuilt = options.PageBuilt;
        var displayOptions = PixelCountDisplayUi.Configure(options);
        _editor.Bind(draft, displayOptions with
        {
            InputSection = options.InputSection ?? "基本参数",
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>()).Concat(new[] { "基本参数", "运行参数", "结果显示" }).Distinct(StringComparer.Ordinal).ToArray(),
            SuppressedFieldRows = suppressed,
            AdditionalAnchoredContents = anchored,
            PageBuilt = (page, fields, input) =>
            {
                previousPageBuilt?.Invoke(page, fields, input);
                if (page == "运行参数")
                {
                    _settingsFields = fields;
                    RefreshRangeVisibility();
                }
                if (page == "基本参数" && input is DeclaredInputFieldsView inputs)
                {
                    _basicPage?.Dispose();
                    _basicPage = NativeStandardBasicPage.Compose(inputs, fields, displayOptions, BasicPageSpec);
                }
            }
        });
        _rangePanel = panel;
    }

    public FrameworkElement View => this;

    public bool HasInvalidInput => _editor.HasInvalidInput;

    public bool HasPendingEdits => _editor.HasPendingEdits || _rangePanel?.HasPendingEdits == true;

    public long AcceptedRevision => _editor.AcceptedRevision;

    public IReadOnlyList<string> AcceptedFieldOrder => _editor.AcceptedFieldOrder;

    public event Action? Changed { add => _editor.Changed += value; remove => _editor.Changed -= value; }

    public FieldSettings CaptureAcceptedFields() => _editor.CaptureAcceptedFields();

    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId = null)
        => _editor.ReadNumericModels(scope, fieldId);

    public bool TryCapture(out FieldSettings fields, out string error) => _editor.TryCapture(out fields, out error);

    /// <summary>参数页就绪信号：字段与输入区在 Bind 内同步构建完成。</summary>
    public Task ReadyTask => Task.CompletedTask;

    /// <summary>按当前有效条数切换 32 组区间槽位的行显隐；不销毁字段，引用随槽位保留。</summary>
    private void RefreshRangeVisibility()
    {
        if (_disposed || _settingsFields is null || _draft is null) return;
        var count = _draft.CaptureFields().Read(PixelCountFields.RangeCount) is ConstantSetting { Value: long raw }
            ? (int)Math.Clamp(raw, 1, PixelCountRanges.MaximumCount)
            : 1;
        var all = Enumerable.Range(0, PixelCountRanges.MaximumCount)
            .SelectMany(i => new[] { PixelCountFields.RangeMinimum(i), PixelCountFields.RangeMaximum(i) })
            .ToArray();
        _settingsFields.SetFieldRowsVisibility(all, true);
        if (count < PixelCountRanges.MaximumCount)
        {
            var hidden = Enumerable.Range(count, PixelCountRanges.MaximumCount - count)
                .SelectMany(i => new[] { PixelCountFields.RangeMinimum(i), PixelCountFields.RangeMaximum(i) })
                .ToArray();
            _settingsFields.SetFieldRowsVisibility(hidden, false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rangePanel = null;
        _settingsFields = null;
        _draft = null;
        _basicPage?.Dispose();
        _basicPage = null;
        _editor.Dispose();
    }
}
