using System;

using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Linq;

using System.Windows;
using System.Windows.Input;

using SunEyeVision.Plugin.SDK.Models;


using SunEyeVision.UI.Features.Dialogs;



using SunEyeVision.UI.Core;

using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;

using SunEyeVision.UI.Core;



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
        private readonly DocumentSession _session;
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

        public DocumentSession EditingSession => _session;
        internal NodeConfigurationEditHost Host { get; }
        internal GlobalVariableConfigurationEditDraft Draft { get; private set; }
        internal ObservableCollection<GlobalVariable> DraftVariables => Draft.Variables;
        private readonly Func<bool> _isCurrent;
        private readonly Func<bool> _canEdit;
        private bool _active = true;
        private readonly DocumentEditSession? _documentEdits;
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => "variables";
        public bool IsValid => _active && _isCurrent();
        public bool CanEdit => IsValid && _canEdit() && IsValid;
        public void RefreshAvailability() { OnPropertyChanged(nameof(CanEdit)); CommandManager.InvalidateRequerySuggested(); }
        public bool HasPendingChanges => _active && (Host.HasPendingChanges(Draft) || HasPendingViewEdits?.Invoke() == true);
        internal Func<string?>? PrepareView { get; set; }
        internal Func<bool>? HasPendingViewEdits { get; set; }
        internal Action? AcceptView { get; set; }
        internal Func<string>? PendingViewStamp { get; set; }
        internal Action<string>? AcknowledgeViewEdits { get; set; }
        private sealed record PreparationToken(GlobalVariableConfigurationEditDraft Draft, string? ViewStamp);

        public string? LastRefreshError { get; private set; }
        public GlobalVariableManagerViewModel(DocumentSession session, Func<bool> isCurrent, Func<bool> canEdit)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
            _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
            _documentEdits = session.Editing;
            Host = _documentEdits.Host;
            Draft = Host.BeginVariableEdit();
            Groups = new ObservableCollection<VariableGroupViewModel>();
            CurrentVariables = new ObservableCollection<GlobalVariable>();
            InitializeCommands();
            LoadGroups();
            try { _documentEdits.Register(this); }
            catch { Draft.Dispose(); throw; }
        }

        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
        {
            if (!ReferenceEquals(host, Host)) throw new InvalidOperationException("变量编辑属于另一个文档。");
            if (!TryPrepare(out var error)) throw new InvalidOperationException(error);
            var viewError = PrepareView?.Invoke();
            if (!string.IsNullOrEmpty(viewError)) throw new InvalidOperationException(viewError);
            return DocumentEditProposal.Prepare(host, variables: Draft, token: new PreparationToken(Draft, PendingViewStamp?.Invoke()));
        }
        public void Acknowledge(DocumentEditProposal prepared, DocumentEditReceipt receipt)
        {
            if (prepared.Token is not PreparationToken token || !ReferenceEquals(token.Draft, Draft))
                throw new InvalidOperationException("变量编辑回执与草稿不匹配。");
            var sameView = token.ViewStamp == PendingViewStamp?.Invoke();
            var sameDraft = prepared.Matches(Draft);
            Draft = Host.RebaseAcceptedDraft(Draft, prepared, receipt.AcceptedVariables);
            LoadGroups();
            if (sameView && sameDraft) AcceptView?.Invoke();
            else if (token.ViewStamp is not null) AcknowledgeViewEdits?.Invoke(token.ViewStamp);
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
            if (!CanEdit) throw new InvalidOperationException("当前方案不可编辑。");
            if (!DraftVariables.Any(v => ReferenceEquals(v, variable)))
                throw new InvalidOperationException("变量不属于当前私有草稿。");
            Draft.SetValue(variable.Id, value);
        }
        internal void Discard()
        {
            if (!_active) return;
            _active = false;
            _documentEdits?.Unregister(ScopeId);
            Draft.Dispose();
        }
        private void InitializeCommands()
        {
            AddVariableCommand = new RelayCommand(AddVariable, CanAddVariable);
            DeleteVariableCommand = new RelayCommand(DeleteVariable, CanDeleteVariable);
            AddGroupCommand = new RelayCommand(AddGroup, () => CanEdit);
            DeleteGroupCommand = new RelayCommand(DeleteGroup, CanDeleteGroup);
            ImportCommand = new RelayCommand(() => LogWarning("变量导入暂未接入会话原子事务。"), () => false);
            ExportCommand = new RelayCommand(() => LogWarning("变量导出暂未接入受控资源与原子写入。"), () => false);
            SaveCommand = new RelayCommand(Save, CanSave);
            CloseCommand = new RelayCommand(Close, () => true);
            // The legacy handler writes name-based __GlobalVars__ settings rather than the
            // current VariableSetting contract. Keep the entry unavailable until the formal
            // target-field adapter and its transaction have been migrated together.
            OpenTargetParameterCommand = new RelayCommand<object?>(OpenTargetParameter, _ => false);
        }

        /// <summary>
        /// 加载分组
        /// </summary>
        private void LoadGroups()
        {
            var variables = DraftVariables;

            // 清空现有分组
            Groups.Clear();

            // 从现有变量中按 Group 字段归类
            if (variables.Count > 0)
            {
                var groupedVariables = variables
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
            return CanEdit && SelectedGroup != null;
        }

        /// <summary>
        /// 确认分组重命名（内联编辑提交时调用）
        /// </summary>
        public void ConfirmGroupRename(VariableGroupViewModel? groupVM)
        {
            if (!CanEdit) return;
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
            if (!CanEdit) return;
            if (SelectedGroup == null) return;

            var groupName = SelectedGroup.Name;
            var variablesToRemove = SelectedGroup.Variables.ToList();

            // 1. 从 Solution 中移除变量
            var solution = DraftVariables;
            if (solution != null)
            {
                foreach (var variable in variablesToRemove)
                {
                    solution.Remove(variable);
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
                Index = variableIndex
            };

            SelectedGroup.AddVariable(variable);
            DraftVariables.Add(variable);
            CurrentVariables.Add(variable);
            SelectedVariable = variable;

            LogInfo($"添加全局变量: {variable.Name} (分组: {SelectedGroup.Name}, 序号: {variableIndex})");
        }

        /// <summary>
        /// 是否可以添加变量
        /// </summary>
        private bool CanAddVariable()
        {
            return CanEdit && SelectedGroup != null;
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
            DraftVariables.Remove(variable);
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
            return CanEdit && (parameter is GlobalVariable || SelectedVariable != null);
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

            var allVariables = DraftVariables;
            return allVariables != null && allVariables.Count > 0
                ? allVariables.Max(v => v.Index) + 1
                : 1;
        }

        /// <summary>
        /// 生成全局唯一的变量名称（跨所有分组查重）
        /// </summary>
        private string GenerateUniqueName(string baseName)
        {
            var allNames = DraftVariables
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
            if (!CanEdit || !DraftVariables.Any(item => ReferenceEquals(item, variable))) return false;
            if (string.IsNullOrWhiteSpace(newName))
            {
                ThemeMessageDialog.Show("变量名称不能为空", "提示",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                return false;
            }

            var allVariables = DraftVariables;
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
        private bool CanSave() => CanEdit && SaveRequested != null;

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
private void OpenTargetParameter(object? parameter) => LogWarning("变量目标参数绑定尚未迁移，当前不可用。");

        #endregion
    }
}
