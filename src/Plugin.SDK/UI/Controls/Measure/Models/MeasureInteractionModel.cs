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

        /// <summary>选择/编辑模式（绘制完成后，显示手柄可编辑/移动）</summary>
        Select,
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

        /// <summary>当前引导几何是否被选中（显示编辑手柄）</summary>
        public bool IsSelected { get; set; }

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
            IsSelected = false;
        }

        /// <summary>
        /// 取消选中（保留引导几何，仅清除选中态）
        /// </summary>
        public void Deselect()
        {
            IsSelected = false;
        }

        /// <summary>
        /// 更新直线端点（保持类型不变）
        /// </summary>
        /// <param name="start">新起点</param>
        /// <param name="end">新终点</param>
        /// <returns>是否更新成功（无引导直线或端点重合时 false）</returns>
        public bool UpdateLineEndpoints(Point start, Point end)
        {
            if (!EditingLine.HasValue) return false;
            var dir = end - start;
            if (dir.LengthSquared < 1e-20) return false;
            EditingLine = new Line2d(start, dir);
            return true;
        }

        /// <summary>
        /// 更新圆半径（保持圆心与类型不变）
        /// </summary>
        /// <returns>是否更新成功（无引导圆时 false）</returns>
        public bool UpdateCircleRadius(double radius)
        {
            if (!EditingCircle.HasValue) return false;
            var c = EditingCircle.Value;
            EditingCircle = new Circle2d(c.Center, Math.Max(1.0, radius));
            return true;
        }

        /// <summary>
        /// 更新圆弧角度（保持圆心/半径与类型不变）
        /// </summary>
        /// <param name="isStart">true=更新起始角，false=更新终止角</param>
        /// <param name="angle">新角度（弧度）</param>
        /// <returns>是否更新成功（无引导圆弧时 false）</returns>
        public bool UpdateArcAngle(bool isStart, double angle)
        {
            if (!EditingArc.HasValue || EditingArc.Value.IsEmpty) return false;
            var a = EditingArc.Value;
            var newStart = isStart ? angle : a.StartAngle;
            var newEnd = isStart ? a.EndAngle : angle;
            if (Math.Abs(newEnd - newStart) < 1e-6) return false;
            EditingArc = new Arc2d(a.Center, a.Radius, newStart, newEnd);
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
