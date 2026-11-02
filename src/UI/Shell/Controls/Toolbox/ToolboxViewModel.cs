using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Shell.Controls.Toolbox;

/// <summary>统一展示目录与当前准入状态；条目存在不意味着允许创建工具。</summary>
public sealed class ToolboxViewModel : ViewModelBase
{
    private readonly ToolDirectoryCatalog? _directory;
    private readonly VmToolboxCatalog _catalog;
    private readonly Func<string, ToolMetadata?> _resolve;
    private readonly Func<string, bool> _hasRuntime;
    private readonly Func<bool> _directoryCurrent;
    private bool _compact = true, _popup;
    private string? _selected;
    private string _status = string.Empty;
    public string StatusMessage { get => _status; private set => SetProperty(ref _status, value); }
    public ObservableCollection<ToolCategory> Categories { get; } = new();
    public ObservableCollection<ToolItem> AllTools => new(Categories.SelectMany(c => BuildCategory(c.Name, true)));
    public ObservableCollection<ToolItem> SelectedCategoryTools { get; } = new();
    public bool IsCompactMode
    {
        get => _compact;
        set { if (SetProperty(ref _compact, value)) { ClosePopup(); OnPropertyChanged(nameof(PreferredWidth)); OnPropertyChanged(nameof(DisplayModeIcon)); OnPropertyChanged(nameof(DisplayModeTooltip)); } }
    }
    public double PreferredWidth => IsCompactMode ? 52 : 230;
    public string DisplayModeIcon => IsCompactMode ? "»" : "«";
    public string DisplayModeTooltip => IsCompactMode ? "展开工具箱" : "收起工具箱";
    public bool IsCompactModePopupOpen { get => _popup; set => SetProperty(ref _popup, value); }
    public string? SelectedCategory
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value) && value == null) return;
            SelectedCategoryTools.Clear();
            var category = Categories.FirstOrDefault(c => c.Name == value);
            if (category != null) LoadCategory(category);
            foreach (var c in Categories) c.IsSelected = c.Name == value;
            IsCompactModePopupOpen = IsCompactMode && category != null;
        }
    }
    public ICommand ToggleDisplayModeCommand { get; }
    public ToolboxViewModel(DesktopToolAdmission admission) : this(admission.Directory,
        id=>admission.Directory.Find(id)?.ToMetadata(),admission.Runtime.Contains,()=>admission.Runtime.IsActive) { }
    internal ToolboxViewModel(ToolDirectoryCatalog? directory, Func<string, ToolMetadata?> resolve, Func<string, bool> hasRuntime, Func<bool> directoryCurrent)
    {
        _directory = directory;
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _hasRuntime = hasRuntime ?? throw new ArgumentNullException(nameof(hasRuntime));
        _directoryCurrent = directoryCurrent ?? throw new ArgumentNullException(nameof(directoryCurrent));
        _catalog = VmToolboxCatalog.Load();
        ToggleDisplayModeCommand = new RelayCommand(() => IsCompactMode = !IsCompactMode);
        foreach (var g in _catalog.Groups)
        {
            var c = new ToolCategory(g.Name, g.Icon, g.Name, 0, false) { HoverIcon = g.HoverIcon };
            c.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ToolCategory.IsExpanded) && c.IsExpanded) LoadCategory(c); };
            Categories.Add(c);
            c.ToolCount = BuildCategory(c.Name, false).Count();
        }
    }
    public IReadOnlyList<ToolItem> GetCategoryTools(string name)
    {
        var category = Categories.FirstOrDefault(c => c.Name == name);
        if (category == null) return Array.Empty<ToolItem>();
        LoadCategory(category);
        return category.Tools?.ToArray() ?? Array.Empty<ToolItem>();
    }
    private void LoadCategory(ToolCategory category)
    {
        try { category.Tools = new ObservableCollection<ToolItem>(BuildCategory(category.Name, true)); StatusMessage = string.Empty; }
        catch (Exception ex) { category.Tools = new(); StatusMessage = "分类读取失败：" + ex.Message; }
        if (SelectedCategory == category.Name)
        {
            SelectedCategoryTools.Clear();
            foreach (var item in category.Tools) SelectedCategoryTools.Add(item);
        }
    }
    private IEnumerable<ToolItem> BuildCategory(string name, bool load)
    {
        var verified = _catalog.Entries.Where(e => e.MappingVerified && e.ToolId != null).Select(e => e.ToolId!).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in _catalog.Entries.Where(e => e.Category == name))
        {
            var id = entry.ToolId ?? "vm:" + entry.Id;
            var runtime = entry.MappingVerified && entry.ToolId != null ? _directory?.Find(entry.ToolId) : null;
            var error = !entry.MappingVerified ? (entry.ToolId == null ? "尚未实现" : "工具映射尚未核实") :
                _directory == null ? "运行目录不可用" : runtime == null ? "工具未部署或未准入" :
                runtime.Category != entry.Category ? "部署分类与已核实映射不一致" : null;
            if (error == null && load) error = CheckRuntime(runtime!);
            yield return new ToolItem(entry.Name, entry.Category, "generic", entry.Name, id)
            { CatalogIcon = entry.Icon, IsAvailable = load && error == null, LoadError = error ?? (load ? null : "等待加载"),
              AvailabilityProbe = error == null && runtime != null ? () => CheckRuntime(runtime) == null : null };
        }
        if (_directory == null) yield break;
        foreach (var runtime in _directory.GetCategory(name).Where(t => !verified.Contains(t.Id)))
        {
            var error = load ? CheckRuntime(runtime) : "等待加载";
            yield return new ToolItem(runtime.DisplayName + "（已部署，未匹配目录）", runtime.Category, runtime.Icon, runtime.Description, runtime.Id)
            { IsAvailable = load && error == null, LoadError = error,
              AvailabilityProbe = error == null ? () => CheckRuntime(runtime) == null : null };
        }
    }
    private string? CheckRuntime(DirectoryTool runtime)
    {
        try
        {
            if (!_directoryCurrent() || !ReferenceEquals(_directory?.Find(runtime.Id), runtime)) return "运行目录已变化，请重新打开工具箱";
            var metadata = _resolve(runtime.Id);
            if (!_hasRuntime(runtime.Id)) return "工具运行组件未准入";
            return metadata == null || metadata.Id != runtime.Id || metadata.Category != runtime.Category ? "工具未成功注册" : null;
        }
        catch (Exception error) { return "工具加载失败：" + error.Message; }
    }
    public void ClosePopup() { IsCompactModePopupOpen = false; SelectedCategory = null; }
}
