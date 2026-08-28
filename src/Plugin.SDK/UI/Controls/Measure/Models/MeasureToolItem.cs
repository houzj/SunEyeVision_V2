using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Models;

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
    /// 继承通用 ToolItem，业务枚举 MeasureGeometryType 经 Value 承载（控件类型无关）
    /// </summary>
    public class MeasureToolItem : ToolItem
    {
        /// <summary>
        /// 几何类型（强类型，宿主可直接访问；同时承载于 Value）
        /// </summary>
        public MeasureGeometryType GeometryType { get; }

        public MeasureToolItem(MeasureGeometryType geometryType, string name, string iconGeometry,
            Key shortcutKey = Key.None, bool isVisible = true, string? toolTip = null)
            : base(name, iconGeometry, geometryType, shortcutKey, isVisible, toolTip)
        {
            GeometryType = geometryType;
        }
    }
}
