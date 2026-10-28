using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

/// <summary>Read-only declaration/result tree over one borrowed publication. It never owns the shared publication.</summary>
public sealed class DeclaredPropertyList : IToolResultView
{
    private const int PageSize = 128;
    private readonly string _schemaFingerprint;
    private readonly PropertyListView _view = new() { IsReadOnly = true };
    private readonly ObservableCollection<Row> _roots = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<(Row Row, PropertyChangedEventHandler Handler)> _subscriptions = new();
    private IResultPublication? _publication;
    private IResultValueLease? _lease;
    private ResultPresentationModel? _model;
    private ResultPublicationStamp? _stamp;
    private long _generation;
    private bool _disposed;

    public FrameworkElement View => _view;
    public IReadOnlyList<Port> Sources { get; }
    public event EventHandler<ResultSelection>? SelectionHighlightChanged;

    public DeclaredPropertyList(ToolSchema schema, ResultProjectionDescriptor projection)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(projection);
        if (projection.Kind != ResultProjectionKind.PropertyList) throw new ArgumentException("A property-list result projection is required.", nameof(projection));
        var errors = projection.Validate(schema);
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors.Select(error => error.Message)), nameof(projection));
        _schemaFingerprint = schema.Fingerprint;
        Sources = Array.AsReadOnly(schema.Outputs.ToArray());
        _view.ItemsSource = new ReadOnlyObservableCollection<Row>(_roots);
    }

    public void Present(IResultPublication publication)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(publication);
        if (publication.Schema.Fingerprint != _schemaFingerprint) throw new ArgumentException("Publication schema differs from the constructed result view.", nameof(publication));
        if (ReferenceEquals(publication, _publication) && _stamp == publication.Stamp && Current(_generation)) return;
        var generation = Retire();
        if (_disposed || generation != _generation || !publication.IsCurrent()) return;
        IResultValueLease? lease = null;
        try
        {
            var stamp = publication.Stamp;
            lease = publication.Borrow();
            bool Installing() => publication.IsCurrent() && publication.Stamp == stamp && !_disposed && generation == _generation;
            if (!Installing() || lease.Stamp != stamp) return;
            var model = ResultPresentationModel.ForPublication(publication, lease, PageSize, Installing);
            if (!Installing()) return;
            _publication = publication; _stamp = stamp; _lease = lease; lease = null; _model = model;
            var root = new Row("$result", "结果", StatusText(publication.State), 0, true) { IsExpanded = true };
            foreach (var node in model.Outputs.Where(node => publication.DisplayedPorts.Contains(node.DeclaredPort!.Id)))
            {
                if (!Current(generation)) return;
                var entry = EntryFor(node, 1, generation);
                if(entry is null||!Current(generation))return;
                if (publication.DisplayNames.TryGetValue(node.DeclaredPort!.Id, out var name)) entry.Row.DisplayName = name;
                root.MutableChildren.Add(entry.Row);
            }
            root.IsGroup = root.Children.Count > 0;
            _view.SelectedCommand = new GuardedCommand(value =>
            {
                if (value is not Row row || !Owns(row, generation)) return;
                _view.SelectedItemPath = row.NodeKey;
                if(!Owns(row,generation))return;
                // Original row input selects a path. Without physical-frame proof it must not invent geometry highlights.
                SelectionHighlightChanged?.Invoke(this, new ResultSelection(stamp, row.NodeKey, Array.Empty<ResultHighlightTarget>()));
            }, value => value is Row row && Owns(row, generation));
            if (!Current(generation)) return;
            _view.CopyPathCommand = new GuardedCommand(value =>
            {
                if (Current(generation) && value is string { Length: > 0 } text && text == _view.SelectedItemPath) Clipboard.SetText(text);
            }, value => Current(generation) && value is string { Length: > 0 } text && text == _view.SelectedItemPath);
            if (!Current(generation)) return;
            _view.ErrorInfo = publication.Diagnostics.Count == 0 ? null : string.Join(Environment.NewLine, publication.Diagnostics.Select(issue => issue.Message));
            if (Current(generation)) _roots.Add(root);
        }
        catch
        {
            if (generation == _generation) Retire();
            throw;
        }
        finally { lease?.Dispose(); }
    }

    private bool Current(long generation)
    {
        var publication = _publication; var lease = _lease; var stamp = _stamp;
        if (_disposed || generation != _generation || publication is null || lease is null || stamp != publication.Stamp || lease.Stamp != stamp) return false;
        var current = publication.IsCurrent();
        return current && !_disposed && generation == _generation && ReferenceEquals(publication, _publication)
            && ReferenceEquals(lease, _lease) && stamp == _stamp;
    }
    private bool Owns(Row row, long generation) => Current(generation) && _entries.TryGetValue(row.Key, out var entry) && ReferenceEquals(entry.Row, row);

    private Entry? EntryFor(ResultPresentationNode node, int level, long generation)
    {
        if(!Current(generation))return null;
        if (_entries.TryGetValue(node.Key, out var existing)) return existing;
        var model=_model;
        if(model is null)return null;
        var text=ValueText(node,model);
        if(!Current(generation))return null;
        var count=node.ChildCount;
        if(!Current(generation))return null;
        var row = new Row(node.Key, node.DeclaredPort?.Id ?? "[" + node.ElementIndex!.Value.ToString(CultureInfo.InvariantCulture) + "]",
            text, level, count is > 0) { IsExpanded = node.IsExpanded, NodeKey = node.Key };
        var entry = new Entry(node, row); _entries.Add(node.Key, entry);
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName != nameof(Row.IsExpanded) || !Owns(row, generation)) return;
            node.IsExpanded = row.IsExpanded;
            if(!Owns(row,generation))return;
            if (row.IsExpanded && entry.Children.Count == 0) LoadPage(entry, generation);
        };
        row.PropertyChanged += handler; _subscriptions.Add((row, handler));
        return entry;
    }

    private void LoadPage(Entry entry, long generation)
    {
        if (!Current(generation) || entry.Loading) return;
        entry.Loading = true;
        try
        {
        var page = entry.Node.GetChildrenPage(entry.Children.Count, PageSize);
        if (!Current(generation)) return;
        var prepared = new List<Entry>();
        foreach (var node in page)
        {
            if (!Current(generation)) return;
            var child=EntryFor(node, entry.Row.Level + 1, generation);
            if(child is null||!Current(generation))return;
            prepared.Add(child);
        }
        var childCount=entry.Node.ChildCount;
        if(!Current(generation))return;
        var offset = entry.Children.Count + prepared.Count;
        Row? more = null;
        PropertyChangedEventHandler? handler = null;
        if (childCount is int count && offset < count)
        {
        var last = Math.Min((long)count - 1, (long)offset + PageSize - 1);
        more = new Row(entry.Node.Key + ":page:" + offset.ToString(CultureInfo.InvariantCulture),
            $"加载下一页 [{offset.ToString(CultureInfo.InvariantCulture)}…{last.ToString(CultureInfo.InvariantCulture)}]", "按需读取", entry.Row.Level + 1, true);
        var pageRow = more;
        handler = (_, args) =>
        {
            if (args.PropertyName == nameof(Row.IsExpanded) && pageRow.IsExpanded && ReferenceEquals(entry.More, pageRow) && Current(generation)) LoadPage(entry, generation);
        };
        }
        // Complete the page and its continuation before any collection observer runs.
        // A throwing observer must not strand a partial page with no way to continue.
        if (entry.More is {} previous && entry.MoreHandler is {} previousHandler)
            previous.PropertyChanged -= previousHandler;
        entry.Children.AddRange(prepared);
        entry.More = more; entry.MoreHandler = handler;
        if (more is not null && handler is not null) more.PropertyChanged += handler;
        var rows = entry.Children.Select(child => child.Row).ToList();
        if (more is not null) rows.Add(more);
        entry.Row.MutableChildren.Install(rows);
        entry.Row.MutableChildren.Publish(() => Owns(entry.Row, generation));
        }
        finally { entry.Loading = false; }
    }

    public void Clear()
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); Retire();
    }
    public void Dispose()
    {
        _view.Dispatcher.VerifyAccess(); if (_disposed) return; _disposed = true;
        try { Retire(); } finally { SelectionHighlightChanged = null; }
    }
    private long Retire()
    {
        var generation = ++_generation; var stamp = _stamp; var lease = _lease;
        _publication = null; _stamp = null; _lease = null; _model = null;
        foreach (var (row, handler) in _subscriptions) row.PropertyChanged -= handler;
        _subscriptions.Clear();
        foreach (var entry in _entries.Values)
        {
            if (entry.More is { } more && entry.MoreHandler is { } handler) more.PropertyChanged -= handler;
            entry.More = null; entry.MoreHandler = null;
        }
        _entries.Clear();
        var errors=new List<Exception>();
        void Clean(Action action,bool ownView=true)
        {if(ownView&&generation!=_generation)return;try{action();}catch(Exception error){errors.Add(error);}}
        Clean(()=>_view.SelectedCommand=null);
        Clean(()=>_view.CopyPathCommand=null);
        Clean(()=>_view.SelectedItem=null);
        Clean(()=>_view.SelectedItemPath=null);
        Clean(()=>_view.ErrorInfo=null);
        Clean(()=>_roots.Clear());
        Clean(()=>lease?.Dispose(),false);
        Clean(()=>{if(stamp is not null)SelectionHighlightChanged?.Invoke(this,new(stamp,null,Array.Empty<ResultHighlightTarget>()));});
        if(errors.Count!=0)throw new AggregateException("Property result retired; cleanup observers failed.",errors);
        return generation;
    }

    private static string ValueText(ResultPresentationNode node,ResultPresentationModel model) => node.ValueState switch
    {
        ResultPresentationValueState.Missing => "missing",
        ResultPresentationValueState.Null => "null",
        ResultPresentationValueState.Unavailable => model.State is ResultViewState.Success or ResultViewState.PartialSuccess ? "值不可用" : StatusText(model.State),
        ResultPresentationValueState.ResourcePending => model.State is ResultViewState.Success or ResultViewState.PartialSuccess ? "资源显示未提供" : StatusText(model.State),
        _ => node.Declaration.Shape switch
        {
            ValueShape.Record => "记录",
            ValueShape.Sequence => node.ChildCount is {} count ? count==0?"空序列 (0)":"["+count.ToString(CultureInfo.InvariantCulture)+" 项]":"值不可用",
            ValueShape.Geometry => node.GeometryValue is { } geometry ? GeometryText(geometry) : "几何值不可用",
            _ => node.ScalarValue switch
            {
                string text => text.Length == 0 ? "empty" : text,
                bool value => value ? "true" : "false",
                long value => value.ToString(CultureInfo.InvariantCulture),
                double value => value.ToString("R", CultureInfo.InvariantCulture),
                _ => "值不可用"
            }
        }
    };
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
    private static string StatusText(ResultViewState state) => state switch
    {
        ResultViewState.Declaration => "声明（未运行）", ResultViewState.NotExecuted => "未执行", ResultViewState.Running => "运行中",
        ResultViewState.Success => "成功", ResultViewState.PartialSuccess => "部分成功", ResultViewState.Failed => "失败",
        ResultViewState.Timeout => "超时", ResultViewState.Cancelled => "已取消", _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
    private sealed class Entry(ResultPresentationNode node, Row row)
    {
        internal ResultPresentationNode Node { get; } = node;
        internal Row Row { get; } = row;
        internal List<Entry> Children { get; } = new();
        internal bool Loading;
        internal Row? More; internal PropertyChangedEventHandler? MoreHandler;
    }
    /// <summary>Visual row identity is the declared model key; no mutable business object or resource escapes.</summary>
    public sealed class Row : INotifyPropertyChanged
    {
        private bool _expanded;
        internal Row(string key, string name, string value, int level, bool group)
        { Key = key; DisplayName = name; DisplayValue = value; Level = level; IsGroup = group; Children = new(MutableChildren); }
        public string Key { get; }
        internal string? NodeKey { get; init; }
        public string DisplayName { get; internal set; }
        public string DisplayValue { get; }
        public int Level { get; }
        public bool IsGroup { get; internal set; }
        public bool IsExpanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); } }
        internal PublishedRows MutableChildren { get; } = new();
        public ReadOnlyObservableCollection<Row> Children { get; }
        public bool CanBindVariable => false;
        public string? BindingDisplayName => null;
        public string BindingUnavailableReason => "只读结果分析视图不提供变量编辑服务。";
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class PublishedRows : ObservableCollection<Row>
    {
        internal void Install(IReadOnlyList<Row> rows)
        { Items.Clear(); foreach (var row in rows) Items.Add(row); }

        internal void Publish(Func<bool> current)
        {
            var errors = new List<Exception>();
            void Notify(Action notify)
            { try { if (current()) notify(); } catch (Exception error) { errors.Add(error); } }
            Notify(() => OnPropertyChanged(new(nameof(Count))));
            Notify(() => OnPropertyChanged(new("Item[]")));
            Notify(() => OnCollectionChanged(new(NotifyCollectionChangedAction.Reset)));
            if (errors.Count != 0) throw new AggregateException("Property result page installed; collection observers failed.", errors);
        }
    }
    private sealed class GuardedCommand(Action<object?> execute, Predicate<object?> canExecute) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute(parameter);
        public void Execute(object? parameter) { if (canExecute(parameter)) execute(parameter); }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
