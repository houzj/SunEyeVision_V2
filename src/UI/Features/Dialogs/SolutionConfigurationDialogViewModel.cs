using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.UI.Features.Dialogs;

public sealed class SolutionConfigurationDialogViewModel : ViewModelBase, IDisposable
{
    private readonly SolutionManager _manager;
    private readonly Func<Task<bool>>? _beforeTransition;
    private readonly Action? _synchronizeCurrent;
    private Window? _owner;
    private string _search = "";
    private SolutionMetadata? _selected;
    private bool _busy;
    public ObservableCollection<SolutionMetadata> SolutionMetadatas { get; } = new();
    public ObservableCollection<SolutionMetadata> FilteredMetadatas { get; } = new();
    public ObservableCollection<SolutionMetadata> RecentSolutions { get; } = new();
    public Solution? LaunchResult { get; private set; }
    public bool IsLaunched { get; private set; }
    public string? CurrentSolutionId => _manager.CurrentSolution?.Id;
    public string? SelectedMetadataId => SelectedMetadata?.Id;
    public int AllSolutionsCount => SolutionMetadatas.Count;
    public bool HasRecentSolutions => RecentSolutions.Count > 0;
    public bool HasSelectedSolution => SelectedMetadata != null;
    public bool HasFilteredSolutions => FilteredMetadatas.Count > 0;
    public string DataRoot => _manager.DataRoot;
    public string CurrentSolutionName => _manager.CurrentSolution?.Name ?? "未打开方案";
    public string SolutionStoragePath => SelectedMetadata?.FilePath ?? "未选择方案";
    public string SolutionInfo => SelectedMetadata == null ? "" : $"格式版本：{SelectedMetadata.Version}  工作流：{SelectedMetadata.WorkflowCount}";
    public string EmptyMessage => SolutionMetadatas.Count == 0 ? "还没有解决方案。请选择“新建方案”或“添加已有”。" : "没有匹配的方案，请清除搜索。";
    public string ToggleDefaultButtonText => SelectedMetadata?.IsDefault == true ? "取消启动默认" : "设为启动默认";
    public string StartupMode { get => _manager.Settings.StartupMode; set { _manager.Settings.StartupMode = value; _manager.MarkSettingsDirty(); OnPropertyChanged(); } }
    public string SearchText { get => _search; set { if (SetProperty(ref _search, value)) { OnPropertyChanged(nameof(HasSearchText)); Filter(); } } }
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); OnPropertyChanged(nameof(CanInteract)); CommandManager.InvalidateRequerySuggested(); } }
    public bool CanInteract => !IsBusy;
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsSelectedCurrent => SelectedMetadata != null && SelectedMetadata.Id == CurrentSolutionId;
    public string OperationStatus => "正在处理方案，请稍候…";
    public SolutionMetadata? SelectedMetadata
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            foreach (var property in new[] { nameof(SelectedMetadataId), nameof(HasSelectedSolution), nameof(SolutionStoragePath), nameof(SolutionInfo), nameof(ToggleDefaultButtonText), nameof(IsSelectedCurrent) }) OnPropertyChanged(property);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand NewSolutionCommand { get; }
    public ICommand OpenSolutionCommand { get; }
    public ICommand SaveSolutionAsCommand { get; }
    public ICommand DeleteSolutionCommand { get; }
    public ICommand RemoveSolutionCommand { get; }
    public ICommand CopySolutionCommand { get; }
    public ICommand EditSolutionCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand ToggleDefaultCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand ShowAllCommand { get; }
    public ICommand LocateCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ChangeDataDirectoryCommand { get; }
    public ICommand RepairAssociationCommand { get; }
    public ICommand RestorePreviousCommand { get; }
    public ICommand PropertiesCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ShowDataDirectoryCommand { get; }
    public ICommand CopyDataDirectoryCommand { get; }
    public ICommand OpenDataDirectoryCommand { get; }

    public SolutionConfigurationDialogViewModel(SolutionManager manager, string? preselectSolutionId = null,
        Func<Task<bool>>? beforeTransition = null, Action? synchronizeCurrent = null)
    {
        _manager = manager; _beforeTransition = beforeTransition; _synchronizeCurrent = synchronizeCurrent;
        NewSolutionCommand = new RelayCommand(New, () => !IsBusy);
        OpenSolutionCommand = new RelayCommand(AddExisting, () => !IsBusy);
        SaveSolutionAsCommand = new RelayCommand(async () => await RunAsync(SaveAs), () => CanSelect() && IsSelectedCurrent);
        DeleteSolutionCommand = new RelayCommand(async () => await RunAsync(() => Remove(true)), CanSelect);
        RemoveSolutionCommand = new RelayCommand(async () => await RunAsync(() => Remove(false)), CanSelect);
        CopySolutionCommand = new RelayCommand(Copy, () => CanSelect() && (!SelectedMetadata!.IsMissing || IsSelectedCurrent));
        EditSolutionCommand = new RelayCommand(_ => Edit(), _ => CanSelect() && !SelectedMetadata!.IsMissing);
        SelectCommand = new RelayCommand(value => SelectedMetadata = value as SolutionMetadata);
        ToggleDefaultCommand = new RelayCommand(ToggleDefault, CanSelect);
        LaunchCommand = new RelayCommand(async value => await RunAsync(() => Launch(value)), _ => CanSelect() && (!SelectedMetadata!.IsMissing || IsSelectedCurrent));
        ShowAllCommand = new RelayCommand(() => SearchText = "", () => !IsBusy);
        LocateCommand = new RelayCommand(Locate, () => CanSelect() && !IsSelectedCurrent);
        OpenFolderCommand = new RelayCommand(OpenFolder, CanSelect);
        ChangeDataDirectoryCommand = new RelayCommand(async () => await RunAsync(ChangeDataDirectory), () => !IsBusy);
        RepairAssociationCommand = new RelayCommand(RepairAssociation, () => !IsBusy);
        RestorePreviousCommand = new RelayCommand(async () => await RunAsync(RestorePrevious), () => CanSelect() && _manager.HasPreviousSave(SelectedMetadata!.Id));
        PropertiesCommand = new RelayCommand(ShowProperties, CanSelect);
        ClearSearchCommand = new RelayCommand(() => SearchText = "", () => !IsBusy && HasSearchText);
        ShowDataDirectoryCommand = new RelayCommand(ShowDataDirectory, () => !IsBusy);
        CopyDataDirectoryCommand = new RelayCommand(() => { try { Clipboard.SetText(DataRoot); } catch (Exception ex) { Error(ex); } }, () => !IsBusy);
        OpenDataDirectoryCommand = new RelayCommand(() => OpenDirectory(DataRoot), () => !IsBusy);
        _manager.SolutionAdded += Changed; _manager.SolutionRemoved += Changed; _manager.SolutionUpdated += Changed;
        _manager.SolutionRenamed += Changed; _manager.CurrentSolutionChanged += Changed;
        _manager.MetadataRefreshed += Refreshed;
        Reload();
        SelectedMetadata = SolutionMetadatas.FirstOrDefault(m => m.Id == preselectSolutionId) ?? SolutionMetadatas.FirstOrDefault(m => m.Id == CurrentSolutionId);
    }

    public void SetOwnerWindow(Window window) => _owner = window;
    public void SaveCurrentSolution() { } // Closing a catalogue is not a save operation.
    public void SaveSettings() => _manager.SaveUserSettings();
    private bool CanSelect() => !IsBusy && SelectedMetadata != null;
    private void Error(Exception exception) => ThemeMessageDialog.Show(exception.Message, "解决方案", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
    private async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await operation(); } catch (Exception ex) { Error(ex); } finally { IsBusy = false; }
    }
    private async Task<bool> BeforeTransition()
    {
        if (_beforeTransition != null) return await _beforeTransition();
        if (_manager.CurrentSolution != null && _manager.HasUnsavedChanges)
            throw new InvalidOperationException("当前方案有未保存修改，请从主工作区完成保存或切换。");
        return true;
    }

    private void New()
    {
        try
        {
            var dialog = new NewSolutionDialog(_manager.SolutionsDirectory) { Owner = _owner };
            if (dialog.ShowDialog() != true) return;
            var metadata = _manager.CreateNewSolution(new SolutionMetadata { Name = dialog.SolutionName, Description = dialog.Description ?? "", DirectoryPath = dialog.SolutionPath });
            SelectRegistered(metadata.Id);
        }
        catch (Exception ex) { Error(ex); }
    }
    private void AddExisting()
    {
        var dialog = new OpenFileDialog { Filter = "解决方案 (*.solution)|*.solution", InitialDirectory = _manager.SolutionsDirectory };
        if (dialog.ShowDialog(_owner) != true) return;
        try { SelectedMetadata = AddExistingFile(dialog.FileName); } catch (Exception ex) { Error(ex); }
    }
    public SolutionMetadata? AddExistingFile(string path)
    {
        var metadata = new SolutionRepository().LoadMetadata(path) ?? throw new InvalidDataException("方案格式不支持或文件无法读取。");
        var existing = _manager.GetMetadata(metadata.Id);
        if (existing != null && !AtomicJsonFile.SamePath(existing.FilePath, path))
        {
            var result = ThemeMessageDialog.Show("该方案 ID 已登记在其他位置。\n是：替换原位置记录（文件不删除）\n否：创建独立副本\n取消：不添加", "方案身份冲突", MessageBoxButton.YesNoCancel, ThemeMessageDialog.MessageIcon.Question);
            if (result == MessageBoxResult.Cancel || result == MessageBoxResult.None) return null;
            if (result == MessageBoxResult.No)
            {
                var target = ChooseCopyPath(metadata.Name);
                if (target == null) return null;
                metadata = _manager.CopySolution(path, Path.GetFileNameWithoutExtension(target), targetDirectory: Path.GetDirectoryName(target))
                    ?? throw new IOException("创建副本失败。");
            }
            else
            {
                if (_manager.CurrentSolution?.Id == existing.Id) throw new InvalidOperationException("当前方案正在打开，请先关闭后替换位置记录。");
                _manager.ReplaceKnownSolution(metadata);
            }
        }
        else _manager.RegisterKnownSolution(metadata);
        _manager.SaveUserSettings(); SelectRegistered(metadata.Id);
        return SelectedMetadata;
    }
    private string? ChooseCopyPath(string name)
    {
        var dialog = new SaveFileDialog { Filter = "解决方案 (*.solution)|*.solution", DefaultExt = ".solution", InitialDirectory = _manager.SolutionsDirectory, FileName = name + "_副本", OverwritePrompt = true };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }
    private void Copy()
    {
        try
        {
            if (SelectedMetadata == null) return;
            _synchronizeCurrent?.Invoke();
            var target = ChooseCopyPath(SelectedMetadata.Name);
            if (target == null) return;
            var result = _manager.CopySolution(SelectedMetadata.FilePath, Path.GetFileNameWithoutExtension(target), newDescription: SelectedMetadata.Description, targetDirectory: Path.GetDirectoryName(target))
                ?? throw new IOException("创建副本失败。");
            SelectRegistered(result.Id);
        }
        catch (Exception ex) { Error(ex); }
    }
    private async Task SaveAs()
    {
        if (_manager.CurrentSolution == null || !IsSelectedCurrent) return;
        _synchronizeCurrent?.Invoke();
        var target = ChooseCopyPath(_manager.CurrentSolution.Name);
        if (target == null) return;
        if (_beforeTransition != null && !await _beforeTransition()) return;
        var metadata = _manager.SaveAsSolution(target, Path.GetFileNameWithoutExtension(target), _manager.CurrentSolution.Description)
            ?? throw new IOException("另存为失败。");
        Reload(); SelectedMetadata = SolutionMetadatas.First(m => m.Id == metadata.Id);
    }
    private async Task Launch(object? value)
    {
        var selected = value as SolutionMetadata ?? SelectedMetadata;
        if (selected == null) return;
        var solution = _manager.CurrentSolution?.Id == selected.Id ? _manager.CurrentSolution : _manager.LoadSolutionOnly(selected.FilePath);
        if (solution == null) throw new IOException(_manager.LastError ?? "方案无法打开，请重新定位文件。");
        if (solution != _manager.CurrentSolution && !await BeforeTransition()) return;
        LaunchResult = solution; IsLaunched = true;
        SaveSettings();
        if (_owner != null) _owner.DialogResult = true;
    }
    private void Edit()
    {
        try
        {
            var selected = SelectedMetadata; if (selected == null) return;
            var dialog = new EditSolutionDialog(selected.Name, selected.Description) { Owner = _owner };
            if (dialog.ShowDialog() != true) return;
            if (dialog.SolutionName != selected.Name && !_manager.RenameSolutionFile(selected.Id, dialog.SolutionName)) throw new IOException("重命名失败，目标可能已存在。");
            var metadata = _manager.GetMetadata(selected.Id)!.Clone();
            metadata.Name = dialog.SolutionName; metadata.Description = dialog.Description ?? "";
            _manager.UpdateMetadata(metadata); Reload(); SelectedMetadata = SolutionMetadatas.First(m => m.Id == metadata.Id);
        }
        catch (Exception ex) { Error(ex); }
    }
    private async Task RestorePrevious()
    {
        var selected = SelectedMetadata; if (selected == null) return;
        if (ThemeMessageDialog.Show("恢复上一成功保存版本并打开？当前磁盘方案将被替换，引用的资源文件不会修改。\n" + selected.FilePath,
            "恢复方案", MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Warning, _owner) != MessageBoxResult.Yes) return;
        if (!await BeforeTransition()) return;
        LaunchResult = _manager.RestorePreviousSave(selected.Id); IsLaunched = true;
        SaveSettings();
        if (_owner != null) _owner.DialogResult = true;
    }
    private async Task Remove(bool deleteFile)
    {
        var selected = SelectedMetadata; if (selected == null) return;
        var text = deleteFile ? $"删除方案文件？\n{selected.FilePath}\n操作不会删除引用的图片或模型。" : $"仅移除管理记录？\n{selected.FilePath}\n磁盘文件将保留。";
        if (ThemeMessageDialog.Show(text, deleteFile ? "删除文件" : "移除记录", MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Question, _owner) != MessageBoxResult.Yes) return;
        if (_manager.CurrentSolution?.Id == selected.Id && !await BeforeTransition()) return;
        if (!_manager.DeleteSolution(selected.Id, deleteFile)) throw new IOException("操作失败，文件或配置无法修改。");
        Reload();
    }
    private void ToggleDefault()
    {
        try { _manager.SetDefaultSolution(SelectedMetadata?.IsDefault == true ? "" : SelectedMetadata!.Id); Reload(); }
        catch (Exception ex) { Error(ex); }
    }
    private void Locate()
    {
        try
        {
            var selected = SelectedMetadata; if (selected == null) return;
            if (_manager.CurrentSolution?.Id == selected.Id) throw new InvalidOperationException("请先关闭当前方案，再重新定位。");
            var dialog = new OpenFileDialog { Filter = "解决方案 (*.solution)|*.solution" };
            if (dialog.ShowDialog(_owner) != true) return;
            var metadata = new SolutionRepository().LoadMetadata(dialog.FileName) ?? throw new InvalidDataException("所选文件无法读取。");
            if (metadata.Id != selected.Id) throw new InvalidDataException("所选文件不是该方案，请使用“添加已有”登记。");
            metadata.LastAccessTime = selected.LastAccessTime;
            _manager.ReplaceKnownSolution(metadata); Reload(); SelectedMetadata = SolutionMetadatas.First(m => m.Id == metadata.Id);
        }
        catch (Exception ex) { Error(ex); }
    }
    private void OpenFolder()
    {
        try
        {
            var directory = Path.GetDirectoryName(SelectedMetadata!.FilePath)!;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("目录已失效。");
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception ex) { Error(ex); }
    }
    private async Task ChangeDataDirectory()
    {
        if (!await BeforeTransition()) return;
        var locations = new ApplicationDataLocationService();
        var dialog = new DataDirectoryDialog(locations, allowLegacy: false) { Owner = _owner };
        if (dialog.ShowDialog() != true) return;
        var target = dialog.SelectedPaths!;
        if (AtomicJsonFile.SamePath(target.Root, _manager.DataRoot)) return;
        if (ThemeMessageDialog.Show($"复制数据到：{target.Root}\n原数据保留。完成后退出软件，下次启动使用新目录。是否继续？", "迁移数据目录", MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Question) != MessageBoxResult.Yes) return;
        _manager.MarkSettingsDirty(); _manager.SaveUserSettings();
        locations.MigrateData(new ApplicationDataPaths(_manager.DataRoot), target);
        ThemeMessageDialog.Show("数据复制及校验完成，请重新启动软件。原数据未删除。", "数据目录", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
        Application.Current.Shutdown();
    }
    private void RepairAssociation()
    {
        try
        {
            new SunEyeVision.UI.Services.FileAssociationService().RegisterFileAssociation();
            ThemeMessageDialog.Show("文件类型和图标已注册。如果 Windows 仍使用其他默认程序，请在系统默认应用中选择 SunEyeVision。", "文件关联", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Reload()
    {
        var id = SelectedMetadata?.Id;
        SolutionMetadatas.Clear();
        foreach (var item in _manager.GetAllMetadata()) SolutionMetadatas.Add(item);
        SelectedMetadata = SolutionMetadatas.FirstOrDefault(m => m.Id == id);
        Filter();
        OnPropertyChanged(nameof(CurrentSolutionId)); OnPropertyChanged(nameof(DataRoot));
        OnPropertyChanged(nameof(CurrentSolutionName));
    }
    private void Filter()
    {
        var selectedId = SelectedMetadata?.Id;
        RecentSolutions.Clear();
        foreach (var item in SolutionMetadatas.Where(m => m.LastAccessTime > DateTime.MinValue).OrderByDescending(m => m.LastAccessTime).Take(10)) RecentSolutions.Add(item);
        FilteredMetadatas.Clear();
        foreach (var item in SolutionMetadatas.Where(m => string.IsNullOrWhiteSpace(SearchText) || (m.Name + " " + m.Description + " " + m.FilePath).Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)).OrderByDescending(m => m.ModifiedTime).ThenBy(m => m.Name))
            FilteredMetadatas.Add(item);
        SelectedMetadata = FilteredMetadatas.FirstOrDefault(m => m.Id == selectedId);
        foreach (var property in new[] { nameof(AllSolutionsCount), nameof(HasRecentSolutions), nameof(HasFilteredSolutions), nameof(EmptyMessage), nameof(ToggleDefaultButtonText), nameof(IsSelectedCurrent) }) OnPropertyChanged(property);
    }

    private void SelectRegistered(string id)
    {
        SearchText = ""; Reload();
        SelectedMetadata = FilteredMetadatas.FirstOrDefault(m => m.Id == id);
    }
    public bool SelectRecent(SolutionMetadata metadata)
    {
        if (IsBusy) return false;
        SearchText = "";
        SelectedMetadata = FilteredMetadatas.FirstOrDefault(m => m.Id == metadata.Id);
        return SelectedMetadata != null;
    }
    private static string TimeText(DateTime value) => value == DateTime.MinValue ? "未打开" : value.ToString("yyyy-MM-dd HH:mm");
    private void ShowProperties()
    {
        var selected = SelectedMetadata; if (selected == null) return;
        var fields = new System.Collections.Generic.KeyValuePair<string, string>[]
        {
            new("名称", selected.Name), new("文件路径", selected.FilePath),
            new("描述", string.IsNullOrWhiteSpace(selected.Description) ? "无" : selected.Description),
            new("修改时间", TimeText(selected.ModifiedTime)), new("最近打开时间", TimeText(selected.LastAccessTime)),
            new("工作流数量", selected.WorkflowCount.ToString()), new("文件格式版本", selected.Version),
            new("文件状态", selected.Availability), new("当前工作区", IsSelectedCurrent ? "已打开" : "未打开"),
            new("启动默认", selected.IsDefault ? "是" : "否")
        };
        new SolutionInformationDialog("方案属性", fields) { Owner = _owner }.ShowDialog();
    }
    private void ShowDataDirectory() =>
        new SolutionInformationDialog("数据目录", new System.Collections.Generic.KeyValuePair<string, string>[]
        { new("数据根目录", DataRoot), new("用户配置", _manager.ConfigurationPath), new("默认方案目录", _manager.SolutionsDirectory),
          new("说明", "方案可以从任意可读位置添加；更改数据目录会复制并校验数据，原数据保留。") }) { Owner = _owner }.ShowDialog();
    private void OpenDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("目录已失效。");
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Changed(object? sender, SolutionMetadataEventArgs e) => Reload();
    private void Refreshed(object? sender, EventArgs e) => Reload();
    public void Dispose()
    {
        _manager.SolutionAdded -= Changed; _manager.SolutionRemoved -= Changed; _manager.SolutionUpdated -= Changed;
        _manager.SolutionRenamed -= Changed; _manager.CurrentSolutionChanged -= Changed; _manager.MetadataRefreshed -= Refreshed;
        _owner = null;
    }
}
