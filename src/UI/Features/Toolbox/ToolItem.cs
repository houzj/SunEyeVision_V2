using System;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 工具箱项模型
    /// </summary>
    public class ToolItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public string ToolId { get; set; }
        public string AlgorithmType { get; set; }
        public bool HasDebugInterface { get; set; }

        public ToolItem(string name, string category, string icon, string description, string algorithmType = null, bool hasDebugInterface = true)
        {
            Name = name;
            Category = category;
            Icon = icon;
            Description = description;
            ToolId = algorithmType; // 向后兼容：如果没有显式设置ToolId，使用algorithmType
            AlgorithmType = algorithmType;
            HasDebugInterface = hasDebugInterface;
        }
    }

    /// <summary>
    /// 工具箱分类模块?
    /// </summary>
    public class ToolCategory : ObservableObject
    {
        private bool _isExpanded;
        private bool _isSelected;
        private System.Collections.ObjectModel.ObservableCollection<ToolItem>? _tools;

        public string Name { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public int ToolCount { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<ToolItem>? Tools
        {
            get => _tools;
            set => SetProperty(ref _tools, value);
        }

        public ToolCategory(string name, string icon, string description, int toolCount = 0, bool isExpanded = true)
        {
            Name = name;
            Icon = icon;
            Description = description;
            ToolCount = toolCount;
            IsExpanded = isExpanded;
            IsSelected = false;
        }
    }
}
