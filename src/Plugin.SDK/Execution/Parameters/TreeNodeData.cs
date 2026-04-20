using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 树节点数据模型
    /// </summary>
    /// <remarks>
    /// 用于在 TreeView 控件中显示层级结构的数据源。
    /// 
    /// 树结构示例：
    /// - 节点名称（不可选择）
    ///   - 分组名称（不可选择）
    ///     - 可选择属性（可选择）
    /// </remarks>
    public class TreeNodeData : INotifyPropertyChanged
    {
        private bool _isExpanded = false;

        /// <summary>
        /// 父节点引用
        /// </summary>
        public TreeNodeData? Parent { get; set; }

        /// <summary>
        /// 节点文本（显示在树中）
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 英文绑定路径片段（用于运行时取值）
        /// </summary>
        /// <remarks>
        /// 叶子节点: "ThresholdUsed"（简单属性）或 "CircleFound.Radius"（嵌套属性）
        /// 分组节点/根节点: string.Empty（无绑定路径，不可选）
        /// </remarks>
        public string BindingPath { get; set; } = string.Empty;

        /// <summary>
        /// 关联的数据源（仅叶子节点有值）
        /// </summary>
        public AvailableDataSource? DataSource { get; set; }

        /// <summary>
        /// 是否可选择
        /// </summary>
        /// <remarks>
        /// true: 可以选择的叶子节点
        /// false: 用于分组的父节点
        /// </remarks>
        public bool IsSelectable { get; set; }

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        /// <summary>
        /// 子节点
        /// </summary>
        public ObservableCollection<TreeNodeData> Children { get; set; } = new ObservableCollection<TreeNodeData>();

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 属性变更通知
        /// </summary>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 创建可选择的数据源节点
        /// </summary>
        public static TreeNodeData CreateDataSourceNode(AvailableDataSource dataSource)
        {
            return new TreeNodeData
            {
                Text = dataSource.DisplayName,
                DataSource = dataSource,
                IsSelectable = true
            };
        }

        /// <summary>
        /// 创建分组节点
        /// </summary>
        public static TreeNodeData CreateGroupNode(string text)
        {
            return new TreeNodeData
            {
                Text = text,
                IsSelectable = false
            };
        }
    }
}
