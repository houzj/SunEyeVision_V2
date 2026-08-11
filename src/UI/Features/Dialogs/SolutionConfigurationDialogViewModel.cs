using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Controls.Helpers;
using SunEyeVision.UI.Views.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 解决方案配置对话框 ViewModel
    /// </summary>
    /// <remarks>
    /// 已从 Project 迁移到 Solution 架构
    /// - Project → Solution
    /// - ProjectManager → SolutionManager
    /// </remarks>
    public class SolutionConfigurationDialogViewModel : ViewModelBase
    {
        private readonly SolutionManager _solutionManager;
        private readonly string? _preselectSolutionId;

        // 状态1：元数据列表（轻量级，仅用于显示和配置）
        public ObservableCollection<SolutionMetadata> SolutionMetadatas { get; } = new();

        // 状态2：选中的元数据（轻量级）
        private SolutionMetadata? _selectedMetadata;
        public SolutionMetadata? SelectedMetadata
        {
            get => _selectedMetadata;
            set
            {
                if (SetProperty(ref _selectedMetadata, value))
                {
                    // 选中时只标记状态，不加载完整解决方案
                    OnPropertyChanged(nameof(SelectedMetadataId));
                    OnPropertyChanged(nameof(HasSelectedSolution));
                    OnPropertyChanged(nameof(SolutionStoragePath));
                    OnPropertyChanged(nameof(SolutionInfo));
                    
                    // ✅ 通知按钮文本和提示更新
                    // 这些属性依赖 SelectedMetadata，必须在 SelectedMetadata 变化时通知
                    OnPropertyChanged(nameof(ToggleDefaultButtonText));
                    OnPropertyChanged(nameof(ToggleDefaultButtonToolTip));
                }
            }
        }

        // 辅助属性：选中的元数据ID（用于XAML数据触发器）
        public string? SelectedMetadataId => SelectedMetadata?.Id;

        // 辅助属性：当前打开的解决方案ID（用于XAML数据触发器）
        /// <summary>
        /// 当前打开的解决方案ID（用于XAML数据触发器）
        /// </summary>
        /// <remarks>
        /// 语义说明：
        /// - 只返回实际加载的解决方案ID（CurrentSolution）
        /// - 不使用 Settings.CurrentSolutionId 的回退值
        /// - 只有用户明确启动解决方案后，才显示"打开"状态
        /// </remarks>
        public string? CurrentSolutionId => _solutionManager.CurrentSolution?.Id;

        // 状态3：完整的 Solution 对象（懒加载，仅在启动时加载）
        private Solution? _selectedSolution;
        public Solution? SelectedSolution
        {
            get => _selectedSolution;
            private set
            {
                if (SetProperty(ref _selectedSolution, value))
                {
                    OnPropertyChanged(nameof(SelectedSolutionId));
                    OnPropertyChanged(nameof(CurrentSolutionId));
                }
            }
        }

        // 辅助属性：选中的Solution ID（用于XAML数据触发器）
        public string? SelectedSolutionId => SelectedSolution?.Id;

        private bool _skipStartupConfig;
        private bool _isLaunched;
        private Solution? _launchResult;
        private Window? _ownerWindow;

        public SolutionConfigurationDialogViewModel(SolutionManager solutionManager, string? preselectSolutionId = null)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _preselectSolutionId = preselectSolutionId;

            InitializeCommands();
            LoadData();

            // ✅ 监听细粒度事件，实现增量更新
            _solutionManager.SolutionAdded += OnSolutionAdded;
            _solutionManager.SolutionRemoved += OnSolutionRemoved;
            _solutionManager.SolutionRenamed += OnSolutionRenamed;
            _solutionManager.SolutionUpdated += OnSolutionUpdated;
            _solutionManager.MetadataRefreshed += OnMetadataRefreshed;
            _solutionManager.CurrentSolutionChanged += OnCurrentSolutionChanged;
        }

        /// <summary>
        /// 设置所有者窗口
        /// </summary>
        public void SetOwnerWindow(Window window)
        {
            _ownerWindow = window;
        }

        /// <summary>
        /// 启动结果
        /// </summary>
        public Solution? LaunchResult => _launchResult;

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsLaunched => _isLaunched;

        /// <summary>
        /// 保存当前解决方案
        /// </summary>
        public void SaveCurrentSolution()
        {
            if (SelectedSolution != null)
            {
                try
                {
                    _solutionManager.SaveSolution(SelectedSolution.FilePath);
                }
                catch (Exception ex)
                {
                    LogError($"保存解决方案失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // ✅ 调用 SolutionManager 的公开方法
                _solutionManager.SaveUserSettings();
                LogInfo("设置已保存到文件");
            }
            catch (Exception ex)
            {
                LogError($"保存设置失败: {ex.Message}");
            }
        }

        #region 解决方案相关

        /// <summary>
        /// 解决方案元数据列表（轻量级，仅用于显示和配置）
        /// </summary>
        // public ObservableCollection<SolutionMetadata> SolutionMetadatas { get; } = new();  // 已在构造函数前定义

        /// <summary>
        /// 选中的完整解决方案（懒加载）
        /// </summary>
        // public Solution? SelectedSolution { get; private set; }  // 已在构造函数前定义

        /// <summary>
        /// 切换默认按钮文本
        /// </summary>
        public string ToggleDefaultButtonText
        {
            get
            {
                if (SelectedMetadata == null)
                    return "设为默认";
                return SelectedMetadata.IsDefault ? "取消默认" : "设为默认";
            }
        }

        /// <summary>
        /// 切换默认按钮提示
        /// </summary>
        public string ToggleDefaultButtonToolTip
        {
            get
            {
                if (SelectedMetadata == null)
                    return "设置默认解决方案";
                return SelectedMetadata.IsDefault ? "取消默认解决方案" : "设置默认解决方案";
            }
        }

        /// <summary>
        /// 是否选中了解决方案
        /// </summary>
        public bool HasSelectedSolution => SelectedMetadata != null;

        /// <summary>
        /// 解决方案存储路径
        /// </summary>
        public string SolutionStoragePath => SelectedMetadata?.FilePath ?? "未选择解决方案";

        /// <summary>
        /// 解决方案信息
        /// </summary>
        public string SolutionInfo
        {
            get
            {
                if (SelectedMetadata == null) return string.Empty;
                return $"版本: {SelectedMetadata.Version}\n" +
                       $"工作流数: {SelectedMetadata.WorkflowCount}";
            }
        }

        /// <summary>
        /// 新建解决方案命令
        /// </summary>
        public ICommand NewSolutionCommand { get; private set; } = null!;

        /// <summary>
        /// 打开解决方案命令
        /// </summary>
        public ICommand OpenSolutionCommand { get; private set; } = null!;

        /// <summary>
        /// 另存为解决方案命令
        /// </summary>
        public ICommand SaveSolutionAsCommand { get; private set; } = null!;

        /// <summary>
        /// 删除解决方案命令
        /// </summary>
        public ICommand DeleteSolutionCommand { get; private set; } = null!;

        /// <summary>
        /// 复制解决方案命令
        /// </summary>
        public ICommand CopySolutionCommand { get; private set; } = null!;

        /// <summary>
        /// 编辑解决方案命令（名称+描述）
        /// </summary>
        public ICommand EditSolutionCommand { get; private set; } = null!;

        /// <summary>
        /// 选中解决方案命令
        /// </summary>
        public ICommand SelectCommand { get; private set; } = null!;

        /// <summary>
        /// 切换默认解决方案命令（设为默认/取消默认）
        /// </summary>
        public ICommand ToggleDefaultCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            NewSolutionCommand = new RelayCommand(NewSolution, () => true);
            OpenSolutionCommand = new RelayCommand(OpenSolution, () => true);
            CopySolutionCommand = new RelayCommand(CopySolution, () => CanCopySolution());

            // ✅ 使用公共版本：有参方法 + 无参 canExecute（使用 _ 忽略参数）
            EditSolutionCommand = new RelayCommand(EditSolution, _ => CanEditSolution());

            SaveSolutionAsCommand = new RelayCommand(SaveSolutionAs, () => CanSaveSolutionAs());
            DeleteSolutionCommand = new RelayCommand(DeleteSolution, () => CanDeleteSolution());

            // ✅ 使用公共版本：有参方法 + 无参 canExecute
            SelectCommand = new RelayCommand(SelectSolution, _ => true);

            // ✅ 新增：切换默认解决方案命令
            ToggleDefaultCommand = new RelayCommand(ExecuteToggleDefault, CanToggleDefault);

            SkipCommand = new RelayCommand(Skip, () => true);

            // ✅ 使用公共版本：有参方法 + 无参 canExecute
            LaunchCommand = new RelayCommand(Launch, _ => true);
        }

        /// <summary>
        /// 选中解决方案（单击卡片，只标记选中状态，不加载完整解决方案）
        /// </summary>
        private void SelectSolution(object? parameter)
        {
            if (parameter is SolutionMetadata metadata)
            {
                SelectedMetadata = metadata;
                LogInfo($"选中解决方案: {metadata.Name}");
            }
            else if (parameter != null)
            {
                LogWarning($"SelectCommand收到未知类型参数: {parameter.GetType().Name}");
            }
        }

        private void NewSolution()
        {
            LogInfo("开始创建新解决方案");

            // ✅ 统一使用解决方案管理器的默认目录
            var defaultPath = _solutionManager.SolutionsDirectory;

            try
            {
                var dialog = new NewSolutionDialog(defaultPath);
                var result = dialog.ShowDialog();

                if (result == true && !string.IsNullOrEmpty(dialog.SolutionName))
                {
                    LogInfo($"用户输入: 名称={dialog.SolutionName}, 路径={dialog.SolutionPath}, 描述={dialog.Description}");

                    try
                    {
                        // ✅ 使用新的 CreateNewSolution 方法，接受元数据
                        var metadata = new SolutionMetadata
                        {
                            Name = dialog.SolutionName,
                            Description = dialog.Description ?? "",
                            DirectoryPath = dialog.SolutionPath
                        };

                        var newMetadata = _solutionManager.CreateNewSolution(metadata);

                        if (newMetadata == null)
                        {
                            LogError("SolutionManager.CreateNewSolution 返回了 null");
                            MessageBox.Show("创建解决方案失败: 返回了 null", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        // ✅ 移除手动添加，由 SolutionAdded 事件统一处理
                        // SolutionMetadatas.Add(newMetadata);
                        // LogInfo($"添加到列表，当前数量: {SolutionMetadatas.Count}");

                        // 设置选中状态
                        SelectedMetadata = newMetadata;
                        LogInfo($"已设置选中解决方案");

                        LogSuccess($"创建解决方案成功: {newMetadata.Name}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"创建解决方案失败: {ex.Message}");
                        MessageBox.Show($"创建解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (result == true)
                {
                    LogWarning("用户点击了创建但未输入解决方案名称");
                }
                else
                {
                    LogInfo("用户取消了创建解决方案");
                }
            }
            catch (Exception ex)
            {
                LogError($"打开新建对话框失败: {ex.Message}");
                MessageBox.Show($"打开新建对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSolution()
        {
            // ✅ 使用 OpenFileDialog 直接选择 .solution 文件
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择解决方案文件",
                Filter = "解决方案文件 (*.solution)|*.solution|所有文件 (*.*)|*.*",
                FilterIndex = 0,
                InitialDirectory = _solutionManager.SolutionsDirectory
            };

            var result = openFileDialog.ShowDialog();
            if (result != true) return;

            var filePath = openFileDialog.FileName;

            if (string.IsNullOrEmpty(filePath))
            {
                LogWarning("未选择文件");
                return;
            }

            try
            {
                // ✅ 仅加载解决方案对象（不设置为当前解决方案）
                var solution = _solutionManager.LoadSolutionOnly(filePath);
                if (solution == null)
                {
                    MessageBox.Show("加载解决方案文件失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ✅ 尝试从已知解决方案中获取元数据，否则创建新的
                var metadata = _solutionManager.Settings.KnownSolutions.Values
                    .FirstOrDefault(m => m.FilePath == filePath);

                if (metadata == null)
                {
                    // 创建新的元数据（使用文件名作为名称）
                    metadata = new SolutionMetadata
                    {
                        Id = solution.Id,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Description = "",
                        Version = solution.Version,
                        FilePath = filePath,
                        DirectoryPath = Path.GetDirectoryName(filePath) ?? "",
                        CreatedTime = File.GetCreationTime(filePath),
                        ModifiedTime = File.GetLastWriteTime(filePath)
                    };
                }
                metadata.UpdateStatistics(solution);

                // 检查是否已存在
                if (!SolutionMetadatas.Any(s => s.Id == solution.Id))
                {
                    SolutionMetadatas.Add(metadata);
                }

                // ✅ 注册已知解决方案（持久化到Settings）
                _solutionManager.RegisterKnownSolution(metadata);

                // 设置选中状态
                SelectedMetadata = metadata;
                LogSuccess($"打开解决方案: {metadata.Name}");
            }
            catch (Exception ex)
            {
                LogError($"打开解决方案失败: {ex.Message}");
                MessageBox.Show($"打开解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSaveSolutionAs()
        {
            return SelectedMetadata != null;
        }

        private void SaveSolutionAs()
        {
            if (SelectedMetadata == null) return;

            var defaultName = $"{SelectedMetadata.Name}_copy";
            var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var defaultDescription = SelectedMetadata.Description;

            try
            {
                // 显示另存为对话框
                var dialog = new NewSolutionDialog(defaultPath)
                {
                    SolutionName = defaultName,
                    Description = defaultDescription
                };
                var result = dialog.ShowDialog();

                if (result == true && !string.IsNullOrEmpty(dialog.SolutionName))
                {
                    var targetFilePath = Path.Combine(dialog.SolutionPath, $"{dialog.SolutionName}.solution");

                    // 智能路由：根据是否另存为当前解决方案选择不同的实现
                    bool isCurrentSolution = SelectedMetadata.Id == CurrentSolutionId;

                    if (isCurrentSolution)
                    {
                        // 另存为打开的解决方案
                        SaveAsCurrentSolution(dialog.SolutionName, dialog.Description, targetFilePath);
                    }
                    else
                    {
                        // 另存为未打开的解决方案
                        SaveAsUnopenedSolution(SelectedMetadata.FilePath, dialog.SolutionName, dialog.Description, targetFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"另存为失败: {ex.Message}");
                MessageBox.Show($"另存为失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 另存为当前打开的解决方案
        /// </summary>
        private void SaveAsCurrentSolution(string newName, string newDescription, string targetFilePath)
        {
            LogInfo($"另存为当前打开的解决方案: {SelectedMetadata?.Name} -> {newName}");

            // 调用 SolutionManager 的 SaveAsSolution（带参数版本）
            var newMetadata = _solutionManager.SaveAsSolution(targetFilePath, newName, newDescription);

            if (newMetadata != null)
            {
                // ✅ 移除手动添加，由 SolutionAdded 事件统一处理
                // SolutionMetadatas.Add(newMetadata);
                SelectedMetadata = newMetadata;
                LogSuccess($"另存为当前解决方案成功: {newName}");
            }
            else
            {
                MessageBox.Show("另存为失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 另存为未打开的解决方案
        /// </summary>
        private void SaveAsUnopenedSolution(string sourcePath, string newName, string newDescription, string targetFilePath)
        {
            LogInfo($"另存为未打开的解决方案: {Path.GetFileNameWithoutExtension(sourcePath)} -> {newName}");

            // 调用 SolutionManager 的 CopySolution（带参数版本）
            var newMetadata = _solutionManager.CopySolution(sourcePath, newName, newDescription, Path.GetDirectoryName(targetFilePath));

            if (newMetadata != null)
            {
                // ✅ 移除手动添加，由 SolutionAdded 事件统一处理
                // SolutionMetadatas.Add(newMetadata);
                SelectedMetadata = newMetadata;
                LogSuccess($"另存为未打开的解决方案成功: {newName}");
            }
            else
            {
                MessageBox.Show("另存为失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanDeleteSolution()
        {
            return SelectedMetadata != null;
        }

        private void DeleteSolution()
        {
            if (SelectedMetadata == null) return;

            // 检查是否是当前打开的解决方案
            bool isCurrentSolution = SelectedMetadata.Id == CurrentSolutionId;

            // ✅ 根据是否是当前解决方案显示不同的提示信息
            string message;
            if (isCurrentSolution)
            {
                message = $"解决方案「{SelectedMetadata.Name}」当前正在使用中。\n\n" +
                         $"删除此解决方案将会：\n" +
                         $"• 自动关闭当前解决方案\n" +
                         $"• 仅删除元数据，保留解决方案文件\n" +
                         $"• 当前将处于无打开解决方案的状态\n\n" +
                         $"确定要删除吗？";
            }
            else
            {
                message = $"确定要删除解决方案「{SelectedMetadata.Name}」吗？\n\n" +
                         $"此操作将会：\n" +
                         $"• 仅删除元数据，保留解决方案文件\n" +
                         $"• 解决方案文件仍可手动打开\n\n" +
                         $"此操作不可恢复！";
            }

            var result = MessageBox.Show(
                message,
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var solutionToDelete = SelectedMetadata;

                    // 从列表中移除（在删除前先移除，避免事件触发时找不到）
                    SolutionMetadatas.Remove(solutionToDelete);

                    // 从SolutionManager中删除（会自动关闭当前解决方案，如果需要）
                    _solutionManager.DeleteSolution(solutionToDelete.Id);

                    // 清除选中状态
                    SelectedMetadata = null;

                    // ✅ 根据删除场景记录不同的日志
                    if (isCurrentSolution)
                    {
                        LogSuccess($"删除当前打开的解决方案: {solutionToDelete.Name} (已自动关闭)");
                    }
                    else
                    {
                        LogSuccess($"删除未打开的解决方案: {solutionToDelete.Name}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"删除解决方案失败: {ex.Message}");
                    MessageBox.Show($"删除解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool CanCopySolution()
        {
            return SelectedMetadata != null;
        }

        /// <summary>
        /// 复制解决方案（文件级操作 + JSON元数据更新）
        /// </summary>
        private void CopySolution()
        {
            if (SelectedMetadata == null) return;

            try
            {
                var baseName = SelectedMetadata.Name;
                var copyName = GenerateUniqueCopyName(baseName);
                var targetDirectory = SelectedMetadata.DirectoryPath;

                LogInfo($"开始复制解决方案: {SelectedMetadata.Name} -> {copyName}");
                LogInfo($"目标目录: {targetDirectory}");

                var newMetadata = _solutionManager.CopySolution(
                    SelectedMetadata.FilePath,
                    copyName,
                    targetDirectory);

                if (newMetadata != null)
                {
                    SelectedMetadata = newMetadata;
                    LogSuccess($"复制解决方案成功: {copyName} (Id={newMetadata.Id})");
                }
                else
                {
                    LogError("复制解决方案失败: SolutionManager.CopySolution 返回了 null");
                    MessageBox.Show("复制解决方案失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogError($"复制解决方案失败: {ex.Message}");
                MessageBox.Show($"复制解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanEditSolution()
        {
            return SelectedMetadata != null;
        }

        private void EditSolution(object? parameter)
        {
            // 优先使用命令参数，其次使用 SelectedMetadata
            var metadata = parameter as SolutionMetadata ?? SelectedMetadata;

            if (metadata == null) return;

            try
            {
                var dialog = new EditSolutionDialog(metadata.Name, metadata.Description)
                {
                    Owner = _ownerWindow
                };

                var result = dialog.ShowDialog();

                if (result != true)
                {
                    LogInfo("用户取消编辑解决方案");
                    return;
                }

                // ==================== 步骤1：前置检查 ====================
                var oldName = metadata.Name;
                bool nameChanged = dialog.SolutionName != metadata.Name;
                bool descriptionChanged = (dialog.Description ?? "") != (metadata.Description ?? "");

                if (!nameChanged && !descriptionChanged)
                {
                    LogInfo("解决方案信息未变化");
                    return;
                }

                // ==================== 步骤2：重命名文件（如果名称变化） ====================
                if (nameChanged)
                {
                    LogInfo($"重命名解决方案: {oldName} → {dialog.SolutionName}");

                    bool renameSuccess = _solutionManager.RenameSolutionFile(metadata.Id, dialog.SolutionName);
                    if (!renameSuccess)
                    {
                        LogError($"重命名解决方案失败: {oldName} → {dialog.SolutionName}");
                        MessageBox.Show("重命名解决方案失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 获取新的元数据（FilePath 已更新）
                    var updatedMetadata = _solutionManager.GetMetadata(metadata.Id);
                    if (updatedMetadata != null)
                    {
                        metadata.FilePath = updatedMetadata.FilePath;
                        metadata.DirectoryPath = updatedMetadata.DirectoryPath;
                    }
                }

                // ==================== 步骤3：更新元数据 ====================
                metadata.Name = dialog.SolutionName;
                metadata.Description = dialog.Description ?? "";
                metadata.ModifiedTime = DateTime.Now;

                // ==================== 步骤4：保存元数据（不加载 Solution 文件） ====================
                _solutionManager.UpdateMetadata(metadata);

                LogSuccess($"解决方案已更新: {dialog.SolutionName}");
            }
            catch (Exception ex)
            {
                LogError($"编辑解决方案失败: {ex.Message}");
                MessageBox.Show($"编辑解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 生成唯一的复制名称
        /// </summary>
        private string GenerateUniqueCopyName(string baseName)
        {
            var existingNames = SolutionMetadatas.Select(s => s.Name).ToHashSet();
            var copyName = $"{baseName}_副本";
            var counter = 2;

            while (existingNames.Contains(copyName))
            {
                copyName = $"{baseName}_副本{counter}";
                counter++;
            }

            return copyName;
        }

        #endregion



        #region 配置和启动

        /// <summary>
        /// 启动时跳过配置界面
        /// </summary>
        public bool SkipStartupConfig
        {
            get => _solutionManager.Settings.SkipStartupConfig;
            set
            {
                if (_solutionManager.Settings.SkipStartupConfig != value)
                {
                    _solutionManager.Settings.SkipStartupConfig = value;
                    _solutionManager.MarkSettingsDirty();
                    OnPropertyChanged(nameof(SkipStartupConfig));
                }
            }
        }

        /// <summary>
        /// 跳过命令
        /// </summary>
        public ICommand SkipCommand { get; private set; } = null!;

        /// <summary>
        /// 启动命令
        /// </summary>
        public ICommand LaunchCommand { get; private set; } = null!;

        /// <summary>
        /// 切换默认解决方案
        /// </summary>
        private void ExecuteToggleDefault(object? parameter)
        {
            if (SelectedMetadata == null)
                return;

            if (SelectedMetadata.IsDefault)
            {
                // 取消默认
                _solutionManager.SetDefaultSolution("");
                LogInfo($"取消默认解决方案: {SelectedMetadata.Name}");
            }
            else
            {
                // 设为默认
                _solutionManager.SetDefaultSolution(SelectedMetadata.Id);
                LogSuccess($"设置默认解决方案: {SelectedMetadata.Name}");
            }

            // 直接更新UI集合中的 IsDefault 属性，避免重新加载列表
            // LoadSolutions() 会清空并重建对象，导致UI无法正确感知 IsDefault 变化
            foreach (var metadata in SolutionMetadatas)
            {
                // 直接设置 IsDefault，触发 PropertyChanged
                metadata.IsDefault = (metadata.Id == _solutionManager.Settings.DefaultSolutionId);
            }

            // 通知按钮文本属性变化
            OnPropertyChanged(nameof(ToggleDefaultButtonText));
            OnPropertyChanged(nameof(ToggleDefaultButtonToolTip));
        }

        /// <summary>
        /// 是否可以切换默认解决方案
        /// </summary>
        private bool CanToggleDefault(object? parameter)
        {
            return SelectedMetadata != null;
        }

        private void Skip()
        {
            if (_ownerWindow != null)
            {
                _ownerWindow.DialogResult = false;
                _ownerWindow.Close();
            }
        }

        private void Launch(object? parameter)
        {
            // 获取要启动的元数据
            var metadataToLaunch = parameter as SolutionMetadata ?? SelectedMetadata;

            if (metadataToLaunch != null)
            {
                // [诊断] 记录双击的选项卡
                LogInfo($"[诊断-双击] 双击的解决方案ID: {metadataToLaunch.Id}");
                LogInfo($"[诊断-双击] 双击的解决方案名称: {metadataToLaunch.Name}");
                LogInfo($"[诊断-双击] 双击的解决方案路径: {metadataToLaunch.FilePath}");
                
                // [诊断] 记录当前解决方案ID
                LogInfo($"[诊断-双击] 当前解决方案ID: {_solutionManager.CurrentSolution?.Id ?? "null"}");
                LogInfo($"[诊断-双击] 默认解决方案ID: {_solutionManager.Settings.DefaultSolutionId ?? "null"}");

                // 如果参数传入的不是当前选中的解决方案，更新选中状态
                if (SelectedMetadata?.Id != metadataToLaunch.Id)
                {
                    SelectedMetadata = metadataToLaunch;
                }

                // ✅ 懒加载：仅在启动时加载完整 Solution 对象
                var fullSolution = _solutionManager.LoadSolutionOnly(metadataToLaunch.FilePath);
                if (fullSolution != null)
                {
                    SelectedSolution = fullSolution;
                    LogInfo($"加载完整解决方案对象: Id={fullSolution.Id}");

                    // ✅ 仅设置启动结果，不调用 SetCurrentSolutionSilently
                    // 由调用方（App.xaml.cs 或 ExecuteShowSolutionConfiguration）决定如何处理
                    _isLaunched = true;

                    // 设置启动结果
                    _launchResult = fullSolution;

                    if (_ownerWindow != null)
                    {
                        _ownerWindow.DialogResult = true;
                        _ownerWindow.Close();
                    }

                    LogSuccess($"启动解决方案: {metadataToLaunch.Name}");
                }
                else
                {
                    LogError($"加载解决方案失败: {metadataToLaunch.Name}");
                    MessageBox.Show($"加载解决方案失败: {metadataToLaunch.Name}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region 数据加载

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            LoadSolutions();

            // 预选指定的解决方案
            if (!string.IsNullOrEmpty(_preselectSolutionId))
            {
                var metadata = SolutionMetadatas.FirstOrDefault(s => s.Id == _preselectSolutionId);
                if (metadata != null)
                {
                    SelectedMetadata = metadata;
                }
            }
        }

        /// <summary>
        /// 从 SolutionManager 加载解决方案元数据列表
        /// </summary>
        /// <remarks>
        /// 架构改进（2026-03-16）：
        /// - 直接使用 SolutionMetadata，避免不必要的转换层
        /// - 避免加载 Workflows、NodeParameters、GlobalVariables、Devices、Communications 等大数据
        /// - 完整 Solution 对象只在需要编辑或执行时通过 LoadSolutionOnly() 加载
        /// - 性能提升：启动时间从数秒降低到毫秒级
        ///
        /// 使用 SolutionManager.GetAllMetadata() 获取所有元数据
        /// 利用 SolutionCache 和 SolutionRegistry 实现懒加载优化
        /// </remarks>
        private void LoadSolutions()
        {
            // 先清空集合
            SolutionMetadatas.Clear();

            // 使用 SolutionManager.GetAllMetadata() 获取所有元数据
            var metadataList = _solutionManager.GetAllMetadata();

            // 批量添加，减少 UI 更新次数
            foreach (var metadata in metadataList)
            {
                SolutionMetadatas.Add(metadata);
            }

            LogInfo($"加载 {metadataList.Count} 个解决方案（元数据模式 + 缓存优化）");
        }

        /// <summary>
        /// 解决方案添加事件处理
        /// </summary>
        /// <remarks>
        /// 增量更新：直接添加到集合，不重新加载
        /// 防御性编程：添加前检查是否已存在，避免重复添加
        /// </remarks>
        private void OnSolutionAdded(object? sender, SolutionMetadataEventArgs e)
        {
            if (e.Metadata != null)
            {
                // ✅ 防御性编程：检查是否已存在，避免重复添加
                if (!SolutionMetadatas.Any(s => s.Id == e.Metadata.Id))
                {
                    SolutionMetadatas.Add(e.Metadata);
                    LogInfo($"添加解决方案到列表: {e.Metadata.Name}");
                }
                else
                {
                    LogWarning($"解决方案已存在，跳过添加: {e.Metadata.Name} (Id={e.Metadata.Id})");
                }
            }
        }

        /// <summary>
        /// 解决方案删除事件处理
        /// </summary>
        /// <remarks>
        /// 增量更新：直接从集合移除，不重新加载
        /// </remarks>
        private void OnSolutionRemoved(object? sender, SolutionMetadataEventArgs e)
        {
            var toRemove = SolutionMetadatas.FirstOrDefault(s => s.Id == e.SolutionId);
            if (toRemove != null)
            {
                SolutionMetadatas.Remove(toRemove);
                LogInfo($"从列表移除解决方案: {toRemove.Name}");
            }
        }

        /// <summary>
        /// 解决方案重命名事件处理
        /// </summary>
        /// <remarks>
        /// 增量更新：直接更新属性，不重新加载
        /// </remarks>
        private void OnSolutionRenamed(object? sender, SolutionMetadataEventArgs e)
        {
            var toUpdate = SolutionMetadatas.FirstOrDefault(s => s.Id == e.Metadata?.Id);
            if (toUpdate != null && e.Metadata != null)
            {
                toUpdate.Name = e.Metadata.Name;
                toUpdate.FilePath = e.Metadata.FilePath;
                toUpdate.DirectoryPath = e.Metadata.DirectoryPath;
                toUpdate.ModifiedTime = e.Metadata.ModifiedTime;
                LogInfo($"重命名解决方案: {e.OldName} → {e.NewName}");
            }
        }

        /// <summary>
        /// 解决方案更新事件处理
        /// </summary>
        /// <remarks>
        /// 增量更新：直接更新属性，不重新加载
        /// </remarks>
        private void OnSolutionUpdated(object? sender, SolutionMetadataEventArgs e)
        {
            var toUpdate = SolutionMetadatas.FirstOrDefault(s => s.Id == e.Metadata?.Id);
            if (toUpdate != null && e.Metadata != null)
            {
                toUpdate.Name = e.Metadata.Name;
                toUpdate.Description = e.Metadata.Description;
                toUpdate.ModifiedTime = e.Metadata.ModifiedTime;
                LogInfo($"更新解决方案元数据: {e.Metadata.Name}");
            }
        }

        /// <summary>
        /// 元数据刷新事件处理
        /// </summary>
        /// <remarks>
        /// 全量刷新：重新加载整个列表
        /// 触发场景：启动扫描、手动扫描、手动刷新
        /// </remarks>
        private void OnMetadataRefreshed(object? sender, EventArgs e)
        {
            LoadSolutions();
            LogInfo("元数据已刷新，重新加载列表");
        }

        /// <summary>
        /// 当前解决方案变更事件处理
        /// </summary>
        /// <remarks>
        /// 增量更新：更新 CurrentSolutionId，触发属性通知
        /// </remarks>
        private void OnCurrentSolutionChanged(object? sender, SolutionMetadataEventArgs e)
        {
            OnPropertyChanged(nameof(CurrentSolutionId));
            LogInfo($"当前解决方案变更: {e.Metadata?.Name}");
        }

        /// <summary>
        /// 清理文件名（移除非法字符）
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars));
            return sanitized;
        }

        #endregion
    }
}
