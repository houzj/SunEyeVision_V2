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

        /// <summary>引导直线起点（端点参数化，保留线段长度）</summary>
        public Point LineStartPoint { get; private set; }

        /// <summary>引导直线终点（端点参数化，保留线段长度）</summary>
        public Point LineEndPoint { get; private set; }

        /// <summary>引导圆（Circle 模式，编辑中）</summary>
        public Circle2d? EditingCircle { get; private set; }

        /// <summary>引导圆弧（Arc 模式，由三点/两点派生）</summary>
        public Arc2d? EditingArc { get; private set; }

        /// <summary>圆弧起点（三点定弧）</summary>
        public Point ArcPoint1 { get; private set; }

        /// <summary>圆弧终点（三点定弧）</summary>
        public Point ArcPoint2 { get; private set; }

        /// <summary>圆弧上一点（三点定弧，决定弯曲方向与半径）</summary>
        public Point ArcPoint3 { get; private set; }

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
        /// 设置编辑中的引导直线（自动清空其他类型，同步端点参数）
        /// </summary>
        public void SetLine(Line2d line, Point? start = null, Point? end = null)
        {
            EditingLine = line;
            EditingCircle = null;
            EditingArc = null;
            if (start.HasValue && end.HasValue)
            {
                LineStartPoint = start.Value;
                LineEndPoint = end.Value;
            }
            else
            {
                // 无显式端点时用固定默认长度（仅回填场景）
                LineStartPoint = line.Point;
                LineEndPoint = line.Point + line.Direction * 200.0;
            }
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
                // 从弧端点派生三点（P3 = 弧中点）
                ArcPoint1 = arc.GetPointOnArc(arc.StartAngle);
                ArcPoint2 = arc.GetPointOnArc(arc.EndAngle);
                ArcPoint3 = arc.GetPointOnArc(arc.StartAngle + arc.AngularSpan / 2);
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
            LineStartPoint = start;
            LineEndPoint = end;
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
        /// 从起点 + 终点创建圆弧（默认生成 P3 = 弧中点）
        /// </summary>
        /// <returns>是否创建成功（两点重合时 false）</returns>
        public bool SetArcFromEndpoints(Point point1, Point point2)
        {
            ArcPoint1 = point1;
            ArcPoint2 = point2;
            // 用默认半径 = 弦长（60° 弧）生成弧，P3 取弧中点
            var baseArc = Arc2d.FromTwoPointsDefault(point1, point2);
            if (baseArc.IsEmpty) return false;
            ArcPoint3 = baseArc.GetPointOnArc(baseArc.StartAngle + baseArc.AngularSpan / 2);
            var arc = Arc2d.FromThreePoints(point1, point2, ArcPoint3);
            if (arc.IsEmpty) return false;
            EditingArc = arc;
            EditingLine = null;
            EditingCircle = null;
            return true;
        }

        /// <summary>
        /// 更新圆弧端点（P1/P2 移动，P3 不动，三点定圆重算圆心/弧长）
        /// </summary>
        /// <returns>是否更新成功（无圆弧或三点共线时 false）</returns>
        public bool UpdateArcEndpoint(bool isStart, Point point)
        {
            if (!EditingArc.HasValue || EditingArc.Value.IsEmpty) return false;
            if (isStart) ArcPoint1 = point; else ArcPoint2 = point;
            var arc = Arc2d.FromThreePoints(ArcPoint1, ArcPoint2, ArcPoint3);
            if (arc.IsEmpty) return false;
            EditingArc = arc;
            return true;
        }

        /// <summary>
        /// 更新圆弧上点 P3（P3 移动，P1/P2 不动，三点定圆重算圆心/半径/弧长）
        /// </summary>
        /// <returns>是否更新成功（无圆弧或三点共线时 false）</returns>
        public bool UpdateArcPoint3(Point point)
        {
            if (!EditingArc.HasValue || EditingArc.Value.IsEmpty) return false;
            ArcPoint3 = point;
            var arc = Arc2d.FromThreePoints(ArcPoint1, ArcPoint2, ArcPoint3);
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
                // 三点整体平移
                ArcPoint1 += delta;
                ArcPoint2 += delta;
                ArcPoint3 += delta;
                EditingArc = Arc2d.FromThreePoints(ArcPoint1, ArcPoint2, ArcPoint3);
            }
        }
    }
}
