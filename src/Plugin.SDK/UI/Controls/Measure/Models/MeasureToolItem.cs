using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models
{
    /// <summary>
    /// 检测几何类型（引导对象类型）
    /// </summary>
    public enum MeasureGeometryType
    {
        /// <summary>直线查找</summary>
        Line,

        /// <summary>圆查找</summary>
        Circle,

        /// <summary>圆弧查找</summary>
        Arc,
    }

    /// <summary>
    /// 测量工具项 - 承载检测引导几何按钮的全部 UI 元数据
    /// </summary>
    /// <remarks>
    /// 数据驱动测量工具栏（对齐 ShapeToolItem 模式），每个几何类型按钮的可见性/启用/选中态可单独控制。
    /// </remarks>
    public class MeasureToolItem : ObservableObject
    {
        /// <summary>
        /// 几何类型
        /// </summary>
        public MeasureGeometryType GeometryType { get; }

        /// <summary>
        /// 中文名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 工具提示（含快捷键）
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// 矢量图标 Path.Data 几何字符串（带卡尺特征，与区域编辑图标区分）
        /// </summary>
        public string IconGeometry { get; }

        /// <summary>
        /// 快捷键
        /// </summary>
        public Key ShortcutKey { get; }

        private bool _isVisible = true;

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private bool _isEnabled = true;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSelected;

        /// <summary>
        /// 是否被选中（高亮状态，由 MeasureToolBar 内部管理）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public MeasureToolItem(MeasureGeometryType geometryType, string name, string iconGeometry,
            Key shortcutKey = Key.None, bool isVisible = true, string? toolTip = null)
        {
            GeometryType = geometryType;
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
