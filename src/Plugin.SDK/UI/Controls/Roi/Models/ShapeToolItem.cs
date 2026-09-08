using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Models;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models
{
    /// <summary>
    /// 形状工具项 - 承载形状按钮的全部 UI 元数据
    /// 继承通用 ToolItem，业务枚举 ShapeType 经 Value 承载（控件类型无关）
    /// </summary>
    public class ShapeToolItem : ToolItem
    {
        /// <summary>
        /// 形状类型（强类型，宿主可直接访问；同时承载于 Value）
        /// </summary>
        public ShapeType ShapeType { get; }

        public ShapeToolItem(ShapeType shapeType, string name, string iconGeometry,
            Key shortcutKey = Key.None, bool isVisible = true, string? toolTip = null)
            : base(name, iconGeometry, shapeType, shortcutKey, isVisible, toolTip)
        {
            ShapeType = shapeType;
        }
    }
}
