using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 数据源选择ViewModel
    /// </summary>
    /// <remarks>
    /// 管理数据源选择界面，支持分组显示和类型过滤。
    /// 
    /// 核心功能：
    /// 1. 加载可用数据源
    /// 2. 按节点分组显示
    /// 3. 类型过滤
    /// 4. 搜索过滤
    /// 5. 预览当前值
    /// </remarks>
    public class DataSourcePickerViewModel : ViewModelBase
    {
        #region 字段

        private readonly IDataSourceQueryService? _dataSourceQueryService;
        private string _currentNodeId = string.Empty;
        private Type? _targetType;
        private string _searchText = string.Empty;
        private DataSourceGroupViewModel? _selectedGroup;
        private AvailableDataSource? _selectedDataSource;
        private bool _isLoading;
        private string _statusMessage = string.Empty;

        #endregion

        #region 属性

        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName { get; }

        /// <summary>
        /// 目标类型
        /// </summary>
        public Type? TargetType
        {
            get => _targetType;
            set => SetProperty(ref _targetType, value);
        }

        /// <summary>
        /// 当前节点ID
        /// </summary>
        public string CurrentNodeId
        {
            get => _currentNodeId;
            set => SetProperty(ref _currentNodeId, value);
        }

        /// <summary>
        /// 数据源分组列表
        /// </summary>
        public ObservableCollection<DataSourceGroupViewModel> Groups { get; }

        /// <summary>
        /// 过滤后的分组列表
        /// </summary>
        public ObservableCollection<DataSourceGroupViewModel> FilteredGroups { get; }

        /// <summary>
        /// 选中的分组
        /// </summary>
        public DataSourceGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set => SetProperty(ref _selectedGroup, value);
        }

        /// <summary>
        /// 选中的数据源
        /// </summary>
        public AvailableDataSource? SelectedDataSource
        {
            get => _selectedDataSource;
            set
            {
                if (SetProperty(ref _selectedDataSource, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(SelectionPreview));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// 是否有选中项
        /// </summary>
        public bool HasSelection => SelectedDataSource != null;

        /// <summary>
        /// 选择预览文本
        /// </summary>
        public string SelectionPreview => SelectedDataSource?.GetDetailedInfo() ?? "未选择";

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
                    FilterDataSources();
                }
            }
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 总数据源数量
        /// </summary>
        public int TotalCount { get; private set; }

        /// <summary>
        /// 过滤后数量
        /// </summary>
        public int FilteredCount { get; private set; }

        /// <summary>
        /// 是否显示类型过滤提示
        /// </summary>
        public bool ShowTypeFilterHint => TargetType != null;

        /// <summary>
        /// 类型过滤提示文本
        /// </summary>
        public string TypeFilterHint => TargetType != null
            ? $"已过滤类型: {TargetType.Name}"
            : string.Empty;

        #endregion

        #region 命令

        /// <summary>
        /// 确认选择命令
        /// </summary>
        public ICommand ConfirmCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// 刷新命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 清除选择命令
        /// </summary>
        public ICommand ClearSelectionCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 选择确认事件
        /// </summary>
        public event EventHandler<DataSourceSelectedEventArgs>? SelectionConfirmed;

        /// <summary>
        /// 取消事件
        /// </summary>
        public event EventHandler? Cancelled;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建数据源选择ViewModel
        /// </summary>
        public DataSourcePickerViewModel(
            string parameterName,
            Type? targetType = null,
            IDataSourceQueryService? dataSourceQueryService = null)
        {
            ParameterName = parameterName;
            TargetType = targetType;
            _dataSourceQueryService = dataSourceQueryService;

            Groups = new ObservableCollection<DataSourceGroupViewModel>();
            FilteredGroups = new ObservableCollection<DataSourceGroupViewModel>();

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            RefreshCommand = new RelayCommand(ExecuteRefresh);
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 加载数据源
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        public void LoadDataSources(string nodeId)
        {
            CurrentNodeId = nodeId;
            ExecuteRefresh();
        }

        /// <summary>
        /// 刷新数据源列表
        /// </summary>
        public void Refresh()
        {
            ExecuteRefresh();
        }

        #endregion

        #region 私有方法

        private void ExecuteRefresh()
        {
            if (_dataSourceQueryService == null || string.IsNullOrEmpty(CurrentNodeId))
            {
                StatusMessage = "无法加载数据源: 服务未配置或节点ID为空";
                return;
            }

            IsLoading = true;
            StatusMessage = "正在加载数据源...";

            try
            {
                Groups.Clear();

                // 获取父节点信息
                var parentNodes = _dataSourceQueryService.GetParentNodes(CurrentNodeId);

                foreach (var parent in parentNodes)
                {
                    var group = new DataSourceGroupViewModel
                    {
                        GroupName = parent.NodeName,
                        GroupIcon = parent.NodeIcon,
                        NodeId = parent.NodeId,
                        NodeType = parent.NodeType,
                        ExecutionStatus = parent.ExecutionStatus,
                        HasExecuted = parent.HasExecuted
                    };

                    // 获取输出属性
                    var properties = parent.OutputProperties;

                    // 类型过滤
                    if (TargetType != null)
                    {
                        properties = parent.GetCompatibleProperties(TargetType);
                    }

                    foreach (var prop in properties)
                    {
                        group.DataSources.Add(prop);
                    }

                    if (group.DataSources.Count > 0)
                    {
                        Groups.Add(group);
                    }
                }

                TotalCount = Groups.Sum(g => g.DataSources.Count);
                FilterDataSources();

                StatusMessage = $"已加载 {FilteredCount} 个数据源";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterDataSources()
        {
            FilteredGroups.Clear();

            foreach (var group in Groups)
            {
                var filteredGroup = new DataSourceGroupViewModel
                {
                    GroupName = group.GroupName,
                    GroupIcon = group.GroupIcon,
                    NodeId = group.NodeId,
                    NodeType = group.NodeType,
                    ExecutionStatus = group.ExecutionStatus,
                    HasExecuted = group.HasExecuted
                };

                foreach (var dataSource in group.DataSources)
                {
                    if (string.IsNullOrWhiteSpace(SearchText) ||
                        MatchesSearch(dataSource))
                    {
                        filteredGroup.DataSources.Add(dataSource);
                    }
                }

                if (filteredGroup.DataSources.Count > 0)
                {
                    FilteredGroups.Add(filteredGroup);
                }
            }

            FilteredCount = FilteredGroups.Sum(g => g.DataSources.Count);
            OnPropertyChanged(nameof(FilteredCount));
        }

        private bool MatchesSearch(AvailableDataSource dataSource)
        {
            var searchLower = SearchText.ToLowerInvariant();

            return dataSource.DisplayName.ToLowerInvariant().Contains(searchLower) ||
                   dataSource.PropertyName.ToLowerInvariant().Contains(searchLower) ||
                   dataSource.SourceNodeName.ToLowerInvariant().Contains(searchLower) ||
                   (dataSource.Description?.ToLowerInvariant().Contains(searchLower) ?? false);
        }

        private bool CanConfirm()
        {
            return SelectedDataSource != null;
        }

        private void ExecuteConfirm()
        {
            if (SelectedDataSource != null)
            {
                SelectionConfirmed?.Invoke(this, new DataSourceSelectedEventArgs(SelectedDataSource));
            }
        }

        private void ExecuteCancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteClearSelection()
        {
            SelectedDataSource = null;
            SearchText = string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// 数据源分组ViewModel
    /// </summary>
    public class DataSourceGroupViewModel : ViewModelBase
    {
        private bool _isExpanded = true;

        /// <summary>
        /// 分组名称
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// 分组图标
        /// </summary>
        public string? GroupIcon { get; set; }

        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus ExecutionStatus { get; set; }

        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool HasExecuted { get; set; }

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// 数据源列表
        /// </summary>
        public ObservableCollection<AvailableDataSource> DataSources { get; } = new ObservableCollection<AvailableDataSource>();

        /// <summary>
        /// 数据源数量
        /// </summary>
        public int Count => DataSources.Count;

        /// <summary>
        /// 状态图标
        /// </summary>
        public string StatusIcon => ExecutionStatus switch
        {
            ExecutionStatus.Success => "✓",
            ExecutionStatus.Failed => "✗",
            ExecutionStatus.Running => "⟳",
            _ => "○"
        };

        /// <summary>
        /// 状态颜色
        /// </summary>
        public string StatusColor => ExecutionStatus switch
        {
            ExecutionStatus.Success => "Green",
            ExecutionStatus.Failed => "Red",
            ExecutionStatus.Running => "Orange",
            _ => "Gray"
        };
    }

    /// <summary>
    /// 数据源选择事件参数
    /// </summary>
    public class DataSourceSelectedEventArgs : EventArgs
    {
        public AvailableDataSource SelectedDataSource { get; }

        public DataSourceSelectedEventArgs(AvailableDataSource selectedDataSource)
        {
            SelectedDataSource = selectedDataSource;
        }
    }
}
