using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions
{
    /// <summary>
    /// 引导几何交互策略接口 - 每种引导几何（直线/圆/圆弧）的完整交互行为
    /// </summary>
    /// <remarks>
    /// 将几何专属的交互逻辑（绘制预览/完成/手柄编辑/手柄创建/主体命中）从编辑器解耦。
    /// 编辑器只做状态机编排与跨几何共享逻辑（鼠标路由、卡尺带命中、渲染），
    /// 几何专属行为下沉到策略实现，加新几何只需新增一个策略类。
    /// 对齐 Halcon "统一事件驱动 + IDrawingStrategy 类型策略" 模式。
    /// </remarks>
    public interface IGuideGeometryInteraction
    {
        /// <summary>几何类型</summary>
        MeasureGeometryType GeometryType { get; }

        /// <summary>
        /// 更新绘制预览（拖动中实时更新引导几何）
        /// </summary>
        /// <param name="model">交互状态模型</param>
        /// <param name="start">绘制起点（按下位置）</param>
        /// <param name="current">当前鼠标位置</param>
        /// <returns>是否产生有效预览</returns>
        bool UpdatePreview(MeasureInteractionModel model, Point start, Point current);

        /// <summary>
        /// 完成绘制（防退化校验 + 写入模型）
        /// </summary>
        /// <returns>是否创建成功（退化时 false）</returns>
        bool Complete(MeasureInteractionModel model, Point start, Point current);

        /// <summary>
        /// 手柄编辑（拖动手柄更新引导几何）
        /// </summary>
        /// <returns>是否更新成功</returns>
        bool EditHandle(MeasureInteractionModel model, HandleType handleType, Point current);

        /// <summary>
        /// 创建编辑手柄（按几何类型与状态）
        /// </summary>
        EditHandle[] CreateHandles(MeasureInteractionModel model, double zoom);

        /// <summary>
        /// 主体命中测试（引导线/圆/弧附近）
        /// </summary>
        /// <param name="model">交互状态模型</param>
        /// <param name="point">测试点（图像坐标）</param>
        /// <param name="tolerance">命中容差（像素）</param>
        /// <returns>是否命中引导主体</returns>
        bool HitTestGuide(MeasureInteractionModel model, Point point, double tolerance);
    }
}
