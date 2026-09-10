using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering
{
    /// <summary>
    /// 区域渲染器接口
    /// 使用 WPF Shape 作为唯一渲染后端，保证渲染一致性和完整的交互能力
    /// </summary>
    public interface IRoiRenderer : IDisposable
    {
        /// <summary>
        /// 获取渲染目标（用于显示）
        /// </summary>
        ImageSource? RenderTarget { get; }

        /// <summary>
        /// 渲染宽度（像素）
        /// </summary>
        int Width { get; set; }

        /// <summary>
        /// 渲染高度（像素）
        /// </summary>
        int Height { get; set; }

        /// <summary>
        /// DPI缩放因子
        /// </summary>
        double DpiScale { get; set; }

        /// <summary>
        /// 渲染所有区域
        /// </summary>
        /// <param name="regions">区域渲染上下文集合</param>
        /// <param name="options">渲染选项</param>
        void Render(IEnumerable<RoiRenderContext> regions, RoiRenderOptions? options = null);

        /// <summary>
        /// 增量更新指定区域（高性能）
        /// </summary>
        /// <param name="region">区域渲染上下文</param>
        void UpdateRegion(RoiRenderContext region);

        /// <summary>
        /// 清空渲染内容
        /// </summary>
        void Clear();

        /// <summary>
        /// 设置视口变换（缩放、平移）
        /// </summary>
        /// <param name="scale">缩放比例</param>
        /// <param name="offset">偏移量</param>
        void SetViewportTransform(double scale, Point offset);
    }

    /// <summary>
    /// 区域渲染上下文 - 包含渲染所需的所有信息
    /// </summary>
    public class RoiRenderContext
    {
        /// <summary>
        /// 区域唯一标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 形状类型
        /// </summary>
        public ShapeType ShapeType { get; set; }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// 是否为预览状态
        /// </summary>
        public bool IsPreview { get; set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 区域类型（检测/屏蔽）
        /// </summary>
        public RoiType RoiType { get; set; } = RoiType.Inspection;

        #region 几何参数

        /// <summary>
        /// 中心点坐标
        /// </summary>
        public Point Center { get; set; }

        /// <summary>
        /// 宽度（矩形、旋转矩形）
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 高度（矩形、旋转矩形）
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// 旋转角度（度数，数学角度系统：[-180°, 180°]，逆时针为正）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 半径（圆形）
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// 直线起点
        /// </summary>
        public Point LineStart { get; set; }

        /// <summary>
        /// 直线终点
        /// </summary>
        public Point LineEnd { get; set; }

        /// <summary>
        /// 多边形顶点
        /// </summary>
        public List<Point> PolygonPoints { get; set; } = new();

        #endregion

        #region 样式参数

        /// <summary>
        /// 填充颜色
        /// </summary>
        public Color FillColor { get; set; } = Color.FromArgb(40, 255, 0, 0);

        /// <summary>
        /// 边框颜色
        /// </summary>
        public Color StrokeColor { get; set; } = Colors.Red;

        /// <summary>
        /// 边框厚度
        /// </summary>
        public double StrokeThickness { get; set; } = RoiEditorSettings.Default.DefaultStrokeThickness;

        /// <summary>
        /// 透明度 (0-1)
        /// </summary>
        public double Opacity { get; set; } = 0.3;

        #endregion

        #region 标签

        /// <summary>
        /// 区域名称标签
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// 标签位置
        /// </summary>
        public Point LabelPosition { get; set; }

        #endregion

        #region 边界（用于虚拟化）

        /// <summary>
        /// 区域边界矩形
        /// </summary>
        public Rect Bounds { get; set; }

        #endregion

        /// <summary>
        /// 计算区域边界
        /// </summary>
        public void CalculateBounds()
        {
            Bounds = ShapeType switch
            {
                ShapeType.Circle => new Rect(
                    Center.X - Radius,
                    Center.Y - Radius,
                    Radius * 2,
                    Radius * 2),
                ShapeType.RotatedRectangle => CalculateRotatedRectangleBounds(),
                ShapeType.Line => new Rect(
                    Math.Min(LineStart.X, LineEnd.X),
                    Math.Min(LineStart.Y, LineEnd.Y),
                    Math.Abs(LineEnd.X - LineStart.X),
                    Math.Abs(LineEnd.Y - LineStart.Y)),
                ShapeType.Polygon => CalculatePolygonBounds(),
                _ => Rect.Empty
            };
        }

        private Rect CalculateRotatedRectangleBounds()
        {
            var corners = GetRotatedRectangleCorners(Center, Width, Height, Angle);
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var corner in corners)
            {
                minX = Math.Min(minX, corner.X);
                minY = Math.Min(minY, corner.Y);
                maxX = Math.Max(maxX, corner.X);
                maxY = Math.Max(maxY, corner.Y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private Rect CalculatePolygonBounds()
        {
            if (PolygonPoints.Count == 0) return Rect.Empty;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var point in PolygonPoints)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 获取旋转矩形的四个角点
        /// </summary>
        public static Point[] GetRotatedRectangleCorners(Point center, double width, double height, double angle)
        {
            var angleRad = -angle * Math.PI / 180;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            var hw = width / 2;
            var hh = height / 2;

            return new[]
            {
                new Point(center.X + (-hw * cos - (-hh) * sin), center.Y + (-hw * sin + (-hh) * cos)), // TopLeft
                new Point(center.X + (hw * cos - (-hh) * sin), center.Y + (hw * sin + (-hh) * cos)),   // TopRight
                new Point(center.X + (hw * cos - hh * sin), center.Y + (hw * sin + hh * cos)),         // BottomRight
                new Point(center.X + (-hw * cos - hh * sin), center.Y + (-hw * sin + hh * cos))        // BottomLeft
            };
        }
    }

    /// <summary>
    /// 渲染选项
    /// </summary>
    public class RoiRenderOptions
    {
        /// <summary>
        /// 是否显示标签
        /// </summary>
        public bool ShowLabels { get; set; } = true;

        /// <summary>
        /// 是否显示编辑手柄
        /// </summary>
        public bool ShowHandles { get; set; } = true;

        /// <summary>
        /// 是否启用抗锯齿
        /// </summary>
        public bool EnableAntialiasing { get; set; } = true;

        /// <summary>
        /// 裁剪区域（用于虚拟化渲染）
        /// </summary>
        public Rect? ClipRect { get; set; }

        /// <summary>
        /// 手柄大小
        /// </summary>
        public double HandleSize { get; set; } = 60;

        /// <summary>
        /// 选中区域的高亮颜色
        /// </summary>
        public Color SelectionHighlightColor { get; set; } = Colors.Blue;

        /// <summary>
        /// 预览区域的填充颜色
        /// </summary>
        public Color PreviewFillColor { get; set; } = Color.FromArgb(30, 0, 120, 215);

        /// <summary>
        /// 预览区域的边框颜色
        /// </summary>
        public Color PreviewStrokeColor { get; set; } = Color.FromArgb(255, 0, 120, 215);
    }
}
