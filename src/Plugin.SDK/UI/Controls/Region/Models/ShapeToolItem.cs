using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Models
{
    /// <summary>
    /// 形状工具项 - 承载形状按钮的全部 UI 元数据
    /// 可用于数据驱动的形状工具栏，每个形状的可见性/启用状态可单独控制
    /// </summary>
    public class ShapeToolItem : ObservableObject
    {
        public ShapeType ShapeType { get; }

        /// <summary>
        /// 形状中文名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 工具提示（含快捷键）
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// 矢量图标 Path.Data 几何字符串
        /// </summary>
        public string IconGeometry { get; }

        /// <summary>
        /// 快捷键
        /// </summary>
        public Key ShortcutKey { get; }

        private bool _isVisible = true;

        /// <summary>
        /// 是否可见（可单独控制）
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private bool _isEnabled = true;

        /// <summary>
        /// 是否启用（可单独控制）
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSelected;

        /// <summary>
        /// 是否被选中（高亮状态，由 ShapeToolBar 内部管理）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ShapeToolItem(ShapeType shapeType, string name, string iconGeometry,
            Key shortcutKey = Key.None, bool isVisible = true, string? toolTip = null)
        {
            ShapeType = shapeType;
            Name = name;
            IconGeometry = iconGeometry;
            ShortcutKey = shortcutKey;
            IsVisible = isVisible;

            ToolTip = toolTip ?? (shortcutKey == Key.None
                ? name
                : $"{name} ({shortcutKey})");
        }
    }
}
