using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

/// <summary>Host result view when a tool has no optional WPF companion.
/// Copies display cells and immutable geometry; retains no published resource or reader.</summary>
public sealed class DeclaredResultTable : UserControl, IToolResultView
{
    private readonly ResultProjectionDescriptor _projection;
    private readonly ObservableCollection<Row> _rows=new();
    private readonly DataGrid _table;
    private bool _disposed;
    public FrameworkElement View=>this;
    public IReadOnlyList<Port> Sources { get; }
    public event EventHandler<IReadOnlyList<GeometryValue>?>? SelectionHighlightChanged;

    public DeclaredResultTable(ToolSchema schema,ResultProjectionDescriptor projection)
    {
        var errors=projection.Validate(schema);
        if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
        if(projection.Kind!=ResultProjectionKind.StandardTable)throw new NotSupportedException("A standard table result projection is required.");
        _projection=projection;
        var sourceIds=projection.Columns.SelectMany(column=>new[]{column.Value,column.ValidWhen,column.Geometry}).Where(path=>path is not null)
            .Select(path=>path!.PortId).Concat(projection.Rows is {} rows?new[]{rows.PortId}:Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        Sources=Array.AsReadOnly(schema.Outputs.Where(port=>sourceIds.Contains(port.Id)).ToArray());
        _table=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,
            SelectionMode=DataGridSelectionMode.Single,ItemsSource=_rows};
        foreach(var column in projection.Columns)
            _table.Columns.Add(new DataGridTextColumn{Header=column.DisplayName,
                Binding=new Binding("["+column.Key+"]"){Mode=BindingMode.OneWay,
                    StringFormat=column.DecimalPlaces is {} decimals?"{0:F"+decimals+"}":null}});
        _table.SelectionChanged+=OnSelectionChanged;
        Content=_table;
    }

    public void Update(InputValues? values)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _rows.Clear();
        if(values is null)return;
        var rows=new List<Row>();
        if(_projection.Rows is {} source)
        {
            var sequence=Read(values.Get<object?>(source.PortId),source.MemberFieldPath) as SequenceValue
                ??throw new ArgumentException("The declared result rows are missing.");
            var identities=new HashSet<object>();
            foreach(var value in sequence)
            {
                var record=value as RecordValue??throw new ArgumentException("Result rows must be declared records.");
                var identity=record.Get<object>(_projection.ElementIdentityFieldId!);
                if(!identities.Add(identity))throw new ArgumentException("Duplicate result element identity.");
                object? Cell(ResultFieldPath path)=>Read(record,path.MemberFieldPath.Skip(source.MemberFieldPath.Count));
                rows.Add(Project(identity,Cell));
            }
        }
        else rows.Add(Project("single",path=>Read(values.Get<object?>(path.PortId),path.MemberFieldPath)));
        foreach(var row in rows)_rows.Add(row);
    }

    private Row Project(object identity,Func<ResultFieldPath,object?> read)
    {
        var cells=new Dictionary<string,object?>(StringComparer.Ordinal);
        var geometry=new List<GeometryValue>();
        var geometryPaths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var column in _projection.Columns)
        {
            cells.Add(column.Key,DisplayCell(read(column.Value)));
            if(column.Geometry is {} path&&geometryPaths.Add(path.PortId+"/"+string.Join("/",path.MemberFieldPath)))
            {
                var value=read(path);
                if(value is GeometryValue single)geometry.Add(single);
                else if(value is SequenceValue sequence&&sequence.Count>=3&&sequence.All(point=>point is PointGeometry))
                {
                    var points=sequence.Cast<PointGeometry>().ToArray();
                    if(points.Any(point=>point.Frame!=points[0].Frame))throw new ArgumentException("Mixed coordinate frames in result geometry.");
                    geometry.Add(new PolygonGeometry(points[0].Frame,points.Select(point=>point.Point)));
                }
            }
        }
        return new(identity,cells,geometry.AsReadOnly());
    }
    private static object? Read(object? value,IEnumerable<string> members)
    {
        foreach(var member in members)
        {
            if(value is not RecordValue record)throw new ArgumentException("Result field traversal requires a declared record.");
            value=record.Contains(member)?record.Get<object?>(member):null;
        }
        return value;
    }
    private static object? DisplayCell(object? value)=>value switch
    {
        null or string or bool or long or double=>value,
        GeometryValue geometry=>geometry.Kind.ToString(),
        IReadOnlyImage=>"图像",IReadOnlyRegion=>"区域",
        RecordValue record=>string.Join(", ",record.Declaration.Fields.Where(field=>record.Contains(field.Id)).Select(field=>field.Id+": "+DisplayCell(record.Get<object?>(field.Id)))),
        SequenceValue sequence=>"["+sequence.Count.ToString(CultureInfo.InvariantCulture)+" 项]",
        double[] array=>"["+array.Length.ToString(CultureInfo.InvariantCulture)+" 项]",
        _=>throw new ArgumentException("Unsupported declared result display value.")
    };
    private void OnSelectionChanged(object sender,SelectionChangedEventArgs args)
        =>SelectionHighlightChanged?.Invoke(this,(_table.SelectedItem as Row)?.Geometry);
    public void Dispose()
    {
        if(_disposed)return;
        _rows.Clear();_table.SelectionChanged-=OnSelectionChanged;_table.ItemsSource=null;
        SelectionHighlightChanged=null;Content=null;_disposed=true;
    }
    private sealed record Row(object ElementIdentity,IReadOnlyDictionary<string,object?> Cells,IReadOnlyList<GeometryValue> Geometry)
    {public object? this[string key]=>Cells[key];}
}
