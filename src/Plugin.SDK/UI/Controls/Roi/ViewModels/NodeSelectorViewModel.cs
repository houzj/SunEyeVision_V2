using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels
{
    /// <summary>
    /// 节点选择器视图模型
    /// </summary>
    public class NodeSelectorViewModel : ObservableObject, IDisposable
    {
        private readonly DataSourceQueryService? _dataProvider;
        private string _searchText = string.Empty;
        private NodeOutputInfo? _selectedItem;
        private string? _targetDataType;
        private bool _isPopupOpen;

        /// <summary>
        /// 节点输出树（原始数据）
        /// </summary>
        public ObservableCollection<NodeOutputInfo> AllNodes { get; } = new();

        /// <summary>
        /// 过滤后的节点输出树
        /// </summary>
        public ObservableCollection<NodeOutputInfo> FilteredNodes { get; } = new();

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
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 选中的项
        /// </summary>
        public NodeOutputInfo? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(SelectionDisplayPath));
                    (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 目标数据类型
        /// </summary>
        public string? TargetDataType
        {
            get => _targetDataType;
            set
            {
                if (SetProperty(ref _targetDataType, value))
                {
                    RefreshNodes();
                }
            }
        }

        /// <summary>
        /// Popup是否打开
        /// </summary>
        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set => SetProperty(ref _isPopupOpen, value);
        }

        /// <summary>
        /// 是否有选中项
        /// </summary>
        public bool HasSelection => SelectedItem != null;

        /// <summary>
        /// 选中项显示路径
        /// </summary>
        public string SelectionDisplayPath => SelectedItem?.DisplayPath ?? "未选择";

        /// <summary>
        /// 命令
        /// </summary>
        public ICommand OpenPopupCommand { get; }
        public ICommand ClosePopupCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 选择确认事件
        /// </summary>
        public event EventHandler<NodeOutputInfo?>? SelectionConfirmed;

        public NodeSelectorViewModel(DataSourceQueryService? dataProvider = null)
        {
            _dataProvider = dataProvider;

            OpenPopupCommand = new RelayCommand(() => IsPopupOpen = true);
            ClosePopupCommand = new RelayCommand(() => IsPopupOpen = false);
            SelectCommand = new RelayCommand(ConfirmSelection, () => SelectedItem != null && SelectedItem.IsTypeMatched);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
            RefreshCommand = new RelayCommand(RefreshNodes);
        }

        /// <summary>
        /// 刷新节点
        /// </summary>
        public void RefreshNodes()
        {
            if (_dataProvider == null) return;

            AllNodes.Clear();

            // 获取当前节点ID作为上下文
            var currentNodeId = _dataProvider.CurrentNodeId ?? "";
            var targetTypes = string.IsNullOrEmpty(_targetDataType) ? null : Type.GetType(_targetDataType);

            var dataSources = _dataProvider.GetAvailableDataSources(currentNodeId, targetTypes);

            foreach (var dataSource in dataSources)
            {
                var nodeOutput = new NodeOutputInfo
                {
                    NodeId = dataSource.SourceNodeId,
                    NodeName = dataSource.SourceNodeName,
                    DataType = dataSource.PropertyType.Name,
                    OutputName = dataSource.PropertyName,
                    PropertyPath = dataSource.PropertyName,
                    // DisplayPath 是只读属性，根据 PropertyPath 自动计算
                    CurrentValue = dataSource.CurrentValue,
                    IsTypeMatched = true
                };

                AllNodes.Add(nodeOutput);
            }

            ApplyFilter();
        }

        /// <summary>
        /// 应用过滤器
        /// </summary>
        private void ApplyFilter()
        {
            FilteredNodes.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var node in AllNodes)
                {
                    FilteredNodes.Add(node);
                }
            }
            else
            {
                foreach (var node in AllNodes)
                {
                    var filteredNode = FilterNode(node, SearchText.ToLower());
                    if (filteredNode != null)
                    {
                        FilteredNodes.Add(filteredNode);
                    }
                }
            }
        }

        /// <summary>
        /// 过滤节点（递归）
        /// </summary>
        private NodeOutputInfo? FilterNode(NodeOutputInfo node, string searchLower)
        {
            var match = node.NodeName.ToLower().Contains(searchLower) ||
                        node.OutputName.ToLower().Contains(searchLower);

            var filteredChildren = node.Children
                .Select(child => FilterNode(child, searchLower))
                .Where(child => child != null)
                .Cast<NodeOutputInfo>()
                .ToList();

            if (match || filteredChildren.Count > 0)
            {
                var result = new NodeOutputInfo
                {
                    NodeId = node.NodeId,
                    NodeName = node.NodeName,
                    OutputName = node.OutputName,
                    PropertyPath = node.PropertyPath,
                    DataType = node.DataType,
                    IsTypeMatched = node.IsTypeMatched,
                    CurrentValue = node.CurrentValue,
                    Depth = node.Depth,
                    Children = filteredChildren
                };
                return result;
            }

            return null;
        }

        /// <summary>
        /// 确认选择
        /// </summary>
        private void ConfirmSelection()
        {
            if (SelectedItem != null && SelectedItem.IsTypeMatched)
            {
                SelectionConfirmed?.Invoke(this, SelectedItem);
                IsPopupOpen = false;
            }
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        private void ClearSelection()
        {
            SelectedItem = null;
            SelectionConfirmed?.Invoke(this, null);
            IsPopupOpen = false;
        }

        /// <summary>
        /// 设置选中项（从 ParamSetting 绑定路径恢复；null=清除选择）
        /// </summary>
        public void SetSelectionFromBinding(ParamSetting? binding)
        {
            if (binding == null)
            {
                SelectedItem = null;
                return;
            }

            SelectedItem = FindNodeByBinding(binding);
        }

        /// <summary>按绑定路径查找对应节点</summary>
        private NodeOutputInfo? FindNodeByBinding(ParamSetting binding)
        {
            return FindNodeRecursive(AllNodes, binding);
        }

        private NodeOutputInfo? FindNodeRecursive(ObservableCollection<NodeOutputInfo> nodes, ParamSetting binding)
        {
            foreach (var node in nodes)
            {
                if (node.NodeId == binding.SourceNodeId &&
                    node.OutputName == binding.SourceProperty)
                {
                    return node;
                }

                var found = FindNodeRecursive(
                    new ObservableCollection<NodeOutputInfo>(node.Children),
                    binding);
                if (found != null) return found;
            }
            return null;
        }

        public void Dispose()
        {
            AllNodes.Clear();
            FilteredNodes.Clear();
        }
    }
}

