using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Workflow;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.GlobalVariableManager
{
    /// <summary>
    /// 全局变量选择 ViewModel（简化版，只读选择，非管理器）
    /// </summary>
    /// <remarks>
    /// 用于 ParamBinding 的"全局变量"动作节点，打开选择对话框让用户选择要绑定的变量。
    /// </remarks>
    public class GlobalVariableSelectViewModel : ViewModelBase
    {
        private readonly System.Collections.Generic.List<GlobalVariable> _allVariables;
        private readonly string? _filterDataType;
        private string _searchText = "";
        private string _selectedGroup = "";
        private GlobalVariable? _selectedVariable;

        /// <summary>
        /// 分组列表（用于 ComboBox 过滤）
        /// </summary>
        public ObservableCollection<string> Groups { get; }

        /// <summary>
        /// 选中的分组过滤条件
        /// </summary>
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                {
                    UpdateFilteredVariables();
                }
            }
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

        /// <summary>
        /// 过滤后的变量列表
        /// </summary>
        public ObservableCollection<GlobalVariable> FilteredVariables { get; }

        /// <summary>
        /// 选中的变量
        /// </summary>
        public GlobalVariable? SelectedVariable
        {
            get => _selectedVariable;
            set => SetProperty(ref _selectedVariable, value);
        }

        /// <summary>
        /// 当前选中变量数量（用于底部状态栏显示）
        /// </summary>
        public int TotalCount => _allVariables.Count;

        /// <summary>
        /// 确定命令（双击行或点确定按钮触发）
        /// </summary>
        public ICommand AcceptCommand { get; }

        /// <summary>
        /// 请求关闭对话框事件
        /// </summary>
        public event EventHandler? RequestClose;

        /// <summary>
        /// 过滤后数量
        /// </summary>
        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            set => SetProperty(ref _filteredCount, value);
        }

        public GlobalVariableSelectViewModel(System.Collections.Generic.List<GlobalVariable> variables, string? filterDataType = null)
        {
            _allVariables = variables ?? throw new ArgumentNullException(nameof(variables));
            _filterDataType = filterDataType;

            FilteredVariables = new ObservableCollection<GlobalVariable>();
            Groups = new ObservableCollection<string>();

            // 提取唯一分组列表并排序
            var uniqueGroups = variables
                .Select(v => v.Group)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            Groups.Add("全部");
            foreach (var group in uniqueGroups)
            {
                Groups.Add(group);
            }

            // 默认全部
            SelectedGroup = Groups[0];

            AcceptCommand = new RelayCommand(Accept, CanAccept);

            UpdateFilteredVariables();
        }

        /// <summary>
        /// 确定操作（通知对话框关闭）
        /// </summary>
        private void Accept()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 是否可以确定（必须选中变量）
        /// </summary>
        private bool CanAccept() => SelectedVariable != null;

        /// <summary>
        /// 更新过滤后的列表
        /// </summary>
        private void UpdateFilteredVariables()
        {
            FilteredVariables.Clear();

            var query = _allVariables.AsEnumerable();

            // ★ 新增：按数据类型过滤
            if (!string.IsNullOrEmpty(_filterDataType))
            {
                query = query.Where(v => string.Equals(v.Type, _filterDataType, StringComparison.OrdinalIgnoreCase));
            }

            // 按分组过滤
            if (!string.IsNullOrEmpty(SelectedGroup) && SelectedGroup != "全部")
            {
                query = query.Where(v => v.Group == SelectedGroup);
            }

            // 按搜索文本过滤
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(v =>
                    v.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    v.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    v.Type.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var variable in query.OrderBy(v => v.Index))
            {
                FilteredVariables.Add(variable);
            }

            FilteredCount = FilteredVariables.Count;
        }
    }
}
