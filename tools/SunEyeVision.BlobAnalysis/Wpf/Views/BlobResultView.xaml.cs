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
    }

    public BlobResultView(ToolSchema schema,ResultProjectionDescriptor projection)
    {
        InitializeComponent();
        (ResultHost.Content as IDisposable)?.Dispose();
        _table=new DeclaredResultTable(schema,projection);
        ResultHost.Content=_table;
    }

    private DeclaredResultTable Table=>_table??throw new InvalidOperationException("结果视图需要正式输出声明。");
    public FrameworkElement View=>this;
    public IReadOnlyList<Port> Sources=>Table.Sources;
    public event EventHandler<ResultSelection>? SelectionHighlightChanged
    {
        add=>Table.SelectionHighlightChanged+=value;
        remove {if(_table is not null)_table.SelectionHighlightChanged-=value;}
    }
    public void Present(IResultPublication publication)=>Table.Present(publication);
    public void Clear()=>Table.Clear();
    public void Dispose()
    {
        try { ResultHost.Content=null; }
        finally { _table?.Dispose(); }
    }
}
