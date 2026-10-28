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

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

/// <summary>Host result view when a tool has no optional WPF companion.
/// Copies display cells and immutable geometry; retains no published resource or reader.</summary>
public sealed class DeclaredResultTable : UserControl, IToolResultView
{
    private readonly ResultProjectionDescriptor _projection;
    private readonly string _schemaFingerprint;
    private IResultPublication? _publication;
    private readonly PublishedRows _rows=new();
    private IReadOnlyList<Row> _prepared=Array.Empty<Row>();
    private long _generation;
    private int _publishing;
    private readonly DataGrid _table;
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
        foreach(var column in projection.Columns)
            _table.Columns.Add(new DataGridTextColumn{Header=column.DisplayName,
                CellStyle=(Style)Resources["dgCellCenterAlignmentStyle"],
                Binding=new Binding("["+column.Key+"]"){Mode=BindingMode.OneWay,
                    StringFormat=column.DecimalPlaces is {} decimals?"{0:F"+decimals+"}":null}});
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
                object? Origin(ResultFieldPath path)=>path.PortId==source.PortId&&path.MemberFieldPath.Take(source.MemberFieldPath.Count).SequenceEqual(source.MemberFieldPath)
                    ?Cell(path):Read(values.Get<object?>(path.PortId),path.MemberFieldPath);
                rows.Add(Project(identity,Cell,Origin));
                if(!Current())return;
            }
        }
        else rows.Add(Project("single",path=>Read(values.Get<object?>(path.PortId),path.MemberFieldPath),
            path=>Read(values.Get<object?>(path.PortId),path.MemberFieldPath)));
        if(Current())Install(rows.AsReadOnly(),expected,publication);
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        Install(Array.Empty<Row>(),_generation,null);
    }

    // The original template executes this command directly. Each command belongs
    // to one installed publication; an old ScrollChanged must not page new rows.
    public ICommand CurResultScrollCommand {get;private set;}=new ScrollCommand(_=>{});
    private void Install(IReadOnlyList<Row> rows,long expected,IResultPublication? publication)
    {
        if(_disposed||expected!=_generation)return;
        var generation=++_generation;
        var previous=_publication;
        _publication=publication;
        _prepared=rows;
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(_publication,publication)
            &&(publication is null||publication.IsCurrent())&&!_disposed&&generation==_generation&&ReferenceEquals(_publication,publication);
        CurResultScrollCommand=new ScrollCommand(value=>
        {
            if(_disposed||_publishing!=0||generation!=_generation||value is not double offset||!double.IsFinite(offset)
                ||offset<=_rows.Count-100||_rows.Count>=_prepared.Count)return;
            if(!Current())return;
            _rows.Install(_prepared.Take(Math.Min(_prepared.Count,_rows.Count+500)).ToArray());
            _publishing++;
            try{_rows.Publish(Current);}
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
            Notify(()=>{if((publication??previous) is {} owner)SelectionHighlightChanged?.Invoke(this,new(owner.Stamp,null,Array.Empty<ResultHighlightTarget>()));});
        }
        finally{_publishing--;}
        if(errors.Count!=0)throw new AggregateException("Result storage installed; publication observers failed.",errors);
    }

    private Row Project(object identity,Func<ResultFieldPath,object?> read,Func<ResultFieldPath,object?> readOrigin)
    {
        var cells=new Dictionary<string,object?>(StringComparer.Ordinal);
        var targets=new Dictionary<(ImageFrameAddress,CoordinateFrame),List<GeometryValue>>();
        var geometryPaths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var column in _projection.Columns)
        {
            var valid=column.ValidWhen is null||read(column.ValidWhen) is true;
            cells.Add(column.Key,valid?DisplayCell(read(column.Value)):null);
            if(!valid)continue;
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
        return new(identity,new System.Collections.ObjectModel.ReadOnlyDictionary<string,object?>(cells),
            Array.AsReadOnly(targets.Select(item=>new ResultHighlightTarget(item.Key.Item1,item.Key.Item2,item.Value)).ToArray()));
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
    {
        if(_disposed||_publishing!=0)return;
        var generation=_generation;var publication=_publication;
        if(publication is null||!publication.IsCurrent()||_disposed||generation!=_generation||!ReferenceEquals(publication,_publication))return;
        var row=_table.SelectedItem as Row;
        var valid=row is not null&&_rows.Any(current=>ReferenceEquals(current,row));
        SelectionHighlightChanged?.Invoke(this,new(publication.Stamp,valid?row!.ElementIdentity:null,
            valid?row!.Targets:Array.Empty<ResultHighlightTarget>()));
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
    private sealed record Row(object ElementIdentity,IReadOnlyDictionary<string,object?> Cells,IReadOnlyList<ResultHighlightTarget> Targets)
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
