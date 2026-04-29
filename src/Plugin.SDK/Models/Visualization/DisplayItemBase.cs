using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 显示项基类 - 图形/文本显示项的抽象公共基类
    /// </summary>
    public abstract class DisplayItemBase : ObservableObject
    {
        private bool _isVisible = true;

        /// <summary>显示项名称（控制寻址标识符）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>是否可见（UI绑定，变更自动触发重绘）</summary>
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value, "可见性"); }

        /// <summary>Z顺序（渲染排序）</summary>
        public int ZOrder { get; set; }

        /// <summary>通知属性变更（供外部订阅者触发重绘）</summary>
        public void NotifyChanged() => OnPropertyChanged("");
    }
}
