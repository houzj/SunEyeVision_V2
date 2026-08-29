using System;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models
{
    /// <summary>
    /// 引导几何变更事件参数
    /// </summary>
    /// <remarks>
    /// 携带当前交互模型 + 几何类型 + 是否绘制预览中，宿主据此同步工具参数：
    /// - Line 模式读取模型的 EditingLine
    /// - Circle 模式读取 EditingCircle
    /// - Arc 模式读取 EditingArc
    /// - IsDrawing 供宿主区分"绘制预览中"与"绘制完成/编辑完成"（对齐 Roi 绘制完成后清除工具栏选中）
    /// </remarks>
    public class GuideGeometryChangedEventArgs : EventArgs
    {
        /// <summary>交互状态模型（含当前编辑的引导几何）</summary>
        public MeasureInteractionModel Model { get; }

        /// <summary>当前几何类型</summary>
        public MeasureGeometryType GeometryType { get; }

        /// <summary>是否处于绘制预览中（true=拖动绘制中，false=绘制完成/编辑完成）</summary>
        public bool IsDrawing { get; }

        /// <summary>
        /// 创建引导几何变更事件参数
        /// </summary>
        public GuideGeometryChangedEventArgs(MeasureInteractionModel model, MeasureGeometryType geometryType, bool isDrawing = false)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            GeometryType = geometryType;
            IsDrawing = isDrawing;
        }
    }
}
