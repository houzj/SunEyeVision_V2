using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models
{
    /// <summary>
    /// 节点输出信息 - 用于树状选择器显示
    /// </summary>
    public class NodeOutputInfo : ObservableObject
    {
        private object? _currentValue;
        private bool _isExpanded;
        private bool _isSelected;

        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点名称（显示名称）
        /// </summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// 输出端口名称
        /// </summary>
        public string OutputName { get; set; } = string.Empty;

        /// <summary>
        /// 属性路径（用于复合对象属性访问）
        /// </summary>
        public string? PropertyPath { get; set; }

        /// <summary>
        /// 数据类型（如 "double", "RoiData", "Point2D"）
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// 是否匹配目标数据类型
        /// </summary>
        public bool IsTypeMatched { get; set; }

        /// <summary>
        /// 子节点（用于复合对象展开）
        /// </summary>
        public List<NodeOutputInfo> Children { get; set; } = new();

        /// <summary>
        /// 当前值（实时更新）
        /// </summary>
        public object? CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        /// <summary>
        /// 显示路径（用于参数绑定显示）
        /// </summary>
        public string DisplayPath => string.IsNullOrEmpty(PropertyPath)
            ? $"{NodeName}.{OutputName}"
            : $"{NodeName}.{OutputName}.{PropertyPath}";

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 层级深度
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// 节点是否已执行（有实际输出数据）
        /// </summary>
        public bool HasExecuted { get; set; } = true;

        /// <summary>
        /// 状态描述（用于UI显示执行状态）
        /// </summary>
        public string StatusDescription => HasExecuted ? "已执行" : "未执行";
    }
}
