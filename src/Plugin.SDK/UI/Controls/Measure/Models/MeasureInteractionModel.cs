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

        /// <summary>圆弧起点（两端点+半径+方向参数化）</summary>
        public Point ArcPoint1 { get; private set; }

        /// <summary>圆弧终点（两端点+半径+方向参数化）</summary>
        public Point ArcPoint2 { get; private set; }

        /// <summary>圆弧半径（两端点+半径+方向参数化）</summary>
        public double ArcRadius { get; private set; }

        /// <summary>圆弧方向（true=顺时针，false=逆时针）</summary>
        public bool ArcClockwise { get; private set; }

        /// <summary>圆弧大小弧状态（true=大弧（张角 &gt; 180°），false=小弧）</summary>
        public bool ArcMajorArc { get; private set; }

        /// <summary>是否正处于绘制过程中（按下未松开）</summary>
        public bool IsDrawing { get; set; }

        private bool _isSelected;

        /// <summary>当前引导几何是否被选中（显示编辑手柄）</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                SelectionChanged?.Invoke(value);
            }
        }

        /// <summary>选中状态变化事件（true=选中，false=取消选中；对齐 Roi 端 HasParametersVisible 通知机制）</summary>
        public event Action<bool>? SelectionChanged;

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
                // 从弧端点派生两端点 + 半径 + 方向 + 大小弧
                ArcPoint1 = arc.GetPointOnArc(arc.StartAngle);
                ArcPoint2 = arc.GetPointOnArc(arc.EndAngle);
                ArcRadius = arc.Radius;
                ArcClockwise = arc.IsClockwise;
                ArcMajorArc = arc.IsMajorArc;
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
            ArcRadius = (point2 - point1).Length;   // 默认半径 = 弦长（60° 弧）
            ArcClockwise = false;                   // 默认逆时针
            ArcMajorArc = false;                    // 默认小弧（60°）
            var arc = Arc2d.FromEndpointsAndRadius(point1, point2, ArcRadius, ArcClockwise, ArcMajorArc);
            if (arc.IsEmpty) return false;
            EditingArc = arc;
            EditingLine = null;
            EditingCircle = null;
            return true;
        }

        /// <summary>
        /// 更新圆弧端点（P1/P2 移动）：锚定弧中点 P3，三点定圆重算，半径/方向/大小弧全部自适应，拖动无跳变
        /// </summary>
        /// <remarks>
        /// 治本设计：不再锁死半径/方向/大小弧（避免旧值绑架导致跳变），而是锚定当前弧中点 P3，
        /// 用 三点定圆 从几何实时算出半径/方向/大小弧；P3 作为连续性锚点，拖动端点时弧始终过它。
        /// </remarks>
        /// <returns>是否更新成功（无圆弧或三点退化时 false，保持原弧）</returns>
        public bool UpdateArcEndpoint(bool isStart, Point point)
        {
            if (!EditingArc.HasValue || EditingArc.Value.IsEmpty) return false;

            // 锚点 P3 = 当前弧的中点（拖端点时保持不动，保证弧始终过它）
            var a = EditingArc.Value;
            var p3 = a.GetPointOnArc(a.StartAngle + a.AngularSpan / 2);

            var p1 = isStart ? point : ArcPoint1;
            var p2 = isStart ? ArcPoint2 : point;

            // 三点定圆：大小弧/方向/半径由三点几何自然算出，180° 处平滑切换，永不跳变
            var arc = Arc2d.FromThreePoints(p1, p2, p3, out bool clockwise, out bool majorArc);
            if (arc.IsEmpty) return false;   // 三点共线退化：保持原弧，等待下帧恢复

            ArcPoint1 = p1;
            ArcPoint2 = p2;
            ArcRadius = arc.Radius;
            ArcClockwise = clockwise;
            ArcMajorArc = majorArc;
            EditingArc = arc;
            return true;
        }

        /// <summary>
        /// 拖 P3（弧中点手柄）改半径/方向/大小弧：三点定圆保证弧过鼠标；三点共线（P3 过弦）用近直线过渡弧保持连续
        /// </summary>
        /// <returns>是否更新成功（无圆弧时 false；共线时以过渡弧平滑衔接）</returns>
        public bool UpdateArcRadiusFromPoint(Point mousePoint)
        {
            if (!EditingArc.HasValue || EditingArc.Value.IsEmpty) return false;

            var arc = Arc2d.FromThreePoints(ArcPoint1, ArcPoint2, mousePoint, out bool clockwise, out bool majorArc);
            if (!arc.IsEmpty)
            {
                ArcRadius = arc.Radius;
                ArcClockwise = clockwise;
                ArcMajorArc = majorArc;
                EditingArc = arc;
                return true;
            }

            // 三点共线（P3 过弦瞬间）：构造"接近直线"的大半径过渡弧，避免弧卡住后猛翻跳变
            const double degenerateRadius = 1e6;
            var transitional = Arc2d.FromEndpointsAndRadius(ArcPoint1, ArcPoint2, degenerateRadius, ArcClockwise, ArcMajorArc);
            if (transitional.IsEmpty) return false;
            EditingArc = transitional;
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
                // ★ 修复拖动无效：渲染/命中测试基于端点参数化（LineStartPoint/LineEndPoint），
                //   整体平移必须同步端点，否则引导线视觉不动
                LineStartPoint += delta;
                LineEndPoint += delta;
            }
            else if (EditingCircle.HasValue)
            {
                var c = EditingCircle.Value;
                EditingCircle = new Circle2d(c.Center + delta, c.Radius);
            }
            else if (EditingArc.HasValue && !EditingArc.Value.IsEmpty)
            {
                // 两端点整体平移（半径/方向/大小弧不变）
                ArcPoint1 += delta;
                ArcPoint2 += delta;
                EditingArc = Arc2d.FromEndpointsAndRadius(ArcPoint1, ArcPoint2, ArcRadius, ArcClockwise, ArcMajorArc);
            }
        }
    }
}
