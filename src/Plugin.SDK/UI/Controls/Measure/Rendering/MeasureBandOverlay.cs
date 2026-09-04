using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Constants;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering
{
    /// <summary>
    /// 测量带叠加渲染器 - 在主界面图像叠加画布上绘制引导几何 + 测量带阵列
    /// </summary>
    /// <remarks>
    /// 用于边缘检测等检测工具的引导几何可视化：
    /// - 引导几何（直线/圆/圆弧）
    /// - 等间距测量带阵列（卡尺），其中**主卡尺高亮**（其余半透明）
    /// - 主卡尺上的**检测方向箭头**
    /// 通过 ImageControl.AddOverlay 添加到主界面图像上。
    ///
    /// 主卡尺语义（对齐 VisionPro 搜索区域）：
    /// - Line：中间那条测量带
    /// - Circle：0°（正右方）那条测量带
    /// - Arc：弧起止角中点那条测量带
    /// 主卡尺高亮显示，其余测量带半透明（背景参照）。
    /// </remarks>
    public class MeasureBandOverlay
    {
        /// <summary>统一描边色 - 检测蓝（与 Blob 检测区域一致，ColorConstants 单一来源）</summary>
        private static readonly Color StrokeColor = ColorConstants.InspectionBlue;

        private readonly Canvas _overlayCanvas;
        private readonly List<FrameworkElement> _elements = new();

        /// <summary>
        /// 创建测量带叠加渲染器
        /// </summary>
        /// <param name="overlayCanvas">主界面图像叠加画布（ImageControl.InteractionOverlayCanvasControl）</param>
        public MeasureBandOverlay(Canvas overlayCanvas)
        {
            _overlayCanvas = overlayCanvas ?? throw new ArgumentNullException(nameof(overlayCanvas));
        }

        /// <summary>
        /// 渲染引导直线 + 测量带（数量参数化）
        /// </summary>
        /// <param name="start">线段起点（用户拖拽起点）</param>
        /// <param name="end">线段终点（用户拖拽终点）</param>
        /// <param name="bandWidth">卡尺宽度（法线方向全宽）</param>
        /// <param name="bandLength">卡尺长度（切线方向全长）</param>
        /// <param name="measureCount">测量带数量</param>
        /// <param name="drawBands">是否绘制测量带（false=只画引导几何预览）</param>
        /// <param name="matrix">位置修正矩阵（参考坐标→当前图像坐标；null=不修正）</param>
        /// <param name="zoom">视口缩放（线宽反除缩放，对齐 Blob 检测区域：线宽=2/zoom，视觉恒定）</param>
        public void RenderLine(Point start, Point end, double bandWidth, double bandLength, int measureCount, bool drawBands = true, AffineMatrix? matrix = null, double zoom = 1.0)
        {
            double scale = matrix != null ? matrix.ScaleFactor() : 1.0;
            Clear();

            // 位置修正：参考坐标 → 当前图像坐标
            if (matrix != null)
            {
                start = matrix.TransformWpfPoint(start);
                end = matrix.TransformWpfPoint(end);
            }

            // Caliber size scales with position-correction matrix (same as RenderCircle/RenderArc radiusScale)
            double scaleWidth = bandWidth * scale;
            double scaleLength = bandLength * scale;
            double stroke = StrokeWidthFor(zoom);
            double bandStroke = BandStrokeWidthFor(zoom);

            // 引导线（检测蓝，与 Blob 检测区域一致线宽）
            AddLine(start, end, StrokeColor, stroke);

            if (!drawBands || measureCount <= 0) return;

            // 测量带四边形（与结果层共用同一几何计算，保证两层显示一致）
            foreach (var quad in BuildLineBands(start, end, scaleWidth, scaleLength, measureCount, null))
            {
                // 卡尺无填充，全检测蓝，粗细/透明度统一（线宽与 Blob 检测区域一致）
                AddQuad(quad[0], quad[1], quad[2], quad[3], Colors.Transparent, StrokeColor, bandStroke, 1.0);
            }

            // 主卡尺极性方向标记（交互层实时显示，与结果层 BuildMeasureGuideItems 同步）。
            // 在主卡尺法向两条边（顶边/底边）上各画一个朝 normal 的小箭头。
            AddMainCaliberEdgeDirectionArrows(start, end, scaleWidth, scaleLength, measureCount, StrokeColor, bandStroke, zoom);
        }

        /// <summary>
        /// 计算直线引导测量带四边形（供交互层渲染与结果层显示共用，保证几何一致）。
        /// 输入为已做位置修正的坐标；matrix 非空时用于二次变换（兼容调用方已变换的场景）。
        /// ★ 与 Halcon 对齐（首末带"外边缘"贴端点，中心向内偏移半带）：
        ///   首条带中心 = 起点 + dir×halfBand（带起点边精确落在起点）、
        ///   末条带中心 = 终点 − dir×halfBand（带终点边精确落在终点）。
        ///   公式：中心_i = 起点 + dir×(halfBand + i×step)，step = (totalLen − 2×halfBand)/(N−1)。
        ///   N=1 时单带中心取线段中点。
        /// </summary>
        public static System.Collections.Generic.IEnumerable<System.Windows.Point[]> BuildLineBands(
            Point start, Point end, double bandWidth, double bandLength, int measureCount, AffineMatrix? matrix = null)
        {
            if (measureCount <= 0) yield break;

            var seg = end - start;
            var totalLen = seg.Length;
            if (totalLen < 1e-6) yield break;

            var dir = seg / totalLen;   // 归一化方向
            var normal = new Vector(-dir.Y, dir.X);
            var halfBand = bandLength / 2;
            var halfWidth = bandWidth / 2;
            double usable = totalLen - 2 * halfBand;
            if (usable < 1e-6) usable = totalLen;
            double step = measureCount > 1 ? usable / (measureCount - 1) : usable;

            for (int i = 0; i < measureCount; i++)
            {
                double t = measureCount > 1 ? halfBand + i * step : totalLen / 2;
                var center = start + dir * t;

                var p1 = center - dir * halfBand - normal * halfWidth;
                var p2 = center + dir * halfBand - normal * halfWidth;
                var p3 = center + dir * halfBand + normal * halfWidth;
                var p4 = center - dir * halfBand + normal * halfWidth;

                if (matrix != null)
                {
                    p1 = matrix.TransformWpfPoint(p1);
                    p2 = matrix.TransformWpfPoint(p2);
                    p3 = matrix.TransformWpfPoint(p3);
                    p4 = matrix.TransformWpfPoint(p4);
                }

                yield return new[] { p1, p2, p3, p4 };
            }
        }

        /// <summary>
        /// 渲染引导圆 + 环形测量带（数量参数化）
        /// </summary>
        /// <param name="circle">引导圆</param>
        /// <param name="bandWidth">卡尺宽度（径向全宽）</param>
        /// <param name="bandLength">卡尺长度（切向全长）</param>
        /// <param name="measureCount">测量带数量</param>
        /// <param name="drawBands">是否绘制测量带（false=只画引导几何预览）</param>
        /// <param name="matrix">位置修正矩阵（参考坐标→当前图像坐标；null=不修正）</param>
        /// <param name="zoom">视口缩放（线宽反除缩放，对齐 Blob 检测区域：线宽=2/zoom，视觉恒定）</param>
        public void RenderCircle(Circle2d circle, double bandWidth, double bandLength, int measureCount, bool drawBands = true, AffineMatrix? matrix = null, double zoom = 1.0)
        {
            Clear();

            // 位置修正：圆心变换 + 半径缩放（含缩放时）
            double radiusScale = 1.0;
            if (matrix != null)
            {
                circle = new Circle2d(
                    matrix.TransformWpfPoint(circle.Center),
                    circle.Radius * matrix.ScaleFactor());
                radiusScale = matrix.ScaleFactor();
            }

            double stroke = StrokeWidthFor(zoom);
            double bandStroke = BandStrokeWidthFor(zoom);

            // 引导圆（检测蓝，与 Blob 检测区域一致线宽）
            AddEllipse(circle.Center, circle.Radius, StrokeColor, stroke);

            if (!drawBands || measureCount <= 0 || circle.Radius <= 0) return;

            // 环形测量带（与结果层共用同一几何计算）
            foreach (var quad in BuildCircleBands(circle, bandWidth, bandLength, measureCount))
            {
                // 卡尺无填充，全检测蓝，粗细/透明度统一（线宽与 Blob 检测区域一致）
                AddQuad(quad[0], quad[1], quad[2], quad[3], Colors.Transparent, StrokeColor, bandStroke, 1.0);
            }

            // 主卡尺极性方向标记：Halcon circle/arc 测量扫描方向 = 圆内侧→外侧（官方定义），
            // 主卡尺（中间条，圆心角 = mainIndex×2π/N）上画朝 +radial（径向外）的小箭头。
            double mainAngle = (measureCount / 2) * (Math.PI * 2 / measureCount);
            AddMainCaliberRadialDirectionArrows(circle.Center, circle.Radius, bandWidth, bandLength, mainAngle,
                StrokeColor, bandStroke, zoom);
        }

        /// <summary>
        /// 计算圆引导环形测量带四边形（供交互层渲染与结果层显示共用）。
        /// 调用方需先对 circle 应用位置修正（圆心变换 + 半径缩放），与 RenderCircle 内部逻辑一致。
        /// ★ 与 Halcon 对齐（整圆 = 等分，格点相位）：
        ///   整圆闭合，HALCON 用 N 条测量带把整圈等分，第 i 条中心 = start + i×(2π/N)，
        ///   首条落在 start_phi 上，末条不与首条重叠（不存在"首末带贴端点"的约束）。
        ///   实测依据（get_metrology_object_measures 回读真值）：
        ///   相邻带角差恒为 2π/N；本地若用半格 (i+0.5)×2π/N 会整体偏 step/2。
        /// </summary>
        public static System.Collections.Generic.IEnumerable<System.Windows.Point[]> BuildCircleBands(
            Circle2d circle, double bandWidth, double bandLength, int measureCount)
        {
            if (measureCount <= 0 || circle.Radius <= 0) yield break;

            // 环形测量带：沿圆周等分（闭合，末条不与首条重叠），径向为宽度
            double stepAngle = Math.PI * 2 / measureCount;
            var halfBand = bandLength / 2;
            var halfWidth = bandWidth / 2;

            for (int i = 0; i < measureCount; i++)
            {
                double angle = i * stepAngle;   // ★ 格点：第 i 条中心 = start + i×(2π/N)
                var dir = new Vector(Math.Cos(angle), Math.Sin(angle));   // 径向方向
                var normal = new Vector(-dir.Y, dir.X);                   // 切向方向

                var center = circle.Center + dir * circle.Radius;

                var p1 = center - dir * halfWidth - normal * halfBand;
                var p2 = center + dir * halfWidth - normal * halfBand;
                var p3 = center + dir * halfWidth + normal * halfBand;
                var p4 = center - dir * halfWidth + normal * halfBand;

                yield return new[] { p1, p2, p3, p4 };
            }
        }

        /// <summary>
        /// 渲染引导圆弧 + 环形测量带（数量参数化）
        /// </summary>
        /// <param name="arc">引导圆弧</param>
        /// <param name="bandWidth">卡尺宽度（径向全宽）</param>
        /// <param name="bandLength">卡尺长度（切向全长）</param>
        /// <param name="measureCount">测量带数量</param>
        /// <param name="drawBands">是否绘制测量带（false=只画引导几何预览）</param>
        /// <param name="matrix">位置修正矩阵（参考坐标→当前图像坐标；null=不修正）</param>
        /// <param name="zoom">视口缩放（线宽反除缩放，对齐 Blob 检测区域：线宽=2/zoom，视觉恒定）</param>
                public void RenderArc(Arc2d arc, double bandWidth, double bandLength, int measureCount, bool drawBands = true, AffineMatrix? matrix = null, double zoom = 1.0)
        {
            Clear();

            // 位置修正：圆弧为有向形状，完整仿射跟随——圆心变换、半径缩放，
            // 且起止角叠加矩阵旋转（DisplayAngleDeg 向量法，避免符号错误导致弧主体与手柄分离）。
            double radiusScale = 1.0;
            if (matrix != null)
            {
                var startDisp = PositionCorrectionResolver.DisplayAngleDeg(matrix, arc.StartAngle * 180.0 / Math.PI) * Math.PI / 180.0;
                var endDisp = PositionCorrectionResolver.DisplayAngleDeg(matrix, arc.EndAngle * 180.0 / Math.PI) * Math.PI / 180.0;
                arc = new Arc2d(
                    matrix.TransformWpfPoint(arc.Center),
                    arc.Radius * matrix.ScaleFactor(),
                    startDisp, endDisp, arc.IsMajorArc);
                radiusScale = matrix.ScaleFactor();
            }

            double stroke = StrokeWidthFor(zoom);
            double bandStroke = BandStrokeWidthFor(zoom);

            // 引导弧（检测蓝，与 Blob 检测区域一致线宽）
            AddArc(arc, StrokeColor, stroke);

            if (!drawBands || measureCount <= 0 || arc.Radius <= 0 || Math.Abs(arc.AngularSpan) <= 1e-6) return;

            // 环形测量带（与结果层共用同一几何计算）
            foreach (var quad in BuildArcBands(arc, bandWidth, bandLength, measureCount, radiusScale))
            {
                // 卡尺无填充，全检测蓝，粗细/透明度统一（线宽与 Blob 检测区域一致）
                AddQuad(quad[0], quad[1], quad[2], quad[3], Colors.Transparent, StrokeColor, bandStroke, 1.0);
            }

            // 主卡尺极性方向标记：Halcon circle/arc 测量扫描方向 = 圆内侧→外侧（官方定义），
            // 主卡尺（弧中点条，中心角 = firstAngle + mainIndex×stepAngle）上画朝 +radial（径向外）的小箭头。
            double span = arc.AngularSpan;
            double halfBandAngle = (bandLength / 2 * radiusScale) / arc.Radius;
            double direction = span >= 0 ? 1.0 : -1.0;
            double sweepEnd = arc.StartAngle + span;
            double firstAngle = arc.StartAngle + direction * halfBandAngle;
            double lastAngle = sweepEnd - direction * halfBandAngle;
            double mainAngle = measureCount > 1
                ? firstAngle + (measureCount / 2) * ((lastAngle - firstAngle) / (measureCount - 1))
                : arc.StartAngle + span / 2;
            AddMainCaliberRadialDirectionArrows(arc.Center, arc.Radius, bandWidth, bandLength * radiusScale, mainAngle,
                StrokeColor, bandStroke, zoom);
        }

        /// <summary>
        /// 计算圆弧引导环形测量带四边形（供交互层渲染与结果层显示共用）。
        /// 调用方需先对 arc 应用位置修正（圆心变换 + 半径缩放，角度不变），与 RenderArc 内部逻辑一致。
        /// </summary>
        /// <param name="radiusScale">位置修正半径缩放系数（无修正时传 1.0）</param>
        public static System.Collections.Generic.IEnumerable<System.Windows.Point[]> BuildArcBands(
            Arc2d arc, double bandWidth, double bandLength, int measureCount, double radiusScale = 1.0)
        {
            if (measureCount <= 0 || arc.Radius <= 0 || Math.Abs(arc.AngularSpan) <= 1e-6) yield break;

            // 环形测量带：带中心沿弧"连续扫掠坐标"均匀分布，径向为宽度
            // ★ 与 Halcon 对齐（首末带外边缘贴弧端点、中心向内偏移半带 + 连续扫掠）：
            //   首条中心 = StartAngle + direction×halfBandAngle（带起点边精确落在弧起点）
            //   末条中心 = sweepEnd − direction×halfBandAngle（带终点边精确落在弧终点）
            //   其中 sweepEnd = StartAngle + AngularSpan（连续，可跨 2π）——不用 EndAngle 原值！
            //   大弧跨 2π 时 EndAngle 与 sweepEnd 差整周期，用它会导致整排带反向。
            //   step 由 (last−first)/(N−1) 得出，符号自然与弧方向一致（顺/逆时针均正确）。
            //   N=1 时单带中心取弧中点。
            double span = arc.AngularSpan;
            double halfBand = bandLength / 2 * radiusScale;
            double halfWidth = bandWidth / 2 * radiusScale;
            double halfBandAngle = halfBand / arc.Radius;
            double direction = span >= 0 ? 1.0 : -1.0;
            double sweepEnd = arc.StartAngle + span;
            double firstAngle = arc.StartAngle + direction * halfBandAngle;
            double lastAngle = sweepEnd - direction * halfBandAngle;
            double stepAngle = measureCount > 1 ? (lastAngle - firstAngle) / (measureCount - 1) : 0.0;

            for (int i = 0; i < measureCount; i++)
            {
                double angle = measureCount > 1 ? firstAngle + i * stepAngle : arc.StartAngle + span / 2;
                var dir = new Vector(Math.Cos(angle), Math.Sin(angle));   // 径向方向
                var normal = new Vector(-dir.Y, dir.X);                   // 切向方向

                var center = arc.Center + dir * arc.Radius;

                var p1 = center - dir * halfWidth - normal * halfBand;
                var p2 = center + dir * halfWidth - normal * halfBand;
                var p3 = center + dir * halfWidth + normal * halfBand;
                var p4 = center - dir * halfWidth + normal * halfBand;

                yield return new[] { p1, p2, p3, p4 };
            }
        }

        /// <summary>
        /// 清除所有渲染元素
        /// </summary>
        public void Clear()
        {
            foreach (var element in _elements)
            {
                _overlayCanvas.Children.Remove(element);
            }
            _elements.Clear();
        }

        #region 绘制辅助

        /// <summary>
        /// 添加直线
        /// </summary>
        private void AddLine(Point start, Point end, Color color, double thickness)
        {
            var line = new Line
            {
                X1 = start.X, Y1 = start.Y,
                X2 = end.X, Y2 = end.Y,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
            };
            _overlayCanvas.Children.Add(line);
            _elements.Add(line);
        }

        /// <summary>
        /// 添加椭圆（圆）
        /// </summary>
        private void AddEllipse(Point center, double radius, Color color, double thickness)
        {
            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = null,
            };
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            _overlayCanvas.Children.Add(ellipse);
            _elements.Add(ellipse);
        }

        /// <summary>
        /// 添加圆弧（Path 方式绘制弧段）
        /// </summary>
        private void AddArc(Arc2d arc, Color color, double thickness)
        {
            var points = arc.GetSamplePoints(48);
            if (points.Length < 2) return;

            var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
            for (int i = 1; i < points.Length; i++)
            {
                figure.Segments.Add(new LineSegment { Point = points[i] });
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = null,
            };
            _overlayCanvas.Children.Add(path);
            _elements.Add(path);
        }

        /// <summary>
        /// 添加四边形（测量带）
        /// </summary>
        private void AddQuad(Point p1, Point p2, Point p3, Point p4, Color fill, Color stroke, double thickness, double opacity)
        {
            var polygon = new Polygon
            {
                Points = new PointCollection { p1, p2, p3, p4 },
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = thickness,
                Opacity = opacity,
            };
            _overlayCanvas.Children.Add(polygon);
            _elements.Add(polygon);
        }

        /// <summary>
        /// 添加多边形（任意顶点，填充 + 描边）
        /// </summary>
        private void AddPolygon(IReadOnlyList<Point> points, Color fill, Color stroke, double thickness, double opacity = 1.0)
        {
            var polygon = new Polygon
            {
                Points = new PointCollection(points),
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = thickness,
                Opacity = opacity,
            };
            _overlayCanvas.Children.Add(polygon);
            _elements.Add(polygon);
        }

        /// <summary>
        /// 主卡尺极性方向标记：把主卡尺与引导线垂直的两条竖直长边（左/右）当"箭杆"，
        /// 在两条竖边沿法向 4/3 全长处各加一个朝 normal 的**纯三角尖端（无杆）**。
        /// ★ 方向 = normal = (-dir.Y, dir.X)，即引导线方向顺时针 90°（屏幕坐标）。
        ///   实验定标（Halcon line measure）：扫描方向取该法向，
        ///   'positive' = 沿该方向暗→亮、'negative' = 亮→暗。
        /// 主卡尺位置与 BuildLineBands / GetMainCaliberFrame 同构（首条贴起点，半带偏移）。
        /// </summary>
        private void AddMainCaliberEdgeDirectionArrows(Point start, Point end, double bandWidth, double bandLength,
            int measureCount, Color color, double thickness, double zoom = 1.0)
        {
            var seg = end - start;
            var totalLen = seg.Length;
            if (totalLen < 1e-6 || measureCount <= 0) return;

            var dir = seg / totalLen;                        // 引导线方向（Halcon RowBegin→RowEnd）
            var normal = new Vector(-dir.Y, dir.X);          // ★ 极性/测量扫描方向（法向）
            int mainIndex = measureCount / 2;
            double halfBand = bandLength / 2;                // 切向半长（主卡尺沿线半宽）
            double usable = totalLen - 2 * halfBand;
            if (usable < 1e-6) usable = totalLen;
            double step = measureCount > 1 ? usable / (measureCount - 1) : usable;
            double t = measureCount > 1 ? halfBand + mainIndex * step : totalLen / 2;
            var center = start + dir * t;

            // ★ 锚点贴图（与结果层同几何，位置不漂移）+ 尺寸屏幕恒定（反除 zoom，像手柄）：
            //   锚点 = 竖边中点沿 normal 走 法向全长×1/4（贴图）；三角高/半宽 = 带宽比例 + 下限，反除 zoom
            var midL = center - dir * (bandLength / 2);    // 左竖直边中点
            var midR = center + dir * (bandLength / 2);    // 右竖直边中点
            double posOffset = bandWidth * 0.25;           // 锚点沿 normal 走 1/4（贴图）
            double triHeight = ViewportScaling.Power(zoom, Math.Max(bandWidth * 0.15, 4.0), 1.0);    // 三角高（屏幕恒定）
            double triHalfWidth = ViewportScaling.Power(zoom, Math.Max(bandWidth * 0.07, 2.0), 1.0); // 三角底半宽（屏幕恒定）

            // 左三角：尖端锚在竖边中点 +normal×1/4 处，尖端朝 +normal
            var tipL = midL + normal * posOffset;
            var bL1 = tipL - normal * triHeight - dir * triHalfWidth;
            var bL2 = tipL - normal * triHeight + dir * triHalfWidth;
            AddPolygon(new[] { tipL, bL1, bL2 }, color, color, thickness, 1.0);

            // 右三角
            var tipR = midR + normal * posOffset;
            var bR1 = tipR - normal * triHeight - dir * triHalfWidth;
            var bR2 = tipR - normal * triHeight + dir * triHalfWidth;
            AddPolygon(new[] { tipR, bR1, bR2 }, color, color, thickness, 1.0);
        }

        /// <summary>
        /// 圆/弧主卡尺极性方向标记（交互层入口）。
        /// ★ Halcon 官方方向语义（set_metrology_object_param）：
        ///   circle/ellipse/rectangle 测量方向 = 圆内侧→外侧（径向外）。
        ///   故主卡尺两端的箭头尖端朝 +radial（远离圆心）。
        /// </summary>
        /// <param name="center">圆心（位置修正后）</param>
        /// <param name="radius">半径（位置修正后）</param>
        /// <param name="bandWidth">卡尺宽度（径向全宽）</param>
        /// <param name="bandLength">卡尺长度（切向全长）</param>
        /// <param name="mainAngle">主卡尺中心角（弧度，圆=mainIndex×2π/N；弧=first+mainIndex×step）</param>
        private void AddMainCaliberRadialDirectionArrows(Point center, double radius, double bandWidth, double bandLength,
            double mainAngle, Color color, double thickness, double zoom = 1.0)
        {
            if (radius <= 0 || bandWidth <= 0) return;
            foreach (var tri in BuildRadialDirectionArrowTriangles(center, radius, mainAngle, bandWidth, bandLength, zoom))
            {
                AddPolygon(tri, color, color, thickness, 1.0);
            }
        }

        /// <summary>
        /// 计算圆/弧主卡尺极性方向三角顶点（供交互层与结果层共用，保证两层显示一致）。
        /// 主卡尺 = 中间条（圆心角 = mainAngle）；三角尖端朝 +radial（径向外 = Halcon 扫描方向）。
        /// 尺寸屏幕恒定（反除 zoom，与直线三角一致），锚点贴主卡尺两端沿径向 1/4 处。
        /// </summary>
        public static System.Collections.Generic.IEnumerable<System.Windows.Point[]> BuildRadialDirectionArrowTriangles(
            Point center, double radius, double mainAngle, double bandWidth, double bandLength, double zoom = 1.0)
        {
            if (radius <= 0 || bandWidth <= 0) yield break;

            var radial = new Vector(Math.Cos(mainAngle), Math.Sin(mainAngle));   // 径向方向（圆心→外）
            var tangent = new Vector(-radial.Y, radial.X);                       // 切向方向
            var calCenter = center + radial * radius;                            // 主卡尺中心（在圆上）
            var mid1 = calCenter - tangent * (bandLength / 2);                   // 切向一端中点
            var mid2 = calCenter + tangent * (bandLength / 2);                   // 切向另一端中点

            double posOffset = bandWidth * 0.25;                                  // 锚点沿径向走 1/4（贴图）
            double triHeight = ViewportScaling.Power(zoom, Math.Max(bandWidth * 0.15, 4.0), 1.0);
            double triHalfWidth = ViewportScaling.Power(zoom, Math.Max(bandWidth * 0.07, 2.0), 1.0);

            // 左三角：尖端朝 +radial
            var tip1 = mid1 + radial * posOffset;
            yield return new[] { tip1,
                tip1 - radial * triHeight - tangent * triHalfWidth,
                tip1 - radial * triHeight + tangent * triHalfWidth };

            // 右三角
            var tip2 = mid2 + radial * posOffset;
            yield return new[] { tip2,
                tip2 - radial * triHeight - tangent * triHalfWidth,
                tip2 - radial * triHeight + tangent * triHalfWidth };
        }

        /// <summary>
        /// 引导几何线宽（反除缩放，对齐 Blob 检测区域默认线宽 2.0）
        /// </summary>
        private static double StrokeWidthFor(double zoom) => ViewportScaling.LineWidth(zoom);

        /// <summary>
        /// 测量带线宽（与引导线一致：2/zoom 屏幕恒定；不再设逻辑像素下限——
        /// 旧 min1.0 导致 zoom≥2 时屏幕线宽随放大变粗，违反幂律屏幕恒定语义）
        /// </summary>
        private static double BandStrokeWidthFor(double zoom) => ViewportScaling.LineWidth(zoom);

        #endregion
    }
}
