using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.TemplateStudio.Views;

/// <summary>
/// 模板面板的宿主契约：工具只提供「创建/载入/编辑/删除」这四件事的业务实现，
/// 列表、预览、选中与行内操作由面板负责。
///
/// 对应原厂 <c>FeatureMatchConfigPanal</c> 与各工具 <c>PanalData</c> 的分工：
/// 面板 XAML 只写 <c>Command="{Binding Decorate.AddModelCommand}"</c>。
/// </summary>
public interface ITemplatePanelHost
{
    /// <summary>工具名（用于错误文案，如“轮廓匹配”）。</summary>
    string ToolLabel { get; }

    /// <summary>创建模板（原厂 Decorate.AddModelCommand）。</summary>
    (bool Ok, string? Error) Create();

    /// <summary>载入模板工程（原厂 Decorate.LoadModuleCommand）。</summary>
    (bool Ok, string? Error) Load();

    /// <summary>编辑选中模板（原厂 Decorate.EditModel）。</summary>
    (bool Ok, string? Error) Edit(int index);

    /// <summary>删除全部模板（原厂 Decorate.DeleteAllModels）。</summary>
    (bool Ok, string? Error) DeleteAll();

    /// <summary>
    /// 模板集合发生变化（增删/排序/改名/勾选）后，把新顺序与元数据落盘。
    /// 原厂由 Decorate 直接操作模块，本方由宿主写回 ModelData。
    /// </summary>
    void Persist(IReadOnlyList<TemplateModelItem<object>> items);
}

/// <summary>
/// 模板面板的数据模型：集合、选中、预览、行内操作与四个主命令。
///
/// 行内操作沿用原厂 <c>FeaturepatMatchConfigPanalDataDecorate</c> 的四件套
/// （<c>UpCommand</c> / <c>DownCommand</c> / <c>DelCommand</c> / <c>SelectedCommand</c>，
/// 见 <c>featurematchconfigpanal.xaml:147/153/159/165</c>）。
/// </summary>
public sealed class TemplateConfigPanelModel : ObservableObject
{
    private readonly ITemplatePanelHost _host;
    private readonly TemplateModelCollection<object> _collection = new();
    private int _selectedIndex = -1;
    private object? _preview;
    private string? _error;

    public TemplateConfigPanelModel(ITemplatePanelHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _collection.Changed += (_, _) => Refresh();
        CreateCommand = new RelayCommand(_ => Execute(Create));
        LoadCommand = new RelayCommand(_ => Execute(Load));
        EditCommand = new RelayCommand(_ => Execute(Edit), _ => _selectedIndex >= 0);
        DeleteAllCommand = new RelayCommand(_ => Execute(DeleteAll), _ => _collection.Count > 0);
        MoveUpCommand = new RelayCommand(item => Move(item, up: true));
        MoveDownCommand = new RelayCommand(item => Move(item, up: false));
        DeleteCommand = new RelayCommand(item => Delete(item));
        SelectedCommand = new RelayCommand(item => Persist(), _ => _collection.Count > 0);
    }

    public IReadOnlyList<TemplateModelItem<object>> Models => _collection.Items;

    public ICommand CreateCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteAllCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SelectedCommand { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            Raise();
            Raise(nameof(CanEdit));
        }
    }

    public bool CanEdit => _selectedIndex >= 0;

    /// <summary>预览图（原厂 98×98 imageView）。由宿主在选中变化时提供。</summary>
    public object? Preview
    {
        get => _preview;
        set { _preview = value; Raise(); }
    }

    /// <summary>最近一次操作的错误文案。</summary>
    public string? Error
    {
        get => _error;
        private set { _error = value; Raise(); }
    }

    /// <summary>用外部数据重建集合（加载/刷新时调用）。</summary>
    public void Reset(IEnumerable<TemplateModelItem<object>> items)
    {
        _collection.Clear();
        foreach (var item in items) _collection.Add(item.Name)!.Model = item.Model;
        Refresh();
    }

    /// <summary>追加一项（创建成功后调用）。已到上限返回 null。</summary>
    public TemplateModelItem<object>? Append(string? name = null) => _collection.Add(name);

    private void Move(object? parameter, bool up)
    {
        if (parameter is not TemplateModelItem<object> item) return;
        var moved = up ? _collection.MoveUp(item) : _collection.MoveDown(item);
        if (moved) Persist();
    }

    private void Delete(object? parameter)
    {
        if (parameter is not TemplateModelItem<object> item) return;
        if (_collection.Remove(item)) Persist();
    }

    private void Persist()
    {
        _host.Persist(_collection.Items);
        Refresh();
    }

    private void Refresh()
    {
        Raise(nameof(Models));
        Raise(nameof(CanEdit));
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Execute(Func<(bool Ok, string? Error)> action)
    {
        var (ok, error) = action();
        Error = ok ? null : error;
        Refresh();
    }

    private (bool Ok, string? Error) Create() => _host.Create();

    private (bool Ok, string? Error) Load() => _host.Load();

    private (bool Ok, string? Error) Edit()
        => _selectedIndex >= 0 ? _host.Edit(_selectedIndex) : (false, "请先选择模板。");

    private (bool Ok, string? Error) DeleteAll()
    {
        _collection.Clear();
        var result = _host.DeleteAll();
        Refresh();
        return result;
    }
}
