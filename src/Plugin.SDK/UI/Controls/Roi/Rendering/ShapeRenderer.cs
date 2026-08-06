using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering
{
    /// <summary>
    /// 形状渲染器 - 参考ROI编辑器的CreateROIShape方法
    /// </summary>
    public class ShapeRenderer
    {
        /// <summary>
        /// ARGB颜色转WPF Color
        /// </summary>
        private static Color ConvertArgbToColor(uint argb)
        {
            var a = (byte)((argb >> 24) & 0xFF);
            var r = (byte)((argb >> 16) & 0xFF);
            var g = (byte)((argb >> 8) & 0xFF);
            var b = (byte)(argb & 0xFF);
            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// 创建形状（参考ROIImageEditor.CreateROIShape 1820-1896行）
        /// </summary>
        public static Shape CreateShape(ShapeParameters shape, bool isSelected, bool isPreview = false)
        {
            if (shape == null) return null;

            // 颜色设置（参考ROI编辑器的样式）
            Color fillColor;
            if (isPreview)
            {
                fillColor = Color.FromArgb(30, 0, 120, 215);
            }
            else
            {
                var fillColorArgb = isSelected ? 0x280000FF : shape.FillColorArgb;
                fillColor = ConvertArgbToColor(fillColorArgb);
            }

            var fillColorBrush = new SolidColorBrush(fillColor)
            {
                Opacity = isPreview ? 0.3 : shape.Opacity
            };

            var strokeColorArgb = isSelected ? 0xFFFF0000 : shape.StrokeColorArgb;
            var strokeColor = new SolidColorBrush(ConvertArgbToColor(strokeColorArgb));
            var strokeThickness = isSelected ? RoiEditorSettings.Default.SelectedStrokeThickness : shape.StrokeThickness;

            Shape result = null;

            switch (shape.ShapeType)
            {
                case ShapeType.Rectangle:
                    result = CreateRectangle(shape, fillColorBrush, strokeColor, strokeThickness, isPreview);
                    break;

                case ShapeType.Circle:
                    result = CreateCircle(shape, fillColorBrush, strokeColor, strokeThickness, isPreview);
                    break;

                case ShapeType.RotatedRectangle:
                    result = CreateRotatedRectangle(shape, fillColorBrush, strokeColor, strokeThickness, isPreview);
                    break;

                case ShapeType.Line:
                    result = CreateLine(shape, strokeColor, strokeThickness, isPreview);
                    break;

                case ShapeType.Polygon:
                    result = CreatePolygon(shape, fillColorBrush, strokeColor, strokeThickness, isPreview);
                    break;
            }

            return result;
        }

        private static Rectangle CreateRectangle(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            if (shape.Geometry is RotatedRect2d rr)
            {
                var rect = new Rectangle
                {
                    Width = rr.Width, Height = rr.Height,
                    Fill = fillColor, Stroke = strokeColor,
                    StrokeThickness = strokeThickness,
                };
                rect.Tag = new { X = rr.CenterX - rr.Width / 2, Y = rr.CenterY - rr.Height / 2 };
                return rect;
            }
            else if (shape.Geometry is System.Windows.Rect r)
            {
                var rect = new Rectangle
                {
                    Width = r.Width, Height = r.Height,
                    Fill = fillColor, Stroke = strokeColor,
                    StrokeThickness = strokeThickness,
                };
                rect.Tag = new { X = r.X, Y = r.Y };
                return rect;
            }
            return null;
        }

        private static Ellipse CreateCircle(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            if (shape.Geometry is not Circle2d c) return null;
            var ellipse = new Ellipse
            {
                Width = c.Radius * 2, Height = c.Radius * 2,
                Fill = fillColor, Stroke = strokeColor,
                StrokeThickness = strokeThickness,
            };
            ellipse.Tag = new { X = c.Center.X - c.Radius, Y = c.Center.Y - c.Radius };
            return ellipse;
        }

        private static Rectangle CreateRotatedRectangle(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            if (shape.Geometry is RotatedRect2d rr)
            {
                var rect = new Rectangle
                {
                    Width = rr.Width,
                    Height = rr.Height,
                    Fill = fillColor,
                    Stroke = strokeColor,
                    StrokeThickness = strokeThickness,
                };
                rect.Tag = new { X = rr.CenterX - rr.Width / 2, Y = rr.CenterY - rr.Height / 2 };
                return rect;
            }
            else if (shape.Geometry is System.Windows.Rect r)
            {
                var rect = new Rectangle
                {
                    Width = r.Width,
                    Height = r.Height,
                    Fill = fillColor,
                    Stroke = strokeColor,
                    StrokeThickness = strokeThickness,
                };
                rect.Tag = new { X = r.X, Y = r.Y };
                return rect;
            }
            return null;
        }

        private static Line CreateLine(ShapeParameters shape, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            if (shape.Geometry is not Line2d l)
                return new Line();

            var startPt = new System.Windows.Point(l.Point.X, l.Point.Y);
            var endPt = l.GetPointAt(100);
            return new Line
            {
                X1 = startPt.X,
                Y1 = startPt.Y,
                X2 = endPt.X,
                Y2 = endPt.Y,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness
            };
        }

        private static Polygon CreatePolygon(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            if (shape.Geometry is not Polygon2d poly || poly.Vertices.Length < 3) return null;

            var polygon = new Polygon
            {
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness,
            };

            var pointCollection = new PointCollection();
            double minX = double.MaxValue, minY = double.MaxValue;
            foreach (var pt in poly.Vertices)
            {
                pointCollection.Add(new System.Windows.Point(pt.X, pt.Y));
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
            }
            polygon.Points = pointCollection;
            polygon.Tag = new { X = minX, Y = minY };

            return polygon;
        }
    }
}
