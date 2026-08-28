using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Common.Models
{
    /// <summary>
    /// 通用工具项 - 承载工具按钮的全部 UI 元数据（类型无关，业务枚举经 Value 承载）
    /// </summary>
    /// <remarks>
    /// 数据驱动通用工具栏（ToolBarControl）的项模型。
    /// ShapeToolItem / MeasureToolItem 继承本类，仅类型化封装各自的业务枚举。
    /// </remarks>
    public class ToolItem : ObservableObject
    {
        /// <summary>
        /// 中文名称
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

        /// <summary>
        /// 业务枚举值（ShapeType / MeasureGeometryType / 其他），控件不感知具体类型
        /// </summary>
        public object? Value { get; }

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
        /// 是否被选中（高亮状态，由 ToolBarControl 内部管理）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ToolItem(string name, string iconGeometry, object? value,
            Key shortcutKey = Key.None, bool isVisible = true, string? toolTip = null)
        {
            Name = name;
            IconGeometry = iconGeometry;
            Value = value;
            ShortcutKey = shortcutKey;
            IsVisible = isVisible;

            ToolTip = toolTip ?? (shortcutKey == Key.None
                ? name
                : $"{name} ({shortcutKey})");
        }
    }
}
