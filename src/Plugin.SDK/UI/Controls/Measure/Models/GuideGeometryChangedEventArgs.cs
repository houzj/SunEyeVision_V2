using System;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models
{
    /// <summary>
    /// 引导几何变更事件参数
    /// </summary>
    /// <remarks>
    /// 携带当前交互模型 + 几何类型，宿主据此同步工具参数：
    /// - Line 模式读取模型的 EditingLine
    /// - Circle 模式读取 EditingCircle
    /// - Arc 模式读取 EditingArc
    /// </remarks>
    public class GuideGeometryChangedEventArgs : EventArgs
    {
        /// <summary>交互状态模型（含当前编辑的引导几何）</summary>
        public MeasureInteractionModel Model { get; }

        /// <summary>当前几何类型</summary>
        public MeasureGeometryType GeometryType { get; }

        /// <summary>
        /// 创建引导几何变更事件参数
        /// </summary>
        public GuideGeometryChangedEventArgs(MeasureInteractionModel model, MeasureGeometryType geometryType)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            GeometryType = geometryType;
        }
    }
}
