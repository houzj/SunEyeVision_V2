using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
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
        internal FlatResultRow? More;
        internal PropertyChangedEventHandler? MoreHandler;
    }

    private readonly ObservableCollection<FlatResultRow> _rows;
    private readonly Action<bool> _setHasResults;
    private readonly int _pageSize;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<(FlatResultRow Row, PropertyChangedEventHandler Handler)> _subscriptions = new();
    private readonly List<Entry> _outputs = new();
    private ResultPresentationModel? _model;
    private DeclaredOutputSnapshot? _snapshot;
    private DeclaredOutputReader? _reader;
    private FlatResultRow? _root;
    private int _generation;
    private bool _disposed;

    public ResultPresentationPresenter(ObservableCollection<FlatResultRow> rows, Action<bool> setHasResults, int pageSize = 128)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        _setHasResults = setHasResults ?? throw new ArgumentNullException(nameof(setHasResults));
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        _pageSize = pageSize;
    }

    public void ShowSchema(ToolSchema schema, IReadOnlyDictionary<string, string>? displayNames = null,
        IReadOnlyDictionary<string, ResultBindingInfo>? bindings = null,IReadOnlySet<string>? displayedPorts=null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(schema);
        if (_snapshot == null && _model?.Schema.Fingerprint == schema.Fingerprint &&
            _outputs.Select(entry=>entry.Node.DeclaredPort!.Id).SequenceEqual(
                schema.Outputs.Where(port=>displayedPorts is null||displayedPorts.Contains(port.Id)).Select(port=>port.Id)))
        {
            UpdateRootMetadata(displayNames, bindings);
            RefreshRows();
            return;
        }
        var generation = Retire();
        if (generation != _generation || _disposed) return;
        Install(ResultPresentationModel.ForSchema(schema, _pageSize), displayNames, bindings,displayedPorts);
        RefreshRows();
    }

    public void ShowRun(DeclaredOutputSnapshot snapshot, IReadOnlyDictionary<string, string>? displayNames = null,
        IReadOnlyDictionary<string, ResultBindingInfo>? bindings = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_snapshot is { } previous && previous.Identity == snapshot.Identity &&
            previous.SchemaRevision == snapshot.SchemaRevision && previous.Schema.Fingerprint == snapshot.Schema.Fingerprint &&
            previous.Status == snapshot.Status)
        {
            UpdateRootMetadata(displayNames, bindings);
            RefreshRows();
            return;
        }
        var generation = Retire();
        if (generation != _generation || _disposed) return;
        var reader = snapshot.AcquireReader();
        try
        {
            var state = snapshot.Status switch
            {
                ExecutionStatus.Success or ExecutionStatus.PartialSuccess => ResultPresentationState.Success,
                ExecutionStatus.Failed or ExecutionStatus.Timeout or ExecutionStatus.Cancelled => ResultPresentationState.Failed,
                ExecutionStatus.NotExecuted or ExecutionStatus.Running => ResultPresentationState.NotRun,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot), "Unknown execution status.")
            };
            var model = ResultPresentationModel.ForRun(snapshot.Schema, snapshot.Identity, state, reader.Values, _pageSize);
            if (generation != _generation || _disposed) return;
            _reader = reader;
            reader = null;
            _snapshot = snapshot;
            Install(model, displayNames, bindings,snapshot.Fields.Where(field=>field.Value.DisplayInResultPanel).Select(field=>field.Key).ToHashSet(StringComparer.Ordinal));
            RefreshRows();
        }
        catch
        {
            if (generation == _generation) Retire();
            throw;
        }
        finally { reader?.Dispose(); }
    }

    /// <summary>Rebuilds only the visible, already requested rows; it never loads another page.</summary>
    public void RefreshRows()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var generation = _generation;
        var visible = new List<FlatResultRow>();
        if (_root != null && _outputs.Count > 0)
        {
            visible.Add(_root);
            if (_root.IsExpanded)
                foreach (var output in _outputs) AppendVisible(output, visible);
        }
        _rows.Clear();
        foreach (var row in visible)
        {
            if (generation != _generation || _disposed) return;
            _rows.Add(row);
        }
        if (generation == _generation && !_disposed) _setHasResults(visible.Count > 0);
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
        _snapshot = null;
        _model = null;
        try { _rows.Clear(); }
        finally
        {
            try { reader?.Dispose(); }
            finally { if (generation == _generation) _setHasResults(false); }
        }
        return generation;
    }

    private void Install(ResultPresentationModel model, IReadOnlyDictionary<string, string>? names,
        IReadOnlyDictionary<string, ResultBindingInfo>? bindings,IReadOnlySet<string>? displayedPorts=null)
    {
        _model = model;
        _root = new FlatResultRow { DisplayName = "结果", DisplayValue = StatusText(model.State), IsGroup = true, IsExpanded = true };
        Subscribe(_root, () => RefreshRows());
        foreach (var node in model.Outputs.Where(node=>displayedPorts is null||displayedPorts.Contains(node.DeclaredPort!.Id))) _outputs.Add(GetEntry(node, 1));
        if(_outputs.Count==0&&model.State!=ResultPresentationState.Failed)_root=null;
        UpdateRootMetadata(names, bindings);
    }

    private void UpdateRootMetadata(IReadOnlyDictionary<string, string>? names, IReadOnlyDictionary<string, ResultBindingInfo>? bindings)
    {
        foreach (var entry in _outputs)
        {
            var id = entry.Node.DeclaredPort!.Id;
            entry.Row.DisplayName = names != null && names.TryGetValue(id, out var name) ? name : id;
            var binding = bindings != null && bindings.TryGetValue(id, out var supplied) ? supplied : null;
            entry.Row.BindingInfo = binding;
            entry.Row.SourceNodeId = binding?.SourceNodeId;
            entry.Row.SourcePropertyName = binding?.ResultPath;
            entry.Row.PropertyClrType = binding?.ValueType;
        }
    }

    private Entry GetEntry(ResultPresentationNode node, int level)
    {
        if (_entries.TryGetValue(node.Key, out var existing)) return existing;
        var row = new FlatResultRow
        {
            DisplayName = node.DeclaredPort?.Id ?? "[" + node.ElementIndex!.Value.ToString(CultureInfo.InvariantCulture) + "]",
            DisplayValue = ValueText(node), Level = level,
            IsGroup = node.ChildCount is > 0, IsExpanded = node.IsExpanded
        };
        var entry = new Entry(node, row);
        _entries.Add(node.Key, entry);
        Subscribe(row, () =>
        {
            node.IsExpanded = row.IsExpanded;
            if (row.IsExpanded && entry.Children.Count == 0) LoadPage(entry);
            RefreshRows();
        });
        return entry;
    }

    private void Subscribe(FlatResultRow row, Action changed)
    {
        var generation = _generation;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (!_disposed && generation == _generation && args.PropertyName == nameof(FlatResultRow.IsExpanded)) changed();
        };
        row.PropertyChanged += handler;
        _subscriptions.Add((row, handler));
    }

    private void LoadPage(Entry entry)
    {
        DetachMore(entry);
        var page = entry.Node.GetChildrenPage(entry.Children.Count, _pageSize);
        foreach (var child in page) entry.Children.Add(GetEntry(child, entry.Row.Level + 1));
        if (entry.Node.ChildCount is not int count || entry.Children.Count >= count) return;
        var offset = entry.Children.Count;
        var last = Math.Min((long)count - 1, (long)offset + _pageSize - 1);
        var more = new FlatResultRow
        {
            DisplayName = $"加载下一页 [{offset.ToString(CultureInfo.InvariantCulture)}…{last.ToString(CultureInfo.InvariantCulture)}]",
            DisplayValue = "按需读取", IsGroup = true, IsExpanded = false, Level = entry.Row.Level + 1
        };
        var generation = _generation;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (!_disposed && generation == _generation && ReferenceEquals(entry.More, more) &&
                args.PropertyName == nameof(FlatResultRow.IsExpanded) && more.IsExpanded)
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
        foreach (var child in entry.Children) AppendVisible(child, visible);
        if (entry.More != null) visible.Add(entry.More);
    }

    private string StatusText(ResultPresentationState state) => state switch
    {
        ResultPresentationState.NotRun => _snapshot?.Status == ExecutionStatus.Running ? "运行中" : "未运行",
        ResultPresentationState.Failed => _snapshot?.Diagnostics.FirstOrDefault() is { } diagnostic ? "失败: " + diagnostic.Message : "失败",
        _ => _snapshot?.Status == ExecutionStatus.PartialSuccess ? "部分成功" : "成功"
    };

    private string ValueText(ResultPresentationNode node) => node.ValueState switch
    {
        ResultPresentationValueState.Missing => "missing",
        ResultPresentationValueState.Null => "null",
        ResultPresentationValueState.ResourcePending => "资源待接入",
        ResultPresentationValueState.Unavailable => _model!.State == ResultPresentationState.Success ? "值不可用" : StatusText(_model.State),
        _ => node.Declaration.Shape switch
        {
            ValueShape.Sequence => node.ChildCount == 0 ? "空序列 (0)" : "[" + node.ChildCount!.Value.ToString(CultureInfo.InvariantCulture) + " 项]",
            ValueShape.Record => "记录",
            ValueShape.Geometry => GeometryText(node.GeometryValue!),
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
