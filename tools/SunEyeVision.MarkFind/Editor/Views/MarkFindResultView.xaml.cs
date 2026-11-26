using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.MarkFind.Runtime;

namespace SunEyeVision.Tool.MarkFind.Wpf.Views;

/// <summary>结果页：共享声明式结果表，叠加需求由 <see cref="MarkFindResultDisplay.Descriptor"/> 声明。</summary>
public partial class MarkFindResultView : UserControl, IToolResultView
{
    private readonly DeclaredResultTable? _table;
    private bool _disposed;

    public MarkFindResultView()
    {
        InitializeComponent();
        if (_table is not null) _table.SelectionHighlightChanged += OnSelectionHighlightChanged;
    }

    public MarkFindResultView(ToolSchema schema, ResultProjectionDescriptor projection)
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
        if (errors.Count > 0) throw new AggregateException("MarkFind结果视图清理失败。", errors);
    }
}
