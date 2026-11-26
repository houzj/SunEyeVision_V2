using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.TemplateStudio;

/// <summary>
/// 模板集合（对应原厂 <c>FeaturepatMatchConfigPanalDataDecorate</c>）。
///
/// **非泛型**：与原厂一致，集合只认识 <see cref="TemplateModelItem"/> 基类。
/// 泛型项由工具创建后加入（如 <c>new TemplateModelItem&lt;ContourMatchTemplate&gt;</c>），
/// 共用面板因此不需要知道具体工具有什么模板类型。
///
/// 与原厂一致的能力：数量上限、新增/删除/删除全部、上移/下移、选中，
/// 以及「内容或选中项变化时通知」。
/// 证据：<c>FeaturepatMatchConfigPanalDataDecorate.cs:159</c> 的
/// <c>protected virtual int MaxModelNum { get; } = 36;</c>；
/// 上移/下移/删除/选中在 <c>:435/442/449/457</c>。
/// </summary>
public sealed class TemplateModelCollection
{
    /// <summary>模板数量上限（原厂 Decorate:159 的 36）。</summary>
    public const int MaxModelNum = 36;

    private readonly ObservableCollection<TemplateModelItem> _items = new();
    private TemplateModelItem? _selected;

    /// <summary>集合内容或选中项变化。UI 层据此刷新列表。</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<TemplateModelItem> Items => _items;

    public int Count => _items.Count;

    public bool IsFull => _items.Count >= MaxModelNum;

    public TemplateModelItem? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>追加一项；已到上限返回 false（不抛，由调用方决定提示方式）。</summary>
    public bool Add(TemplateModelItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsFull) return false;
        item.Index = _items.Count;
        _items.Add(item);
        _selected = item;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>插入到指定位置（原厂 ReplaceUserData 的插入语义）。</summary>
    public bool Insert(int index, TemplateModelItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsFull) return false;
        var position = Math.Clamp(index, 0, _items.Count);
        _items.Insert(position, item);
        Reindex();
        _selected = item;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Remove(TemplateModelItem item)
    {
        if (!_items.Remove(item)) return false;
        Reindex();
        if (ReferenceEquals(_selected, item)) _selected = _items.Count == 0 ? null : _items[0];
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>删除全部（原厂 DeleteAllModels）。</summary>
    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        _selected = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>上移一位（原厂 UpCmdExecute）；已在首位返回 false。</summary>
    public bool MoveUp(TemplateModelItem item)
    {
        var index = _items.IndexOf(item);
        if (index <= 0) return false;
        _items.Move(index, index - 1);
        Reindex();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>下移一位（原厂 DownCmdExecute）；已在末位返回 false。</summary>
    public bool MoveDown(TemplateModelItem item)
    {
        var index = _items.IndexOf(item);
        if (index < 0 || index >= _items.Count - 1) return false;
        _items.Move(index, index + 1);
        Reindex();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>按下标取项；越界返回 null。</summary>
    public TemplateModelItem? At(int index)
        => index >= 0 && index < _items.Count ? _items[index] : null;

    private void Reindex()
    {
        for (var index = 0; index < _items.Count; index++)
            _items[index].Index = index;
    }
}
