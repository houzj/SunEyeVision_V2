using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Features.ParameterSetting;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shared.Events;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

using SunEyeVision.UI.Core;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.GlobalVariableManager
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
    public class GlobalVariableManagerViewModel : ViewModelBase, IDocumentEditParticipant
    {
        private readonly SolutionManager _solutionManager;
        private string _searchText = "";
        private VariableGroupViewModel? _selectedGroup;

        // 已释放序号池（全局），用于空洞填充
        private readonly SortedSet<int> _releasedIndices = new();

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

        /// <summary>
        /// 打开目标参数管理命令
        /// </summary>
        public ICommand OpenTargetParameterCommand { get; private set; }

        #endregion

        /// <summary>
        /// 类型选项列表（中文显示名 + 英文值，用于 ComboBox 绑定）
        /// </summary>
        public List<TypeOption> TypeOptions { get; } = SdkTypeVocabulary.GetAllTypeOptions();

        public Solution? EditingDocument { get; }
        internal NodeConfigurationEditHost Host { get; }
        internal GlobalVariableConfigurationEditDraft Draft { get; private set; }
        internal Solution DraftDocument => Draft.Configuration;
        private readonly Func<bool> _isCurrent;
        private bool _active = true;
        private readonly DocumentEditSession? _documentEdits;
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => "variables";
        public bool IsValid => _active && _isCurrent();
        public bool HasPendingChanges => _active && (Host.HasPendingChanges(Draft) || HasPendingViewEdits?.Invoke() == true);
        internal Func<string?>? PrepareView { get; set; }
        internal Func<bool>? HasPendingViewEdits { get; set; }
        internal Action? AcceptView { get; set; }
        internal Func<string>? PendingViewStamp { get; set; }
        internal Action<string>? AcknowledgeViewEdits { get; set; }
        private sealed record PreparationToken(GlobalVariableConfigurationEditDraft Draft, string? ViewStamp);

        public string? LastRefreshError { get; private set; }
        public GlobalVariableManagerViewModel(SolutionManager solutionManager,
            NodeConfigurationEditHost? host = null, Func<bool>? isCurrent = null, DocumentEditSession? documentEdits = null)
        {
            _documentEdits = documentEdits;
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            EditingDocument = solutionManager.CurrentSolution;
            Host = host ?? new NodeConfigurationEditHost(EditingDocument ?? throw new InvalidOperationException("当前没有方案。"));
            _isCurrent = isCurrent ?? (() => ReferenceEquals(EditingDocument, solutionManager.CurrentSolution));
            Draft = Host.BeginVariableEdit();

            Groups = new ObservableCollection<VariableGroupViewModel>();
            CurrentVariables = new ObservableCollection<GlobalVariable>();

            InitializeCommands();
            LoadGroups();
            documentEdits?.Register(this);
        }

        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
        {
            if (!ReferenceEquals(host, Host)) throw new InvalidOperationException("变量编辑属于另一个文档。");
            if (!TryPrepare(out var error)) throw new InvalidOperationException(error);
            // An unchanged scope may follow another node's Apply without publishing anything itself.
            if (Draft.BaseVersion != Host.Capture().Version && !HasPendingChanges) AcceptPublication();
            var viewError = PrepareView?.Invoke();
            if (!string.IsNullOrEmpty(viewError)) throw new InvalidOperationException(viewError);
            return DocumentEditProposal.ForVariables(host, Draft, new PreparationToken(Draft, PendingViewStamp?.Invoke()));
        }
        public void Acknowledge(DocumentEditProposal prepared, BindingConfigurationPublication publication)
        {
            if (prepared.Token is not PreparationToken token || !ReferenceEquals(token.Draft, Draft)) return;
            if (!prepared.Matches(Draft) || token.ViewStamp != PendingViewStamp?.Invoke())
            {
                Draft = prepared.PreserveNewerVariableEdits(Host, Draft);
                if (token.ViewStamp is not null) AcknowledgeViewEdits?.Invoke(token.ViewStamp);
                return;
            }
            if (AcceptView is { } accept) accept(); else AcceptPublication();
        }
        public void Cancel() => Discard();

        internal bool TryPrepare(out string error)
        {
            if (!_active || !_isCurrent())
            { _active = false; error = "变量编辑所属方案或会话已失效，草稿不能提交。"; return false; }
            error = ""; return true;
        }
        internal void SetDraftValue(GlobalVariable variable, object? value)
        {
            if (!DraftDocument.GlobalVariables.Any(v => ReferenceEquals(v, variable)))
                throw new InvalidOperationException("变量不属于当前私有草稿。");
            Draft.SetValue(variable.Id, value);
        }
        internal void Discard()
        {
            _active = false;
            _documentEdits?.Unregister(ScopeId);
        }
        internal void AcceptPublication()
        {
            try
            {
                if (!TryPrepare(out var error)) throw new InvalidOperationException(error);
                Draft = Host.BeginVariableEdit();
                LoadGroups();
            }
            catch (Exception ex) { LastRefreshError = ex.Message; Discard(); }
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
            OpenTargetParameterCommand = new RelayCommand<object?>(OpenTargetParameter, _ => true);
        }

        /// <summary>
        /// 加载分组
        /// </summary>
        private void LoadGroups()
        {
            var currentSolution = DraftDocument;

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
            var solution = DraftDocument;
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
        /// 添加变量（全局唯一名称 + 空洞序号填充）
        /// </summary>
        private void AddVariable()
        {
            if (SelectedGroup == null)
            {
                LogWarning("请先选择分组");
                return;
            }

            int variableIndex = GetNextAvailableIndex();
            var variable = new GlobalVariable
            {
                Name = GenerateUniqueName($"变量{variableIndex}"),
                Type = "String",
                Value = "",
                Description = "",
                Group = SelectedGroup.Name,
                Index = variableIndex,
                ConsumerBindings = new List<ConsumerBinding>()
            };

            SelectedGroup.AddVariable(variable);
            DraftDocument.GlobalVariables.Add(variable);
            CurrentVariables.Add(variable);
            SelectedVariable = variable;

            LogInfo($"添加全局变量: {variable.Name} (分组: {SelectedGroup.Name}, 序号: {variableIndex})");
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

            // 回收序号到全局空洞池
            _releasedIndices.Add(variable.Index);

            SelectedGroup?.RemoveVariable(variable);
            DraftDocument.GlobalVariables.Remove(variable);
            CurrentVariables.Remove(variable);

            if (SelectedVariable == variable)
                SelectedVariable = null;

            LogInfo($"删除全局变量: {variableName} (分组: {groupName}, 序号 {variable.Index} 已回收)");
        }

        /// <summary>
        /// 是否可以删除变量
        /// </summary>
        private bool CanDeleteVariable(object? parameter)
        {
            return parameter is GlobalVariable || SelectedVariable != null;
        }

        #endregion

        #region 全局变量名称/序号管理

        /// <summary>
        /// 获取下一个可用序号（优先从空洞池中取）
        /// </summary>
        private int GetNextAvailableIndex()
        {
            if (_releasedIndices.Count > 0)
            {
                var index = _releasedIndices.Min;
                _releasedIndices.Remove(index);
                return index;
            }

            var allVariables = DraftDocument.GlobalVariables;
            return allVariables != null && allVariables.Count > 0
                ? allVariables.Max(v => v.Index) + 1
                : 1;
        }

        /// <summary>
        /// 生成全局唯一的变量名称（跨所有分组查重）
        /// </summary>
        private string GenerateUniqueName(string baseName)
        {
            var allNames = DraftDocument.GlobalVariables
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>();

            if (!allNames.Contains(baseName))
                return baseName;

            int suffix = 1;
            while (allNames.Contains($"{baseName}({suffix})"))
                suffix++;

            return $"{baseName}({suffix})";
        }

        /// <summary>
        /// 确认变量重命名（名称列 TextBox 失焦时调用）
        /// </summary>
        /// <param name="variable">目标变量</param>
        /// <param name="newName">编辑后的新名称</param>
        /// <param name="oldName">编辑前名称（TextBox 绑定实时写回，进入此处时 variable.Name 可能已是 newName，
        /// 故以显式传入的 oldName 判断是否发生改名，避免绑定时序导致传播被跳过）</param>
        /// <returns>true=通过，false=弹窗提示且调用方需还原旧值</returns>
        public bool ConfirmVariableRename(GlobalVariable variable, string newName, string oldName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                ThemeMessageDialog.Show("变量名称不能为空", "提示",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                return false;
            }

            var allVariables = DraftDocument.GlobalVariables;
            if (allVariables == null)
                return true;

            if (GlobalVariable.IsNameDuplicate(allVariables, newName, variable.Id))
            {
                ThemeMessageDialog.Show($"变量名称 '{newName}' 已存在，请使用其他名称",
                    "提示", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                return false;
            }

            // 名称已变更 → 执行重命名并传播
            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                variable.Name = newName;

                // ★ 传播到所有引用处（节点 ParamSettings + 模块结果行）
                LogInfo($"变量草稿重命名: {oldName} → {newName}");
            }

            return true;
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

                // 添加到对应分组
                foreach (var variable in variables)
                {
                    variable.Id = Guid.NewGuid().ToString();
                    DraftDocument.GlobalVariables.Add(variable);

                    // 查找或创建分组
                    var group = Groups.FirstOrDefault(g => g.Name == variable.Group);
                    if (group == null)
                    {
                        group = new VariableGroupViewModel(variable.Group);
                        Groups.Add(group);
                    }

                    // 重新分配序号（全局空洞填充）
                    variable.Index = GetNextAvailableIndex();
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
        private Func<bool>? _saveRequested;
        public Func<bool>? SaveRequested
        {
            get => _saveRequested;
            set { _saveRequested = value; CommandManager.InvalidateRequerySuggested(); }
        }
        private void Save()
        {
            try
            {
                if (SaveRequested?.Invoke() == true) LogSuccess("方案文件已保存（含全局变量）。");
                else LogWarning("本次未保存，当前编辑内容保留。");
            }
            catch (Exception ex) { LogError("保存未完成，当前编辑保留：" + ex.Message); }
        }
        private bool CanSave() => _active && _isCurrent() && SaveRequested != null;

        /// <summary>
        /// 关闭
        /// </summary>
        private void Close()
        {
            LogInfo("关闭全局变量管理器");
        }

        /// <summary>
        /// 打开目标参数管理对话框
        /// </summary>
        private void OpenTargetParameter(object? parameter)
        {
            if (parameter is not GlobalVariable variable)
                return;

            try
            {
                var currentSolution = DraftDocument;
                if (currentSolution == null || currentSolution.Workflows.Count == 0)
                {
                    LogWarning("当前解决方案没有工作流，无法管理消费者");
                    return;
                }

                var result = TargetParameterDialog.ShowDialog(
                    currentSolution.Workflows,
                    variable.ConsumerBindings,
                    variable.Type);

                if (result != null)
                {
                    // 1. 计算变化：哪些新增，哪些移除
                    var oldSet = variable.ConsumerBindings
                        .Select(b => b.BindingKey)
                        .ToHashSet(StringComparer.Ordinal);
                    var newSet = result
                        .Select(b => b.BindingKey)
                        .ToHashSet(StringComparer.Ordinal);

                    var added = result.Where(b => !oldSet.Contains(b.BindingKey)).ToList();
                    var removed = variable.ConsumerBindings
                        .Where(b => !newSet.Contains(b.BindingKey)).ToList();

                    // 2. 更新 ConsumerBindings
                    variable.ConsumerBindings = result;

                    // 3. 新增绑定 → 设置节点 ParamSettings（全局变量绑定）
                    foreach (var binding in added)
                        SetNodeParamBinding(currentSolution, binding, variable.Name);

                    // 4. 移除绑定 → 清除节点 ParamSettings（切回常量模式）
                    foreach (var binding in removed)
                        ClearNodeParamBinding(currentSolution, binding);

                    variable.NotifyOutputDisplayChanged();
                    LogSuccess($"更新目标输出 (共 {result.Count} 个消费者)");
                }
            }
            catch (Exception ex)
            {
                LogError($"打开目标参数管理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为节点参数设置全局变量绑定（支持纯 CLR 参数和集合型输入）
        /// </summary>
        private static void SetNodeParamBinding(Solution solution, ConsumerBinding binding, string variableName)
        {
            var workflow = solution.Workflows.FirstOrDefault(w => w.Id == binding.WorkflowId);
            var node = workflow?.Nodes?.FirstOrDefault(n => n.Id == binding.NodeId)
                ?? (string.IsNullOrEmpty(binding.WorkflowId)
                    ? solution.Workflows.SelectMany(w => w.Nodes ?? Enumerable.Empty<WorkflowNodeBase>())
                        .SingleOrDefault(n => n.Id == binding.NodeId)
                    : null);
            if (node?.Parameters == null) return;

            // ★ 集合型输入（脚本工具 Inputs / 子程序 Inputs）
            if (TrySetInputBinding(node.Parameters, binding.ParameterName, variableName))
                return;

            var prop = node.Parameters.GetType().GetProperty(binding.ParameterName);
            if (prop == null) return;

            // 新参数模型：绑定定义存放在节点 ParamSettings，算法参数保持纯 CLR 值。
            // 该定义是设计期数据，运行时由 ToolExecutionRequest 解析为快照。
            node.ParamSettings ??= new ParamSettingContainer();
            node.ParamSettings.SetSetting(new ParamSetting
            {
                ParameterName = binding.ParameterName,
                BindingType = BindingType.Binding,
                SourceNodeId = "__GlobalVars__",
                SourceProperty = variableName,
                SourceNodeName = "全局变量",
                SourceDisplayName = variableName,
                TargetType = prop.PropertyType
            });

        }

        /// <summary>
        /// 尝试为集合型输入（Inputs 集合）设置绑定
        /// </summary>
        private static bool TrySetInputBinding(ToolParameters parameters, string parameterName, string variableName)
        {
            var inputsProp = parameters.GetType().GetProperty("Inputs");
            if (inputsProp == null) return false;

            if (inputsProp.GetValue(parameters) is IEnumerable inputs)
            {
                foreach (var input in inputs)
                {
                    var nameProp = input.GetType().GetProperty("Name");
                    if (nameProp?.GetValue(input) is string name && name == parameterName)
                    {
                        var bindingSourceProp = input.GetType().GetProperty("BindingSource");
                        var bindingDisplayProp = input.GetType().GetProperty("BindingDisplayText");
                        bindingSourceProp?.SetValue(input, $"__GlobalVars__.{variableName}");
                        bindingDisplayProp?.SetValue(input, $"全局变量.{variableName}");
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 清除节点参数的绑定（支持纯 CLR 参数和集合型输入）
        /// </summary>
        private static void ClearNodeParamBinding(Solution solution, ConsumerBinding binding)
        {
            var workflow = solution.Workflows.FirstOrDefault(w => w.Id == binding.WorkflowId);
            var node = workflow?.Nodes?.FirstOrDefault(n => n.Id == binding.NodeId)
                ?? (string.IsNullOrEmpty(binding.WorkflowId)
                    ? solution.Workflows.SelectMany(w => w.Nodes ?? Enumerable.Empty<WorkflowNodeBase>())
                        .SingleOrDefault(n => n.Id == binding.NodeId)
                    : null);
            if (node?.Parameters == null) return;

            // ★ 集合型输入
            if (TryClearInputBinding(node.Parameters, binding.ParameterName))
                return;

            var prop = node.Parameters.GetType().GetProperty(binding.ParameterName);
            if (prop == null) return;

            // 新参数模型：先移除节点级外部绑定定义。
            node.ParamSettings?.RemoveSetting(binding.ParameterName);

        }

        /// <summary>
        /// 尝试清除集合型输入（Inputs 集合）的绑定
        /// </summary>
        private static bool TryClearInputBinding(ToolParameters parameters, string parameterName)
        {
            var inputsProp = parameters.GetType().GetProperty("Inputs");
            if (inputsProp == null) return false;

            if (inputsProp.GetValue(parameters) is IEnumerable inputs)
            {
                foreach (var input in inputs)
                {
                    var nameProp = input.GetType().GetProperty("Name");
                    if (nameProp?.GetValue(input) is string name && name == parameterName)
                    {
                        var bindingSourceProp = input.GetType().GetProperty("BindingSource");
                        var bindingDisplayProp = input.GetType().GetProperty("BindingDisplayText");
                        bindingSourceProp?.SetValue(input, string.Empty);
                        bindingDisplayProp?.SetValue(input, string.Empty);
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
