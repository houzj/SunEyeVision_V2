using System;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Shell.Controls.Toolbox
{
    /// <summary>
    /// 工具箱项模型
    /// </summary>
    public class ToolItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        public string? CatalogIcon { get; init; }
        public string IconSource => CatalogIcon != null ? "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/" + CatalogIcon : "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/tool-" + Icon + ".png";
        public string Description { get; set; }
        public string ToolId { get; set; }
        public bool IsAvailable { get; init; } = true;
        public string? LoadError { get; init; }
        public string ToolTip => IsAvailable ? Name : Name + "：" + LoadError;

        public ToolItem(string name, string category, string icon, string description, string toolId)
        {
            Name = name;
            Category = category;
            Icon = icon;
            Description = description;
            ToolId = toolId;
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
        public string HoverIcon { get; set; } = string.Empty;
        public string? CatalogIcon { get; init; }
        public string IconSource => CatalogIcon != null ? "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/" + CatalogIcon : "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/" + (IsSelected ? HoverIcon : Icon);
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
            set { if(SetProperty(ref _isSelected,value)) OnPropertyChanged(nameof(IconSource)); }
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
