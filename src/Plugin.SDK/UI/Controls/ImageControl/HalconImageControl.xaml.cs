using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 图像自动适配窗口模式
    /// </summary>
    public enum ImageAutoFitMode
    {
        /// <summary>图像变化时始终自动适配窗口</summary>
        Always,
        /// <summary>仅在首次加载图像时自动适配（默认）</summary>
        FirstOnly,
        /// <summary>从不自动适配</summary>
        Never
    }

    /// <summary>
    /// 基于 HSmartWindowControlWPF 的图像显示控件（V2）
    /// 两层架构：HSmartWindowControlWPF(HALCON渲染) + Canvas(预览绘制)
    /// ROI 可编辑对象使用 HALCON DrawingObject（自带命中测试/拖拽/缩放/旋转）
    /// </summary>
    public partial class HalconImageControl : UserControl
    {
        #region 私有字段

        private HImage? _currentHImage;
        private bool _isHalconReady;

        // 绘制预览状态
        private bool _isDrawing;
        private bool _isDrawingActive;
        private Point _drawStartCanvasPos;       // Canvas 坐标（预览绘制定位用）
        private Point _drawStartImagePos;        // 图像坐标（供回调使用）
        private Shape? _previewShape;
        private ShapeType _previewShapeType;

        // 多边形多点交互状态
        private readonly List<Point> _polygonCanvasPoints = new();
        private readonly List<Point> _polygonImagePoints = new();
        private System.Windows.Shapes.Polyline? _polygonPreviewPolyline;
        private System.Windows.Shapes.Line? _polygonTailLine;
        private DateTime _polygonLastClickTime = DateTime.MinValue;
        private bool _polygonCompleted;

        // 全屏相关
        private bool _isFullscreen;
        private Window? _fullscreenWindow;
        private FrameworkElement? _originalParent;
        private int _originalIndex;
        private bool _parentIsPanel;

        // 图像尺寸
        private int _originalImageWidth;
        private int _originalImageHeight;

        // 已附加的 DrawingObject 列表
        private readonly List<HDrawingObject> _drawingObjects = new();

        // 区域 ID → DrawingObject 映射（供 RegionEditorControl 双向同步使用）
        private readonly Dictionary<Guid, HDrawingObject> _regionDrawObjects = new();

        // 预览绘制完成回调（供 RegionEditorControl 注册）
        // 参数: ShapeParameters（含形状类型和所有坐标数据）, 返回 true 表示已处理（跳过默认 DrawingObject 创建）
        internal Func<ShapeParameters, bool>? OnPreviewDrawComplete;

        // AutoFitMode 首次加载标记
        private bool _isFirstAutoFitDone;

        #endregion

        #region 依赖属性

        public static readonly DependencyProperty ShowToolBarProperty =
            DependencyProperty.Register(nameof(ShowToolBar), typeof(bool), typeof(HalconImageControl),
                new PropertyMetadata(true, OnShowToolBarChanged));

        public static readonly DependencyProperty ShowImageSourceSelectorProperty =
            DependencyProperty.Register(nameof(ShowImageSourceSelector), typeof(bool), typeof(HalconImageControl),
                new PropertyMetadata(true, OnShowImageSourceSelectorChanged));

        public static readonly DependencyProperty ShowStatusBarProperty =
            DependencyProperty.Register(nameof(ShowStatusBar), typeof(bool), typeof(HalconImageControl),
                new PropertyMetadata(true, OnShowStatusBarChanged));

        public static readonly DependencyProperty ImageSourcesProperty =
            DependencyProperty.Register(nameof(ImageSources), typeof(ObservableCollection<AvailableDataSource>), typeof(HalconImageControl),
                new PropertyMetadata(null, OnImageSourcesChanged));

        public static readonly DependencyProperty SelectedImageSourceIndexProperty =
            DependencyProperty.Register(nameof(SelectedImageSourceIndex), typeof(int), typeof(HalconImageControl),
                new PropertyMetadata(-1, OnSelectedImageSourceIndexChanged));

        /// <summary>
        /// 图像自动适配窗口模式
        /// </summary>
        public static readonly DependencyProperty AutoFitModeProperty =
            DependencyProperty.Register(nameof(AutoFitMode), typeof(ImageAutoFitMode), typeof(HalconImageControl),
                new PropertyMetadata(ImageAutoFitMode.FirstOnly, OnAutoFitModeChanged));

        #endregion

        #region 属性

        public bool ShowToolBar
        {
            get => (bool)GetValue(ShowToolBarProperty);
            set => SetValue(ShowToolBarProperty, value);
        }

        public bool ShowImageSourceSelector
        {
            get => (bool)GetValue(ShowImageSourceSelectorProperty);
            set => SetValue(ShowImageSourceSelectorProperty, value);
        }

        public bool ShowStatusBar
        {
            get => (bool)GetValue(ShowStatusBarProperty);
            set => SetValue(ShowStatusBarProperty, value);
        }

        public ObservableCollection<AvailableDataSource>? ImageSources
        {
            get => (ObservableCollection<AvailableDataSource>?)GetValue(ImageSourcesProperty);
            set => SetValue(ImageSourcesProperty, value);
        }

        public int SelectedImageSourceIndex
        {
            get => (int)GetValue(SelectedImageSourceIndexProperty);
            set => SetValue(SelectedImageSourceIndexProperty, value);
        }

        public HImage? CurrentHImage => _currentHImage;

        public HWindow? HalconWindow => _isHalconReady ? SmartWindow.HalconWindow : null;

        public bool IsHalconReady => _isHalconReady;

        public bool IsFullscreen => _isFullscreen;

        /// <summary>图像自动适配窗口模式</summary>
        public ImageAutoFitMode AutoFitMode
        {
            get => (ImageAutoFitMode)GetValue(AutoFitModeProperty);
            set => SetValue(AutoFitModeProperty, value);
        }

        public int ImageWidth => _originalImageWidth;

        public int ImageHeight => _originalImageHeight;

        /// <summary>当前是否处于绘制预览状态（Canvas 覆盖层驱动）</summary>
        public bool IsDrawingPreviewActive => _isDrawingActive;

        /// <summary>绘制预览 Canvas（视觉叠加层）</summary>
        public Canvas PreviewOverlayCanvas => PreviewCanvas;

        #endregion

        #region 路由事件

        public static readonly RoutedEvent HalconInitializedEvent =
            EventManager.RegisterRoutedEvent(nameof(HalconInitialized), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(HalconImageControl));

        public event RoutedEventHandler HalconInitialized
        {
            add => AddHandler(HalconInitializedEvent, value);
            remove => RemoveHandler(HalconInitializedEvent, value);
        }

        public static readonly RoutedEvent ImageMouseMoveEvent =
            EventManager.RegisterRoutedEvent(nameof(ImageMouseMove), RoutingStrategy.Bubble,
                typeof(EventHandler<ImageMouseEventArgs>), typeof(HalconImageControl));

        public event EventHandler<ImageMouseEventArgs> ImageMouseMove
        {
            add => AddHandler(ImageMouseMoveEvent, value);
            remove => RemoveHandler(ImageMouseMoveEvent, value);
        }

        public static readonly RoutedEvent ImageSourceSelectionChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ImageSourceSelectionChanged), RoutingStrategy.Bubble,
                typeof(EventHandler<ImageSourceSelectionChangedEventArgs>), typeof(HalconImageControl));

        public event EventHandler<ImageSourceSelectionChangedEventArgs> ImageSourceSelectionChanged
        {
            add => AddHandler(ImageSourceSelectionChangedEvent, value);
            remove => RemoveHandler(ImageSourceSelectionChangedEvent, value);
        }

        /// <summary>绘制预览鼠标按下事件（RegionEditorManager 订阅使用）</summary>
        public event EventHandler<MouseButtonEventArgs>? PreviewDrawMouseDown;

        /// <summary>绘制预览鼠标移动事件（RegionEditorManager 订阅使用）</summary>
        public event EventHandler<MouseEventArgs>? PreviewDrawMouseMove;

        /// <summary>绘制预览鼠标松开事件（RegionEditorManager 订阅使用）</summary>
        public event EventHandler<MouseButtonEventArgs>? PreviewDrawMouseUp;

        #endregion

        #region 构造函数

        public HalconImageControl()
        {
            InitializeComponent();
            SmartWindow.SizeChanged += OnSmartWindowSizeChanged;
        }

        #endregion

        #region HALCON 初始化

        private void OnHalconInit(object? sender, EventArgs e)
        {
            _isHalconReady = true;

            SmartWindow.HKeepAspectRatio = true;

            var hWindow = SmartWindow.HalconWindow;
            hWindow.SetLineWidth(2);
            hWindow.SetDraw("margin");
            hWindow.SetColored(12);
            hWindow.SetWindowParam("background_color", "#1E1E1E");

                // 订阅 HALCON 鼠标事件（使用标准 WPF 事件避免委托签名冲突）
            SmartWindow.MouseMove += OnSmartWindowMouseMove;

            RaiseEvent(new RoutedEventArgs(HalconInitializedEvent));

            if (_currentHImage != null)
            {
                DisplayHImageInternal(_currentHImage);
            }
        }

        /// <summary>
        /// resize 后重新贴背景，消除 D3D 表面重建导致的闪白。
        /// 不调用 DetachBackgroundFromWindow / ClearWindow ——
        /// AttachBackgroundToWindow 会自动替换已有背景，避免中间空白帧。
        /// 不调用 SetFullImagePart（否则会覆盖 HKeepAspectRatio 的自动等比例行为）。
        /// </summary>
        private void OnSmartWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isHalconReady && _currentHImage != null)
            {
                var hWindow = SmartWindow.HalconWindow;
                hWindow.AttachBackgroundToWindow(_currentHImage);
            }
        }

        #endregion

        #region 图像显示

        public void DisplayHImage(HImage image)
        {
            if (image == null) return;

            _currentHImage?.Dispose();
            _currentHImage = image.Clone();

            image.GetImageSize(out int width, out int height);
            _originalImageWidth = width;
            _originalImageHeight = height;

            ImageSizeText.Text = $"图像: {_originalImageWidth} x {_originalImageHeight}";

            if (_isHalconReady)
            {
                DisplayHImageInternal(_currentHImage);
            }
        }

        private void DisplayHImageInternal(HImage image)
        {
            var hWindow = SmartWindow.HalconWindow;
            hWindow.AttachBackgroundToWindow(image);
            ApplyAutoFit();
        }

        /// <summary>
        /// 设置图像引用但不操作 HWindow（供 WindowRenderer 使用）
        /// 更新 _currentHImage、图像尺寸、状态栏文本
        /// </summary>
        public void SetImageReference(HImage image)
        {
            if (image == null) return;

            _currentHImage?.Dispose();
            _currentHImage = image.Clone();

            image.GetImageSize(out int width, out int height);
            _originalImageWidth = width;
            _originalImageHeight = height;

            ImageSizeText.Text = $"图像: {_originalImageWidth} x {_originalImageHeight}";
        }

        /// <summary>
        /// 通知控件图像已更新，触发 AutoFit 策略决策
        /// 供 NodeResultManager 在 WindowRenderer 渲染后调用
        /// </summary>
        public void NotifyImageUpdated()
        {
            ApplyAutoFit();
        }

        /// <summary>
        /// 显示 HObject（HImage/HRegion/XLD 等）
        /// </summary>
        public void DisplayHObject(HObject obj)
        {
            if (obj == null) return;

            if (obj is HImage image)
            {
                DisplayHImage(image);
                return;
            }

            if (_isHalconReady)
            {
                SmartWindow.HalconWindow.ClearWindow();
                SmartWindow.HalconWindow.DispObj(obj);
            }
        }

        /// <summary>显示算法结果覆盖（替代旧 SetDisplayCollector 等功能）</summary>
        public void DisplayOverlay(HObject? overlay)
        {
            if (!_isHalconReady) return;

            var hWindow = SmartWindow.HalconWindow;
            if (overlay != null)
            {
                hWindow.SetColor("green");
                hWindow.SetDraw("margin");
                hWindow.SetLineWidth(2);
                hWindow.DispObj(overlay);
            }
        }

        /// <summary>清空所有显示（图像 + 覆盖 + DrawingObject）</summary>
        public void ClearAll()
        {
            DetachAllDrawingObjects();
            if (_isHalconReady && _currentHImage != null)
            {
                SmartWindow.HalconWindow.ClearWindow();
            }
            _currentHImage?.Dispose();
            _currentHImage = null;
            _isFirstAutoFitDone = false;
        }

        #endregion

        #region DrawingObject 管理（基础）

        /// <summary>创建矩形 DrawingObject 并附加到窗口</summary>
        public HDrawingObject CreateDrawingObjectRect(double row1, double col1, double row2, double col2)
        {
            var drawObj = new HDrawingObject(row1, col1, row2, col2);
            drawObj.SetDrawingObjectParams("color", "green");
            drawObj.SetDrawingObjectParams("line_width", 2.0);
            SmartWindow.HalconWindow.AttachDrawingObjectToWindow(drawObj);
            _drawingObjects.Add(drawObj);
            return drawObj;
        }

        /// <summary>创建圆形 DrawingObject</summary>
        public HDrawingObject CreateDrawingObjectCircle(double row, double col, double radius)
        {
            var drawObj = new HDrawingObject(row, col, radius);
            drawObj.SetDrawingObjectParams("color", "green");
            drawObj.SetDrawingObjectParams("line_width", 2.0);
            SmartWindow.HalconWindow.AttachDrawingObjectToWindow(drawObj);
            _drawingObjects.Add(drawObj);
            return drawObj;
        }

        /// <summary>创建旋转矩形 DrawingObject</summary>
        public HDrawingObject CreateDrawingObjectRect2(double row, double col, double phi, double length1, double length2)
        {
            var drawObj = new HDrawingObject(row, col, phi, length1, length2);
            drawObj.SetDrawingObjectParams("color", "green");
            drawObj.SetDrawingObjectParams("line_width", 2.0);
            SmartWindow.HalconWindow.AttachDrawingObjectToWindow(drawObj);
            _drawingObjects.Add(drawObj);
            return drawObj;
        }

        /// <summary>分离并清理所有 DrawingObject</summary>
        public void DetachAllDrawingObjects()
        {
            _regionDrawObjects.Clear();
            if (!_isHalconReady) return;

            foreach (var drawObj in _drawingObjects)
            {
                try
                {
                    SmartWindow.HalconWindow.DetachDrawingObjectFromWindow(drawObj);
                    drawObj.Dispose();
                }
                catch { }
            }
            _drawingObjects.Clear();
        }

        #endregion

        #region 区域 DrawingObject 管理（按 Guid 映射，供 RegionEditorControl 双向同步）

        /// <summary>根据 ShapeParameters 创建 DrawingObject 并关联到区域 ID</summary>
        public HDrawingObject? CreateRegionDrawingObject(Guid regionId, ShapeParameters shape)
        {
            if (!_isHalconReady) return null;

            // 如果已存在同名 ID，先分离
            if (_regionDrawObjects.ContainsKey(regionId))
                DetachRegionDrawingObject(regionId);

            HDrawingObject? drawObj = null;
            var shapeType = shape.ShapeType;

            try
            {
                switch (shapeType)
                {
                    case ShapeType.Rectangle:
                    {
                        double r1 = shape.CenterY - shape.Height / 2;
                        double c1 = shape.CenterX - shape.Width / 2;
                        double r2 = shape.CenterY + shape.Height / 2;
                        double c2 = shape.CenterX + shape.Width / 2;
                        drawObj = new HDrawingObject(r1, c1, r2, c2);
                        break;
                    }
                    case ShapeType.Circle:
                        drawObj = new HDrawingObject(shape.CenterY, shape.CenterX, shape.Radius);
                        break;
                    case ShapeType.RotatedRectangle:
                    {
                        double phiRad = shape.Angle * Math.PI / 180.0;
                        double len1 = shape.Width / 2;
                        double len2 = shape.Height / 2;
                        drawObj = new HDrawingObject(shape.CenterY, shape.CenterX, phiRad, len1, len2);
                        break;
                    }
                    case ShapeType.Line:
                    {
                        drawObj = new HDrawingObject();
                        drawObj.CreateDrawingObjectLine(shape.StartY, shape.StartX, shape.EndY, shape.EndX);
                        break;
                    }
                    case ShapeType.Polygon:
                    {
                        var rows = new HTuple(shape.Points.Select(p => p.Y).ToArray());
                        var cols = new HTuple(shape.Points.Select(p => p.X).ToArray());
                        drawObj = new HDrawingObject();
                        drawObj.CreateDrawingObjectXld(rows, cols);
                        break;
                    }
                }

                if (drawObj != null)
                {
                    // 设置显示样式
                    ApplyDrawingObjectStyle(drawObj, shape);

                    SmartWindow.HalconWindow.AttachDrawingObjectToWindow(drawObj);
                    _drawingObjects.Add(drawObj);
                    _regionDrawObjects[regionId] = drawObj;
                }
            }
            catch (Exception ex)
            {
                drawObj?.Dispose();
                drawObj = null;
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[CreateRegionDrawingObject] 创建失败 | RegionId={regionId:B} | Type={shape.ShapeType} | Error={ex.Message}",
                    "HalconImageControl");
            }

            return drawObj;
        }

        /// <summary>根据 ShapeParameters 更新已有 DrawingObject 的几何和样式</summary>
        public void UpdateRegionDrawingObject(Guid regionId, ShapeParameters shape)
        {
            if (!_isHalconReady) return;
            if (!_regionDrawObjects.TryGetValue(regionId, out var drawObj)) return;

            var shapeType = shape.ShapeType;

            try
            {
                switch (shapeType)
                {
                    case ShapeType.Rectangle:
                    {
                        double r1 = shape.CenterY - shape.Height / 2;
                        double c1 = shape.CenterX - shape.Width / 2;
                        double r2 = shape.CenterY + shape.Height / 2;
                        double c2 = shape.CenterX + shape.Width / 2;
                        drawObj.SetDrawingObjectParams("row1", r1);
                        drawObj.SetDrawingObjectParams("column1", c1);
                        drawObj.SetDrawingObjectParams("row2", r2);
                        drawObj.SetDrawingObjectParams("column2", c2);
                        break;
                    }
                    case ShapeType.Circle:
                        drawObj.SetDrawingObjectParams("row", shape.CenterY);
                        drawObj.SetDrawingObjectParams("column", shape.CenterX);
                        drawObj.SetDrawingObjectParams("radius", shape.Radius);
                        break;
                    case ShapeType.RotatedRectangle:
                    {
                        double phiRad = shape.Angle * Math.PI / 180.0;
                        double len1 = shape.Width / 2;
                        double len2 = shape.Height / 2;
                        drawObj.SetDrawingObjectParams("row", shape.CenterY);
                        drawObj.SetDrawingObjectParams("column", shape.CenterX);
                        drawObj.SetDrawingObjectParams("phi", phiRad);
                        drawObj.SetDrawingObjectParams("length1", len1);
                        drawObj.SetDrawingObjectParams("length2", len2);
                        break;
                    }
                    case ShapeType.Line:
                    {
                        drawObj.SetDrawingObjectParams("row1", shape.StartY);
                        drawObj.SetDrawingObjectParams("column1", shape.StartX);
                        drawObj.SetDrawingObjectParams("row2", shape.EndY);
                        drawObj.SetDrawingObjectParams("column2", shape.EndX);
                        break;
                    }
                    case ShapeType.Polygon:
                    {
                        var rows = new HTuple(shape.Points.Select(p => p.Y).ToArray());
                        var cols = new HTuple(shape.Points.Select(p => p.X).ToArray());
                        var xld = new HXLDCont();
                        xld.GenContourPolygonXld(rows, cols);
                        drawObj.SetDrawingObjectXld(xld);
                        xld.Dispose();
                        break;
                    }
                }

                ApplyDrawingObjectStyle(drawObj, shape);
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[UpdateRegionDrawingObject] 更新失败 | RegionId={regionId:B} | Type={shape.ShapeType} | Error={ex.Message}",
                    "HalconImageControl");
            }
        }

        /// <summary>从 DrawingObject 读取几何参数，写回 ShapeParameters（正向同步用）</summary>
        public void ReadDrawingObjectParams(Guid regionId, ShapeParameters shape)
        {
            if (!_regionDrawObjects.TryGetValue(regionId, out var drawObj)) return;

            try
            {
                var shapeType = shape.ShapeType;

                switch (shapeType)
                {
                    case ShapeType.Rectangle:
                    {
                        var row1 = drawObj.GetDrawingObjectParams("row1").D;
                        var col1 = drawObj.GetDrawingObjectParams("column1").D;
                        var row2 = drawObj.GetDrawingObjectParams("row2").D;
                        var col2 = drawObj.GetDrawingObjectParams("column2").D;
                        shape.CenterX = (col1 + col2) / 2;
                        shape.CenterY = (row1 + row2) / 2;
                        shape.Width = Math.Abs(col2 - col1);
                        shape.Height = Math.Abs(row2 - row1);
                        break;
                    }
                    case ShapeType.Circle:
                    {
                        var row = drawObj.GetDrawingObjectParams("row").D;
                        var col = drawObj.GetDrawingObjectParams("column").D;
                        var radius = drawObj.GetDrawingObjectParams("radius").D;
                        shape.CenterY = row;
                        shape.CenterX = col;
                        shape.Radius = radius;
                        break;
                    }
                    case ShapeType.RotatedRectangle:
                    {
                        var row = drawObj.GetDrawingObjectParams("row").D;
                        var col = drawObj.GetDrawingObjectParams("column").D;
                        var phi = drawObj.GetDrawingObjectParams("phi").D;
                        var l1 = drawObj.GetDrawingObjectParams("length1").D;
                        var l2 = drawObj.GetDrawingObjectParams("length2").D;
                        shape.CenterY = row;
                        shape.CenterX = col;
                        shape.Angle = phi * 180.0 / Math.PI;
                        shape.Width = l1 * 2;
                        shape.Height = l2 * 2;
                        break;
                    }
                    case ShapeType.Line:
                    {
                        shape.StartY = drawObj.GetDrawingObjectParams("row1").D;
                        shape.StartX = drawObj.GetDrawingObjectParams("column1").D;
                        shape.EndY = drawObj.GetDrawingObjectParams("row2").D;
                        shape.EndX = drawObj.GetDrawingObjectParams("column2").D;
                        break;
                    }
                    case ShapeType.Polygon:
                    {
                        var hXld = drawObj.GetDrawingObjectIconic();
                        if (hXld is HXLDCont xldCont && xldCont.IsInitialized())
                        {
                            xldCont.GetContourXld(out HTuple rows, out HTuple cols);
                            shape.Points.Clear();
                            for (int i = 0; i < rows.Length; i++)
                                shape.Points.Add(new Point2D(cols[i].D, rows[i].D));
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[ReadDrawingObjectParams] 读取失败 | RegionId={regionId:B} | ShapeType={shape.ShapeType} | Error={ex.Message}",
                    "HalconImageControl");
            }
        }

        /// <summary>设置 DrawingObject 显示样式（根据 ShapeParameters）</summary>
        private void ApplyDrawingObjectStyle(HDrawingObject drawObj, ShapeParameters shape)
        {
            drawObj.SetDrawingObjectParams("line_width", shape.StrokeThickness);

            // 颜色转换：ARGB uint → HALCON color string
            var strokeColor = ArgbToHalconColor(shape.StrokeColorArgb);
            drawObj.SetDrawingObjectParams("color", strokeColor);

            // 填充色（HALCON DrawingObject 仅支持 "margin" 或 "fill"）
            if (shape.FillColorArgb != 0)
            {
                drawObj.SetDrawingObjectParams("draw", "fill");
                drawObj.SetDrawingObjectParams("fill_color", ArgbToHalconColor(shape.FillColorArgb));
            }
            else
            {
                drawObj.SetDrawingObjectParams("draw", "margin");
            }
        }

        /// <summary>ARGB uint 转 HALCON 颜色字符串（返回 "#RRGGBB"）</summary>
        private static string ArgbToHalconColor(uint argb)
        {
            var r = (byte)((argb >> 16) & 0xFF);
            var g = (byte)((argb >> 8) & 0xFF);
            var b = (byte)(argb & 0xFF);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>根据区域 ID 获取关联的 DrawingObject</summary>
        public HDrawingObject? GetRegionDrawingObject(Guid regionId)
        {
            return _regionDrawObjects.TryGetValue(regionId, out var drawObj) ? drawObj : null;
        }

        /// <summary>分离并释放指定区域的 DrawingObject</summary>
        public void DetachRegionDrawingObject(Guid regionId)
        {
            if (!_regionDrawObjects.TryGetValue(regionId, out var drawObj)) return;
            _regionDrawObjects.Remove(regionId);

            if (_isHalconReady && drawObj != null)
            {
                try
                {
                    SmartWindow.HalconWindow.DetachDrawingObjectFromWindow(drawObj);
                }
                catch { }
                drawObj.Dispose();
            }
            _drawingObjects.Remove(drawObj);
        }

        #endregion

        #region 绘制预览 (Canvas 覆盖层事件)

        /// <summary>开始绘制预览模式（默认形状为 Rectangle）</summary>
        public void BeginPreviewDraw()
        {
            _previewShapeType = ShapeType.Rectangle;
            ActivatePreviewCanvas();
        }

        /// <summary>开始绘制预览模式，指定要绘制的形状类型</summary>
        public void BeginPreviewDraw(ShapeType shapeType)
        {
            _previewShapeType = shapeType;
            ActivatePreviewCanvas();
        }

        private void ActivatePreviewCanvas()
        {
            _isDrawingActive = true;
            PreviewCanvas.Visibility = Visibility.Visible;
            PreviewCanvas.MouseLeftButtonDown += PreviewCanvas_MouseLeftButtonDown;
            PreviewCanvas.MouseMove += PreviewCanvas_MouseMove;
            PreviewCanvas.MouseUp += PreviewCanvas_MouseUp;
            Cursor = Cursors.Cross;
        }

        /// <summary>结束绘制预览模式（隐藏 Canvas 覆盖层，恢复鼠标穿透到 SmartWindow）</summary>
        public void EndPreviewDraw()
        {
            _isDrawingActive = false;
            PreviewCanvas.MouseLeftButtonDown -= PreviewCanvas_MouseLeftButtonDown;
            PreviewCanvas.MouseMove -= PreviewCanvas_MouseMove;
            PreviewCanvas.MouseUp -= PreviewCanvas_MouseUp;
            PreviewCanvas.Visibility = Visibility.Collapsed;
            Cursor = Cursors.Arrow;
            RemovePreviewShape();
        }

        private void RemovePreviewShape()
        {
            PreviewCanvas.Children.Clear();
            _previewShape = null;
            _isDrawing = false;
            _polygonCanvasPoints.Clear();
            _polygonImagePoints.Clear();
            _polygonPreviewPolyline = null;
            _polygonTailLine = null;
            _polygonCompleted = false;
        }

        /// <summary>Canvas 坐标 → 图像坐标（利用 Halcon 窗口坐标转换，Canvas 与 SmartWindow 位置重叠）</summary>
        private Point CanvasToImagePoint(Point canvasPt)
        {
            if (!_isHalconReady || _currentHImage == null) return new Point(0, 0);
            try
            {
                var hWindow = SmartWindow.HalconWindow;
                hWindow.ConvertCoordinatesWindowToImage(
                    canvasPt.Y, canvasPt.X, out double imgRow, out double imgCol);
                return new Point(imgCol, imgRow);
            }
            catch
            {
                return new Point(0, 0);
            }
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawingActive) return;

            // 多边形模式特殊处理：单击累加顶点，双击完成
            if (_previewShapeType == ShapeType.Polygon)
            {
                // 双击检测
                var now = DateTime.Now;
                bool isDoubleClick = (now - _polygonLastClickTime).TotalMilliseconds < 500
                    && _polygonCanvasPoints.Count > 0;
                _polygonLastClickTime = now;

                if (_polygonCompleted) return;

                if (isDoubleClick && _polygonCanvasPoints.Count >= 3)
                {
                    CompletePolygonPreview();
                    PreviewDrawMouseDown?.Invoke(this, e);
                    return;
                }

                // 累加顶点
                var canvasPt = e.GetPosition(PreviewCanvas);
                var imagePt = CanvasToImagePoint(canvasPt);
                _polygonCanvasPoints.Add(canvasPt);
                _polygonImagePoints.Add(imagePt);

                // 更新多边形预览（已有顶点连成折线）
                UpdatePolygonPreview(canvasPt);
                PreviewDrawMouseDown?.Invoke(this, e);
                return;
            }

            _isDrawing = true;
            _drawStartCanvasPos = e.GetPosition(PreviewCanvas);
            _drawStartImagePos = CanvasToImagePoint(_drawStartCanvasPos);

            switch (_previewShapeType)
            {
                case ShapeType.Circle:
                    _previewShape = new Ellipse
                    {
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0))
                    };
                    break;

                case ShapeType.Line:
                    _previewShape = new System.Windows.Shapes.Line
                    {
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        X1 = _drawStartCanvasPos.X,
                        Y1 = _drawStartCanvasPos.Y,
                        X2 = _drawStartCanvasPos.X,
                        Y2 = _drawStartCanvasPos.Y
                    };
                    break;

                default: // Rectangle, RotatedRectangle
                    _previewShape = new Rectangle
                    {
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0))
                    };
                    break;
            }

            PreviewCanvas.Children.Add(_previewShape);
            PreviewDrawMouseDown?.Invoke(this, e);
        }

        /// <summary>更新多边形预览：已固定顶点 + 当前鼠标尾随线</summary>
        private void UpdatePolygonPreview(Point currentCanvasPt)
        {
            PreviewCanvas.Children.Clear();

            if (_polygonCanvasPoints.Count >= 1)
            {
                var polyline = new System.Windows.Shapes.Polyline
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Points = new PointCollection(_polygonCanvasPoints)
                };
                PreviewCanvas.Children.Add(polyline);
                _polygonPreviewPolyline = polyline;

                // 尾随线：从最后一个顶点到鼠标位置
                var lastPt = _polygonCanvasPoints[_polygonCanvasPoints.Count - 1];
                var tailLine = new System.Windows.Shapes.Line
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 2, 4 },
                    X1 = lastPt.X,
                    Y1 = lastPt.Y,
                    X2 = currentCanvasPt.X,
                    Y2 = currentCanvasPt.Y
                };
                PreviewCanvas.Children.Add(tailLine);
                _polygonTailLine = tailLine;
            }
        }

        /// <summary>完成多边形绘制，构造 ShapeParameters 并触发回调</summary>
        private void CompletePolygonPreview()
        {
            _polygonCompleted = true;

            // 构造 ShapeParameters
            var shape = new ShapeParameters { ShapeType = ShapeType.Polygon };
            shape.Points.Clear();
            foreach (var pt in _polygonImagePoints)
                shape.Points.Add(new Point2D(pt.X, pt.Y));

            // 清预览
            RemovePreviewShape();

            bool handled = false;
            if (OnPreviewDrawComplete != null)
            {
                try
                {
                    handled = OnPreviewDrawComplete(shape);
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Error(
                        $"[HalconImageControl] Polygon OnPreviewDrawComplete 异常: {ex.Message}",
                        "HalconImageControl");
                    throw;
                }
            }

            PreviewDrawMouseUp?.Invoke(this, null!);
            EndPreviewDraw();
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var canvasPos = e.GetPosition(PreviewCanvas);

            // 多边形模式：更新尾随线
            if (_previewShapeType == ShapeType.Polygon && _polygonCanvasPoints.Count >= 1 && !_polygonCompleted)
            {
                if (_polygonTailLine != null)
                {
                    var lastPt = _polygonCanvasPoints[_polygonCanvasPoints.Count - 1];
                    _polygonTailLine.X1 = lastPt.X;
                    _polygonTailLine.Y1 = lastPt.Y;
                    _polygonTailLine.X2 = canvasPos.X;
                    _polygonTailLine.Y2 = canvasPos.Y;
                    return;
                }
                // 首次移动时创建预览
                if (_polygonPreviewPolyline == null)
                {
                    UpdatePolygonPreview(canvasPos);
                }
                return;
            }

            if (!_isDrawing || _previewShape == null) return;

            switch (_previewShapeType)
            {
                case ShapeType.Circle:
                {
                    double dx = canvasPos.X - _drawStartCanvasPos.X;
                    double dy = canvasPos.Y - _drawStartCanvasPos.Y;
                    double radius = Math.Sqrt(dx * dx + dy * dy);
                    _previewShape.Width = _previewShape.Height = radius * 2;
                    Canvas.SetLeft(_previewShape, _drawStartCanvasPos.X - radius);
                    Canvas.SetTop(_previewShape, _drawStartCanvasPos.Y - radius);
                    break;
                }
                case ShapeType.Line:
                {
                    var line = (System.Windows.Shapes.Line)_previewShape;
                    line.X2 = canvasPos.X;
                    line.Y2 = canvasPos.Y;
                    break;
                }
                default: // Rectangle, RotatedRectangle
                {
                    var x = Math.Min(_drawStartCanvasPos.X, canvasPos.X);
                    var y = Math.Min(_drawStartCanvasPos.Y, canvasPos.Y);
                    _previewShape.Width = Math.Abs(canvasPos.X - _drawStartCanvasPos.X);
                    _previewShape.Height = Math.Abs(canvasPos.Y - _drawStartCanvasPos.Y);
                    Canvas.SetLeft(_previewShape, x);
                    Canvas.SetTop(_previewShape, y);
                    break;
                }
            }

            PreviewDrawMouseMove?.Invoke(this, e);
        }

        private void PreviewCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;
            // 多边形不在此处完成（由 CompletePolygonPreview 处理）
            if (_previewShapeType == ShapeType.Polygon) return;

            _isDrawing = false;

            var canvasPos = e.GetPosition(PreviewCanvas);
            var imagePos = CanvasToImagePoint(canvasPos);

            // 构造 ShapeParameters
            var shape = new ShapeParameters { ShapeType = _previewShapeType };

            switch (_previewShapeType)
            {
                case ShapeType.Line:
                {
                    shape.StartX = _drawStartImagePos.X;
                    shape.StartY = _drawStartImagePos.Y;
                    shape.EndX = imagePos.X;
                    shape.EndY = imagePos.Y;
                    break;
                }
                case ShapeType.Circle:
                {
                    double radius = Math.Max(
                        Math.Abs(imagePos.X - _drawStartImagePos.X),
                        Math.Abs(imagePos.Y - _drawStartImagePos.Y));
                    shape.CenterX = _drawStartImagePos.X;
                    shape.CenterY = _drawStartImagePos.Y;
                    shape.Radius = radius;
                    break;
                }
                case ShapeType.Rectangle:
                case ShapeType.RotatedRectangle:
                default:
                {
                    double r1 = Math.Min(_drawStartImagePos.Y, imagePos.Y);
                    double c1 = Math.Min(_drawStartImagePos.X, imagePos.X);
                    double r2 = Math.Max(_drawStartImagePos.Y, imagePos.Y);
                    double c2 = Math.Max(_drawStartImagePos.X, imagePos.X);
                    shape.CenterX = (c1 + c2) / 2;
                    shape.CenterY = (r1 + r2) / 2;
                    shape.Width = Math.Abs(c2 - c1);
                    shape.Height = Math.Abs(r2 - r1);
                    break;
                }
            }

            // 先清预览
            RemovePreviewShape();

            bool handled = false;
            if (OnPreviewDrawComplete != null)
            {
                try
                {
                    handled = OnPreviewDrawComplete(shape);
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Error(
                        $"[HalconImageControl] OnPreviewDrawComplete 异常: {ex.Message}",
                        "HalconImageControl");
                    throw;
                }
            }

            if (!handled && _isHalconReady && _currentHImage != null)
            {
                // 默认回退：为 Rectangle 创建 DrawingObject
                if (_previewShapeType == ShapeType.Rectangle)
                {
                    CreateDrawingObjectRect(
                        shape.CenterY - shape.Height / 2,
                        shape.CenterX - shape.Width / 2,
                        shape.CenterY + shape.Height / 2,
                        shape.CenterX + shape.Width / 2);
                }
            }

            PreviewDrawMouseUp?.Invoke(this, e);

            // 绘制完成自动结束预览模式（隐藏 Canvas，恢复 SmartWindow 交互）
            EndPreviewDraw();
        }

        #endregion

        #region 缩放控制

        /// <summary>适应窗口（等比例居中显示）</summary>
        public void FitToWindow()
        {
            if (!_isHalconReady || _currentHImage == null) return;

            _currentHImage.GetImageSize(out int imgW, out int imgH);
            double winW = SmartWindow.ActualWidth;
            double winH = SmartWindow.ActualHeight;
            if (winW <= 0 || winH <= 0) return;

            double imgAspect = (double)imgW / imgH;
            double winAspect = winW / winH;

            double partW, partH;
            if (imgAspect > winAspect)
            {
                partW = imgW;
                partH = imgW / winAspect;
            }
            else
            {
                partH = imgH;
                partW = imgH * winAspect;
            }

            double offsetRow = (partH - imgH) / 2.0;
            double offsetCol = (partW - imgW) / 2.0;

            SmartWindow.HalconWindow.SetPart(
                -offsetRow, -offsetCol,
                partH - offsetRow - 1, partW - offsetCol - 1);
        }

        /// <summary>
        /// 根据 AutoFitMode 策略决策是否执行 FitToWindow
        /// </summary>
        private void ApplyAutoFit()
        {
            if (_currentHImage == null || !_isHalconReady) return;

            switch (AutoFitMode)
            {
                case ImageAutoFitMode.Always:
                    FitToWindow();
                    break;
                case ImageAutoFitMode.FirstOnly:
                    if (!_isFirstAutoFitDone)
                    {
                        FitToWindow();
                        _isFirstAutoFitDone = true;
                    }
                    break;
                case ImageAutoFitMode.Never:
                    break;
            }
        }

        /// <summary>放大</summary>
        public void ZoomIn()
        {
            if (_isHalconReady)
            {
                var center = new Point(SmartWindow.ActualWidth / 2, SmartWindow.ActualHeight / 2);
                SmartWindow.HZoomWindowContents(center.X, center.Y, 1);
            }
        }

        /// <summary>缩小</summary>
        public void ZoomOut()
        {
            if (_isHalconReady)
            {
                var center = new Point(SmartWindow.ActualWidth / 2, SmartWindow.ActualHeight / 2);
                SmartWindow.HZoomWindowContents(center.X, center.Y, -1);
            }
        }

        #endregion

        #region 公共方法

        public void ToggleFullscreen()
        {
            if (!_isFullscreen)
                EnterFullscreen();
            else
                ExitFullscreen();
        }

        public void EnterFullscreenMode()
        {
            if (!_isFullscreen) EnterFullscreen();
        }

        public void ExitFullscreenMode()
        {
            ExitFullscreen();
        }

        public void GetMposition(out int row, out int col, out int button)
        {
            row = 0; col = 0; button = 0;
            if (_isHalconReady)
            {
                try { SmartWindow.HalconWindow.GetMposition(out row, out col, out button); }
                catch { }
            }
        }

        #endregion

        #region 事件处理

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
        }

        private static void OnShowToolBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalconImageControl control)
                control.ToolBar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnShowImageSourceSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalconImageControl control && control.ImageSourceSelector != null)
                control.ImageSourceSelector.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnShowStatusBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalconImageControl control)
                control.StatusBar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnImageSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalconImageControl control)
            {
                if (e.OldValue is ObservableCollection<AvailableDataSource> oldCollection)
                    oldCollection.CollectionChanged -= control.OnImageSourcesCollectionChanged;

                control.ImageSourceSelector.ItemsSource = e.NewValue as ObservableCollection<AvailableDataSource>;

                if (e.NewValue is ObservableCollection<AvailableDataSource> newCollection)
                    newCollection.CollectionChanged += control.OnImageSourcesCollectionChanged;
            }
        }

        private static void OnSelectedImageSourceIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalconImageControl control)
            {
                var newIndex = (int)e.NewValue;
                if (newIndex >= 0 && control.ImageSourceSelector.Items.Count > newIndex)
                {
                    control.ImageSourceSelector.SelectedIndex = -1;
                    control.ImageSourceSelector.SelectedIndex = newIndex;

                    if (control.ImageSourceSelector.Template.FindName("ToggleButton", control.ImageSourceSelector) is ToggleButton toggleButton)
                    {
                        toggleButton.Content = control.ImageSourceSelector.Items[newIndex];
                    }
                }
            }
        }

        private static void OnAutoFitModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HalconImageControl control && (ImageAutoFitMode)e.NewValue == ImageAutoFitMode.Always)
            {
                control.ApplyAutoFit();
            }
        }

        private void OnImageSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
        }

        private void ZoomIn_Click(object? sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        private void ZoomOut_Click(object? sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        private void FitToWindow_Click(object? sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void ToggleFullscreen_Click(object? sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ImageSourceSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is AvailableDataSource selectedSource)
            {
                SelectedImageSourceIndex = ImageSourceSelector.SelectedIndex;
                RaiseEvent(new ImageSourceSelectionChangedEventArgs(selectedSource, ImageSourceSelectionChangedEvent));
            }
        }

        private void OnSmartWindowMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isHalconReady || _currentHImage == null) return;

            var winPos = e.GetPosition(SmartWindow);
            var hWindow = SmartWindow.HalconWindow;

            try
            {
                hWindow.ConvertCoordinatesWindowToImage(
                    winPos.Y, winPos.X, out double imgRow, out double imgCol);
                PositionText.Text = $"位置: ({imgCol:F1}, {imgRow:F1})";
                PixelValueText.Text = GetPixelValueString((int)imgCol, (int)imgRow);

                var routedArgs = new ImageMouseEventArgs(
                    new System.Windows.Point(imgCol, imgRow),
                    winPos,
                    null, ImageMouseMoveEvent);
                RaiseEvent(routedArgs);
            }
            catch { }
        }

        #endregion

        #region 全屏

        private void EnterFullscreen()
        {
            if (_isFullscreen) return;

            _originalParent = Parent as FrameworkElement;
            if (_originalParent == null) return;

            if (_originalParent is Panel panel)
            {
                _parentIsPanel = true;
                _originalIndex = panel.Children.IndexOf(this);
                panel.Children.Remove(this);
            }
            else if (_originalParent is Decorator decorator)
            {
                _parentIsPanel = false;
                decorator.Child = null;
            }
            else
            {
                _originalParent = null;
                return;
            }

            _fullscreenWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                WindowState = WindowState.Maximized,
                Background = Brushes.Black,
                Content = this,
                Title = "全屏预览"
            };

            _fullscreenWindow.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape) ExitFullscreen();
            };
            _fullscreenWindow.Closed += (s, args) => _isFullscreen = false;

            _isFullscreen = true;
            _fullscreenWindow.Show();
            FitToWindow();
        }

        private void ExitFullscreen()
        {
            if (!_isFullscreen || _originalParent == null) return;

            _isFullscreen = false;

            if (_fullscreenWindow != null)
            {
                _fullscreenWindow.Content = null;
                _fullscreenWindow.Close();
                _fullscreenWindow = null;
            }

            if (_parentIsPanel && _originalParent is Panel panel)
                panel.Children.Insert(_originalIndex, this);
            else if (!_parentIsPanel && _originalParent is Decorator decorator)
                decorator.Child = this;

            FitToWindow();
        }

        #endregion

        #region 辅助方法

        private string GetPixelValueString(int col, int row)
        {
            if (_currentHImage == null) return "灰度: -";

            try
            {
                var grayVal = _currentHImage.GetGrayval(row, col);
                return $"灰度: {grayVal}";
            }
            catch
            {
                return "灰度: -";
            }
        }

        #endregion
    }
}
