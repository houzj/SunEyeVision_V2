using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Execution.Results;
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
            Clear();

            // 位置修正：参考坐标 → 当前图像坐标
            if (matrix != null)
            {
                start = matrix.TransformWpfPoint(start);
                end = matrix.TransformWpfPoint(end);
            }

            double stroke = StrokeWidthFor(zoom);
            double bandStroke = BandStrokeWidthFor(zoom);

            // 引导线（检测蓝，与 Blob 检测区域一致线宽）
            AddLine(start, end, StrokeColor, stroke);

            if (!drawBands || measureCount <= 0) return;

            var seg = end - start;
            var totalLen = seg.Length;
            if (totalLen < 1e-6) return;

            var dir = seg / totalLen;   // 归一化方向
            var normal = new Vector(-dir.Y, dir.X);
            double step = totalLen / measureCount;
            var halfBand = bandLength / 2;
            var halfWidth = bandWidth / 2;
            int mainIndex = measureCount / 2; // 主卡尺 = 中间条

            for (int i = 0; i < measureCount; i++)
            {
                double t = (i + 0.5) * step;
                var center = start + dir * t;

                var p1 = center - dir * halfBand - normal * halfWidth;
                var p2 = center + dir * halfBand - normal * halfWidth;
                var p3 = center + dir * halfBand + normal * halfWidth;
                var p4 = center - dir * halfBand + normal * halfWidth;

                bool isMain = i == mainIndex;
                // 卡尺无填充，全检测蓝，粗细/透明度统一（线宽与 Blob 检测区域一致）
                AddQuad(p1, p2, p3, p4, Colors.Transparent, StrokeColor, bandStroke, 1.0);

                // 主卡尺上绘制检测方向箭头（沿法线方向）
                if (isMain)
                {
                    var arrowBase = center - dir * halfBand * 0.3;
                    AddDirectionArrow(arrowBase, normal, bandWidth * 0.6, bandStroke);
                }
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

            // 环形测量带：沿圆周等间距分布，径向为宽度（卡尺宽度随矩阵缩放）
            double stepAngle = Math.PI * 2 / measureCount;
            var halfBand = bandLength / 2 * radiusScale;
            var halfWidth = bandWidth / 2 * radiusScale;

            for (int i = 0; i < measureCount; i++)
            {
                double angle = i * stepAngle;
                var dir = new Vector(Math.Cos(angle), Math.Sin(angle));   // 径向方向
                var normal = new Vector(-dir.Y, dir.X);                   // 切向方向

                var center = circle.Center + dir * circle.Radius;

                var p1 = center - dir * halfWidth - normal * halfBand;
                var p2 = center + dir * halfWidth - normal * halfBand;
                var p3 = center + dir * halfWidth + normal * halfBand;
                var p4 = center - dir * halfWidth + normal * halfBand;

                bool isMain = i == 0; // 主卡尺 = 0°（正右方）条
                // 卡尺无填充，全检测蓝，粗细/透明度统一（线宽与 Blob 检测区域一致）
                AddQuad(p1, p2, p3, p4, Colors.Transparent, StrokeColor, bandStroke, 1.0);

                // 主卡尺上绘制检测方向箭头（沿径向向外）
                if (isMain)
                {
                    var arrowBase = center - dir * halfWidth * 0.3;
                    AddDirectionArrow(arrowBase, dir, bandWidth * 0.6, bandStroke);
                }
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

            // 位置修正：圆心变换 + 半径缩放（含缩放时）；角度范围不变（刚体变换）
            double radiusScale = 1.0;
            if (matrix != null)
            {
                arc = new Arc2d(
                    matrix.TransformWpfPoint(arc.Center),
                    arc.Radius * matrix.ScaleFactor(),
                    arc.StartAngle, arc.EndAngle, arc.IsMajorArc);
                radiusScale = matrix.ScaleFactor();
            }

            double stroke = StrokeWidthFor(zoom);
            double bandStroke = BandStrokeWidthFor(zoom);

            // 引导弧（检测蓝，与 Blob 检测区域一致线宽）
            AddArc(arc, StrokeColor, stroke);

            if (!drawBands || measureCount <= 0 || arc.Radius <= 0 || Math.Abs(arc.AngularSpan) <= 1e-6) return;

            // 环形测量带：沿弧方向等间距分布，径向为宽度（卡尺宽度随矩阵缩放）
            // 支持负 AngularSpan（顺时针弧，P3 过弦后方向翻转）
            double span = arc.AngularSpan;
            double stepAngle = span / measureCount;   // 保留符号：顺时针 stepAngle 为负 → 角度递减
            var halfBand = bandLength / 2 * radiusScale;
            var halfWidth = bandWidth / 2 * radiusScale;
            // 主卡尺 = 弧中点对应的卡尺索引（固定索引判定，与 RenderLine 一致；
            // 避免"角度差比较"在顺时针/跨±π/大小弧时判定失效导致方向箭头丢失）
            int mainIndex = measureCount / 2;

            for (int i = 0; i < measureCount; i++)
            {
                double angle = arc.StartAngle + (i + 0.5) * stepAngle;
                // 角度规范化到 [-π, π]，避免跨 0° 边界时 cos/sin 大角度误差
                angle = ((angle + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI;
                var dir = new Vector(Math.Cos(angle), Math.Sin(angle));   // 径向方向
                var normal = new Vector(-dir.Y, dir.X);                   // 切向方向

                var center = arc.Center + dir * arc.Radius;

                var p1 = center - dir * halfWidth - normal * halfBand;
                var p2 = center + dir * halfWidth - normal * halfBand;
                var p3 = center + dir * halfWidth + normal * halfBand;
                var p4 = center - dir * halfWidth + normal * halfBand;

                bool isMain = i == mainIndex;
                // 卡尺无填充，全检测蓝，粗细/透明度统一（线宽与 Blob 检测区域一致）
                AddQuad(p1, p2, p3, p4, Colors.Transparent, StrokeColor, bandStroke, 1.0);

                // 主卡尺上绘制检测方向箭头（沿径向向外）
                if (isMain)
                {
                    var arrowBase = center - dir * halfWidth * 0.3;
                    AddDirectionArrow(arrowBase, dir, bandWidth * 0.6, bandStroke);
                }
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
        /// 添加检测方向箭头（线段 + 三角箭头，沿 direction 方向）
        /// </summary>
        /// <param name="basePoint">箭头起点</param>
        /// <param name="direction">箭头方向</param>
        /// <param name="length">箭头长度</param>
        /// <param name="thickness">线宽（反除缩放，与 Blob 检测区域一致）</param>
        private void AddDirectionArrow(Point basePoint, Vector direction, double length, double thickness)
        {
            if (direction.Length < 1e-9) return;

            var dir = direction;
            dir.Normalize();
            var tip = basePoint + dir * length;

            // 箭头主干
            AddLine(basePoint, tip, StrokeColor, thickness);

            // 箭头两翼
            double wing = length * 0.25;
            var perp = new Vector(-dir.Y, dir.X);
            var wing1 = tip - dir * wing + perp * wing * 0.5;
            var wing2 = tip - dir * wing - perp * wing * 0.5;

            AddLine(tip, wing1, StrokeColor, thickness);
            AddLine(tip, wing2, StrokeColor, thickness);
        }

        /// <summary>
        /// 引导几何线宽（反除缩放，对齐 Blob 检测区域默认线宽 2.0）
        /// </summary>
        private static double StrokeWidthFor(double zoom) => 2.0 / Math.Max(zoom, 0.01);

        /// <summary>
        /// 测量带线宽（与 Blob 检测区域边框线宽一致；单像素保底避免高倍缩放消失）
        /// </summary>
        private static double BandStrokeWidthFor(double zoom) => Math.Max(2.0 / Math.Max(zoom, 0.01), 1.0);

        #endregion
    }
}
