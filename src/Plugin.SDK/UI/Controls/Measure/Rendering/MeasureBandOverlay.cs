using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering
{
    /// <summary>
    /// 测量带叠加渲染器 - 在主界面图像叠加画布上绘制引导几何 + 测量带
    /// </summary>
    /// <remarks>
    /// 用于边缘检测等检测工具的引导几何可视化：
    /// - 引导几何（直线/圆/圆弧）
    /// - 等间距测量带阵列（卡尺）
    /// 通过 ImageControl.AddOverlay 添加到主界面图像上。
    /// </remarks>
    public class MeasureBandOverlay
    {
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
        /// 渲染引导直线 + 测量带
        /// </summary>
        public void RenderLine(Line2d line, double bandWidth, double bandLength, int measureCount)
        {
            Clear();

            if (measureCount <= 0) return;

            // 引导线（白色实线）
            var start = line.Point;
            var end = line.GetPointAt(1.0);
            AddLine(start, end, Colors.White, 2.0);

            // 测量带阵列：沿引导线方向等间距分布，垂直引导线
            var dir = line.Direction;
            var normal = line.Normal;
            var totalLen = (end - start).Length;
            if (totalLen < 1e-6 || measureCount < 1) return;

            double step = totalLen / measureCount;
            var halfBand = bandLength / 2;
            var halfWidth = bandWidth / 2;

            for (int i = 0; i < measureCount; i++)
            {
                double t = (i + 0.5) * step;
                var center = start + dir * t;

                // 测量带矩形：中心在引导线上，法线方向为宽度，引导方向为长度
                var p1 = center - dir * halfBand - normal * halfWidth;
                var p2 = center + dir * halfBand - normal * halfWidth;
                var p3 = center + dir * halfBand + normal * halfWidth;
                var p4 = center - dir * halfBand + normal * halfWidth;

                AddQuad(p1, p2, p3, p4, Color.FromArgb(60, 100, 200, 255), Colors.SteelBlue, 1.0);
            }
        }

        /// <summary>
        /// 渲染引导圆 + 环形测量带
        /// </summary>
        public void RenderCircle(Circle2d circle, double bandWidth, double bandLength, int measureCount)
        {
            Clear();

            if (measureCount <= 0 || circle.Radius <= 0) return;

            // 引导圆（白色实线）
            AddEllipse(circle.Center, circle.Radius, Colors.White, 2.0);

            // 环形测量带：沿圆周等间距分布，径向为宽度
            double stepAngle = Math.PI * 2 / measureCount;
            var halfBand = bandLength / 2;
            var halfWidth = bandWidth / 2;

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

                AddQuad(p1, p2, p3, p4, Color.FromArgb(60, 100, 200, 255), Colors.SteelBlue, 1.0);
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
        /// 添加四边形（测量带）
        /// </summary>
        private void AddQuad(Point p1, Point p2, Point p3, Point p4, Color fill, Color stroke, double thickness)
        {
            var polygon = new Polygon
            {
                Points = new PointCollection { p1, p2, p3, p4 },
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = thickness,
            };
            _overlayCanvas.Children.Add(polygon);
            _elements.Add(polygon);
        }

        #endregion
    }
}
