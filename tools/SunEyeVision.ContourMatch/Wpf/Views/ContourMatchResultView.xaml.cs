using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>结果页：共享声明式结果表，叠加需求由 <see cref="ContourMatchResultDisplay.Descriptor"/> 声明。</summary>
public partial class ContourMatchResultView : UserControl, IToolResultView
{
    private readonly DeclaredResultTable? _table;
    private bool _disposed;

    public ContourMatchResultView()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var tool = new ContourMatchTool();
            _table = new DeclaredResultTable(tool.ResultSchema, tool.ResultProjection);
            ResultHost.Content = _table;
        }
        if (_table is not null) _table.SelectionHighlightChanged += OnSelectionHighlightChanged;
    }

    public ContourMatchResultView(ToolSchema schema, ResultProjectionDescriptor projection)
    {
        InitializeComponent();
        (ResultHost.Content as IDisposable)?.Dispose();
        _table = new DeclaredResultTable(schema, projection);
        ResultHost.Content = _table;
        _table.SelectionHighlightChanged += OnSelectionHighlightChanged;
    }

    private DeclaredResultTable Table => _table ?? throw new InvalidOperationException("结果视图需要正式输出声明。");
    public FrameworkElement View => this;
    public IReadOnlyList<Port> Sources => Table.Sources;
    public event EventHandler<ResultSelection>? SelectionHighlightChanged;

    private void OnSelectionHighlightChanged(object? sender, ResultSelection selection)
    {
        if (ReferenceEquals(sender, _table)) SelectionHighlightChanged?.Invoke(this, selection);
    }

    public void Present(IResultPublication publication) => Table.Present(publication);
    public void Clear() => Table.Clear();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var errors = new List<Exception>();
        try { ResultHost.Content = null; } catch (Exception error) { errors.Add(error); }
        try { _table?.Dispose(); } catch (Exception error) { errors.Add(error); }
        finally
        {
            if (_table is not null) _table.SelectionHighlightChanged -= OnSelectionHighlightChanged;
            SelectionHighlightChanged = null;
        }
        if (errors.Count > 0) throw new AggregateException("ContourMatch 结果视图清理失败。", errors);
    }
}
