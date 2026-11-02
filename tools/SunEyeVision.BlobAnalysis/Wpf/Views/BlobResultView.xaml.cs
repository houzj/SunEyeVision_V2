using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Views;

/// <summary>Blob's XAML entry hosts the shared declared result table and borrows its publication.</summary>
public partial class BlobResultView : UserControl, IToolResultView
{
    private readonly DeclaredResultTable? _table;
    private bool _disposed;

    public BlobResultView()
    {
        InitializeComponent();
        _table=ResultHost.Content as DeclaredResultTable;
        if(_table is null)
        {
            var tool=new BlobTool();
            _table=new DeclaredResultTable(tool.ResultSchema,tool.ResultProjection);
            ResultHost.Content=_table;
        }
        _table.SelectionHighlightChanged+=OnSelectionHighlightChanged;
    }

    public BlobResultView(ToolSchema schema,ResultProjectionDescriptor projection)
    {
        InitializeComponent();
        (ResultHost.Content as IDisposable)?.Dispose();
        _table=new DeclaredResultTable(schema,projection);
        ResultHost.Content=_table;
        _table.SelectionHighlightChanged+=OnSelectionHighlightChanged;
    }

    private DeclaredResultTable Table=>_table??throw new InvalidOperationException("结果视图需要正式输出声明。");
    public FrameworkElement View=>this;
    public IReadOnlyList<Port> Sources=>Table.Sources;
    public event EventHandler<ResultSelection>? SelectionHighlightChanged;
    private void OnSelectionHighlightChanged(object? sender,ResultSelection selection)
    {
        // The wrapper is the registered view identity; preserve the table's exact publication and payload.
        if(ReferenceEquals(sender,_table))SelectionHighlightChanged?.Invoke(this,selection);
    }
    public void Present(IResultPublication publication)=>Table.Present(publication);
    public void Clear()=>Table.Clear();
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        var errors=new List<Exception>();
        try{ResultHost.Content=null;}catch(Exception error){errors.Add(error);}
        // Preserve the table's final empty selection before detaching the forwarding subscription.
        try{_table?.Dispose();}catch(Exception error){errors.Add(error);}
        finally
        {
            if(_table is not null)_table.SelectionHighlightChanged-=OnSelectionHighlightChanged;
            SelectionHighlightChanged=null;
        }
        if(errors.Count>0)throw new AggregateException("Blob结果视图清理失败。",errors);
    }
}
