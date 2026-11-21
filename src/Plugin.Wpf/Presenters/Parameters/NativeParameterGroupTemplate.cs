using System.Collections.ObjectModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Shared parameter-group boundary modeled after the VM ParamGroup template host.</summary>
public sealed class NativeParameterGroupTemplate : IDisposable
{
    private readonly List<Action<NativeParameterGroupState>> _projections = [];
    private readonly Dictionary<string,NativeParameterGroupItem> _items = new(StringComparer.Ordinal);
    private bool _disposed;
    public string DisplayType { get; }
    public NativeParameterGroupState State { get; private set; }
    public ReadOnlyObservableCollection<NativeParameterGroupItem> Items { get; }
    private readonly ObservableCollection<NativeParameterGroupItem> _itemList = [];

    public NativeParameterGroupTemplate(string displayType, NativeParameterGroupState initial)
    {
        DisplayType = string.IsNullOrWhiteSpace(displayType)
            ? throw new ArgumentException("参数组显示类型不能为空。", nameof(displayType))
            : displayType;
        State = initial;
        Items=new ReadOnlyObservableCollection<NativeParameterGroupItem>(_itemList);
    }

    public void RegisterItem(string id, FrameworkElement view, Action<NativeParameterGroupState> projection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(projection);
        if (_items.ContainsKey(id)) return;
        var item=new NativeParameterGroupItem(id,view,projection);
        _items.Add(id,item);
        _itemList.Add(item);
        projection(State);
    }

    public void RegisterProjection(Action<NativeParameterGroupState> projection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(projection);
        _projections.Add(projection);
        projection(State);
    }

    public void Project(NativeParameterGroupState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        State = state;
        foreach (var item in _itemList.ToArray()) item.Project(state);
        foreach (var projection in _projections.ToArray()) projection(state);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _projections.Clear();
        _items.Clear();
        _itemList.Clear();
    }
}

public sealed class NativeParameterGroupItem
{
    private readonly Action<NativeParameterGroupState> _projection;
    internal NativeParameterGroupItem(string id,FrameworkElement view,Action<NativeParameterGroupState> projection)
    { Id=id;View=view;_projection=projection; }
    public string Id { get; }
    public FrameworkElement View { get; }
    internal void Project(NativeParameterGroupState state)=>_projection(state);
}

public sealed record NativeParameterGroupState(bool Enabled, int Mode);
