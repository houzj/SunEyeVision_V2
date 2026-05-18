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

using System.Windows.Threading;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>图像自动适配窗口模式</summary>
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
    /// 使用 HDrawingObject 进行区域交互
    /// </summary>
    public partial class HalconImageControl : UserControl
    {
        #region 私有字段

        private HImage? _currentHImage;
        private bool _isHalconReady;

        // 全屏相关
        private bool _isFullscreen;
        private Window? _fullscreenWindow;
        private FrameworkElement? _originalParent;
        private int _originalIndex;
        private bool _parentIsPanel;

        // 图像尺寸
        private int _originalImageWidth;
        private int _originalImageHeight;

        // AutoFitMode 首次加载标记
        private bool _isFirstAutoFitDone;

        // 交互式绘制临时状态
        private HDrawingObject? _tempDrawObj;
        private ShapeParameters _tempShape;
        private HDrawingObject.HDrawingObjectCallback? _tempDrawCallback;

        // 区域 ID → HDrawingObject 映射
        private readonly Dictionary<Guid, HDrawingObject> _regionDrawingObjects = new();

        // 区域 HDrawingObject 回调委托持久化（防止 GC 回收导致 native crash）
        private readonly Dictionary<Guid, HDrawingObject.HDrawingObjectCallback> _regionCallbacks = new();

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

        #endregion

        #region 构造函数

        public HalconImageControl()
        {
            InitializeComponent();
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
        /// 设置图像引用但不操作 HWindow（供 WindowRenderer / DisplayResultImage 使用）
        /// 调用方必须传递我们可拥有的克隆图像，不再二次 Clone
        /// </summary>
        public void SetImageReference(HImage image)
        {
            if (image == null) return;

            _currentHImage?.Dispose();
            _currentHImage = image;

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

        /// <summary>清空所有显示（图像 + 覆盖 + HDrawingObject）</summary>
        public void ClearAll()
        {
            ClearAllRegionDrawingObjects();
            if (_isHalconReady && _currentHImage != null)
            {
                SmartWindow.HalconWindow.ClearWindow();
            }
            _currentHImage?.Dispose();
            _currentHImage = null;
            _isFirstAutoFitDone = false;
        }

        #endregion

        #region HDrawingObject 管理

        /// <summary>根据 ShapeParameters 创建 HDrawingObject</summary>
        private static HDrawingObject CreateHDrawingObject(ShapeParameters shape)
        {
            return shape.ShapeType switch
            {
                ShapeType.Circle => new HDrawingObject(shape.CenterY, shape.CenterX, shape.Radius),
                ShapeType.Rectangle => new HDrawingObject(
                    shape.CenterY - shape.Height / 2,
                    shape.CenterX - shape.Width / 2,
                    shape.CenterY + shape.Height / 2,
                    shape.CenterX + shape.Width / 2),
                ShapeType.RotatedRectangle => new HDrawingObject(
                    shape.CenterY, shape.CenterX,
                    shape.Angle * Math.PI / 180.0,
                    shape.Width / 2, shape.Height / 2),
                ShapeType.Line => CreateLineDrawingObject(shape),
                ShapeType.Polygon => CreatePolygonDrawingObject(shape),
                _ => throw new NotSupportedException($"不支持的形状: {shape.ShapeType}")
            };
        }

        private static HDrawingObject CreateLineDrawingObject(ShapeParameters shape)
        {
            var dummy = new HDrawingObject(shape.StartY, shape.StartX, shape.EndY, shape.EndX);
            dummy.CreateDrawingObjectLine(shape.StartY, shape.StartX, shape.EndY, shape.EndX);
            return dummy;
        }

        private static HDrawingObject CreatePolygonDrawingObject(ShapeParameters shape)
        {
            var rows = new HTuple(shape.Points.Select(p => p.Y).ToArray());
            var cols = new HTuple(shape.Points.Select(p => p.X).ToArray());
            var dummy = new HDrawingObject(0, 0, 1);
            dummy.CreateDrawingObjectXld(rows, cols);
            return dummy;
        }

        /// <summary>从 HDrawingObject 读取参数到 ShapeParameters</summary>
        private static void ReadDrawingObjectParams(HDrawingObject drawObj, ShapeType shapeType, ShapeParameters result)
        {
            switch (shapeType)
            {
                case ShapeType.Circle:
                    result.CenterY = drawObj.GetDrawingObjectParams("row");
                    result.CenterX = drawObj.GetDrawingObjectParams("column");
                    result.Radius = drawObj.GetDrawingObjectParams("radius");
                    break;
                case ShapeType.Rectangle:
                    double r1 = drawObj.GetDrawingObjectParams("row1");
                    double c1 = drawObj.GetDrawingObjectParams("column1");
                    double r2 = drawObj.GetDrawingObjectParams("row2");
                    double c2 = drawObj.GetDrawingObjectParams("column2");
                    result.CenterY = (r1 + r2) / 2;
                    result.CenterX = (c1 + c2) / 2;
                    result.Height = Math.Abs(r2 - r1);
                    result.Width = Math.Abs(c2 - c1);
                    break;
                case ShapeType.RotatedRectangle:
                    result.CenterY = drawObj.GetDrawingObjectParams("row");
                    result.CenterX = drawObj.GetDrawingObjectParams("column");
                    result.Angle = drawObj.GetDrawingObjectParams("phi") * 180.0 / Math.PI;
                    result.Width = drawObj.GetDrawingObjectParams("length1") * 2;
                    result.Height = drawObj.GetDrawingObjectParams("length2") * 2;
                    break;
                case ShapeType.Line:
                    result.StartY = drawObj.GetDrawingObjectParams("row1");
                    result.StartX = drawObj.GetDrawingObjectParams("column1");
                    result.EndY = drawObj.GetDrawingObjectParams("row2");
                    result.EndX = drawObj.GetDrawingObjectParams("column2");
                    break;
            }
        }

        /// <summary>从原生 drawId 读取参数（回调线程直接调用，避免 UI 线程重入）</summary>
        private static void NativeReadDrawingObjectParams(IntPtr drawId, ShapeType shapeType, ShapeParameters result)
        {
            var hId = new HTuple(drawId.ToInt64());
            switch (shapeType)
            {
                case ShapeType.Circle:
                    HOperatorSet.GetDrawingObjectParams(hId, "row", out var row);
                    HOperatorSet.GetDrawingObjectParams(hId, "column", out var col);
                    HOperatorSet.GetDrawingObjectParams(hId, "radius", out var radius);
                    result.CenterY = row.D;
                    result.CenterX = col.D;
                    result.Radius = radius.D;
                    break;
                case ShapeType.Rectangle:
                    HOperatorSet.GetDrawingObjectParams(hId, "row1", out var r1);
                    HOperatorSet.GetDrawingObjectParams(hId, "column1", out var c1);
                    HOperatorSet.GetDrawingObjectParams(hId, "row2", out var r2);
                    HOperatorSet.GetDrawingObjectParams(hId, "column2", out var c2);
                    result.CenterY = (r1.D + r2.D) / 2;
                    result.CenterX = (c1.D + c2.D) / 2;
                    result.Height = Math.Abs(r2.D - r1.D);
                    result.Width = Math.Abs(c2.D - c1.D);
                    break;
                case ShapeType.RotatedRectangle:
                    HOperatorSet.GetDrawingObjectParams(hId, "row", out var rotRow);
                    HOperatorSet.GetDrawingObjectParams(hId, "column", out var rotCol);
                    HOperatorSet.GetDrawingObjectParams(hId, "phi", out var phi);
                    HOperatorSet.GetDrawingObjectParams(hId, "length1", out var len1);
                    HOperatorSet.GetDrawingObjectParams(hId, "length2", out var len2);
                    result.CenterY = rotRow.D;
                    result.CenterX = rotCol.D;
                    result.Angle = phi.D * 180.0 / Math.PI;
                    result.Width = len1.D * 2;
                    result.Height = len2.D * 2;
                    break;
                case ShapeType.Line:
                    HOperatorSet.GetDrawingObjectParams(hId, "row1", out var lr1);
                    HOperatorSet.GetDrawingObjectParams(hId, "column1", out var lc1);
                    HOperatorSet.GetDrawingObjectParams(hId, "row2", out var lr2);
                    HOperatorSet.GetDrawingObjectParams(hId, "column2", out var lc2);
                    result.StartY = lr1.D;
                    result.StartX = lc1.D;
                    result.EndY = lr2.D;
                    result.EndX = lc2.D;
                    break;
            }
        }

        /// <summary>开始交互式绘制形状</summary>
        public void BeginInteractiveDraw(ShapeType shapeType)
        {
            if (!_isHalconReady) return;

            var hWindow = SmartWindow.HalconWindow;
            hWindow.GetPart(out int r1, out int c1, out int r2, out int c2);
            double vw = Math.Abs(c2 - c1) + 1;
            double vh = Math.Abs(r2 - r1) + 1;
            double cr = (r1 + r2) / 2.0, cc = (c1 + c2) / 2.0;

            _tempShape = new ShapeParameters { ShapeType = shapeType };

            var drawObj = shapeType switch
            {
                ShapeType.Circle => new HDrawingObject(cr, cc, Math.Min(vw, vh) / 4.0),
                ShapeType.Rectangle => new HDrawingObject(cr - vh / 6, cc - vw / 6, cr + vh / 6, cc + vw / 6),
                ShapeType.RotatedRectangle => new HDrawingObject(cr, cc, 0.0, vw / 6, vh / 6),
                ShapeType.Line => CreateLineDrawAtCenter(cr, cc, vw),
                ShapeType.Polygon => CreatePolygonDrawAtCenter(cr, cc, vw, vh),
                _ => throw new NotSupportedException($"不支持的形状: {shapeType}")
            };

            drawObj.SetDrawingObjectParams(new HTuple("color"), new HTuple("yellow"));
            drawObj.SetDrawingObjectParams(new HTuple("line_width"), new HTuple(2));

            // 拖拽/缩放时同步参数
            _tempDrawCallback = new HDrawingObject.HDrawingObjectCallback(OnTempDrawObjEvent);
            drawObj.OnDrag(_tempDrawCallback);
            drawObj.OnResize(_tempDrawCallback);

            _tempDrawObj = drawObj;
            hWindow.AttachDrawingObjectToWindow(drawObj);
        }

        private static HDrawingObject CreateLineDrawAtCenter(double cr, double cc, double vw)
        {
            var dummy = new HDrawingObject(cr, cc - vw / 4, cr, cc + vw / 4);
            dummy.CreateDrawingObjectLine(cr, cc - vw / 4, cr, cc + vw / 4);
            return dummy;
        }

        private static HDrawingObject CreatePolygonDrawAtCenter(double cr, double cc, double vw, double vh)
        {
            double hw6 = vw / 6, hh6 = vh / 6;
            var dummy = new HDrawingObject(0, 0, 1);
            dummy.CreateDrawingObjectXld(
                new HTuple(new[] { cr - hh6, cr, cr + hh6, cr }),
                new HTuple(new[] { cc, cc + hw6, cc, cc - hw6 }));
            return dummy;
        }

        private void OnTempDrawObjEvent(IntPtr drawId, IntPtr windowHandle, string type)
        {
            if (_tempDrawObj == null) return;
            // ★ 直接使用原生 API 读取参数，不经过 UI 线程调度
            // 避免拖拽操作期间 UI 线程重入 Halcon 图形系统导致其他 DrawingObject 丢失
            NativeReadDrawingObjectParams(drawId, _tempShape.ShapeType, _tempShape);
        }

        /// <summary>结束交互式绘制并获取结果</summary>
        public bool EndInteractiveDraw(out ShapeParameters? result)
        {
            if (_tempDrawObj == null || !_isHalconReady)
            {
                result = null;
                return false;
            }

            ReadDrawingObjectParams(_tempDrawObj, _tempShape.ShapeType, _tempShape);

            var hWindow = SmartWindow.HalconWindow;
            hWindow.DetachDrawingObjectFromWindow(_tempDrawObj);
            _tempDrawObj.Dispose();
            _tempDrawObj = null;
            _tempDrawCallback = null;

            result = _tempShape;
            return true;
        }

        /// <summary>取消交互式绘制</summary>
        public void CancelInteractiveDraw()
        {
            if (_tempDrawObj == null) return;

            try
            {
                if (_isHalconReady)
                {
                    var hWindow = SmartWindow.HalconWindow;
                    hWindow.DetachDrawingObjectFromWindow(_tempDrawObj);
                }
            }
            catch { }
            _tempDrawObj.Dispose();
            _tempDrawObj = null;
            _tempDrawCallback = null;
        }

        /// <summary>渲染区域为 HDrawingObject 并附着到窗口</summary>
        public void RenderRegionDrawingObject(Guid regionId, ShapeParameters shape)
        {
            if (!_isHalconReady) return;

            if (_regionDrawingObjects.ContainsKey(regionId))
                RemoveRegionDrawingObject(regionId);

            var drawObj = CreateHDrawingObject(shape);
            drawObj.SetDrawingObjectParams(new HTuple("color"), new HTuple("cyan"));
            drawObj.SetDrawingObjectParams(new HTuple("line_width"), new HTuple(2));

            // 注册永久区域回调：拖拽/缩放时同步参数到数据模型
            var capturedShape = shape;
            var onDragResize = new HDrawingObject.HDrawingObjectCallback((id, hwnd, type) =>
            {
                if (capturedShape == null) return;
                // ★ 直接使用原生 API 读取参数，不经过 UI 线程调度
                // 避免拖拽操作期间 UI 线程重入 Halcon 图形系统导致其他 DrawingObject 丢失
                NativeReadDrawingObjectParams(id, capturedShape.ShapeType, capturedShape);
            });

            drawObj.OnDrag(onDragResize);
            drawObj.OnResize(onDragResize);

            _regionCallbacks[regionId] = onDragResize;

            var hWindow = SmartWindow.HalconWindow;
            hWindow.AttachDrawingObjectToWindow(drawObj);
            _regionDrawingObjects[regionId] = drawObj;
        }

        /// <summary>更新 HDrawingObject 位置/大小</summary>
        public void UpdateRegionDrawingObject(Guid regionId, ShapeParameters shape)
        {
            if (!_isHalconReady) return;
            RemoveRegionDrawingObject(regionId);
            RenderRegionDrawingObject(regionId, shape);
        }

        /// <summary>移除区域的 HDrawingObject</summary>
        public void RemoveRegionDrawingObject(Guid regionId)
        {
            if (!_regionDrawingObjects.TryGetValue(regionId, out var drawObj)) return;

            try
            {
                if (_isHalconReady)
                {
                    var hWindow = SmartWindow.HalconWindow;
                    hWindow.DetachDrawingObjectFromWindow(drawObj);
                }
            }
            catch { }
            drawObj.Dispose();
            _regionDrawingObjects.Remove(regionId);
            _regionCallbacks.Remove(regionId);
        }

        /// <summary>清理所有 HDrawingObject</summary>
        public void ClearAllRegionDrawingObjects()
        {
            if (!_isHalconReady)
            {
                foreach (var drawObj in _regionDrawingObjects.Values)
                {
                    try { drawObj.Dispose(); }
                    catch { }
                }
                _regionDrawingObjects.Clear();
                _regionCallbacks.Clear();
                return;
            }

            var hWindow = SmartWindow.HalconWindow;
            foreach (var drawObj in _regionDrawingObjects.Values)
            {
                try { hWindow.DetachDrawingObjectFromWindow(drawObj); }
                catch { }
                try { drawObj.Dispose(); }
                catch { }
            }
            _regionDrawingObjects.Clear();
            _regionCallbacks.Clear();
        }

        /// <summary>选中指定区域的 HDrawingObject（高亮）</summary>
        public void SelectRegionDrawingObject(Guid regionId)
        {
            foreach (var kvp in _regionDrawingObjects)
            {
                var color = kvp.Key == regionId ? new HTuple("yellow") : new HTuple("cyan");
                kvp.Value.SetDrawingObjectParams(new HTuple("color"), color);
            }
        }

        #endregion

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
