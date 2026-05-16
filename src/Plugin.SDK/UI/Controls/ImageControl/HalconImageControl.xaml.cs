using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
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
        private Point _drawStartPos;
        private Rectangle? _previewRect;

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
        // 参数: r1, c1, r2, c2 (图像坐标), 返回 true 表示已处理（跳过默认 DrawingObject 创建）
        internal Func<double, double, double, double, bool>? OnPreviewDrawComplete;

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

        /// <summary>当前是否处于绘制预览状态（Canvas 拦截鼠标事件）</summary>
        public bool IsDrawingPreviewActive => _isDrawingActive;

        /// <summary>绘制预览 Canvas（供 RegionEditorManager 设置绘制模式）</summary>
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
                System.Diagnostics.Debug.WriteLine($"[HalconImageControl] CreateRegionDrawingObject 失败: {ex.Message}");
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
                }

                ApplyDrawingObjectStyle(drawObj, shape);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconImageControl] UpdateRegionDrawingObject 失败: {ex.Message}");
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconImageControl] ReadDrawingObjectParams 失败: {ex.Message}");
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

        #region 绘制预览 (PreviewCanvas)

        /// <summary>开始绘制预览模式（Canvas 拦截鼠标事件）</summary>
        public void BeginPreviewDraw()
        {
            _isDrawingActive = true;
            PreviewCanvas.IsHitTestVisible = true;
            Cursor = Cursors.Cross;
        }

        /// <summary>结束绘制预览模式（Canvas 穿透事件）</summary>
        public void EndPreviewDraw()
        {
            _isDrawingActive = false;
            PreviewCanvas.IsHitTestVisible = false;
            Cursor = Cursors.Arrow;
            RemovePreviewShape();
        }

        private void RemovePreviewShape()
        {
            if (_previewRect != null)
            {
                PreviewCanvas.Children.Remove(_previewRect);
                _previewRect = null;
            }
            _isDrawing = false;
        }

        private Point GetMouseImagePos(MouseEventArgs e)
        {
            if (!_isHalconReady || _currentHImage == null) return new Point(0, 0);
            try
            {
                var winPos = e.GetPosition(SmartWindow);
                var hWindow = SmartWindow.HalconWindow;
                hWindow.ConvertCoordinatesWindowToImage(
                    winPos.Y, winPos.X, out double imgRow, out double imgCol);
                return new Point(imgCol, imgRow);
            }
            catch
            {
                return new Point(0, 0);
            }
        }

        private void PreviewCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            _drawStartPos = GetMouseImagePos(e);

            _previewRect = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0))
            };
            PreviewCanvas.Children.Add(_previewRect);

            PreviewDrawMouseDown?.Invoke(this, e);
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _previewRect == null) return;

            var pos = GetMouseImagePos(e);
            var x = Math.Min(_drawStartPos.X, pos.X);
            var y = Math.Min(_drawStartPos.Y, pos.Y);
            _previewRect.Width = Math.Abs(pos.X - _drawStartPos.X);
            _previewRect.Height = Math.Abs(pos.Y - _drawStartPos.Y);
            Canvas.SetLeft(_previewRect, x);
            Canvas.SetTop(_previewRect, y);

            PreviewDrawMouseMove?.Invoke(this, e);
        }

        private void PreviewCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing) return;
            _isDrawing = false;

            var pos = GetMouseImagePos(e);
            double r1 = Math.Min(_drawStartPos.Y, pos.Y);
            double c1 = Math.Min(_drawStartPos.X, pos.X);
            double r2 = Math.Max(_drawStartPos.Y, pos.Y);
            double c2 = Math.Max(_drawStartPos.X, pos.X);

            // 触发预览绘制完成回调（供 RegionEditorControl 处理）
            bool handled = false;
            if (OnPreviewDrawComplete != null)
            {
                try
                {
                    handled = OnPreviewDrawComplete.Invoke(r1, c1, r2, c2);
                }
                catch { }
            }

            // 如果回调未处理，按默认方式创建矩形 DrawingObject
            if (!handled && _isHalconReady && _currentHImage != null)
            {
                CreateDrawingObjectRect(r1, c1, r2, c2);
            }

            RemovePreviewShape();
            EndPreviewDraw();

            PreviewDrawMouseUp?.Invoke(this, e);
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
