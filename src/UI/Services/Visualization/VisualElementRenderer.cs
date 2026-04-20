using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

// 定义类型别名解决歧义
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfPath = System.Windows.Shapes.Path;
using WpfCanvas = System.Windows.Controls.Canvas;
using SdkLineSegment = SunEyeVision.Plugin.SDK.Models.Visualization.LineSegment;

namespace SunEyeVision.UI.Services.Visualization
{
    /// <summary>
    /// 可视化元素渲染器 - 将 VisualElement 转换为 WPF UIElement
    /// </summary>
    /// <remarks>
    /// 支持的可视化元素类型：
    /// - Point: 点
    /// - Line: 线段
    /// - Rectangle: 矩形（支持旋转矩形）
    /// - Circle: 圆
    /// - Arc: 圆弧
    /// - Polygon: 多边形
    /// - Text: 文本
    /// - Arrow: 箭头
    /// - Contour: 轮廓
    /// - CoordinateSystem: 坐标系
    /// </remarks>
    public class VisualElementRenderer
    {
        /// <summary>
        /// 渲染可视化元素集合
        /// </summary>
        /// <param name="elements">可视化元素集合</param>
        /// <returns>WPF UIElement 列表</returns>
        public List<UIElement> Render(IEnumerable<VisualElement> elements)
        {
            var result = new List<UIElement>();
            
            if (elements == null)
                return result;
            
            foreach (var element in elements)
            {
                if (!element.IsVisible) continue;
                
                try
                {
                    var uiElement = RenderElement(element);
                    if (uiElement != null)
                    {
                        result.Add(uiElement);
                    }
                }
                catch (Exception ex)
                {
}
            }
            
            return result;
        }
        
        /// <summary>
        /// 渲染单个可视化元素
        /// </summary>
        /// <param name="element">可视化元素</param>
        /// <returns>WPF UIElement</returns>
        public UIElement? RenderElement(VisualElement element)
        {
            if (element == null || element.Geometry == null)
                return null;
            
            return element.Type switch
            {
                VisualElementType.Point => RenderPoint(element),
                VisualElementType.Line => RenderLine(element),
                VisualElementType.Rectangle => RenderRectangle(element),
                VisualElementType.Circle => RenderCircle(element),
                VisualElementType.Arc => RenderArc(element),
                VisualElementType.Polygon => RenderPolygon(element),
                VisualElementType.Text => RenderText(element),
                VisualElementType.Arrow => RenderArrow(element),
                VisualElementType.Contour => RenderContour(element),
                VisualElementType.CoordinateSystem => RenderCoordinateSystem(element),
                VisualElementType.Path => RenderPath(element),
                _ => null
            };
        }
        
        #region 渲染方法
        
        /// <summary>
        /// 渲染点
        /// </summary>
        private UIElement RenderPoint(VisualElement element)
        {
            if (element.Geometry is Point2d point)
            {
                var size = element.LineWidth * 2;
                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = CreateBrush(element.Color)
                };
                
                WpfCanvas.SetLeft(ellipse, point.X - element.LineWidth);
                WpfCanvas.SetTop(ellipse, point.Y - element.LineWidth);
                
                return ellipse;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染线段
        /// </summary>
        private UIElement RenderLine(VisualElement element)
        {
            if (element.Geometry is SdkLineSegment line)
            {
                var lineShape = new Line
                {
                    X1 = line.Start.X,
                    Y1 = line.Start.Y,
                    X2 = line.End.X,
                    Y2 = line.End.Y,
                    Stroke = CreateBrush(element.Color),
                    StrokeThickness = element.LineWidth
                };
                
                return lineShape;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染矩形
        /// </summary>
        private UIElement RenderRectangle(VisualElement element)
        {
            // 处理 Rect2d
            if (element.Geometry is Rect2d rect)
            {
                var rectangle = new System.Windows.Shapes.Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Stroke = CreateBrush(element.Color),
                    StrokeThickness = element.LineWidth,
                    Fill = element.IsFilled ? CreateBrush(element.FillColor) : Brushes.Transparent
                };
                
                WpfCanvas.SetLeft(rectangle, rect.X);
                WpfCanvas.SetTop(rectangle, rect.Y);
                
                return rectangle;
            }
            
            // 处理 RotatedRect
            if (element.Geometry is RotatedRect rotatedRect)
            {
                var points = Cv2.BoxPoints(rotatedRect);
                // 转换 Point2f[] 到 Point2d[]
                var points2d = Array.ConvertAll(points, p => new Point2d(p.X, p.Y));
                return RenderPolygonFromPoints(points2d, element);
            }
            
            return null!;
        }
        
        /// <summary>
        /// 渲染圆形
        /// </summary>
        private UIElement RenderCircle(VisualElement element)
        {
            if (element.Geometry is Circle2d circle)
            {
                var ellipse = new Ellipse
                {
                    Width = circle.Radius * 2,
                    Height = circle.Radius * 2,
                    Stroke = CreateBrush(element.Color),
                    StrokeThickness = element.LineWidth,
                    Fill = element.IsFilled ? CreateBrush(element.FillColor) : Brushes.Transparent
                };
                
                WpfCanvas.SetLeft(ellipse, circle.Center.X - circle.Radius);
                WpfCanvas.SetTop(ellipse, circle.Center.Y - circle.Radius);
                
                return ellipse;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染圆弧
        /// </summary>
        private UIElement RenderArc(VisualElement element)
        {
            if (element.Geometry is ArcGeometry arc)
            {
                var path = new WpfPath
                {
                    Stroke = CreateBrush(element.Color),
                    StrokeThickness = element.LineWidth
                };
                
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    // 计算起点和终点
                    var startAngleRad = arc.StartAngle * Math.PI / 180;
                    var endAngleRad = arc.EndAngle * Math.PI / 180;
                    
                    var startX = arc.Center.X + arc.Radius * Math.Cos(startAngleRad);
                    var startY = arc.Center.Y + arc.Radius * Math.Sin(startAngleRad);
                    var endX = arc.Center.X + arc.Radius * Math.Cos(endAngleRad);
                    var endY = arc.Center.Y + arc.Radius * Math.Sin(endAngleRad);
                    
                    context.BeginFigure(new WpfPoint(startX, startY), false, false);
                    context.ArcTo(
                        new WpfPoint(endX, endY),
                        new WpfSize(arc.Radius, arc.Radius),
                        0,
                        false,
                        SweepDirection.Clockwise,
                        true,
                        true);
                }
                
                path.Data = geometry;
                return path;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染多边形
        /// </summary>
        private UIElement RenderPolygon(VisualElement element)
        {
            if (element.Geometry is Point2d[] points)
            {
                return RenderPolygonFromPoints(points, element);
            }
            return null!;
        }
        
        /// <summary>
        /// 从点数组渲染多边形
        /// </summary>
        private UIElement RenderPolygonFromPoints(Point2d[] points, VisualElement element)
        {
            if (points == null || points.Length < 2)
                return null!;
            
            var polygon = new Polygon
            {
                Stroke = CreateBrush(element.Color),
                StrokeThickness = element.LineWidth,
                Fill = element.IsFilled ? CreateBrush(element.FillColor) : Brushes.Transparent
            };
            
            var pointCollection = new PointCollection();
            foreach (var p in points)
            {
                pointCollection.Add(new WpfPoint(p.X, p.Y));
            }
            polygon.Points = pointCollection;
            
            return polygon;
        }
        
        /// <summary>
        /// 渲染文本
        /// </summary>
        private UIElement RenderText(VisualElement element)
        {
            if (element.Geometry is Point2d position && !string.IsNullOrEmpty(element.Label))
            {
                var textBlock = new TextBlock
                {
                    Text = element.Label,
                    Foreground = CreateBrush(element.Color),
                    FontSize = element.FontSize
                };
                
                WpfCanvas.SetLeft(textBlock, position.X);
                WpfCanvas.SetTop(textBlock, position.Y);
                
                return textBlock;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染箭头
        /// </summary>
        private UIElement RenderArrow(VisualElement element)
        {
            if (element.Geometry is SdkLineSegment line)
            {
                var path = new WpfPath
                {
                    Stroke = CreateBrush(element.Color),
                    StrokeThickness = element.LineWidth
                };
                
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    // 绘制主线
                    context.BeginFigure(new WpfPoint(line.Start.X, line.Start.Y), false, false);
                    context.LineTo(new WpfPoint(line.End.X, line.End.Y), true, true);
                    
                    // 添加箭头
                    var angle = Math.Atan2(line.End.Y - line.Start.Y, line.End.X - line.Start.X);
                    var arrowSize = element.LineWidth * 4;
                    
                    var arrowAngle1 = angle + Math.PI * 0.85;
                    var arrowAngle2 = angle - Math.PI * 0.85;
                    
                    var arrow1 = new WpfPoint(
                        line.End.X + arrowSize * Math.Cos(arrowAngle1),
                        line.End.Y + arrowSize * Math.Sin(arrowAngle1));
                    var arrow2 = new WpfPoint(
                        line.End.X + arrowSize * Math.Cos(arrowAngle2),
                        line.End.Y + arrowSize * Math.Sin(arrowAngle2));
                    
                    context.BeginFigure(new WpfPoint(line.End.X, line.End.Y), true, false);
                    context.LineTo(arrow1, true, true);
                    context.BeginFigure(new WpfPoint(line.End.X, line.End.Y), true, false);
                    context.LineTo(arrow2, true, true);
                }
                
                path.Data = geometry;
                return path;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染轮廓
        /// </summary>
        private UIElement RenderContour(VisualElement element)
        {
            if (element.Geometry is ContourGeometry contour)
            {
                if (contour.Points == null || contour.Points.Length < 2)
                    return null!;
                
                if (contour.IsClosed && contour.Points.Length >= 3)
                {
                    return RenderPolygonFromPoints(contour.Points, element);
                }
                else
                {
                    // 开放轮廓使用折线
                    var polyline = new Polyline
                    {
                        Stroke = CreateBrush(element.Color),
                        StrokeThickness = element.LineWidth
                    };
                    
                    var pointCollection = new PointCollection();
                    foreach (var p in contour.Points)
                    {
                        pointCollection.Add(new WpfPoint(p.X, p.Y));
                    }
                    polyline.Points = pointCollection;
                    
                    return polyline;
                }
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染坐标系
        /// </summary>
        private UIElement RenderCoordinateSystem(VisualElement element)
        {
            if (element.Geometry is CoordinateSystemGeometry coordSys)
            {
                var group = new Grid();
                
                // TODO: 根据变换矩阵绘制坐标轴
                // 暂时返回空
                return group;
            }
            return null!;
        }
        
        /// <summary>
        /// 渲染路径（通用路径）
        /// </summary>
        private UIElement RenderPath(VisualElement element)
        {
            // Path 类型需要更复杂的处理，暂时返回空
            return null!;
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 从 ARGB 颜色值创建画刷
        /// </summary>
        /// <param name="argb">ARGB 格式的颜色值</param>
        /// <returns>SolidColorBrush</returns>
        private SolidColorBrush CreateBrush(uint argb)
        {
            var a = (byte)((argb >> 24) & 0xFF);
            var r = (byte)((argb >> 16) & 0xFF);
            var g = (byte)((argb >> 8) & 0xFF);
            var b = (byte)(argb & 0xFF);
            
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
        
        #endregion
    }
}
