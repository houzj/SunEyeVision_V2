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
    private NativeStandardBasicPage? _basicPage;
    private DeclaredFieldsView? _settingsFields;
    private IToolSettingsDraft? _draft;
    private long _lastRangeCount = -1;
    private bool _disposed;

    public PixelCountParameterEditor()
    {
        // 工具贡献的参数行模板（原厂合并 ResourceDictionary）；没有自定义模板时返回 false，不是错误。
        ParameterTemplateResources.RegisterFromAssembly(typeof(PixelCountParameterEditor).Assembly);
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
        _draft = draft;
        // 区间数量不再抑制：它由工具模板 PixelCountRangeList 渲染成原厂命令条，
        // 行本身就落在“区间数量”声明的顺序位上（原厂命令条在区间列表上方）。
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            PixelCountFields.RoiAreaType,
            PixelCountFields.OutputMaskEnabled
        };
        var previousPageBuilt = options.PageBuilt;
        var displayOptions = PixelCountDisplayUi.Configure(options);
        _editor.Bind(draft, displayOptions with
        {
            InputSection = options.InputSection ?? "基本参数",
            PageOnlyPages = (options.PageOnlyPages ?? Array.Empty<string>()).Concat(new[] { "基本参数", "运行参数", "结果显示" }).Distinct(StringComparer.Ordinal).ToArray(),
            SuppressedFieldRows = suppressed,
            AdditionalAnchoredContents = options.AdditionalAnchoredContents,
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
        _lastRangeCount = ReadRangeCount();
        _editor.Changed += OnEditorChanged;
    }

    /// <summary>当前有效区间条数（读草稿已接受值，钳到声明上限）。</summary>
    private long ReadRangeCount()
        => _draft?.CaptureFields().Read(PixelCountFields.RangeCount) is ConstantSetting { Value: long raw }
            ? Math.Clamp(raw, 1, PixelCountRanges.MaximumCount)
            : 1;

    /// <summary>
    /// 区间数量经模板命令条写入后刷新槽位行显隐。原来由面板回调触发，
    /// 现在行值走统一的 Changed 事件，这里只在条数真的变化时重算。
    /// </summary>
    private void OnEditorChanged()
    {
        if (_disposed || _settingsFields is null || _draft is null) return;
        var count = ReadRangeCount();
        if (count == _lastRangeCount) return;
        _lastRangeCount = count;
        RefreshRangeVisibility();
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
        _editor.Changed -= OnEditorChanged;
        _settingsFields = null;
        _draft = null;
        _basicPage?.Dispose();
        _basicPage = null;
        _editor.Dispose();
    }
}
