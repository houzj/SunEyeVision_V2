using System.ComponentModel;

namespace SunEyeVision.Plugin.TemplateStudio;

/// <summary>
/// 一个可训练模板项（对应原厂 <c>FeatureMatchCommonControl.ModelItem</c>）。
///
/// 字段与原厂对齐：
/// <list type="bullet">
/// <item><c>Name</c> ↔ 原厂 ModelName（列表内可改名）</item>
/// <item><c>Selected</c> ↔ 原厂 IsSelected（列表选中）</item>
/// <item><c>Visible</c> ↔ 原厂 PatternVisible（眼睛开关）</item>
/// <item><c>Model</c> ↔ 原厂 ModelData（工具自有强类型模板对象）</item>
/// <item><c>Training</c> ↔ 原厂 modelData.ItemsData（每模板独立样本，按工具既有编码保存）</item>
/// </list>
/// 选中与可见是两个独立状态，不能合并成一个布尔。
///
/// 与原厂不同的部分（有意）：原厂把模板数据拆成 5 个 UserData 键
/// （<c>SolModelFile_{Index}_BasicData / _CurImage / _OtherMask / _OtherMask2 / _Points</c>），
/// 那是原厂框架键值存储的约定；本方按工具既有格式整包保存，因此不复制那套命名。
/// </summary>
public sealed class TemplateModelItem<TModel> : INotifyPropertyChanged
    where TModel : class
{
    private string _name;
    private bool _visible = true;
    private bool _selected;

    public TemplateModelItem(int index, string? name = null)
    {
        Index = index;
        _name = string.IsNullOrWhiteSpace(name) ? "模板" + (index + 1) : name;
    }

    /// <summary>模板在集合中的位置（0 基）。移动/删除后由集合重排。</summary>
    public int Index { get; internal set; }

    /// <summary>模板名（原厂 ModelName）。</summary>
    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_name, next, StringComparison.Ordinal)) return;
            _name = next;
            Raise(nameof(Name));
        }
    }

    /// <summary>列表选中态（原厂 IsSelected）。与 <see cref="Visible"/> 相互独立。</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Raise(nameof(Selected));
        }
    }

    /// <summary>眼睛开关（原厂 PatternVisible）。</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            Raise(nameof(Visible));
        }
    }

    /// <summary>模板本体（工具自有强类型对象，对应原厂 ModelData）。</summary>
    public TModel? Model { get; set; }

    /// <summary>模板样本（原厂 modelData.ItemsData），按工具既有格式保存。</summary>
    public string? Training { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
