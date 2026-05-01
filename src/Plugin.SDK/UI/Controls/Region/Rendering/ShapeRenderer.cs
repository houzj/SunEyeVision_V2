using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering
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
            var strokeThickness = isSelected ? RegionEditorSettings.Default.SelectedStrokeThickness : shape.StrokeThickness;

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
            var width = shape.Width;
            var height = shape.Height;

            var rect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness,
            };

            // 中心定位（参考ROI编辑器的定位方式）
            var x = shape.CenterX - width / 2;
            var y = shape.CenterY - height / 2;

            // 如果是在Canvas上定位，这些值需要外部设置
            // 这里返回一个带有Tag信息的形状，便于定位
            rect.Tag = new { X = x, Y = y };

            return rect;
        }

        private static Ellipse CreateCircle(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            var radius = shape.Radius;

            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness,
            };

            // 中心定位
            var x = shape.CenterX - radius;
            var y = shape.CenterY - radius;

            ellipse.Tag = new { X = x, Y = y };

            return ellipse;
        }

        private static Rectangle CreateRotatedRectangle(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            var width = shape.Width;
            var height = shape.Height;
            var rotation = shape.Angle;

            var rect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness,
            };

            // 中心定位
            var x = shape.CenterX - width / 2;
            var y = shape.CenterY - height / 2;

            rect.Tag = new { X = x, Y = y };

            return rect;
        }

        private static Line CreateLine(ShapeParameters shape, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            var line = new Line
            {
                X1 = shape.StartX,
                Y1 = shape.StartY,
                X2 = shape.EndX,
                Y2 = shape.EndY,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness
            };

            return line;
        }

        private static Polygon CreatePolygon(ShapeParameters shape, Brush fillColor, Brush strokeColor, double strokeThickness, bool isPreview)
        {
            if (shape.Points.Count < 3) return null;

            var polygon = new Polygon
            {
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = strokeThickness,
            };

            var pointCollection = new PointCollection();
            double minX = double.MaxValue, minY = double.MaxValue;
            foreach (var pt in shape.Points)
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
