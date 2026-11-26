using System.Windows.Input;

namespace SunEyeVision.Plugin.TemplateStudio.Views;

/// <summary>
/// 模板面板的宿主契约：工具只提供「集合 + 创建/载入/编辑/删除 + 持久化」，
/// 面板负责列表、选中、预览与行内操作。
///
/// 对应原厂 <c>FeatureMatchConfigPanal</c> 与各工具 <c>PanalData</c> 的分工：
/// 面板 XAML 只写 <c>Command="{Binding Decorate.AddModelCommand}"</c>。
///
/// **集合由宿主提供**：面板不新建实例，直接绑定宿主持有的
/// <see cref="TemplateModelCollection"/>，这样列表上的排序/勾选/改名就是宿主的真实状态，
/// 不需要在两侧做同步。原厂 <c>Panal.ItemsSource</c> 绑 <c>Decorate.Models</c> 即此意。
/// </summary>
public interface ITemplatePanelHost
{
    /// <summary>工具名（用于错误文案，如“轮廓匹配”）。</summary>
    string ToolLabel { get; }

    /// <summary>宿主持有的模板集合（与工具内部使用同一实例）。</summary>
    TemplateModelCollection Items { get; }

    /// <summary>创建模板（原厂 Decorate.AddModelCommand）。</summary>
    (bool Ok, string? Error) Create();

    /// <summary>载入模板工程（原厂 Decorate.LoadModuleCommand）。</summary>
    (bool Ok, string? Error) Load();

    /// <summary>编辑指定序号的模板（原厂 Decorate.EditModel）。</summary>
    (bool Ok, string? Error) Edit(int index);

    /// <summary>删除全部模板（原厂 Decorate.DeleteAllModels）。</summary>
    (bool Ok, string? Error) DeleteAll();

    /// <summary>集合已变更（顺序/勾选/改名），请求工具写回设置。</summary>
    void Persist();
}

/// <summary>
/// 模板面板的数据模型：选中、预览、错误与命令。
/// 列表数据来自宿主的集合，面板不持有独立副本。
/// </summary>
public sealed class TemplateConfigPanelModel : ObservableObject
{
    private readonly ITemplatePanelHost _host;
    private int _selectedIndex = -1;
    private object? _preview;
    private string? _error;

    public TemplateConfigPanelModel(ITemplatePanelHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _host.Items.Changed += (_, _) => Refresh();
        CreateCommand = new RelayCommand(_ => Execute(Create));
        LoadCommand = new RelayCommand(_ => Execute(Load));
        EditCommand = new RelayCommand(_ => Execute(Edit), _ => _selectedIndex >= 0);
        DeleteAllCommand = new RelayCommand(_ => Execute(DeleteAll), _ => Models.Count > 0);
        MoveUpCommand = new RelayCommand(item => Move(item, up: true));
        MoveDownCommand = new RelayCommand(item => Move(item, up: false));
        DeleteCommand = new RelayCommand(item => Delete(item));
        SelectedCommand = new RelayCommand(item => Persist(), _ => Models.Count > 0);
    }

    /// <summary>列表行：直接来自宿主集合，排序/勾选即宿主状态。</summary>
    public IReadOnlyList<TemplateModelItem> Models => _host.Items.Items;

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

    /// <summary>预览图（原厂 98×98 imageView）。由工具在选中变化时提供。</summary>
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

    /// <summary>宿主的集合发生变化（如工具侧创建成功）后刷新视图状态。</summary>
    public void Refresh()
    {
        if (_selectedIndex >= Models.Count) _selectedIndex = Models.Count - 1;
        Raise(nameof(Models));
        Raise(nameof(CanEdit));
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Move(object? parameter, bool up)
    {
        if (parameter is not TemplateModelItem item) return;
        var moved = up ? _host.Items.MoveUp(item) : _host.Items.MoveDown(item);
        if (moved) Persist();
    }

    private void Delete(object? parameter)
    {
        if (parameter is not TemplateModelItem item) return;
        if (_host.Items.Remove(item)) Persist();
    }

    private void Persist()
    {
        _host.Persist();
        Refresh();
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

    private (bool Ok, string? Error) DeleteAll() => _host.DeleteAll();
}
