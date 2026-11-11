using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

/// <summary>Host result view when a tool has no optional WPF companion.
/// Copies display cells. Only the selected row borrows its highlight geometry.</summary>
public sealed class DeclaredResultTable : UserControl, IToolResultView
{
    private readonly ResultProjectionDescriptor _projection;
    private readonly string _schemaFingerprint;
    private IResultPublication? _publication;
    private readonly PublishedRows _rows=new();
    private IReadOnlyList<Row> _prepared=Array.Empty<Row>();
    private long _generation;
    private long _selectionRevision;
    private int _publishing;
    private readonly bool _hasHighlight;
    private readonly DataGrid _table;
    private int[] _columnTextLengths;
    private bool _disposed;
    public FrameworkElement View=>this;
    public IReadOnlyList<Port> Sources { get; }
    public event EventHandler<ResultSelection>? SelectionHighlightChanged;

    public DeclaredResultTable(ToolSchema schema,ResultProjectionDescriptor projection)
    {
        var errors=projection.Validate(schema);
        if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));
        if(projection.Kind!=ResultProjectionKind.StandardTable)throw new NotSupportedException("A standard table result projection is required.");
        _projection=projection;
        _columnTextLengths=new int[projection.Columns.Count];
        _hasHighlight=projection.Columns.Any(column=>column.GeometrySource is not null);
        foreach(var column in projection.Columns.Where(column=>column.GeometrySource is not null))
        {
            var type=Resolve(schema.Outputs.Single(port=>port.Id==column.Geometry!.PortId).Type,column.Geometry!.MemberFieldPath);
            if(projection.Rows is not null)type=type.Element!;
            if(type.Shape!=ValueShape.Geometry&&!(type.Shape==ValueShape.Sequence&&type.Element!.Shape==ValueShape.Geometry&&type.Element.Geometry==GeometryKind.Point))
                throw new NotSupportedException("The standard table requires immutable geometry or a declared point sequence; use an explicit renderer for region/record geometry.");
        }
        _schemaFingerprint=new ToolSchema(Array.Empty<Port>(),schema.Outputs).Fingerprint;
        var sourceIds=projection.Columns.SelectMany(column=>new[]{column.Value,column.ValidWhen,column.Geometry,column.GeometrySource}).Where(path=>path is not null)
            .Select(path=>path!.PortId).Concat(projection.Rows is {} rows?new[]{rows.PortId}:Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        Sources=Array.AsReadOnly(schema.Outputs.Where(port=>sourceIds.Contains(port.Id)).ToArray());
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Results/StandardTable.xaml",UriKind.Relative)});
        _table=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,
            SelectionMode=DataGridSelectionMode.Single,ItemsSource=_rows};
        _table.Style=(Style)Resources["CurResultDataGridStyle"];
        _table.DataContext=this;
        var unavailable=projection.Columns.Where(column=>column.Value is null).ToArray();
        if(unavailable.Length!=0)_table.ToolTip=string.Join("；",unavailable.Select(column=>column.DisplayName+"："+column.UnavailableReason))+"。空结果不代表这些特征已计算。";
        foreach(var column in projection.Columns)
        {
            var header=column.DisplayNameResourceKey is {} key?AppLocalizationService.GetLang(key,column.DisplayName):column.DisplayName;
            _table.Columns.Add(new DataGridTextColumn{Header=column.Value is null?header+"（未实现）":header,
                CellStyle=(Style)Resources["dgCellCenterAlignmentStyle"],
                Binding=new Binding("["+column.Key+"]"){Mode=BindingMode.OneWay,
                    StringFormat=column.DecimalPlaces is {} decimals?"{0:F"+decimals+"}":null}});
        }
        _table.SelectionChanged+=OnSelectionChanged;
        Content=_table;
    }
    private static TypeRef Resolve(TypeRef type,IEnumerable<string> members)
    {
        foreach(var member in members)
        {
            if(type.Shape==ValueShape.Sequence)
                type=TypeRef.SequenceOf(Resolve(type.Element!,new[]{member}),type.Count);
            else type=type.Fields.Single(field=>field.Id==member).Type;
        }
        return type;
    }

    public void Present(IResultPublication publication)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(publication);
        var expected=_generation;
        if(publication.Schema.Fingerprint!=_schemaFingerprint||publication.Stamp.SchemaFingerprint!=_schemaFingerprint)
            throw new ArgumentException("Result publication differs from this view's frozen declaration.");
        bool Current()=>!_disposed&&expected==_generation&&publication.IsCurrent()&&!_disposed&&expected==_generation;
        if(!Current())return;
        using var lease=publication.Borrow();
        if(lease.Stamp!=publication.Stamp)throw new ArgumentException("Result lease belongs to another publication.");
        var values=lease.Values;
        var rows=new List<Row>();
        if(values is null){if(Current())Install(rows,expected,publication);return;}
        var columnTextLengths=new int[_projection.Columns.Count];
        // A valid publication may omit optional ports. Missing cells are empty;
        // required ports and value types were already checked by InputValues.
        object? RootValue(string id)=>ReadRoot(values,id);
        if(_projection.Rows is {} source)
        {
            var rowValue=Read(RootValue(source.PortId),source.MemberFieldPath);
            if(rowValue is null)
            {
                // An omitted optional or null parent yields no rows for this publication.
                // Install an empty scene so the previous row selection cannot survive.
                if(Current())Install(rows.AsReadOnly(),expected,publication);
                return;
            }
            var sequence=rowValue as SequenceValue
                ??throw new ArgumentException("The declared result rows must be a sequence.");
            var identities=new HashSet<object>();
            foreach(var value in sequence)
            {
                var record=value as RecordValue??throw new ArgumentException("Result rows must be declared records.");
                var identity=record.Get<object>(_projection.ElementIdentityFieldId!);
                if(!identities.Add(identity))throw new ArgumentException("Duplicate result element identity.");
                object? Cell(ResultFieldPath path)=>Read(record,path.MemberFieldPath.Skip(source.MemberFieldPath.Count));
                rows.Add(new(rows.Count,identity,ProjectCells(Cell,columnTextLengths)));
                if(!Current())return;
            }
        }
        else rows.Add(new(0,"single",ProjectCells(path=>Read(RootValue(path.PortId),path.MemberFieldPath),columnTextLengths)));
        if(Current())Install(rows.AsReadOnly(),expected,publication,columnTextLengths);
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        Install(Array.Empty<Row>(),_generation,null);
    }

    // The original template executes this command directly. Each command belongs
    // to one installed publication; an old ScrollChanged must not page new rows.
    public ICommand CurResultScrollCommand {get;private set;}=new ScrollCommand(_=>{});
    private void Install(IReadOnlyList<Row> rows,long expected,IResultPublication? publication,int[]? columnTextLengths=null)
    {
        if(_disposed||expected!=_generation)return;
        var generation=++_generation;
        var previous=_publication;
        _publication=publication;
        _prepared=rows;
        var previousTextLengths=_columnTextLengths;
        if(columnTextLengths is not null)_columnTextLengths=columnTextLengths;
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(_publication,publication)
            &&(publication is null||publication.IsCurrent())&&!_disposed&&generation==_generation&&ReferenceEquals(_publication,publication);
        CurResultScrollCommand=new ScrollCommand(value=>
        {
            if(_disposed||_publishing!=0||generation!=_generation||value is not double offset||!double.IsFinite(offset)
                ||offset<=_rows.Count-100||_rows.Count>=_prepared.Count)return;
            if(!Current())return;
            var selection=_table.SelectedItem as Row;
            _rows.Append(_prepared.Skip(_rows.Count).Take(500));
            _publishing++;
            try
            {
                try{_rows.Publish(Current);}
                finally
                {
                    if(Current()&&selection is not null&&_table.SelectedItem is null)
                        _table.SelectedItem=selection;
                }
            }
            finally{_publishing--;}
        });
        // No callbacks until the complete storage and command are installed.
        _rows.Install(rows.Take(500).ToArray());
        var errors=new List<Exception>();
        void Notify(Action notify){try{if(Current())notify();}catch(Exception error){errors.Add(error);}}
        _publishing++;
        try
        {
            Notify(()=>_table.SelectedItem=null);
            Notify(()=>_table.DataContext=null);
            Notify(()=>_table.DataContext=this);
            Notify(()=>_rows.Publish(Current));
            if(columnTextLengths is not null)
                for(var index=0;index<columnTextLengths.Length;index++)
                {
                    if(previousTextLengths[index]-columnTextLengths[index]<=50)continue;
                    var column=_table.Columns[index];
                    Notify(()=>column.Width=DataGridLength.SizeToHeader);
                    Notify(()=>column.Width=DataGridLength.Auto);
                }
            Notify(()=>{if((publication??previous) is {} owner)SelectionHighlightChanged?.Invoke(this,new(owner.Stamp,null,Array.Empty<ResultHighlightTarget>()));});
        }
        finally{_publishing--;}
        if(errors.Count!=0)throw new AggregateException("Result storage installed; publication observers failed.",errors);
    }

    private IReadOnlyDictionary<string,object?> ProjectCells(Func<ResultFieldPath,object?> read,int[] columnTextLengths)
    {
        var cells=new Dictionary<string,object?>(StringComparer.Ordinal);
        for(var index=0;index<_projection.Columns.Count;index++)
        {
            var column=_projection.Columns[index];
            if(column.Value is null){cells.Add(column.Key,"未实现");continue;}
            var valid=column.ValidWhen is null||read(column.ValidWhen) is true;
            var valueToDisplay=valid?read(column.Value):null;
            var display=column.UseSinglePrecision&&valueToDisplay is double number
                ?((float)number).ToString(CultureInfo.CurrentCulture):DisplayCell(valueToDisplay);
            cells.Add(column.Key,display);
            if(column.DisplayNameResourceKey!="Number")
                columnTextLengths[index]=Math.Max(columnTextLengths[index],display?.ToString()?.Length??0);
        }
        return new ReadOnlyDictionary<string,object?>(cells);
    }

    private IReadOnlyList<ResultHighlightTarget> ProjectHighlight(Func<ResultFieldPath,object?> read,Func<ResultFieldPath,object?> readOrigin)
    {
        var targets=new Dictionary<(ImageFrameAddress,CoordinateFrame),List<GeometryValue>>();
        var geometryPaths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var column in _projection.Columns)
        {
            if(column.ValidWhen is {} condition&&read(condition) is not true)continue;
            if(column.Geometry is {} path&&column.GeometrySource is {} origin&&geometryPaths.Add(Key(path)+Key(origin)))
            {
                var value=read(path);
                var geometry=new List<GeometryValue>();
                if(value is null)continue;
                if(value is SequenceValue {Count:0})continue;
                if(value is GeometryValue single)geometry.Add(single);
                else if(value is SequenceValue sequence&&sequence.Count>=3&&sequence.All(point=>point is PointGeometry))
                {
                    var points=sequence.Cast<PointGeometry>().ToArray();
                    if(points.Any(point=>point.Frame!=points[0].Frame))throw new ArgumentException("Mixed coordinate frames in result geometry.");
                    geometry.Add(new PolygonGeometry(points[0].Frame,points.Select(point=>point.Point)));
                }
                else throw new NotSupportedException("This result geometry requires an explicit supported renderer.");
                var (frame,coordinates)=ReadOrigin(readOrigin(origin));
                foreach(var item in geometry)
                {
                    if(item.Frame!=coordinates)throw new ArgumentException("Geometry and physical origin use different coordinate frames.");
                    if(!targets.TryGetValue((frame,coordinates),out var group))targets.Add((frame,coordinates),group=new());
                    group.Add(item);
                }
            }
        }
        return Array.AsReadOnly(targets.Select(item=>new ResultHighlightTarget(item.Key.Item1,item.Key.Item2,item.Value)).ToArray());
    }
    private static string Key(ResultFieldPath path)=>path.PortId.Length+":"+path.PortId
        +string.Concat(path.MemberFieldPath.Select(member=>member.Length+":"+member))+";";
    private static (ImageFrameAddress,CoordinateFrame) ReadOrigin(object? value)
    {
        if(value is IReadOnlyRegion region)
        {
            using var lease=region.AcquireReader();
            if(lease.Frame!=region.Frame||lease.SourceFrame!=region.SourceFrame)throw new ArgumentException("Region origin lease is inconsistent.");
            return (lease.SourceFrame,lease.Frame);
        }
        if(value is IReadOnlyImage image)
        {
            ResourceContractRules.ValidateImage(image);
            var expectedDomain=image.EffectiveDomain;
            using var lease=image.AcquirePixels();
            if(lease.Frame!=image.Frame||lease.Layout!=image.Layout)throw new ArgumentException("Image origin lease is inconsistent.");
            using var domain=lease.AcquireDomainReader();
            if(domain.SourceFrame!=lease.Frame||domain.SourceFrame!=lease.EffectiveDomain.SourceFrame
                ||domain.Frame!=lease.EffectiveDomain.Frame||domain.SourceFrame!=expectedDomain.SourceFrame
                ||domain.Frame!=expectedDomain.Frame)throw new ArgumentException("Image domain origin lease is inconsistent.");
            return(lease.Frame,domain.Frame);
        }
        throw new ArgumentException("Declared geometry origin is missing.");
    }
    private static object? Read(object? value,IEnumerable<string> members)
    {
        foreach(var member in members)
        {
            if(value is null)return null;
            if(value is not RecordValue record)throw new ArgumentException("Result field traversal requires a declared record.");
            value=record.Contains(member)?record.Get<object?>(member):null;
        }
        return value;
    }
    private static object? ReadRoot(InputValues values,string id)=>values.Contains(id)?values.Get<object?>(id):null;
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
    {
        if(_disposed||_publishing!=0)return;
        var generation=_generation;var publication=_publication;
        if(publication is null)return;
        var revision=++_selectionRevision;
        bool SameSelection()=>!_disposed&&generation==_generation&&revision==_selectionRevision&&ReferenceEquals(publication,_publication);
        bool Current()=>SameSelection()&&publication.IsCurrent()&&SameSelection();
        if(!Current())return;
        var row=_table.SelectedItem as Row;
        if(row is not null&&(row.Ordinal>=_rows.Count||!ReferenceEquals(_rows[row.Ordinal],row)))row=null;
        IReadOnlyList<ResultHighlightTarget> targets=Array.Empty<ResultHighlightTarget>();
        if(row is not null&&_hasHighlight)
        {
            try
            {
                using var lease=publication.Borrow();
                if(!Current())return;
                if(lease.Stamp!=publication.Stamp)throw new ArgumentException("Result lease belongs to another publication.");
                if(lease.Values is {} values)targets=ReadSelectedHighlight(values,row);
            }
            catch(ObjectDisposedException) when(!Current()){return;}
        }
        if(Current())SelectionHighlightChanged?.Invoke(this,new(publication.Stamp,row?.ElementIdentity,targets));
    }
    private IReadOnlyList<ResultHighlightTarget> ReadSelectedHighlight(InputValues values,Row row)
    {
        object? Root(ResultFieldPath path)=>Read(ReadRoot(values,path.PortId),path.MemberFieldPath);
        if(_projection.Rows is not {} source)return ProjectHighlight(Root,Root);
        if(Root(source) is not SequenceValue sequence||row.Ordinal>=sequence.Count||
            sequence[row.Ordinal] is not RecordValue record||!Equals(record.Get<object>(_projection.ElementIdentityFieldId!),row.ElementIdentity))
            throw new ArgumentException("Selected row differs from the published result.");
        object? Cell(ResultFieldPath path)=>Read(record,path.MemberFieldPath.Skip(source.MemberFieldPath.Count));
        object? Origin(ResultFieldPath path)=>path.PortId==source.PortId&&path.MemberFieldPath.Take(source.MemberFieldPath.Count).SequenceEqual(source.MemberFieldPath)
            ?Cell(path):Root(path);
        return ProjectHighlight(Cell,Origin);
    }
    public void Dispose()
    {
        if(_disposed)return;
        var stamp=_publication?.Stamp;
        _disposed=true;++_generation;_prepared=Array.Empty<Row>();_publication=null;
        CurResultScrollCommand=new ScrollCommand(_=>{});
        _rows.Install(Array.Empty<Row>());
        var errors=new List<Exception>();
        void Clean(Action action){try{action();}catch(Exception error){errors.Add(error);}}
        Clean(()=>_table.SelectionChanged-=OnSelectionChanged);
        Clean(()=>_table.SelectedItem=null);
        Clean(()=>_table.ItemsSource=null);
        Clean(()=>_table.DataContext=null);
        Clean(()=>_rows.Publish(()=>true));
        if(stamp is not null)Clean(()=>SelectionHighlightChanged?.Invoke(this,new(stamp,null,Array.Empty<ResultHighlightTarget>())));
        SelectionHighlightChanged=null;
        Clean(()=>Content=null);
        if(errors.Count!=0)throw new AggregateException("Result view retired; cleanup observers failed.",errors);
    }
    private sealed record Row(int Ordinal,object ElementIdentity,IReadOnlyDictionary<string,object?> Cells)
    {public object? this[string key]=>Cells[key];}
    private sealed class ScrollCommand(Action<object?> execute):ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>execute(parameter);
        public event EventHandler? CanExecuteChanged {add{} remove{}}
    }
    private sealed class PublishedRows:ObservableCollection<Row>
    {
        internal void Install(IReadOnlyList<Row> rows)
        {
            // Install all storage before notifying; a throwing observer cannot
            // leave a half batch installed or retain a reader/resource lease.
            Items.Clear();foreach(var row in rows)Items.Add(row);
        }
        internal void Append(IEnumerable<Row> rows)
        {
            foreach(var row in rows)Items.Add(row);
        }
        internal void Publish(Func<bool> current)
        {
            var errors=new List<Exception>();
            void Notify(Action notify){try{if(current())notify();}catch(Exception error){errors.Add(error);}}
            Notify(()=>OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count))));
            Notify(()=>OnPropertyChanged(new PropertyChangedEventArgs("Item[]")));
            Notify(()=>OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
            if(errors.Count!=0)throw new AggregateException("Result rows installed; collection observers failed.",errors);
        }
    }
}
