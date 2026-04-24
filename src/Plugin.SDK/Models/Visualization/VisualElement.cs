using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Extensions;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 可视化元素类型
    /// </summary>
    public enum VisualElementType
    {
        /// <summary>
        /// 点
        /// </summary>
        Point,

        /// <summary>
        /// 线段
        /// </summary>
        Line,

        /// <summary>
        /// 矩形
        /// </summary>
        Rectangle,

        /// <summary>
        /// 圆
        /// </summary>
        Circle,

        /// <summary>
        /// 圆弧
        /// </summary>
        Arc,

        /// <summary>
        /// 多边形
        /// </summary>
        Polygon,

        /// <summary>
        /// 文本
        /// </summary>
        Text,

        /// <summary>
        /// 箭头
        /// </summary>
        Arrow,

        /// <summary>
        /// 坐标系
        /// </summary>
        CoordinateSystem,

        /// <summary>
        /// 轮廓
        /// </summary>
        Contour,

        /// <summary>
        /// 路径
        /// </summary>
        Path
    }

    /// <summary>
    /// 可视化元素
    /// </summary>
    /// <remarks>
    /// 用于在图像上绘制检测结果、辅助线等可视化内容。
    /// 使用OpenCvSharp原生类型。
    /// </remarks>
    public sealed class VisualElement
    {
        /// <summary>
        /// 元素类型
        /// </summary>
        public VisualElementType Type { get; set; }

        /// <summary>
        /// 几何形状
        /// </summary>
        public object? Geometry { get; set; }

        /// <summary>
        /// 颜色（ARGB格式）
        /// </summary>
        public uint Color { get; set; } = 0xFFFF0000; // 默认红色

        /// <summary>
        /// 线宽
        /// </summary>
        public double LineWidth { get; set; } = 1.0;

        /// <summary>
        /// 是否填充
        /// </summary>
        public bool IsFilled { get; set; } = false;

        /// <summary>
        /// 填充颜色（ARGB格式）
        /// </summary>
        public uint FillColor { get; set; } = 0x800000FF; // 半透明蓝色

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 标签文本
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// 字体大小
        /// </summary>
        public double FontSize { get; set; } = 12.0;

        /// <summary>
        /// 元素名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 分类名称（用于分类控制可见性）
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 是否OK状态（影响颜色）
        /// </summary>
        public bool IsOk { get; set; } = true;

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Z顺序（用于渲染排序）
        /// </summary>
        public int ZOrder { get; set; }

        #region 静态工厂方法
        /// <summary>
        /// 创建点元素
        /// </summary>
        public static VisualElement Point(Point2d position, uint color = 0xFFFF0000, double size = 3.0)
        {
            return new VisualElement
            {
                Type = VisualElementType.Point,
                Geometry = position,
                Color = color,
                LineWidth = size
            };
        }

        /// <summary>
        /// 创建线段元素
        /// </summary>
        public static VisualElement Line(Point2d start, Point2d end, uint color = 0xFF00FF00, double lineWidth = 1.0)
        {
            return new VisualElement
            {
                Type = VisualElementType.Line,
                Geometry = new LineSegment(start, end),
                Color = color,
                LineWidth = lineWidth
            };
        }

        /// <summary>
        /// 创建线段元素（从Line2d结构）
        /// </summary>
        public static VisualElement FromLine(Line2d line, double length, uint color = 0xFF00FF00, double lineWidth = 1.0)
        {
            Point2d start = line.GetPointAt(-length / 2);
            Point2d end = line.GetPointAt(length / 2);
            return Line(start, end, color, lineWidth);
        }

        /// <summary>
        /// 创建矩形元素
        /// </summary>
        public static VisualElement Rectangle(Rect2d rect, uint color = 0xFFFF0000, double lineWidth = 1.0, bool filled = false)
        {
            return new VisualElement
            {
                Type = VisualElementType.Rectangle,
                Geometry = rect,
                Color = color,
                LineWidth = lineWidth,
                IsFilled = filled
            };
        }

        /// <summary>
        /// 创建矩形元素（从RotatedRect）
        /// </summary>
        public static VisualElement FromRotatedRect(RotatedRect rect, uint color = 0xFFFF0000, double lineWidth = 1.0, bool filled = false)
        {
            return new VisualElement
            {
                Type = VisualElementType.Rectangle,
                Geometry = rect,
                Color = color,
                LineWidth = lineWidth,
                IsFilled = filled
            };
        }

        /// <summary>
        /// 创建圆形元素
        /// </summary>
        public static VisualElement Circle(Circle2d circle, uint color = 0xFF0000FF, double lineWidth = 1.0, bool filled = false)
        {
            return new VisualElement
            {
                Type = VisualElementType.Circle,
                Geometry = circle,
                Color = color,
                LineWidth = lineWidth,
                IsFilled = filled
            };
        }

        /// <summary>
        /// 创建圆弧元素
        /// </summary>
        public static VisualElement Arc(Point2d center, double radius, double startAngle, double endAngle, uint color = 0xFF00FFFF, double lineWidth = 1.0)
        {
            return new VisualElement
            {
                Type = VisualElementType.Arc,
                Geometry = new ArcGeometry(center, radius, startAngle, endAngle),
                Color = color,
                LineWidth = lineWidth
            };
        }

        /// <summary>
        /// 创建多边形元素
        /// </summary>
        public static VisualElement Polygon(Point2d[] points, uint color = 0xFFFF00FF, double lineWidth = 1.0, bool filled = false)
        {
            return new VisualElement
            {
                Type = VisualElementType.Polygon,
                Geometry = points,
                Color = color,
                LineWidth = lineWidth,
                IsFilled = filled
            };
        }

        /// <summary>
        /// 创建文本元素
        /// </summary>
        public static VisualElement Text(string text, Point2d position, uint color = 0xFFFFFFFF, double fontSize = 12.0)
        {
            return new VisualElement
            {
                Type = VisualElementType.Text,
                Geometry = position,
                Label = text,
                Color = color,
                FontSize = fontSize
            };
        }

        /// <summary>
        /// 创建箭头元素
        /// </summary>
        public static VisualElement Arrow(Point2d start, Point2d end, uint color = 0xFFFFFF00, double lineWidth = 2.0)
        {
            return new VisualElement
            {
                Type = VisualElementType.Arrow,
                Geometry = new LineSegment(start, end),
                Color = color,
                LineWidth = lineWidth
            };
        }

        /// <summary>
        /// 创建坐标系元素
        /// </summary>
        public static VisualElement CoordinateSystem(Mat transformMatrix, double axisLength = 50, uint xAxisColor = 0xFFFF0000, uint yAxisColor = 0xFF00FF00)
        {
            return new VisualElement
            {
                Type = VisualElementType.CoordinateSystem,
                Geometry = new CoordinateSystemGeometry(transformMatrix, axisLength, xAxisColor, yAxisColor)
            };
        }

        /// <summary>
        /// 创建轮廓元素
        /// </summary>
        public static VisualElement Contour(Point2d[] points, uint color = 0xFF00FF00, double lineWidth = 1.0, bool closed = true)
        {
            return new VisualElement
            {
                Type = VisualElementType.Contour,
                Geometry = new ContourGeometry(points, closed),
                Color = color,
                LineWidth = lineWidth
            };
        }

        #endregion
    }

    /// <summary>
    /// 线段几何
    /// </summary>
    public sealed class LineSegment
    {
        /// <summary>
        /// 起点
        /// </summary>
        public Point2d Start { get; }

        /// <summary>
        /// 终点
        /// </summary>
        public Point2d End { get; }

        public LineSegment(Point2d start, Point2d end)
        {
            Start = start;
            End = end;
        }

        public double Length => Start.DistanceTo(End);
    }

    /// <summary>
    /// 圆弧几何
    /// </summary>
    public sealed class ArcGeometry
    {
        public Point2d Center { get; }
        public double Radius { get; }
        public double StartAngle { get; }
        public double EndAngle { get; }

        public ArcGeometry(Point2d center, double radius, double startAngle, double endAngle)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }
    }

    /// <summary>
    /// 坐标系几何
    /// </summary>
    public sealed class CoordinateSystemGeometry
    {
        public Mat TransformMatrix { get; }
        public double AxisLength { get; }
        public uint XAxisColor { get; }
        public uint YAxisColor { get; }

        public CoordinateSystemGeometry(Mat transformMatrix, double axisLength, uint xAxisColor, uint yAxisColor)
        {
            TransformMatrix = transformMatrix;
            AxisLength = axisLength;
            XAxisColor = xAxisColor;
            YAxisColor = yAxisColor;
        }
    }

    /// <summary>
    /// 轮廓几何
    /// </summary>
    public sealed class ContourGeometry
    {
        public Point2d[] Points { get; }
        public bool IsClosed { get; }

        public ContourGeometry(Point2d[] points, bool isClosed)
        {
            Points = points;
            IsClosed = isClosed;
        }
    }

    /// <summary>
    /// 可视化元素集合
    /// </summary>
    public sealed class VisualElementCollection : List<VisualElement>
    {
        /// <summary>
        /// 添加点
        /// </summary>
        public void AddPoint(Point2d position, uint color = 0xFFFF0000, double size = 3.0)
        {
            Add(VisualElement.Point(position, color, size));
        }

        /// <summary>
        /// 添加线段
        /// </summary>
        public void AddLine(Point2d start, Point2d end, uint color = 0xFF00FF00, double lineWidth = 1.0)
        {
            Add(VisualElement.Line(start, end, color, lineWidth));
        }

        /// <summary>
        /// 添加矩形
        /// </summary>
        public void AddRectangle(Rect2d rect, uint color = 0xFFFF0000, double lineWidth = 1.0, bool filled = false)
        {
            Add(VisualElement.Rectangle(rect, color, lineWidth, filled));
        }

        /// <summary>
        /// 添加圆形
        /// </summary>
        public void AddCircle(Circle2d circle, uint color = 0xFF0000FF, double lineWidth = 1.0, bool filled = false)
        {
            Add(VisualElement.Circle(circle, color, lineWidth, filled));
        }

        /// <summary>
        /// 添加文本
        /// </summary>
        public void AddText(string text, Point2d position, uint color = 0xFFFFFFFF, double fontSize = 12.0)
        {
            Add(VisualElement.Text(text, position, color, fontSize));
        }

        /// <summary>
        /// 获取所有可见元素
        /// </summary>
        public IEnumerable<VisualElement> GetVisibleElements()
        {
            return this.Where(e => e.IsVisible);
        }

        /// <summary>
        /// 按Z顺序排序
        /// </summary>
        public IEnumerable<VisualElement> GetSortedByZOrder()
        {
            return this.OrderBy(e => e.ZOrder);
        }
    }
}
