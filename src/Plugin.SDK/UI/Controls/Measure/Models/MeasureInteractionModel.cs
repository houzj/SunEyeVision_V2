using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models
{
    /// <summary>
    /// 测量交互模式
    /// </summary>
    public enum MeasureInteractionMode
    {
        /// <summary>空闲（无引导几何）</summary>
        None,

        /// <summary>绘制模式（在图像上拖动创建引导几何）</summary>
        Draw,

        /// <summary>编辑模式（移动/旋转/缩放引导几何）</summary>
        Edit,
    }

    /// <summary>
    /// 测量交互宿主 - 承载引导几何的当前状态与编辑事务
    /// </summary>
    /// <remarks>
    /// 对齐 RoiEditorControl 的"宿主状态机"模式：
    /// - 维护当前交互模式（绘制/编辑）
    /// - 承载当前编辑的引导几何（Line/Circle/Arc 三态互斥）
    /// - 提供绘制/编辑的纯数据操作，供交互控件调用
    /// </remarks>
    public class MeasureInteractionModel
    {
        /// <summary>当前交互模式</summary>
        public MeasureInteractionMode Mode { get; set; } = MeasureInteractionMode.None;

        /// <summary>引导直线（Line 模式，编辑中）</summary>
        public Line2d? EditingLine { get; private set; }

        /// <summary>引导圆（Circle 模式，编辑中）</summary>
        public Circle2d? EditingCircle { get; private set; }

        /// <summary>引导圆弧（Arc 模式，编辑中）</summary>
        public Arc2d? EditingArc { get; private set; }

        /// <summary>是否正处于绘制过程中（按下未松开）</summary>
        public bool IsDrawing { get; set; }

        /// <summary>绘制起点</summary>
        public Point DrawStart { get; set; }

        /// <summary>是否拥有有效引导几何</summary>
        public bool HasGuide
        {
            get
            {
                if (EditingLine.HasValue) return true;
                if (EditingCircle.HasValue) return true;
                return EditingArc.HasValue && !EditingArc.Value.IsEmpty;
            }
        }

        /// <summary>
        /// 设置编辑中的引导直线（自动清空其他类型）
        /// </summary>
        public void SetLine(Line2d line)
        {
            EditingLine = line;
            EditingCircle = null;
            EditingArc = null;
        }

        /// <summary>
        /// 设置编辑中的引导圆（自动清空其他类型）
        /// </summary>
        public void SetCircle(Circle2d circle)
        {
            EditingCircle = circle;
            EditingLine = null;
            EditingArc = null;
        }

        /// <summary>
        /// 设置编辑中的引导圆弧（自动清空其他类型）
        /// </summary>
        public void SetArc(Arc2d arc)
        {
            EditingArc = arc;
            EditingLine = null;
            EditingCircle = null;
        }

        /// <summary>
        /// 清除全部引导几何
        /// </summary>
        public void Clear()
        {
            EditingLine = null;
            EditingCircle = null;
            EditingArc = null;
            IsDrawing = false;
        }

        /// <summary>
        /// 更新当前编辑中的直线（保持类型不变）
        /// </summary>
        public bool TryUpdateLine(Line2d line)
        {
            if (!EditingLine.HasValue) return false;
            EditingLine = line;
            return true;
        }

        /// <summary>
        /// 更新当前编辑中的圆（保持类型不变）
        /// </summary>
        public bool TryUpdateCircle(Circle2d circle)
        {
            if (!EditingCircle.HasValue) return false;
            EditingCircle = circle;
            return true;
        }

        /// <summary>
        /// 更新当前编辑中的圆弧（保持类型不变）
        /// </summary>
        public bool TryUpdateArc(Arc2d arc)
        {
            if (!EditingArc.HasValue) return false;
            EditingArc = arc;
            return true;
        }

        /// <summary>
        /// 整体平移当前引导几何
        /// </summary>
        public void Translate(Vector delta)
        {
            if (EditingLine.HasValue)
            {
                EditingLine = new Line2d(EditingLine.Value.Point + delta, EditingLine.Value.Direction);
            }
            else if (EditingCircle.HasValue)
            {
                var c = EditingCircle.Value;
                EditingCircle = new Circle2d(c.Center + delta, c.Radius);
            }
            else if (EditingArc.HasValue && !EditingArc.Value.IsEmpty)
            {
                var a = EditingArc.Value;
                EditingArc = new Arc2d(a.Center + delta, a.Radius, a.StartAngle, a.EndAngle);
            }
        }
    }
}
