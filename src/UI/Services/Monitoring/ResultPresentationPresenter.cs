using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Services.Monitoring;

/// <summary>
/// Adapts a declaration or one explicitly selected run to the existing result grid.
/// All calls and row changes belong to the host UI thread. The host owns this presenter;
/// the presenter owns its reader, never the borrowed resources in that reader.
/// </summary>
public sealed class ResultPresentationPresenter : IDisposable
{
    private sealed class Entry(ResultPresentationNode node, FlatResultRow row)
    {
        internal readonly ResultPresentationNode Node = node;
        internal readonly FlatResultRow Row = row;
        internal readonly List<Entry> Children = new();
        internal Entry? AllMembers;
        internal FlatResultRow? More;
        internal PropertyChangedEventHandler? MoreHandler;
    }

    private readonly ObservableCollection<FlatResultRow> _rows;
    private readonly ObservableCollection<FlatResultRow> _roots=new();
    public ReadOnlyObservableCollection<FlatResultRow> Roots {get;}
    public bool ContainsRow(FlatResultRow row)=>Current(_generation)&&(ReferenceEquals(row,_root)
        ||_entries.TryGetValue(row.Key,out var entry)&&ReferenceEquals(entry.Row,row));
    private readonly Action<bool> _setHasResults;
    private readonly Action<IResultPublication>? _projectionFailed;
    private readonly int _pageSize;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<(FlatResultRow Row, PropertyChangedEventHandler Handler)> _subscriptions = new();
    private readonly List<Entry> _outputs = new();
    private IReadOnlyDictionary<string,ResultBindingInfo>? _bindingRoots;
    private ResultPresentationModel? _model;
    private IResultPublication? _publication;
    private ResultPublicationStamp? _stamp;
    private IResultValueLease? _reader;
    private IReadOnlyList<Diagnostic> _diagnostics=Array.Empty<Diagnostic>();
    private FlatResultRow? _root;
    private int _generation;
    private bool _disposed;

    public ResultPresentationPresenter(ObservableCollection<FlatResultRow> rows, Action<bool> setHasResults, int pageSize = 128,
        Action<IResultPublication>? projectionFailed=null)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Roots=new(_roots);
        _setHasResults = setHasResults ?? throw new ArgumentNullException(nameof(setHasResults));
        _projectionFailed=projectionFailed;
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        _pageSize = pageSize;
    }

    public void Present(IResultPublication publication,IReadOnlyDictionary<string,ResultBindingInfo>? bindings=null)
        =>RunProjection(()=>PresentCore(publication,bindings));

    private void PresentCore(IResultPublication publication,IReadOnlyDictionary<string,ResultBindingInfo>? bindings)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(publication);
        var entryGeneration=_generation;
        var stamp=publication.Stamp;
        if(!publication.IsCurrent())
        {if(ReferenceEquals(publication,_publication))Abandon(entryGeneration);return;}
        if(entryGeneration!=_generation||_disposed)return;
        if(ReferenceEquals(publication,_publication)&&_stamp==stamp&&Current(_generation))
        {
            var names=publication.DisplayNames;if(!Current(entryGeneration)){Abandon(entryGeneration);return;}
            UpdateRootMetadata(names,bindings,entryGeneration);if(Current(entryGeneration))RefreshRows();else Abandon(entryGeneration);return;
        }
        if(entryGeneration!=_generation||_disposed)return;
        var generation=Retire();
        if(generation!=_generation||_disposed)return;
        IResultValueLease? lease=null;
        try
        {
            _publication=publication;_stamp=stamp;
            if(!Current(generation)){Abandon(generation);return;}
            lease=publication.Borrow();
            if(!Current(generation)||lease.Stamp!=stamp){Abandon(generation);return;}
            var model=ResultPresentationModel.ForPublication(publication,lease,_pageSize,()=>Current(generation));
            if(!Current(generation)){Abandon(generation);return;}
            _reader=lease;lease=null;
            var names=publication.DisplayNames;if(!Current(generation)){Abandon(generation);return;}
            var displayed=publication.DisplayedPorts;if(!Current(generation)){Abandon(generation);return;}
            var diagnostics=publication.Diagnostics;if(!Current(generation)){Abandon(generation);return;}_diagnostics=diagnostics;
            Install(model,names,bindings,displayed,generation);
            if(Current(generation))RefreshRows();else Abandon(generation);
        }
        catch(Exception error)
        {
            try{Abandon(generation);}catch(Exception cleanup){throw new AggregateException("结果准备及统一清理失败。",error,cleanup);}
            throw;
        }
        finally{lease?.Dispose();}
    }

    private bool Current(int generation)
    {
        if(_disposed||generation!=_generation||_publication is not {} publication||_stamp is not {} stamp)return false;
        var current=publication.IsCurrent();var actualStamp=publication.Stamp;
        return current&&actualStamp==stamp&&!_disposed&&generation==_generation&&ReferenceEquals(_publication,publication)&&_stamp==stamp;
    }

    private void Abandon(int generation)
    {
        if(_disposed||generation!=_generation)return;
        var publication=_publication;var failures=new List<Exception>();
        try{Retire();}catch(Exception error){failures.Add(error);}
        if(_generation==generation+1&&_publication is null&&publication is not null)
            try{_projectionFailed?.Invoke(publication);}catch(Exception error){failures.Add(error);}
        if(failures.Count!=0)throw new AggregateException("结果投影退休失败。",failures);
    }

    /// <summary>Rebuilds only the visible, already requested rows; it never loads another page.</summary>
    public void RefreshRows()
        =>RunProjection(RefreshRowsCore);

    private void RunProjection(Action action)
    {
        var generation=_generation;var publication=_publication;var stamp=_stamp;
        try{action();}
        catch(Exception error)
        {
            var failures=new List<Exception>{error};
            if(publication is not null&&generation==_generation&&ReferenceEquals(_publication,publication)&&_stamp==stamp)
            {
                try{Abandon(generation);}catch(Exception cleanup){failures.Add(cleanup);}
            }
            if(failures.Count==1)throw;
            throw new AggregateException("结果树投影及统一清理失败。",failures);
        }
    }

    private void RefreshRowsCore()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var generation = _generation;
        if(!Current(generation)){Abandon(generation);return;}
        // Both surfaces borrow these exact row objects and the same retained reader.
        // Hierarchy projection never reads another value page.
        foreach(var entry in _entries.Values.ToArray())
        {
            var children=(entry.AllMembers is {} all?new[]{all}:Array.Empty<Entry>()).Concat(entry.Children).Select(child=>child.Row)
                .Concat(entry.More is {} more?new[]{more}:Array.Empty<FlatResultRow>()).ToArray();
            if(!Synchronize(entry.Row.MutableChildren,children,generation))return;
        }
        if(_root is {} root&&!Synchronize(root.MutableChildren,_outputs.Select(entry=>entry.Row).ToArray(),generation))return;
        if(!Synchronize(_roots,_root is {} current?new[]{current}:Array.Empty<FlatResultRow>(),generation))return;
        var visible = new List<FlatResultRow>();
        if (_root != null)
        {
            visible.Add(_root);
            if (_root.IsExpanded)
                foreach (var output in _outputs) AppendVisible(output, visible);
        }
        if(!Current(generation)){Abandon(generation);return;}
        _rows.Clear();
        foreach (var row in visible)
        {
            if (!Current(generation)){Abandon(generation);return;}
            _rows.Add(row);
        }
        if(!Current(generation)){Abandon(generation);return;}
        _setHasResults(visible.Count > 0);
        if(!Current(generation))Abandon(generation);
    }

    private bool Synchronize(ObservableCollection<FlatResultRow> destination,IReadOnlyList<FlatResultRow> rows,int generation)
    {
        if(!Current(generation)){Abandon(generation);return false;}
        if(destination.Count==rows.Count&&destination.Where((row,index)=>!ReferenceEquals(row,rows[index])).Any()==false)return true;
        destination.Clear();
        foreach(var row in rows)
        {
            if(!Current(generation)){Abandon(generation);return false;}
            destination.Add(row);
        }
        if(!Current(generation)){Abandon(generation);return false;}return true;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Retire();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Retire();
    }

    private int Retire()
    {
        var generation = ++_generation;
        var reader = _reader;
        _reader = null;
        foreach (var (row, handler) in _subscriptions) row.PropertyChanged -= handler;
        _subscriptions.Clear();
        foreach (var entry in _entries.Values) DetachMore(entry);
        _entries.Clear();
        _outputs.Clear();
        _root = null;
        _publication = null;_stamp=null;_diagnostics=Array.Empty<Diagnostic>();
        _model = null;_bindingRoots=null;
        var failures=new List<Exception>();
        void Cleanup(Action action){try{action();}catch(Exception error){failures.Add(error);}}
        Cleanup(()=>_roots.Clear());
        if(generation==_generation)Cleanup(()=>_rows.Clear());
        Cleanup(()=>reader?.Dispose());
        if(generation==_generation)Cleanup(()=>_setHasResults(false));
        if(failures.Count!=0)throw new AggregateException("结果树清理失败，所有拥有的清理均已尝试。",failures);
        return generation;
    }

    private void Install(ResultPresentationModel model, IReadOnlyDictionary<string, string>? names,
        IReadOnlyDictionary<string, ResultBindingInfo>? bindings,IReadOnlySet<string>? displayedPorts,int generation)
    {
        if(!Current(generation)){Abandon(generation);return;}
        _model = model;_bindingRoots=bindings;
        var root = new FlatResultRow { Key="result",DisplayName = "结果", DisplayValue = StatusText(model.State), IsGroup = true, IsExpanded = true };
        if(!Current(generation)){Abandon(generation);return;}_root=root;
        Subscribe(root, () => RefreshRows());
        foreach (var node in model.Outputs.Where(node=>displayedPorts is null||displayedPorts.Contains(node.DeclaredPort!.Id)))
        {if(!Current(generation)){Abandon(generation);return;}var entry=GetEntry(node,1);if(entry is null||!Current(generation))return;_outputs.Add(entry);}
        _root.IsGroup=_outputs.Count>0;
        if(_outputs.Count==0&&model.State is ResultViewState.Declaration or ResultViewState.NotExecuted)_root=null;
        UpdateRootMetadata(names, bindings,generation);
    }

    private void UpdateRootMetadata(IReadOnlyDictionary<string, string>? names, IReadOnlyDictionary<string, ResultBindingInfo>? bindings,int generation)
    {
        _bindingRoots=bindings;
        foreach (var entry in _outputs.ToArray())
        {
            if(!Current(generation)){Abandon(generation);return;}
            var id = entry.Node.DeclaredPort!.Id;
            entry.Row.DisplayName = names != null && names.TryGetValue(id, out var name) ? name : id;
            if(!Current(generation)){Abandon(generation);return;}
        }
        foreach(var entry in _entries.Values.ToArray())
        {
            if(!Current(generation)){Abandon(generation);return;}
            if(!ApplyBinding(entry,generation))return;
        }
    }

    private Entry? GetEntry(ResultPresentationNode node, int level)
    {
        var generation=_generation;
        if(!Current(generation)){Abandon(generation);return null;}
        if (_entries.TryGetValue(node.Key, out var existing)) return existing;
        var value=ValueText(node);var childCount=node.ChildCount;
        if(!Current(generation)){Abandon(generation);return null;}
        var row = new FlatResultRow
        {
            Key=node.Key,
            DisplayName = node.IsAllProjection?(node.DeclaredPort?.Id is {} field?field+" [All]":"All · 全部元素成员"):node.DeclaredPort?.Id ?? "[" + node.ElementIndex!.Value.ToString(CultureInfo.InvariantCulture) + "]",
            DisplayValue = value, Level = level,
            IsGroup = childCount is > 0||(node.HasAllMembers&&node.DeclarationAddress is {} address&&_bindingRoots?.ContainsKey(address.RootPort.Id)==true), IsExpanded = node.IsExpanded
        };
        var entry = new Entry(node, row);
        _entries.Add(node.Key, entry);
        if(!ApplyBinding(entry,generation))return null;
        Subscribe(row, () =>
        {
            node.IsExpanded = row.IsExpanded;
            if (row.IsExpanded && entry.Children.Count == 0) LoadPage(entry);
            RefreshRows();
        });
        return entry;
    }

    private bool ApplyBinding(Entry entry,int generation)
    {
        if(!Current(generation)){Abandon(generation);return false;}
        var address=entry.Node.DeclarationAddress;
        ResultBindingInfo? binding=null;
        if(address is not null&&!address.IsSampleRow&&_bindingRoots?.TryGetValue(address.RootPort.Id,out var root)==true)
        {
            var reference=address.Reference(root.Reference.NodeId,root.Reference.ScopeId!);
            var path=address.RootPort.Id+string.Concat(address.MemberFieldPath.Select(member=>" / "+member));
            if(reference.Type.Shape==ValueShape.Sequence||address.ThroughSequence)path+=" [All]";
            binding=new ResultBindingInfo(reference,path,root.BindingNames);
            if(!binding.CanBind)binding=null;
        }
        entry.Row.BindingInfo=binding;
        if(!Current(generation)){Abandon(generation);return false;}
        entry.Row.BindingDisplayName=binding?.BoundVariableNames;
        if(!Current(generation)){Abandon(generation);return false;}
        entry.Row.SourceNodeId=binding?.SourceNodeId;
        entry.Row.SourcePropertyName=binding?.ResultPath;
        entry.Row.PropertyClrType=null;
        return Current(generation);
    }

    private void Subscribe(FlatResultRow row, Action changed)
    {
        var generation = _generation;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if(args.PropertyName!=nameof(FlatResultRow.IsExpanded))return;
            if(!Current(generation)){Abandon(generation);return;}changed();
        };
        row.PropertyChanged += handler;
        _subscriptions.Add((row, handler));
    }

    private void LoadPage(Entry entry)
        =>RunProjection(()=>LoadPageCore(entry));

    private void LoadPageCore(Entry entry)
    {
        var generation=_generation;
        if(!Current(generation)){Abandon(generation);return;}
        DetachMore(entry);
        if(entry.AllMembers is null&&entry.Node.DeclarationAddress is {IsSampleRow:false} address&&_bindingRoots?.ContainsKey(address.RootPort.Id)==true&&entry.Node.CreateAllMembers() is {} all)
        {entry.AllMembers=GetEntry(all,entry.Row.Level+1);if(!Current(generation)){Abandon(generation);return;}}
        var page = entry.Node.GetChildrenPage(entry.Children.Count, _pageSize);
        if(!Current(generation)){Abandon(generation);return;}
        foreach (var child in page){if(!Current(generation)){Abandon(generation);return;}var childEntry=GetEntry(child,entry.Row.Level+1);if(childEntry is null||!Current(generation))return;entry.Children.Add(childEntry);}
        if (entry.Node.ChildCount is not int count || entry.Children.Count >= count) return;
        var offset = entry.Children.Count;
        var last = Math.Min((long)count - 1, (long)offset + _pageSize - 1);
        var more = new FlatResultRow
        {
            Key=entry.Node.Key+":page:"+offset.ToString(CultureInfo.InvariantCulture),
            DisplayName = $"加载下一页 [{offset.ToString(CultureInfo.InvariantCulture)}…{last.ToString(CultureInfo.InvariantCulture)}]",
            DisplayValue = "按需读取", IsGroup = true, IsExpanded = false, Level = entry.Row.Level + 1
        };
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if(!Current(generation)){Abandon(generation);return;}
            if (ReferenceEquals(entry.More, more) && args.PropertyName == nameof(FlatResultRow.IsExpanded) && more.IsExpanded)
            {
                LoadPage(entry);
                RefreshRows();
            }
        };
        entry.More = more;
        entry.MoreHandler = handler;
        more.PropertyChanged += handler;
    }

    private static void DetachMore(Entry entry)
    {
        if (entry.More != null && entry.MoreHandler != null) entry.More.PropertyChanged -= entry.MoreHandler;
        entry.More = null;
        entry.MoreHandler = null;
    }

    private static void AppendVisible(Entry entry, List<FlatResultRow> visible)
    {
        visible.Add(entry.Row);
        if (!entry.Row.IsExpanded) return;
        if(entry.AllMembers is {} all)AppendVisible(all,visible);
        foreach (var child in entry.Children) AppendVisible(child, visible);
        if (entry.More != null) visible.Add(entry.More);
    }

    private string StatusText(ResultViewState state)
    {
        var label=state switch
        {
            ResultViewState.Declaration=>"未运行",ResultViewState.NotExecuted=>"未运行",ResultViewState.Running=>"运行中",
            ResultViewState.Success=>"成功",ResultViewState.PartialSuccess=>"部分成功",
            ResultViewState.Failed=>"失败",ResultViewState.Timeout=>"超时",
            ResultViewState.Cancelled=>"已取消",_=>throw new ArgumentOutOfRangeException(nameof(state))
        };
        return _diagnostics.FirstOrDefault() is {} diagnostic?label+": "+diagnostic.Message:label;
    }

    private string ValueText(ResultPresentationNode node) => node.IsAllProjection?"All · 按声明绑定全部元素，不是样本索引值":node.ValueState switch
    {
        ResultPresentationValueState.Missing => "missing",
        ResultPresentationValueState.Null => "null",
        ResultPresentationValueState.ResourcePending => _model!.State is ResultViewState.Success or ResultViewState.PartialSuccess?"资源待接入":StatusText(_model.State),
        ResultPresentationValueState.Unavailable => _model!.State is ResultViewState.Success or ResultViewState.PartialSuccess ? "值不可用" : StatusText(_model.State),
        _ => node.Declaration.Shape switch
        {
            ValueShape.Sequence => SequenceText(node.ChildCount),
            ValueShape.Record => "记录",
            ValueShape.Geometry => node.GeometryValue is {} geometry?GeometryText(geometry):"值不可用",
            _ => node.ScalarValue switch
            {
                string text => text.Length == 0 ? "empty" : text,
                bool boolean => boolean ? "true" : "false",
                long integer => integer.ToString(CultureInfo.InvariantCulture),
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                _ => "值不可用"
            }
        }
    };
    private static string SequenceText(int? count)=>count is null?"值不可用":count==0?"空序列 (0)":"["+count.Value.ToString(CultureInfo.InvariantCulture)+" 项]";
    private static string GeometryText(GeometryValue geometry)
    {
        static string N(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
        static string P(Point2 point)=>"("+N(point.X)+", "+N(point.Y)+")";
        var value=geometry switch
        {
            PointGeometry point=>"点 "+P(point.Point),
            LineGeometry line=>"线 "+P(line.Start)+" → "+P(line.End),
            CircleGeometry circle=>"圆 "+P(circle.Center)+" r="+N(circle.Radius),
            ArcGeometry arc=>"弧 "+P(arc.Center)+" r="+N(arc.Radius)+" start="+N(arc.StartRadians)+" rad sweep="+N(arc.SweepRadians)+" rad",
            OrientedRectangleGeometry rectangle=>"旋转矩形 "+P(rectangle.Center)+" "+N(rectangle.Width)+" × "+N(rectangle.Height)+" angle="+N(rectangle.AngleRadians)+" rad",
            EllipseGeometry ellipse=>"椭圆 "+P(ellipse.Center)+" rx="+N(ellipse.RadiusX)+" ry="+N(ellipse.RadiusY)+" angle="+N(ellipse.AngleRadians)+" rad",
            PolygonGeometry polygon=>"多边形 ["+polygon.Vertices.Count.ToString(CultureInfo.InvariantCulture)+" 顶点]",
            _=>throw new InvalidOperationException("Unknown immutable geometry.")
        };
        return value+" · "+geometry.Frame.Id+" · "+geometry.Frame.Unit;
    }
}
