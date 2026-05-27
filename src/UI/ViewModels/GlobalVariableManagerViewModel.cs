using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.Models;

using SunEyeVision.Workflow;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 全局变量管理器 ViewModel
    /// </summary>
    /// <remarks>
    /// 设计原则（rule-004: 方案设计要求）：
    /// - 支持分组管理（多工位/多相机场景）
    /// - 支持搜索过滤
    /// - 支持导入/导出
    /// - VisionMaster风格界面
    /// 
    /// 功能特性：
    /// 1. 分组管理：创建、删除、切换分组
    /// 2. 变量管理：添加、删除、编辑变量
    /// 3. 搜索过滤：按名称、类型搜索
    /// 4. 导入导出：JSON格式的变量配置
    /// </remarks>
    public class GlobalVariableManagerViewModel : ViewModelBase
    {
        private readonly SolutionManager _solutionManager;
        private string _searchText = "";
        private VariableGroupViewModel? _selectedGroup;

        /// <summary>
        /// 分组列表
        /// </summary>
        public ObservableCollection<VariableGroupViewModel> Groups { get; }

        /// <summary>
        /// 选中的分组
        /// </summary>
        public VariableGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                {
                    UpdateFilteredVariables();
                    OnPropertyChanged(nameof(CurrentVariables));
                }
            }
        }

        /// <summary>
        /// 当前显示的变量列表（过滤后）
        /// </summary>
        public ObservableCollection<GlobalVariable> CurrentVariables { get; }

        /// <summary>
        /// 选中的变量
        /// </summary>
        private GlobalVariable? _selectedVariable;
        public GlobalVariable? SelectedVariable
        {
            get => _selectedVariable;
            set => SetProperty(ref _selectedVariable, value);
        }

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    UpdateFilteredVariables();
                }
            }
        }

        #region 命令

        /// <summary>
        /// 添加变量命令
        /// </summary>
        public ICommand AddVariableCommand { get; private set; }

        /// <summary>
        /// 删除变量命令
        /// </summary>
        public ICommand DeleteVariableCommand { get; private set; }

        /// <summary>
        /// 添加分组命令
        /// </summary>
        public ICommand AddGroupCommand { get; private set; }

        /// <summary>
        /// 删除分组命令
        /// </summary>
        public ICommand DeleteGroupCommand { get; private set; }

        /// <summary>
        /// 导入命令
        /// </summary>
        public ICommand ImportCommand { get; private set; }

        /// <summary>
        /// 导出命令
        /// </summary>
        public ICommand ExportCommand { get; private set; }

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; private set; }

        /// <summary>
        /// 关闭命令
        /// </summary>
        public ICommand CloseCommand { get; private set; }

        #endregion

        public GlobalVariableManagerViewModel(SolutionManager solutionManager)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));

            Groups = new ObservableCollection<VariableGroupViewModel>();
            CurrentVariables = new ObservableCollection<GlobalVariable>();

            InitializeCommands();
            LoadGroups();
        }

        private void InitializeCommands()
        {
            AddVariableCommand = new RelayCommand(AddVariable, CanAddVariable);
            DeleteVariableCommand = new RelayCommand(DeleteVariable, CanDeleteVariable);
            AddGroupCommand = new RelayCommand(AddGroup, () => true);
            DeleteGroupCommand = new RelayCommand(DeleteGroup, CanDeleteGroup);
            ImportCommand = new RelayCommand(Import, () => true);
            ExportCommand = new RelayCommand(Export, CanExport);
            SaveCommand = new RelayCommand(Save, CanSave);
            CloseCommand = new RelayCommand(Close, () => true);
        }

        /// <summary>
        /// 加载分组
        /// </summary>
        private void LoadGroups()
        {
            var currentSolution = _solutionManager.CurrentSolution;

            // 清空现有分组
            Groups.Clear();

            // 从现有变量中按 Group 字段归类
            if (currentSolution?.GlobalVariables != null && currentSolution.GlobalVariables.Count > 0)
            {
                var groupedVariables = currentSolution.GlobalVariables
                    .GroupBy(v => v.Group)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedVariables)
                {
                    var groupVM = new VariableGroupViewModel(group.Key);
                    foreach (var variable in group.OrderBy(v => v.Index))
                    {
                        groupVM.AddVariable(variable);
                    }
                    Groups.Add(groupVM);
                }
            }

            // 始终保证至少有一个默认分组
            if (Groups.Count == 0)
            {
                var defaultGroup = new VariableGroupViewModel("分组1");
                Groups.Add(defaultGroup);
            }

            // 默认选中第一个分组
            SelectedGroup = Groups[0];
            SelectedGroup.IsSelected = true;

            LogInfo($"加载分组完成，共 {Groups.Count} 个分组");
        }

        /// <summary>
        /// 更新过滤后的变量列表
        /// </summary>
        private void UpdateFilteredVariables()
        {
            CurrentVariables.Clear();

            if (SelectedGroup == null)
                return;

            var variables = SelectedGroup.Variables.AsEnumerable();

            // 应用搜索过滤
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                variables = variables.Where(v =>
                    v.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    v.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    v.Type.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var variable in variables.OrderBy(v => v.Index))
            {
                CurrentVariables.Add(variable);
            }
        }

        #region 分组操作

        /// <summary>
        /// 添加分组（上限 10 个）
        /// </summary>
        private void AddGroup()
        {
            const int MaxGroups = 10;
            if (Groups.Count >= MaxGroups)
            {
                LogWarning($"分组数量已达上限 ({MaxGroups} 个)");
                return;
            }

            // 生成新分组名称
            int groupIndex = Groups.Count + 1;
            string groupName = $"分组{groupIndex}";

            // 确保名称唯一
            while (Groups.Any(g => g.Name == groupName))
            {
                groupIndex++;
                groupName = $"分组{groupIndex}";
            }

            var newGroup = new VariableGroupViewModel(groupName);
            Groups.Add(newGroup);
            SelectedGroup = newGroup;

            LogInfo($"添加分组: {groupName}");
        }

        /// <summary>
        /// 删除分组
        /// </summary>
        private void DeleteGroup()
        {
            if (SelectedGroup == null)
                return;

            // 检查分组中是否有变量
            if (SelectedGroup.Variables.Count > 0)
            {
                LogWarning($"分组 '{SelectedGroup.Name}' 中有 {SelectedGroup.Variables.Count} 个变量，请先删除变量");
                return;
            }

            var groupName = SelectedGroup.Name;
            Groups.Remove(SelectedGroup);

            // 选中第一个分组
            if (Groups.Count > 0)
            {
                SelectedGroup = Groups[0];
            }
            else
            {
                SelectedGroup = null;
            }

            LogInfo($"删除分组: {groupName}");
        }

        /// <summary>
        /// 是否可以删除分组
        /// </summary>
        private bool CanDeleteGroup()
        {
            return SelectedGroup != null;
        }

        /// <summary>
        /// 确认分组重命名（内联编辑提交时调用）
        /// </summary>
        public void ConfirmGroupRename(VariableGroupViewModel? groupVM)
        {
            if (groupVM == null || !groupVM.IsEditing) return;

            string newName = groupVM.EditingText?.Trim();

            // 空名称 → 还原
            if (string.IsNullOrWhiteSpace(newName))
            {
                groupVM.CancelEdit();
                return;
            }

            // 名称未变 → 直接退出编辑
            if (newName == groupVM.Name)
            {
                groupVM.CommitEdit();
                return;
            }

            // 名称唯一性
            if (Groups.Any(g => g.Name == newName && g != groupVM))
            {
                LogWarning($"分组名称 '{newName}' 已存在");
                groupVM.BeginEdit(); // 重新进入编辑让用户修改
                return;
            }

            string oldName = groupVM.Name;
            groupVM.Name = newName;
            groupVM.CommitEdit();

            foreach (var variable in groupVM.Variables.ToList())
                variable.Group = newName;

            LogInfo($"重命名分组: '{oldName}' → '{newName}' ({groupVM.Variables.Count} 个变量)");
        }

        /// <summary>
        /// 删除分组（级联删除变量，从右键菜单调用）
        /// </summary>
        public void DeleteGroupWithVariables()
        {
            if (SelectedGroup == null) return;

            var groupName = SelectedGroup.Name;
            var variablesToRemove = SelectedGroup.Variables.ToList();

            // 1. 从 Solution 中移除变量
            var solution = _solutionManager.CurrentSolution;
            if (solution != null)
            {
                foreach (var variable in variablesToRemove)
                {
                    solution.GlobalVariables.Remove(variable);
                }
            }

            // 2. 从 Groups 集合中移除分组
            Groups.Remove(SelectedGroup);

            // 3. 选中剩余的第一个分组
            if (Groups.Count > 0)
            {
                SelectedGroup = Groups[0];
                SelectedGroup.IsSelected = true;
            }
            else
            {
                // 全删了则创建默认分组
                var defaultGroup = new VariableGroupViewModel("分组1");
                Groups.Add(defaultGroup);
                SelectedGroup = defaultGroup;
                SelectedGroup.IsSelected = true;
            }

            LogInfo($"删除分组: '{groupName}' (同时删除 {variablesToRemove.Count} 个变量)");
        }

        #endregion

        #region 变量操作

        /// <summary>
        /// 添加变量
        /// </summary>
        private void AddVariable()
        {
            if (SelectedGroup == null)
            {
                LogWarning("请先选择分组");
                return;
            }

            int variableIndex = SelectedGroup.Variables.Count + 1;
            var variable = new GlobalVariable
            {
                Name = $"变量{variableIndex}",
                Type = "String",
                Value = "",
                Description = "",
                Group = SelectedGroup.Name,
                Index = variableIndex,
                InputSource = "手动输入",
                OutputTarget = "无"
            };

            SelectedGroup.AddVariable(variable);
            _solutionManager.CurrentSolution?.GlobalVariables.Add(variable);
            CurrentVariables.Add(variable);
            SelectedVariable = variable;

            LogInfo($"添加全局变量: {variable.Name} (分组: {SelectedGroup.Name})");
        }

        /// <summary>
        /// 是否可以添加变量
        /// </summary>
        private bool CanAddVariable()
        {
            return SelectedGroup != null;
        }

        /// <summary>
        /// 删除变量（支持参数化调用：行内删除按钮传入当前行变量）
        /// </summary>
        private void DeleteVariable(object? parameter)
        {
            var variable = parameter as GlobalVariable ?? SelectedVariable;
            if (variable == null)
                return;

            var variableName = variable.Name;
            var groupName = variable.Group;

            SelectedGroup?.RemoveVariable(variable);
            _solutionManager.CurrentSolution?.GlobalVariables.Remove(variable);
            CurrentVariables.Remove(variable);

            if (SelectedVariable == variable)
                SelectedVariable = null;

            LogInfo($"删除全局变量: {variableName} (分组: {groupName})");
        }

        /// <summary>
        /// 是否可以删除变量
        /// </summary>
        private bool CanDeleteVariable(object? parameter)
        {
            return parameter is GlobalVariable || SelectedVariable != null;
        }

        #endregion

        #region 导入导出

        /// <summary>
        /// 导入变量
        /// </summary>
        private void Import()
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入全局变量",
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var variables = JsonSerializer.Deserialize<List<GlobalVariable>>(json, WorkflowSerializationOptions.Default);

                if (variables == null || variables.Count == 0)
                {
                    LogWarning("导入文件中没有变量");
                    return;
                }

                // 添加到当前分组
                foreach (var variable in variables)
                {
                    variable.Id = Guid.NewGuid().ToString();
                    _solutionManager.CurrentSolution?.GlobalVariables.Add(variable);

                    // 查找或创建分组
                    var group = Groups.FirstOrDefault(g => g.Name == variable.Group);
                    if (group == null)
                    {
                        group = new VariableGroupViewModel(variable.Group);
                        Groups.Add(group);
                    }
                    group.AddVariable(variable);
                }

                UpdateFilteredVariables();
                LogSuccess($"导入全局变量成功，共 {variables.Count} 个变量");
            }
            catch (Exception ex)
            {
                LogError($"导入全局变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出变量
        /// </summary>
        private void Export()
        {
            if (SelectedGroup == null || SelectedGroup.Variables.Count == 0)
            {
                LogWarning("当前分组没有变量可导出");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出全局变量",
                Filter = "JSON文件 (*.json)|*.json",
                FilterIndex = 1,
                FileName = $"{SelectedGroup.Name}_variables.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var variables = SelectedGroup.Variables.ToList();
                var json = JsonSerializer.Serialize(variables, WorkflowSerializationOptions.Default);
                File.WriteAllText(dialog.FileName, json);

                LogSuccess($"导出全局变量成功，共 {variables.Count} 个变量");
            }
            catch (Exception ex)
            {
                LogError($"导出全局变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否可以导出
        /// </summary>
        private bool CanExport()
        {
            return SelectedGroup != null && SelectedGroup.Variables.Count > 0;
        }

        #endregion

        #region 保存和关闭

        /// <summary>
        /// 保存
        /// </summary>
        private void Save()
        {
            var currentSolution = _solutionManager.CurrentSolution;
            if (currentSolution?.FilePath != null)
            {
                try
                {
                    // 更新所有变量的Index属性
                    foreach (var group in Groups)
                    {
                        int index = 1;
                        foreach (var variable in group.Variables.OrderBy(v => v.Index))
                        {
                            variable.Index = index++;
                        }
                    }

                    _solutionManager.SaveSolution(currentSolution.FilePath);
                    LogSuccess("全局变量保存成功");
                }
                catch (Exception ex)
                {
                    LogError($"保存全局变量失败: {ex.Message}");
                }
            }
            else
            {
                LogWarning("解决方案未保存，无法保存全局变量");
            }
        }

        /// <summary>
        /// 是否可以保存
        /// </summary>
        private bool CanSave()
        {
            return _solutionManager.CurrentSolution?.FilePath != null;
        }

        /// <summary>
        /// 关闭
        /// </summary>
        private void Close()
        {
            LogInfo("关闭全局变量管理器");
        }

        #endregion
    }
}
