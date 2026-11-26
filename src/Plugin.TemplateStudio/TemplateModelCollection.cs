using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.TemplateStudio;

/// <summary>
/// 模板集合（对应原厂 <c>FeaturepatMatchConfigPanalDataDecorate</c>）。
///
/// 与原厂一致：数量上限、新增/删除/删除全部、上移/下移、选中，
/// 以及「内容或选中项变化时通知」——这些是原厂的共用逻辑，不是某个工具特有。
/// 证据：<c>FeaturepatMatchConfigPanalDataDecorate.cs:159</c>
/// 的 <c>protected virtual int MaxModelNum { get; } = 36;</c>；
/// 上移/下移/删除/选中在 <c>:435/442/449/457</c>。
/// </summary>
public sealed class TemplateModelCollection<TModel>
    where TModel : class
{
    /// <summary>模板数量上限（原厂 Decorate:159 的 36）。</summary>
    public const int MaxModelNum = 36;

    private readonly ObservableCollection<TemplateModelItem<TModel>> _items = new();
    private TemplateModelItem<TModel>? _selected;

    /// <summary>集合内容或选中项变化。UI 层据此刷新列表。</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<TemplateModelItem<TModel>> Items => _items;

    public int Count => _items.Count;

    public bool IsFull => _items.Count >= MaxModelNum;

    public TemplateModelItem<TModel>? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>追加一个模板；已到上限返回 null（不抛，由调用方决定提示方式）。</summary>
    public TemplateModelItem<TModel>? Add(string? name = null)
    {
        if (IsFull) return null;
        var item = new TemplateModelItem<TModel>(_items.Count, name);
        _items.Add(item);
        _selected = item;
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    /// <summary>插入到指定位置（原厂 ReplaceUserData 的插入语义）。</summary>
    public TemplateModelItem<TModel>? Insert(int index, string? name = null)
    {
        if (IsFull) return null;
        var position = Math.Clamp(index, 0, _items.Count);
        var item = new TemplateModelItem<TModel>(position, name);
        _items.Insert(position, item);
        Reindex();
        _selected = item;
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    public bool Remove(TemplateModelItem<TModel> item)
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
    public bool MoveUp(TemplateModelItem<TModel> item)
    {
        var index = _items.IndexOf(item);
        if (index <= 0) return false;
        _items.Move(index, index - 1);
        Reindex();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>下移一位（原厂 DownCmdExecute）；已在末位返回 false。</summary>
    public bool MoveDown(TemplateModelItem<TModel> item)
    {
        var index = _items.IndexOf(item);
        if (index < 0 || index >= _items.Count - 1) return false;
        _items.Move(index, index + 1);
        Reindex();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void Reindex()
    {
        for (var index = 0; index < _items.Count; index++)
            _items[index].Index = index;
    }
}
