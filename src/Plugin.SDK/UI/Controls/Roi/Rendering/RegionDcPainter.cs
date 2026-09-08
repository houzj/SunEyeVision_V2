using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.UI.Constants;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering
{
    /// <summary>
    /// 区域 DC 绘制单一实现：把一个 RoiRenderContext（区域）的全部视觉
    /// （形状本体 + 方向箭头 + 名称标签）画入一个 DrawingContext。
    /// 供 RoiVisualHost 的每区域 DrawingVisual 使用；几何唯一实现，避免双渲染漂移。
    /// 坐标 = 画布坐标（宿主原点），不乘缩放；线宽/字号按 ViewportScaling 反除，由外层 ScaleTransform 放大。
    /// </summary>
    public static class RegionDcPainter
    {
        /// <summary>绘制一个区域的完整视觉（形状 → 方向箭头 → 标签）。</summary>
        public static void DrawRegion(DrawingContext dc, RoiRenderContext region, double viewportScale, double pixelsPerDip = 1.0)
        {
            region.CalculateBounds();

            Color fillColor = region.FillColor;
            Color strokeColor = region.StrokeColor;
            double strokeThickness = region.StrokeThickness;

            // 预览状态使用类型差异色（对齐旧 WpfShapeRenderer.CreateShape）
            if (region.IsPreview)
            {
                switch (region.RoiType)
                {
                    case RoiType.Mask:
                        fillColor = ColorConstants.MaskFill;
                        strokeColor = ColorConstants.MaskOrange;
                        break;
                    case RoiType.Inspection:
                    default:
                        fillColor = ColorConstants.InspectionFill;
                        strokeColor = ColorConstants.InspectionBlue;
                        break;
                }
            }

            // 选中状态增加边框宽度（对齐 RoiEditorSettings.Default.SelectedStrokeThickness）
            if (region.IsSelected)
                strokeThickness = RoiEditorSettings.Default.SelectedStrokeThickness;

            // 反除缩放：屏幕线宽恒定
            strokeThickness = ViewportScaling.InvScale(viewportScale, strokeThickness);

            DrawShape(dc, region, fillColor, strokeColor, strokeThickness);
            DrawDirectionArrow(dc, region, viewportScale);
            DrawLabel(dc, region, viewportScale, pixelsPerDip);
        }

        private static void DrawShape(DrawingContext dc, RoiRenderContext region, Color fillColor, Color strokeColor, double strokeThickness)
        {
            Brush? fill = MakeFill(fillColor, region.Opacity);
            Pen? pen = MakePen(strokeColor, strokeThickness);

            switch (region.ShapeType)
            {
                case ShapeType.Rectangle:
                    var rect = new Rect(region.Center.X - region.Width / 2, region.Center.Y - region.Height / 2, region.Width, region.Height);
                    dc.DrawRectangle(fill, pen, rect);
                    break;

                case ShapeType.Circle:
                    dc.DrawEllipse(fill, pen, region.Center, region.Radius, region.Radius);
                    break;

                case ShapeType.RotatedRectangle:
                    // 屏幕角度系：顺时针为正，绕矩形中心旋转（对齐旧 Rectangle.RenderTransform(Angle=region.Angle)）
                    dc.PushTransform(new RotateTransform(region.Angle, region.Center.X, region.Center.Y));
                    var rrect = new Rect(region.Center.X - region.Width / 2, region.Center.Y - region.Height / 2, region.Width, region.Height);
                    dc.DrawRectangle(fill, pen, rrect);
                    dc.Pop();
                    break;

                case ShapeType.Line:
                    dc.DrawLine(pen, region.LineStart, region.LineEnd);
                    break;

                case ShapeType.Polygon:
                    if (region.PolygonPoints.Count >= 3)
                        dc.DrawGeometry(fill, pen, BuildPolygonGeometry(region.PolygonPoints));
                    break;
            }
        }

        private static Geometry BuildPolygonGeometry(System.Collections.Generic.List<Point> points)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(points[0], true, true);
                for (int i = 1; i < points.Count; i++)
                    ctx.LineTo(points[i], true, true);
            }
            geo.Freeze();
            return geo;
        }

        /// <summary>旋转矩形方向箭头（对齐 RotatedRectangleHelper.GetDirectionArrow + 旧 DrawDirectionArrow 反除参数）。</summary>
        private static void DrawDirectionArrow(DrawingContext dc, RoiRenderContext region, double viewportScale)
        {
            if (region.ShapeType != ShapeType.RotatedRectangle) return;

            var arrow = RotatedRectangleHelper.GetDirectionArrow(region.Center, region.Width, region.Angle);
            var color = region.IsSelected ? Colors.Blue : region.StrokeColor;

            double lineThickness = ViewportScaling.InvScale(viewportScale, 3.0);
            double arrowSize = ViewportScaling.InvScale(viewportScale, 10.0);
            const double arrowAngleRad = 25.0 * Math.PI / 180.0;

            var pen = MakePen(color, lineThickness);
            if (pen == null) return;

            // 主箭头线（Start → End）
            dc.DrawLine(pen, arrow.Start, arrow.End);

            // 方向向量与两翼
            double dx = arrow.End.X - arrow.Start.X;
            double dy = arrow.End.Y - arrow.Start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 5) return;
            double ux = dx / length, uy = dy / length;

            double cosA = Math.Cos(arrowAngleRad), sinA = Math.Sin(arrowAngleRad);
            var leftWing = new Point(
                arrow.End.X - arrowSize * (ux * cosA + uy * sinA),
                arrow.End.Y - arrowSize * (-ux * sinA + uy * cosA));
            var rightWing = new Point(
                arrow.End.X - arrowSize * (ux * cosA - uy * sinA),
                arrow.End.Y - arrowSize * (ux * sinA + uy * cosA));

            dc.DrawLine(pen, arrow.End, leftWing);
            dc.DrawLine(pen, arrow.End, rightWing);
        }

        /// <summary>名称标签（对齐旧 TextBlock 渲染：背景 + 内边距 + 白字，幂律字号）。</summary>
        private static void DrawLabel(DrawingContext dc, RoiRenderContext region, double viewportScale, double pixelsPerDip)
        {
            if (string.IsNullOrEmpty(region.Label)) return;

            double scaledFontSize = HandleRenderer.GetScaledLabelFontSize(viewportScale);
            double padH = ViewportScaling.InvScale(viewportScale, 4.0);
            double padV = ViewportScaling.InvScale(viewportScale, 2.0);

            var formatted = new FormattedText(region.Label,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                scaledFontSize, Brushes.White,
                Math.Max(pixelsPerDip, 1.0));

            // 位置：标签整体置于区域外上方，文本底边与区域上边保持 ≥6px 屏间距（不压区域）
            // 旧实现把屏蔽区标签额外下移 1.2 字号 → 标签顶部贴入区域上边缘内部（0.3 字号），背景块遮挡区域；
            // 改为统一外置：上移量 = 文本高度 + 安全间距，屏蔽区不再额外下移压入。
            double labelLeft = region.Bounds.Left + (region.Bounds.Width - formatted.Width) / 2;
            double safeGap = ViewportScaling.InvScale(viewportScale, 6.0);   // 屏 ≥6px 外距
            double labelTop = region.Bounds.Top - formatted.Height - padV - safeGap;
            if (labelTop < 0) labelTop = region.Bounds.Bottom + safeGap;      // 顶部无空间 → 放区域下方外侧

            // 背景 + 文字（文字相对背景的内边距）
            var bgRect = new Rect(labelLeft, labelTop, formatted.Width + padH * 2, formatted.Height + padV * 2);
            var bg = MakeFill(ColorConstants.LabelBackground, 1.0);
            dc.DrawRectangle(bg, null, bgRect);
            dc.DrawText(formatted, new Point(labelLeft + padH, labelTop + padV));
        }

        private static Brush? MakeFill(Color color, double opacity)
        {
            var brush = new SolidColorBrush(color);
            if (opacity < 1.0) brush.Opacity = opacity;
            brush.Freeze();
            return brush;
        }

        private static Pen? MakePen(Color color, double thickness)
        {
            if (thickness <= 0) return null;
            var pen = new Pen(new SolidColorBrush(color), thickness);
            pen.Freeze();
            return pen;
        }
    }
}
