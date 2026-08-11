using System.Windows;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 属性项
    /// </summary>
    public class PropertyItem
    {
        /// <summary>
        /// 显示标签
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 属性值
        /// </summary>
        public object Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// 属性分组
    /// </summary>
    public class PropertyGroup
    {
        /// <summary>
        /// 分组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// 参数列表
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<PropertyItem> Parameters { get; set; } =
            new System.Collections.ObjectModel.ObservableCollection<PropertyItem>();
    }
}
