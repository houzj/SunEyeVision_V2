using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 文本样式 - SDK级文本样式配置（持久化，UI双向绑定，全链路共享同一实例）
    /// </summary>
    public class TextStyle : ObservableObject
    {
        private string _name = "";
        private bool _isVisible = true;
        private Color _okColor = Color.FromRgb(0, 255, 0);
        private Color _ngColor = Color.FromRgb(255, 0, 0);
        private double _opacity = 1.0;
        private double _fontSize = 14.0;
        private bool _isBold;
        private double _positionX = 10;
        private double _positionY = 10;

        /// <summary>样式名称</summary>
        public string Name { get => _name; set => SetProperty(ref _name, value, "名称"); }

        /// <summary>是否可见</summary>
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value, "可见性"); }

        /// <summary>OK状态文本颜色</summary>
        public Color OkColor { get => _okColor; set => SetProperty(ref _okColor, value, "OK颜色"); }

        /// <summary>NG状态文本颜色</summary>
        public Color NgColor { get => _ngColor; set => SetProperty(ref _ngColor, value, "NG颜色"); }

        /// <summary>透明度(0-1)，渲染时通过 PushOpacity 生效</summary>
        public double Opacity { get => _opacity; set => SetProperty(ref _opacity, value, "透明度"); }

        /// <summary>字号</summary>
        public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value, "字号"); }

        /// <summary>是否加粗</summary>
        public bool IsBold { get => _isBold; set => SetProperty(ref _isBold, value, "加粗"); }

        /// <summary>X坐标位置（基准位置或ParamBinding动态值）</summary>
        public double PositionX { get => _positionX; set => SetProperty(ref _positionX, value, "X坐标"); }

        /// <summary>Y坐标位置（基准位置或ParamBinding动态值）</summary>
        public double PositionY { get => _positionY; set => SetProperty(ref _positionY, value, "Y坐标"); }

        /// <summary>获取指定状态的有效颜色</summary>
        public Color GetEffectiveColor(bool isOk) => isOk ? OkColor : NgColor;

        /// <summary>获取指定状态的有效颜色（ARGB uint 格式，用于渲染层）</summary>
        public uint GetEffectiveColorArgb(bool isOk)
        {
            var c = GetEffectiveColor(isOk);
            return (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B);
        }
    }
}
