using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 显示项配置
    /// </summary>
    /// <remarks>
    /// 用于配置视觉显示项的可见性、颜色、透明度、粗细等属性。
    /// 支持OK和NG两种状态的颜色配置。
    /// </remarks>
    public class DisplayItemConfig : ObservableObject
    {
        private string _name = "";
        /// <summary>
        /// 显示项名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, "名称");
        }

        private string _category = "";
        /// <summary>
        /// 分类名称
        /// </summary>
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value, "分类");
        }

        private bool _isVisible = true;
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value, "可见性");
        }

        private Color _okColor = Color.FromRgb(0, 255, 0);
        /// <summary>
        /// OK状态颜色
        /// </summary>
        public Color OkColor
        {
            get => _okColor;
            set => SetProperty(ref _okColor, value, "OK颜色");
        }

        private Color _ngColor = Color.FromRgb(255, 0, 0);
        /// <summary>
        /// NG状态颜色
        /// </summary>
        public Color NgColor
        {
            get => _ngColor;
            set => SetProperty(ref _ngColor, value, "NG颜色");
        }

        private double _opacity = 1.0;
        /// <summary>
        /// 透明度(0-1)
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value, "透明度");
        }

        private double _thickness = 1.0;
        /// <summary>
        /// 线条粗细
        /// </summary>
        public double Thickness
        {
            get => _thickness;
            set => SetProperty(ref _thickness, value, "线条粗细");
        }

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public DisplayItemConfig Clone()
        {
            return new DisplayItemConfig
            {
                Name = _name,
                Category = _category,
                IsVisible = _isVisible,
                OkColor = _okColor,
                NgColor = _ngColor,
                Opacity = _opacity,
                Thickness = _thickness
            };
        }
    }
}
