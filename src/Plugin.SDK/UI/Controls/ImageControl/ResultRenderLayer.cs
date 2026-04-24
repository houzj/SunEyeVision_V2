using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

// 类型别名解决歧义
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfRect = System.Windows.Rect;
using SdkLineSegment = SunEyeVision.Plugin.SDK.Models.Visualization.LineSegment;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 结果渲染层 - 基于 DrawingContext 的高性能渲染
    /// </summary>
    /// <remarks>
    /// 三通道设计（参考Halcon）：
    /// - DispImage: 底层Image控件负责
    /// - DispObj: 几何图形（DrawingContext.Draw*）
    /// - DispText: 文本标签（DrawingContext.DrawText）
    /// 
    /// 性能优化：
    /// - 输入用 IEnumerable，内部用 List 物化（OnRender 可能被多次调用）
    /// - Brush/Pen 缓存 + Freeze() 减少 WPF 渲染管线开销
    /// - 分类可见性用 Dictionary 独立控制
    /// </remarks>
    public sealed class ResultRenderLayer : FrameworkElement
    {
        #region 私有字段

        private List<VisualElement> _shapes = new();
        private List<DisplayText> _texts = new();
        private readonly Dictionary<string, bool> _categoryVisibility = new();
        private readonly Dictionary<string, DisplayItemConfig> _shapeConfigs = new();
        private readonly Dictionary<string, TextConfigBase> _textConfigs = new();
        private readonly Dictionary<uint, SolidColorBrush> _brushCache = new();
        private readonly Dictionary<uint, Pen> _penCache = new();

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置几何图形（DispObj通道）
        /// </summary>
        public void SetShapes(IEnumerable<VisualElement>? shapes)
        {
            _shapes = shapes?.ToList() ?? new List<VisualElement>();
            InvalidateVisual();
        }

        /// <summary>
        /// 设置文本（DispText通道）
        /// </summary>
        public void SetTexts(IEnumerable<DisplayText>? texts)
        {
            _texts = texts?.ToList() ?? new List<DisplayText>();
            InvalidateVisual();
        }

        /// <summary>
        /// 设置分类可见性
        /// </summary>
        public void SetCategoryVisible(string category, bool visible)
        {
            _categoryVisibility[category] = visible;
            InvalidateVisual();
        }

        /// <summary>
        /// 设置形状配置
        /// </summary>
        public void SetShapeConfig(string category, DisplayItemConfig config)
        {
            _shapeConfigs[category] = config;
            InvalidateVisual();
        }

        /// <summary>
        /// 设置文本配置
        /// </summary>
        public void SetTextConfig(string category, TextConfigBase config)
        {
            _textConfigs[category] = config;
            InvalidateVisual();
        }

        /// <summary>
        /// 清空所有内容
        /// </summary>
        public void Clear()
        {
            _shapes.Clear();
            _texts.Clear();
            InvalidateVisual();
        }

        #endregion

        #region 幂律线宽支持

        private double _viewportScale = 1.0;

        /// <summary>
        /// 视口缩放比例（用于幂律线宽调整）
        /// </summary>
        public double ViewportScale
        {
            get => _viewportScale;
            set
            {
                if (Math.Abs(_viewportScale - value) > 0.001)
                {
                    _viewportScale = value;
                    InvalidateVisual(); // 缩放变化时需要重绘
                }
            }
        }

        /// <summary>
        /// 设置视口缩放（幂律公式：线宽 / 缩放 = 视觉恒定）
        /// </summary>
        public void SetViewportScale(double scale)
        {
            ViewportScale = scale;
        }

        /// <summary>
        /// 获取幂律调整后的线宽
        /// </summary>
        private double GetScaledLineWidth(double rawWidth)
        {
            return _viewportScale > 0.001 ? rawWidth / _viewportScale : rawWidth;
        }

        #endregion

        #region 渲染

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // 清空缓存（尺寸变化时需要重建）
            _brushCache.Clear();
            _penCache.Clear();

            // 渲染图形（按ZOrder排序）
            foreach (var shape in _shapes.OrderBy(s => s.ZOrder))
            {
                if (!shape.IsVisible) continue;
                if (!string.IsNullOrEmpty(shape.Category) && 
                    _categoryVisibility.TryGetValue(shape.Category, out var visible) && !visible) continue;

                RenderShape(dc, shape);
            }

            // 渲染文本（按ZOrder排序）
            foreach (var text in _texts.OrderBy(t => t.ZOrder))
            {
                if (!string.IsNullOrEmpty(text.Category) && 
                    _categoryVisibility.TryGetValue(text.Category, out var visible) && !visible) continue;

                RenderText(dc, text);
            }
        }

        private void RenderShape(DrawingContext dc, VisualElement shape)
        {
            var colorArgb = GetEffectiveColor(shape);
            var color = ArgbToColor(colorArgb);
            var brush = GetBrush(colorArgb);
            var pen = GetPen(colorArgb, GetScaledLineWidth(shape.LineWidth));

            switch (shape.Type)
            {
                case VisualElementType.Point:
                    RenderPoint(dc, shape, brush);
                    break;
                case VisualElementType.Line:
                    RenderLine(dc, shape, pen);
                    break;
                case VisualElementType.Rectangle:
                    RenderRectangle(dc, shape, brush, pen);
                    break;
                case VisualElementType.Circle:
                    RenderCircle(dc, shape, brush, pen);
                    break;
                case VisualElementType.Arc:
                    RenderArc(dc, shape, pen);
                    break;
                case VisualElementType.Polygon:
                    RenderPolygon(dc, shape, brush, pen);
                    break;
                case VisualElementType.Text:
                    RenderLabel(dc, shape, color);
                    break;
                case VisualElementType.Arrow:
                    RenderArrow(dc, shape, pen);
                    break;
                case VisualElementType.Contour:
                    RenderContour(dc, shape, brush, pen);
                    break;
            }
        }

        private void RenderPoint(DrawingContext dc, VisualElement shape, SolidColorBrush brush)
        {
            if (shape.Geometry is Point2d point)
            {
                var size = GetScaledLineWidth(shape.LineWidth) * 2;
                dc.DrawEllipse(brush, null, new WpfPoint(point.X, point.Y), size / 2, size / 2);
            }
        }

        private void RenderLine(DrawingContext dc, VisualElement shape, Pen pen)
        {
            if (shape.Geometry is SdkLineSegment line)
            {
                dc.DrawLine(pen, new WpfPoint(line.Start.X, line.Start.Y), new WpfPoint(line.End.X, line.End.Y));
            }
        }

        private void RenderRectangle(DrawingContext dc, VisualElement shape, SolidColorBrush brush, Pen pen)
        {
            // 处理 Rect2d
            if (shape.Geometry is Rect2d rect)
            {
                var fillBrush = shape.IsFilled ? brush : null;
                dc.DrawRectangle(fillBrush, pen, new WpfRect(rect.X, rect.Y, rect.Width, rect.Height));
            }
            // 处理 RotatedRect
            else if (shape.Geometry is RotatedRect rotatedRect)
            {
                var points = Cv2.BoxPoints(rotatedRect);
                var points2d = Array.ConvertAll(points, p => new WpfPoint(p.X, p.Y));
                RenderPolygonFromPoints(dc, points2d, shape.IsFilled ? brush : null, pen);
            }
        }

        private void RenderCircle(DrawingContext dc, VisualElement shape, SolidColorBrush brush, Pen pen)
        {
            if (shape.Geometry is Circle2d circle)
            {
                var fillBrush = shape.IsFilled ? brush : null;
                dc.DrawEllipse(fillBrush, pen, new WpfPoint(circle.Center.X, circle.Center.Y), circle.Radius, circle.Radius);
            }
        }

        private void RenderArc(DrawingContext dc, VisualElement shape, Pen pen)
        {
            if (shape.Geometry is ArcGeometry arc)
            {
                var geometry = CreateArcGeometry(arc);
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        private void RenderPolygon(DrawingContext dc, VisualElement shape, SolidColorBrush brush, Pen pen)
        {
            if (shape.Geometry is Point2d[] points)
            {
                var wpfPoints = Array.ConvertAll(points, p => new WpfPoint(p.X, p.Y));
                RenderPolygonFromPoints(dc, wpfPoints, shape.IsFilled ? brush : null, pen);
            }
        }

        private void RenderPolygonFromPoints(DrawingContext dc, WpfPoint[] points, Brush? fill, Pen stroke)
        {
            if (points == null || points.Length < 2) return;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], fill != null, fill != null);
                for (int i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i], true, true);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, stroke, geometry);
        }

        private void RenderLabel(DrawingContext dc, VisualElement shape, Color color)
        {
            if (shape.Geometry is Point2d position && !string.IsNullOrEmpty(shape.Label))
            {
                var formattedText = CreateFormattedText(shape.Label, color, shape.FontSize, false);
                dc.DrawText(formattedText, new WpfPoint(position.X, position.Y));
            }
        }

        private void RenderArrow(DrawingContext dc, VisualElement shape, Pen pen)
        {
            if (shape.Geometry is SdkLineSegment line)
            {
                var geometry = CreateArrowGeometry(line.Start, line.End, GetScaledLineWidth(shape.LineWidth));
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        private void RenderContour(DrawingContext dc, VisualElement shape, SolidColorBrush brush, Pen pen)
        {
            if (shape.Geometry is ContourGeometry contour && contour.Points != null && contour.Points.Length >= 2)
            {
                var points = Array.ConvertAll(contour.Points, p => new WpfPoint(p.X, p.Y));
                if (contour.IsClosed && points.Length >= 3)
                {
                    RenderPolygonFromPoints(dc, points, shape.IsFilled ? brush : null, pen);
                }
                else
                {
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(points[0], false, false);
                        for (int i = 1; i < points.Length; i++)
                        {
                            ctx.LineTo(points[i], true, true);
                        }
                    }
                    geometry.Freeze();
                    dc.DrawGeometry(null, pen, geometry);
                }
            }
        }

        private void RenderText(DrawingContext dc, DisplayText text)
        {
            var color = GetTextColor(text);
            var formattedText = CreateFormattedText(text.Content, color, text.FontSize, text.IsBold);
            dc.DrawText(formattedText, new WpfPoint(text.X, text.Y));
        }

        #endregion

        #region 辅助方法

        private uint GetEffectiveColor(VisualElement shape)
        {
            if (!string.IsNullOrEmpty(shape.Category) && _shapeConfigs.TryGetValue(shape.Category, out var config))
            {
                var color = shape.IsOk ? config.OkColor : config.NgColor;
                return ColorToArgb(color);
            }
            return shape.Color;
        }

        private Color GetTextColor(DisplayText text)
        {
            if (!string.IsNullOrEmpty(text.Category) && _textConfigs.TryGetValue(text.Category, out var config))
            {
                return config.GetColor(text.IsOk);
            }
            return ArgbToColor(text.Color);
        }

        private SolidColorBrush GetBrush(uint argb)
        {
            if (_brushCache.TryGetValue(argb, out var cached)) return cached;
            var brush = new SolidColorBrush(ArgbToColor(argb));
            brush.Freeze();
            _brushCache[argb] = brush;
            return brush;
        }

        private Pen GetPen(uint argb, double thickness)
        {
            var key = argb ^ ((uint)(thickness * 100) << 16);
            if (_penCache.TryGetValue(key, out var cached)) return cached;
            var brush = GetBrush(argb);
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            _penCache[key] = pen;
            return pen;
        }

        private static uint ColorToArgb(Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        private static Color ArgbToColor(uint argb)
        {
            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        private static FormattedText CreateFormattedText(string text, Color color, double fontSize, bool isBold)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI"), 
                FontStyles.Normal, 
                isBold ? FontWeights.Bold : FontWeights.Normal, 
                FontStretches.Normal);
                
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                new SolidColorBrush(color),
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText;
        }

        private static StreamGeometry CreateArcGeometry(ArcGeometry arc)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var startAngleRad = arc.StartAngle * Math.PI / 180;
                var endAngleRad = arc.EndAngle * Math.PI / 180;

                var startX = arc.Center.X + arc.Radius * Math.Cos(startAngleRad);
                var startY = arc.Center.Y + arc.Radius * Math.Sin(startAngleRad);
                var endX = arc.Center.X + arc.Radius * Math.Cos(endAngleRad);
                var endY = arc.Center.Y + arc.Radius * Math.Sin(endAngleRad);

                ctx.BeginFigure(new WpfPoint(startX, startY), false, false);
                ctx.ArcTo(
                    new WpfPoint(endX, endY),
                    new WpfSize(arc.Radius, arc.Radius),
                    0,
                    false,
                    SweepDirection.Clockwise,
                    true,
                    true);
            }
            geometry.Freeze();
            return geometry;
        }

        private static StreamGeometry CreateArrowGeometry(Point2d start, Point2d end, double thickness)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // 绘制主线
                ctx.BeginFigure(new WpfPoint(start.X, start.Y), false, false);
                ctx.LineTo(new WpfPoint(end.X, end.Y), true, true);

                // 添加箭头
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var arrowSize = thickness * 4;

                var arrowAngle1 = angle + Math.PI * 0.85;
                var arrowAngle2 = angle - Math.PI * 0.85;

                var arrow1 = new WpfPoint(
                    end.X + arrowSize * Math.Cos(arrowAngle1),
                    end.Y + arrowSize * Math.Sin(arrowAngle1));
                var arrow2 = new WpfPoint(
                    end.X + arrowSize * Math.Cos(arrowAngle2),
                    end.Y + arrowSize * Math.Sin(arrowAngle2));

                ctx.BeginFigure(new WpfPoint(end.X, end.Y), true, false);
                ctx.LineTo(arrow1, true, true);
                ctx.BeginFigure(new WpfPoint(end.X, end.Y), true, false);
                ctx.LineTo(arrow2, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        #endregion
    }
}
