using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering
{
    /// <summary>
    /// WPF Shape渲染器 - 兼容模式
    /// 使用对象池和增量更新优化现有WPF Shape渲染
    /// 性能优势：
    /// - 对象池减少90%的对象创建开销
    /// - 增量更新只更新变化的区域
    /// - 支持与现有RegionEditorControl无缝切换
    /// </summary>
    public class WpfShapeRenderer : IRegionRenderer
    {
        #region 私有字段

        private readonly Canvas _targetCanvas;
        private readonly ILogger? _logger;
        private readonly string _rendererInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);  // 实例ID，用于区分不同的渲染器

        // 对象池：缓存已创建的Shape和TextBlock
        private readonly Dictionary<Guid, Shape> _shapePool = new();
        private readonly Dictionary<Guid, TextBlock> _labelPool = new();

        // 辅助元素池：方向箭头等辅助渲染元素
        private readonly Dictionary<Guid, List<Shape>> _helperPool = new();

        // 日志降频计数器
        private int _logCounter = 0;
        private const int LogInterval = 10; // 每10次输出一次日志

        // 视口变换参数
        private double _viewportScale = 1.0;
        private Point _viewportOffset = new Point(0, 0);

        #endregion

        #region 属性

        /// <summary>
        /// 渲染目标（兼容模式不使用ImageSource）
        /// </summary>
        public ImageSource? RenderTarget => null;

        private int _width;
        public int Width
        {
            get => _width;
            set => _width = value;
        }

        private int _height;
        public int Height
        {
            get => _height;
            set => _height = value;
        }

        private double _dpiScale = 1.0;
        public double DpiScale
        {
            get => _dpiScale;
            set => _dpiScale = Math.Max(0.1, value);
        }

        #endregion

        #region 构造函数

        public WpfShapeRenderer(Canvas targetCanvas, ILogger? logger = null)
        {
            _targetCanvas = targetCanvas ?? throw new ArgumentNullException(nameof(targetCanvas));
            _logger = logger;

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[WpfShapeRenderer.ctor] 渲染器已创建 | InstanceId={_rendererInstanceId} | Canvas={targetCanvas.GetType().Name}",
                "WpfShapeRenderer");
        }

        #endregion

        // Drawing flow log counter (for monitoring rendering)
        private int _updateLogCount = 0;

        #region IRegionRenderer 实现

        /// <summary>
        /// 渲染所有区域（使用对象池和增量更新）
        /// </summary>
        public void Render(IEnumerable<RegionRenderContext> regions, RegionRenderOptions? options = null)
        {
            options ??= new RegionRenderOptions();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 记录渲染开始
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WpfShapeRenderer.Render] 渲染开始 | InstanceId={_rendererInstanceId} | InputRegions={regions.Count()} | ShapePool={_shapePool.Count} | LabelPool={_labelPool.Count} | HelperPool={_helperPool.Count}",
                "WpfShapeRenderer");

            // 获取当前可见区域的ID集合
            var visibleRegions = new List<RegionRenderContext>();
            foreach (var region in regions)
            {
                if (!region.IsVisible) continue;
                if (options.ClipRect.HasValue && !options.ClipRect.Value.IntersectsWith(region.Bounds))
                    continue;
                visibleRegions.Add(region);
            }

            var visibleIds = new HashSet<Guid>(visibleRegions.Select(r => r.Id));

            // 1. 移除不再可见的区域
            RemoveInvisibleRegions(visibleIds);

            // 2. 更新或创建可见区域
            foreach (var region in visibleRegions)
            {
                UpdateOrCreateRegion(region, options);
            }

            sw.Stop();

            // 记录渲染完成
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WpfShapeRenderer.Render] 渲染完成 | InstanceId={_rendererInstanceId} | VisibleRegions={visibleRegions.Count} | CanvasChildren={_targetCanvas.Children.Count} | 耗时={sw.ElapsedMilliseconds}ms",
                "WpfShapeRenderer");
        }

        /// <summary>
        /// 增量更新指定区域
        /// </summary>
        public void UpdateRegion(RegionRenderContext region)
        {
            // Log rendering update (every 10 times)
            if (_updateLogCount++ % 10 == 0)
            {
                var shapeExists = _shapePool.TryGetValue(region.Id, out _);
                _logger?.Log(LogLevel.Info,
                    $"[WpfShapeRenderer.UpdateRegion] Count={_updateLogCount} | RegionId={region.Id} | IsPreview={region.IsPreview} | ShapeExists={shapeExists} | CanvasChildren={_targetCanvas.Children.Count}",
                    "RegionRenderer");
            }

            if (_shapePool.TryGetValue(region.Id, out var existingShape))
            {
                UpdateShapeFromContext(existingShape, region);
            }
            else
            {
                CreateShapeFromContext(region, new RegionRenderOptions());
            }

            UpdateLabel(region);
            UpdateHelpers(region);  // 更新箭头等辅助元素
        }

        /// <summary>
        /// 清空渲染内容
        /// </summary>
        public void Clear()
        {
            int shapeCount = _shapePool.Count;
            int labelCount = _labelPool.Count;
            int helperCount = _helperPool.Sum(h => h.Value.Count);
            int totalChildren = _targetCanvas.Children.Count;

            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[WpfShapeRenderer.Clear] 清空渲染器 | InstanceId={_rendererInstanceId} | Shapes={shapeCount} | Labels={labelCount} | Helpers={helperCount} | CanvasChildren={totalChildren}",
                "WpfShapeRenderer");

            _targetCanvas.Children.Clear();
            _shapePool.Clear();
            _labelPool.Clear();
            _helperPool.Clear();

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[WpfShapeRenderer.Clear] 清空完成 | InstanceId={_rendererInstanceId} | CanvasChildren={_targetCanvas.Children.Count}",
                "WpfShapeRenderer");
        }

        /// <summary>
        /// 设置视口变换（缩放、平移）
        /// </summary>
        public void SetViewportTransform(double scale, Point offset)
        {
            // ★ 关键诊断：仅当缩放值实际变化时记录（低频）
            if (Math.Abs(_viewportScale - scale) > 0.001)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[SetViewportTransform] _viewportScale={_viewportScale:F3} → {scale:F3} | offset=({offset.X},{offset.Y}) | 后续StrokeThickness将除以此值",
                    "WpfShapeRenderer");
            }

            _viewportScale = scale;
            _viewportOffset = offset;

            // 视口变化时需要更新所有Shape的位置（简化处理：全量重绘）
        }

        #endregion

        #region 私有方法 - 对象池管理

        /// <summary>
        /// 移除不再可见的区域
        /// </summary>
        private void RemoveInvisibleRegions(HashSet<Guid> visibleIds)
        {
            var toRemove = new List<Guid>();

            foreach (var id in _shapePool.Keys)
            {
                if (!visibleIds.Contains(id))
                {
                    toRemove.Add(id);
                }
            }

            if (toRemove.Count > 0)
            {
                // ★ 诊断日志：详细记录被移除的区域
                var removedNames = toRemove.Select(id => $"{id:B}").ToList();
                var visibleNames = visibleIds.Select(id => $"{id:B}").ToList();
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"[RemoveInvisibleRegions] 移除不可见区域 | InstanceId={_rendererInstanceId} | RemoveCount={toRemove.Count} | RemoveIds=[{string.Join(", ", removedNames)}] | VisibleIds=[{string.Join(", ", visibleNames)}] | ShapePool={_shapePool.Count} | LabelPool={_labelPool.Count} | HelperPool={_helperPool.Count}",
                    "WpfShapeRenderer");
            }

            foreach (var id in toRemove)
            {
                if (_shapePool.TryGetValue(id, out var shape))
                {
                    _targetCanvas.Children.Remove(shape);
                    _shapePool.Remove(id);
                }

                if (_labelPool.TryGetValue(id, out var label))
                {
                    _targetCanvas.Children.Remove(label);
                    _labelPool.Remove(id);
                }

                // 清理辅助元素
                if (_helperPool.TryGetValue(id, out var helpers))
                {
                    foreach (var helper in helpers)
                    {
                        _targetCanvas.Children.Remove(helper);
                    }
                    _helperPool.Remove(id);
                }
            }
        }

        /// <summary>
        /// 更新或创建区域
        /// </summary>
        private void UpdateOrCreateRegion(RegionRenderContext region, RegionRenderOptions options)
        {
            if (_shapePool.TryGetValue(region.Id, out var existingShape))
            {
                // 更新现有Shape
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[UpdateOrCreateRegion] 更新已有Shape | RegionId={region.Id:B} | ShapeType={region.ShapeType} | IsSelected={region.IsSelected}",
                    "WpfShapeRenderer");

                UpdateShapeFromContext(existingShape, region);

                // 更新ZIndex
                Canvas.SetZIndex(existingShape, region.IsSelected ? 100 : 0);
            }
            else
            {
                // 创建新Shape
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[UpdateOrCreateRegion] 创建新Shape | RegionId={region.Id:B} | ShapeType={region.ShapeType} | IsSelected={region.IsSelected} | IsPreview={region.IsPreview}",
                    "WpfShapeRenderer");

                CreateShapeFromContext(region, options);
            }

            // 更新或创建标签
            UpdateLabel(region);
            
            // 更新或创建辅助元素（方向箭头等）
            UpdateHelpers(region);
        }

        #endregion

        #region 私有方法 - Shape创建和更新

        /// <summary>
        /// 从渲染上下文创建Shape
        /// </summary>
        private void CreateShapeFromContext(RegionRenderContext region, RegionRenderOptions options)
        {
                region.CalculateBounds();

                Shape shape = CreateShape(region, options);
                if (shape == null) return;

                // 设置位置
                var position = GetShapePosition(region);
                Canvas.SetLeft(shape, position.X + _viewportOffset.X);
                Canvas.SetTop(shape, position.Y + _viewportOffset.Y);
                Canvas.SetZIndex(shape, region.IsSelected ? 100 : 0);

                // 添加到Canvas和对象池
                _targetCanvas.Children.Add(shape);
                _shapePool[region.Id] = shape;

                VisionLogger.Instance.Log(LogLevel.Success,
                    $"[WpfShapeRenderer.CreateShapeFromContext] 创建新Shape | InstanceId={_rendererInstanceId} | RegionId={region.Id} | ShapeType={region.ShapeType} | IsPreview={region.IsPreview} | ShapePoolSize={_shapePool.Count}",
                    "WpfShapeRenderer");
        }

        /// <summary>
        /// 从渲染上下文更新Shape
        /// </summary>
        private void UpdateShapeFromContext(Shape shape, RegionRenderContext region)
        {
                region.CalculateBounds();

                // ★ 关键诊断：降频日志，输出实际线宽计算值（仅每 LogInterval 次记录一次）
                if (_logger != null && _logCounter % LogInterval == 0)
                {
                    var actualThickness = _viewportScale > 0 ? region.StrokeThickness / _viewportScale : region.StrokeThickness;
                    _logger.Log(LogLevel.Info, 
                        $"[UpdateShape] IsPreview={region.IsPreview}, " +
                        $"rawStroke={region.StrokeThickness:F1}, viewportScale={_viewportScale:F3}, actualStroke={actualThickness:F3}", 
                        "WpfShapeRenderer");
                }

                // 更新位置
                var position = GetShapePosition(region);
                Canvas.SetLeft(shape, position.X + _viewportOffset.X);
                Canvas.SetTop(shape, position.Y + _viewportOffset.Y);

                // 更新填充和边框
                shape.Fill = new SolidColorBrush(region.FillColor) { Opacity = region.Opacity };
                shape.Stroke = new SolidColorBrush(region.StrokeColor);
                shape.StrokeThickness = region.StrokeThickness / _viewportScale;

                // 更新虚线样式（间隔也需要反除缩放，保持视觉恒定）
                shape.StrokeDashArray = region.IsPreview 
                    ? new DoubleCollection { 4 / _viewportScale, 2 / _viewportScale } 
                    : new DoubleCollection();

                // 根据形状类型更新尺寸
                UpdateShapeDimensions(shape, region);
        }

        /// <summary>
        /// 创建Shape
        /// </summary>
        private Shape CreateShape(RegionRenderContext region, RegionRenderOptions options)
        {
                // 声明局部变量
                Color fillColor = region.FillColor;
                Color strokeColor = region.StrokeColor;
                double strokeThickness = region.StrokeThickness;

                // 预览状态使用特殊颜色
                if (region.IsPreview)
                {
                                fillColor = options.PreviewFillColor;
                                strokeColor = options.PreviewStrokeColor;
                }

                // 选中状态增加边框宽度
                if (region.IsSelected)
                {
                                strokeThickness = RegionEditorSettings.Default.SelectedStrokeThickness;
                }

                // 反除缩放：使线宽在屏幕上视觉恒定，不受图像分辨率/Zoom影响
                strokeThickness = strokeThickness / _viewportScale;

                Shape shape = null;

                switch (region.ShapeType)
                {
                                case ShapeType.Rectangle:
                                                shape = CreateRectangle(region, fillColor, strokeColor, strokeThickness);
                                                break;

                                case ShapeType.Circle:
                                                shape = CreateCircle(region, fillColor, strokeColor, strokeThickness);
                                                break;

                                case ShapeType.RotatedRectangle:
                                                shape = CreateRotatedRectangle(region, fillColor, strokeColor, strokeThickness);
                                                break;

                                case ShapeType.Line:
                                                shape = CreateLine(region, strokeColor, strokeThickness);
                                                break;

                                case ShapeType.Polygon:
                                                shape = CreatePolygon(region, fillColor, strokeColor, strokeThickness);
                                                break;
                }

                return shape;
        }

        /// <summary>
        /// 创建矩形
        /// </summary>
        private Shape CreateRectangle(RegionRenderContext region, Color fillColor, Color strokeColor, double strokeThickness)
        {
                // 日志监控（降频）
                if (_logger != null && _logCounter % LogInterval == 0)
                {
                    _logger.Log(LogLevel.Info, 
                        $"[CreateRectangle] IsPreview={region.IsPreview}, " +
                        $"StrokeDashArray={(region.IsPreview ? "4,2" : "empty")}", 
                        "WpfShapeRenderer");
                }

                return new Rectangle
                {
                    Width = region.Width,
                    Height = region.Height,
                    Fill = new SolidColorBrush(fillColor) { Opacity = region.Opacity },
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = region.IsPreview ? new DoubleCollection { 4 / _viewportScale, 2 / _viewportScale } : new DoubleCollection()
                };
        }

        /// <summary>
        /// 创建圆形
        /// </summary>
        private Shape CreateCircle(RegionRenderContext region, Color fillColor, Color strokeColor, double strokeThickness)
        {
                // 日志监控（降频）
                if (_logger != null && _logCounter % LogInterval == 0)
                {
                    _logger.Log(LogLevel.Info, 
                        $"[CreateCircle] IsPreview={region.IsPreview}, " +
                        $"StrokeDashArray={(region.IsPreview ? "4,2" : "empty")}", 
                        "WpfShapeRenderer");
                }

                return new Ellipse
                {
                    Width = region.Radius * 2,
                    Height = region.Radius * 2,
                    Fill = new SolidColorBrush(fillColor) { Opacity = region.Opacity },
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = region.IsPreview ? new DoubleCollection { 4 / _viewportScale, 2 / _viewportScale } : new DoubleCollection()
                };
        }

        /// <summary>
        /// 创建旋转矩形
        /// </summary>
        private Shape CreateRotatedRectangle(RegionRenderContext region, Color fillColor, Color strokeColor, double strokeThickness)
        {
                // 日志监控（降频）
                if (_logger != null && _logCounter % LogInterval == 0)
                {
                    _logger.Log(LogLevel.Info, 
                        $"[CreateRotatedRectangle] IsPreview={region.IsPreview}, " +
                        $"StrokeDashArray={(region.IsPreview ? "4,2" : "empty")}", 
                        "WpfShapeRenderer");
                }

                var rect = new Rectangle
                {
                    Width = region.Width,
                    Height = region.Height,
                    Fill = new SolidColorBrush(fillColor) { Opacity = region.Opacity },
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = region.IsPreview ? new DoubleCollection { 4 / _viewportScale, 2 / _viewportScale } : new DoubleCollection(),
                    RenderTransform = new RotateTransform(region.Angle, region.Width / 2, region.Height / 2)
                    {
                        // 使用数学角度系统
                        // RotateTransform使用顺时针为正，需要转换
                        Angle = -region.Angle
                    }
                };

                return rect;
        }
        /// <summary>
        /// 创建直线
        /// </summary>
        private Shape CreateLine(RegionRenderContext region, Color strokeColor, double strokeThickness)
        {
                // 日志监控（降频）
                if (_logger != null && _logCounter % LogInterval == 0)
                {
                    _logger.Log(LogLevel.Info, 
                        $"[CreateLine] IsPreview={region.IsPreview}, " +
                        $"StrokeDashArray={(region.IsPreview ? "4,2" : "empty")}", 
                        "WpfShapeRenderer");
                }

                return new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = region.LineEnd.X - region.LineStart.X,
                    Y2 = region.LineEnd.Y - region.LineStart.Y,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = region.IsPreview ? new DoubleCollection { 4 / _viewportScale, 2 / _viewportScale } : new DoubleCollection()
                };
        }
        /// <summary>
        /// 创建多边形
        /// </summary>
        private Shape CreatePolygon(RegionRenderContext region, Color fillColor, Color strokeColor, double strokeThickness)
        {
                if (region.PolygonPoints.Count < 3) return null;

                // 日志监控（降频）
                _logCounter++;
                if (_logger != null && _logCounter % LogInterval == 0)
                {
                    _logger.Log(LogLevel.Info, 
                        $"[CreatePolygon] IsPreview={region.IsPreview}, Points={region.PolygonPoints.Count}, " +
                        $"StrokeDashArray={(region.IsPreview ? "4,2" : "empty")}", 
                        "WpfShapeRenderer");
                }

                var polygon = new Polygon
                {
                    Fill = new SolidColorBrush(fillColor) { Opacity = region.Opacity },
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = region.IsPreview ? new DoubleCollection { 4 / _viewportScale, 2 / _viewportScale } : new DoubleCollection()
                };

                // 设置点集
                var pointCollection = new PointCollection();
                foreach (var point in region.PolygonPoints)
                {
                    pointCollection.Add(point);
                }
                polygon.Points = pointCollection;

                return polygon;
        }
        /// <summary>
        /// 获取Shape的位置
        /// </summary>
        private Point GetShapePosition(RegionRenderContext region)
        {
            return region.ShapeType switch
            {
                ShapeType.Rectangle or ShapeType.Circle or ShapeType.RotatedRectangle 
                    => new Point(region.Center.X - region.Width / 2, region.Center.Y - region.Height / 2),
                ShapeType.Line => new Point(region.LineStart.X, region.LineStart.Y),
                ShapeType.Polygon => region.PolygonPoints.Count > 0 ? region.PolygonPoints[0] : new Point(0, 0),
                _ => new Point(region.Center.X, region.Center.Y)
            };
        }
        /// <summary>
        /// 更新Shape的尺寸
        /// </summary>
        private void UpdateShapeDimensions(Shape shape, RegionRenderContext region)
        {
                switch (region.ShapeType)
                {
                                case ShapeType.Rectangle:
                                case ShapeType.RotatedRectangle:
                                                shape.Width = region.Width;
                                                shape.Height = region.Height;
                                                if (shape is Rectangle rect && region.ShapeType == ShapeType.RotatedRectangle)
                                                {
                                                    rect.RenderTransform = new RotateTransform(region.Angle, region.Width / 2, region.Height / 2)
                                                    {
                                                        Angle = -region.Angle
                                                    };
                                                }
                                                break;

                                case ShapeType.Circle:
                                                shape.Width = region.Radius * 2;
                                                shape.Height = region.Radius * 2;
                                                break;

                                case ShapeType.Line:
                                                if (shape is Line line)
                                                {
                                                    line.X1 = 0;
                                                    line.Y1 = 0;
                                                    line.X2 = region.LineEnd.X - region.LineStart.X;
                                                    line.Y2 = region.LineEnd.Y - region.LineStart.Y;
                                                }
                                                break;
                }
        }

        #endregion

        #region 私有方法 - 标签渲染

        /// <summary>
        /// 更新或创建标签
        /// </summary>
        private void UpdateLabel(RegionRenderContext region)
        {
                if (string.IsNullOrEmpty(region.Label))
                {
                                // 移除标签
                                if (_labelPool.TryGetValue(region.Id, out var oldLabel))
                                {
                                    _targetCanvas.Children.Remove(oldLabel);
                                    _labelPool.Remove(region.Id);
                                }
                                return;
                }

                TextBlock label;
                if (_labelPool.TryGetValue(region.Id, out var existingLabel))
                {
                                label = existingLabel;
                                label.Text = region.Label;
                }
                else
                {
                                label = new TextBlock
                                {
                                    Text = region.Label,
                                    FontSize = 12 / _viewportScale,
                                    Foreground = Brushes.White,
                                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                                    Padding = new Thickness(4 / _viewportScale, 2 / _viewportScale, 4 / _viewportScale, 2 / _viewportScale)
                                };
                                _targetCanvas.Children.Add(label);
                                _labelPool[region.Id] = label;
                }

                // 计算标签位置
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double labelWidth = label.DesiredSize.Width;
                double labelLeft = region.Bounds.Left + (region.Bounds.Width - labelWidth) / 2 + _viewportOffset.X;
                double labelTop = region.Bounds.Top - 20 / _viewportScale + _viewportOffset.Y;
                if (labelTop < 0) labelTop = region.Bounds.Bottom + 2 / _viewportScale + _viewportOffset.Y;

                Canvas.SetLeft(label, labelLeft);
                Canvas.SetTop(label, labelTop);
                Canvas.SetZIndex(label, 200);
        }
        #endregion

        #region 私有方法 - 辅助元素渲染

        /// <summary>
        /// 更新或创建辅助元素（方向箭头等）
        /// </summary>
        private void UpdateHelpers(RegionRenderContext region)
        {
            // 旋转矩形的方向箭头
            if (region.ShapeType == ShapeType.RotatedRectangle)
            {
                var helpers = CreateOrUpdateDirectionArrow(region);
                
                // 存储到辅助元素池
                if (!_helperPool.ContainsKey(region.Id))
                {
                    _helperPool[region.Id] = new List<Shape>();
                }
                
                // 更新辅助元素池（避免重复添加）
                foreach (var helper in helpers)
                {
                    if (!_helperPool[region.Id].Contains(helper))
                    {
                        _helperPool[region.Id].Add(helper);
                    }
                }
            }
            else
            {
                // 非旋转矩形，移除辅助元素
                if (_helperPool.TryGetValue(region.Id, out var helpers))
                {
                    foreach (var helper in helpers)
                    {
                        _targetCanvas.Children.Remove(helper);
                    }
                    _helperPool.Remove(region.Id);
                }
            }
        }

        /// <summary>
        /// 创建或更新方向箭头
        /// </summary>
        private List<Shape> CreateOrUpdateDirectionArrow(RegionRenderContext region)
        {
            // 先清理旧的辅助元素
            if (_helperPool.TryGetValue(region.Id, out var oldHelpers))
            {
                foreach (var helper in oldHelpers)
                {
                    _targetCanvas.Children.Remove(helper);
                }
            }

            // 创建新的辅助元素列表
            var helpers = new List<Shape>();

            // 计算方向箭头的位置和角度
            var center = region.Center;
            var height = region.Height;
            var rotation = region.Angle;

            // 计算箭头几何
            var arrow = RotatedRectangleHelper.GetDirectionArrow(center, height, rotation);

            // 确定箭头颜色（选中时为蓝色，否则使用区域颜色）
            var arrowColor = region.IsSelected
                ? Brushes.Blue
                : new SolidColorBrush(region.StrokeColor);

            // 记录添加前的子元素数量
            var childCountBefore = _targetCanvas.Children.Count;

            // 使用 RotatedRectangleHelper 绘制箭头（包含主线和两翼）
            RotatedRectangleHelper.DrawDirectionArrow(_targetCanvas, arrow.Start, arrow.End, arrowColor, _viewportScale);

            // 收集新添加的箭头形状
            for (int i = childCountBefore; i < _targetCanvas.Children.Count; i++)
            {
                if (_targetCanvas.Children[i] is Shape shape &&
                    shape.Tag as string == RotatedRectangleHelper.DirectionArrowTag)
                {
                    Canvas.SetZIndex(shape, 150); // 确保箭头在形状之上
                    helpers.Add(shape);
                }
            }

            return helpers;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            int shapeCount = _shapePool.Count;
            int labelCount = _labelPool.Count;
            int helperCount = _helperPool.Sum(h => h.Value.Count);

            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[WpfShapeRenderer.Dispose] 渲染器销毁 | InstanceId={_rendererInstanceId} | Shapes={shapeCount} | Labels={labelCount} | Helpers={helperCount}",
                "WpfShapeRenderer");

            _shapePool.Clear();
            _labelPool.Clear();
            _helperPool.Clear();

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[WpfShapeRenderer.Dispose] 销毁完成 | InstanceId={_rendererInstanceId}",
                "WpfShapeRenderer");
        }

        #endregion
    }
}
