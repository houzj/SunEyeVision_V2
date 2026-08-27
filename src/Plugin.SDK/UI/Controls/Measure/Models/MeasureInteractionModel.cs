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

        /// <summary>引导圆弧（Arc 模式，由三点/两点派生）</summary>
        public Arc2d? EditingArc { get; private set; }

        /// <summary>圆弧起点（端点+半径参数化）</summary>
        public Point ArcPoint1 { get; private set; }

        /// <summary>圆弧终点（端点+半径参数化）</summary>
        public Point ArcPoint2 { get; private set; }

        /// <summary>圆弧半径（端点+半径参数化）</summary>
        public double ArcRadius { get; private set; }

        /// <summary>圆弧方向（true=顺时针，false=逆时针）</summary>
        public bool ArcClockwise { get; private set; }

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
            if (!arc.IsEmpty)
            {
                // 从弧端点同步两点+半径参数
                ArcPoint1 = arc.GetPointOnArc(arc.StartAngle);
                ArcPoint2 = arc.GetPointOnArc(arc.EndAngle);
                ArcRadius = arc.Radius;
                ArcClockwise = arc.AngularSpan < 0;
            }
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
        /// 从起点 + 终点创建圆弧（默认半径 = 弦长）
        /// </summary>
        /// <returns>是否创建成功（两点重合时 false）</returns>
        public bool SetArcFromEndpoints(Point point1, Point point2, bool clockwise = false)
        {
            ArcPoint1 = point1;
            ArcPoint2 = point2;
            ArcRadius = (point2 - point1).Length;
            ArcClockwise = clockwise;
            var arc = Arc2d.FromTwoPointsDefault(point1, point2, clockwise);
            if (arc.IsEmpty) return false;
            EditingArc = arc;
            EditingLine = null;
            EditingCircle = null;
            return true;
        }

        /// <summary>
        /// 更新圆弧端点（保持另一端 + 半径不变，圆心重算）
        /// </summary>
        /// <returns>是否更新成功（无圆弧或退化时 false）</returns>
        public bool UpdateArcEndpoint(bool isStart, Point point)
        {
            if (!EditingArc.HasValue) return false;
            if (isStart) ArcPoint1 = point; else ArcPoint2 = point;
            var arc = Arc2d.FromEndpointsAndRadius(ArcPoint1, ArcPoint2, ArcRadius, ArcClockwise);
            if (arc.IsEmpty) return false;
            EditingArc = arc;
            return true;
        }

        /// <summary>
        /// 更新圆弧半径（保持两端点不变，圆心沿中垂线滑动）
        /// </summary>
        /// <returns>是否更新成功（无圆弧或半径过小 false）</returns>
        public bool UpdateArcRadius(double radius)
        {
            if (!EditingArc.HasValue) return false;
            ArcRadius = radius;
            var arc = Arc2d.FromEndpointsAndRadius(ArcPoint1, ArcPoint2, radius, ArcClockwise);
            if (arc.IsEmpty) return false;
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
                // 用端点平移重算，保持半径/方向
                ArcPoint1 += delta;
                ArcPoint2 += delta;
                EditingArc = Arc2d.FromEndpointsAndRadius(ArcPoint1, ArcPoint2, ArcRadius, ArcClockwise);
            }
        }
    }
}
