using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 测量编辑器管理器 - 统一编排"测量卡尺引导 + 屏蔽区域"
    /// </summary>
    /// <remarks>
    /// 对齐 RoiEditorManagerControl 的生命周期模式（SDK 通用控件，工具层只组装）：
    /// - guideEditor（MeasureEditorControl）：测量卡尺引导（直线/圆/弧，单对象）
    /// - maskEditor（RoiEditorControl）：屏蔽区域（多边形/圆/矩形，多对象）
    /// - 统一：SetMainImageControl / ClearMainOverlay / 缩放同步 / 渲染节流 / 事件路由
    /// - 事件路由（异质）：绘制模式优先 → 交互中编辑器 → 命中测试（屏蔽优先）
    /// 两个子编辑器均为"无头"（不自订阅 ImageControl 事件），由本管理器统一路由，
    /// 避免共用一个 ImageControl 时的事件冲突。
    /// </remarks>
    public partial class MeasurementEditorManagerControl : UserControl
    {
        private ImageControl? _mainImageControl;

        // 视口缩放跟踪
        private double _viewportZoom = 1.0;

        // 渲染节流
        private bool _renderScheduled = false;

        /// <summary>当前激活交互的子编辑器（绘制模式/交互中）</summary>
        private object? _activeEditor;

        /// <summary>屏蔽区域渲染器（对齐 RoiEditorManagerControl 的 WpfShapeRenderer）</summary>
        private WpfShapeRenderer? _maskRenderer;

        public MeasurementEditorManagerControl()
        {
            // 设计时完全跳过初始化，避免 XAML 解析触发 NullReferenceException
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            InitializeComponent();

            // 订阅子编辑器渲染请求（屏蔽区域经 RenderRequested 触发统一渲染）
            maskEditor.RenderRequested += OnMaskRenderRequested;
        }

        #region Dependency Properties

        /// <summary>
        /// 屏蔽区域集合（多 ROI）
        /// </summary>
        public static readonly DependencyProperty MaskRegionsSourceProperty =
            DependencyProperty.Register(nameof(MaskRegionsSource),
                typeof(ObservableCollection<RoiData>),
                typeof(MeasurementEditorManagerControl),
                new PropertyMetadata(null, OnMaskSourceChanged));

        /// <summary>
        /// 屏蔽区域集合（多 ROI）
        /// </summary>
        public ObservableCollection<RoiData>? MaskRegionsSource
        {
            get => (ObservableCollection<RoiData>?)GetValue(MaskRegionsSourceProperty);
            set => SetValue(MaskRegionsSourceProperty, value);
        }

        private static void OnMaskSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MeasurementEditorManagerControl control && e.NewValue is ObservableCollection<RoiData> source)
            {
                control.maskEditor?.SetRegionsSource(source);
            }
        }

        #endregion

        #region 引导编辑器转发（宿主直接操作测量卡尺）

        /// <summary>测量卡尺编辑器（供宿主设置引导/卡尺参数/进入绘制）</summary>
        public MeasureEditorControl GuideEditor => guideEditor;

        /// <summary>
        /// 设置位置修正矩阵（作用于引导几何 + 屏蔽区域，显示/交互/算法全修正）
        /// </summary>
        public void SetPositionCorrectionMatrix(Execution.Results.AffineMatrix? matrix)
        {
            guideEditor.SetPositionCorrectionMatrix(matrix);
            maskEditor.SetPositionCorrectionMatrix(matrix);
        }

        #endregion

        #region 生命周期：绑定 / 解绑

        /// <summary>
        /// 绑定主图像控件（统一编排引导 + 屏蔽，订阅一次事件）
        /// </summary>
        public void SetMainImageControl(ImageControl? imageControl)
        {
            if (_mainImageControl == imageControl)
                return;

            if (_mainImageControl != null)
            {
                UnsubscribeEvents();
            }

            _mainImageControl = imageControl;

            if (_mainImageControl != null)
            {
                _mainImageControl.OverlayHitTestVisible = false;

                // 两个子编辑器共用同一叠加画布
                var overlayCanvas = _mainImageControl.InteractionOverlayCanvasControl;
                guideEditor.SetOverlayCanvas(overlayCanvas);
                maskEditor.OverlayCanvas = overlayCanvas;

                // 屏蔽区域渲染器（单个，合并渲染）
                _maskRenderer?.Dispose();
                _maskRenderer = new WpfShapeRenderer(overlayCanvas, VisionLogger.Instance);

                // 初始缩放同步
                _viewportZoom = _mainImageControl.Zoom;
                guideEditor.SetZoom(_viewportZoom);
                maskEditor.CurrentZoom = _viewportZoom;

                SubscribeEvents();

                PluginLogger.Info("[MeasurementEditorManager] ImageControl set with centralized routing", "MeasurementEditorManager");
            }
        }

        /// <summary>
        /// 清除主叠加层：解订阅 + 清空画布 + 解除子编辑器
        /// </summary>
        public void ClearMainOverlay()
        {
            try
            {
                UnsubscribeEvents();

                if (_mainImageControl?.InteractionOverlayCanvasControl != null)
                {
                    _mainImageControl.OverlayHitTestVisible = false;
                    _mainImageControl.InteractionOverlayCanvasControl.Children.Clear();
                }

                if (_maskRenderer != null)
                {
                    _maskRenderer.Clear();
                    _maskRenderer.Dispose();
                    _maskRenderer = null;
                }

                guideEditor.SetOverlayCanvas(null);
                maskEditor.OverlayCanvas = null;
                _mainImageControl = null;

                PluginLogger.Info("[MeasurementEditorManager] Overlay cleared and events unsubscribed", "MeasurementEditorManager");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("[MeasurementEditorManager] ClearMainOverlay failed: " + ex.Message, exception: ex);
            }
        }

        #endregion

        #region 事件订阅

        private void SubscribeEvents()
        {
            if (_mainImageControl == null) return;
            _mainImageControl.ImageMouseMove += OnImageMouseMove;
            _mainImageControl.CanvasMouseLeftButtonDown += OnCanvasMouseLeftButtonDown;
            _mainImageControl.CanvasMouseLeftButtonUp += OnCanvasMouseLeftButtonUp;
            _mainImageControl.ViewTransformed += OnViewTransformed;

            var window = Window.GetWindow(_mainImageControl);
            if (window != null)
            {
                window.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
                window.AddHandler(Window.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowMouseLeftButtonUp));
            }
        }

        private void UnsubscribeEvents()
        {
            if (_mainImageControl == null) return;
            _mainImageControl.ImageMouseMove -= OnImageMouseMove;
            _mainImageControl.CanvasMouseLeftButtonDown -= OnCanvasMouseLeftButtonDown;
            _mainImageControl.CanvasMouseLeftButtonUp -= OnCanvasMouseLeftButtonUp;
            _mainImageControl.ViewTransformed -= OnViewTransformed;

            var window = Window.GetWindow(_mainImageControl);
            if (window != null)
            {
                window.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
                window.RemoveHandler(Window.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowMouseLeftButtonUp));
            }
        }

        #endregion

        #region 事件路由（引导 vs 屏蔽，异质）

        /// <summary>
        /// 获取当前正在交互的子编辑器
        /// </summary>
        private object? GetInteractingEditor()
        {
            if (guideEditor.IsAnyInteractionInProgress) return guideEditor;
            if (maskEditor.IsAnyInteractionInProgress) return maskEditor;
            return null;
        }

        private void OnImageMouseMove(object? sender, ImageMouseEventArgs e)
        {
            var pos = e.ImagePosition;

            // 只路由给正在交互的编辑器
            if (guideEditor.IsAnyInteractionInProgress)
            {
                guideEditor.ProcessMouseMove(pos);
            }
            else if (maskEditor.IsAnyInteractionInProgress)
            {
                maskEditor.ProcessMouseMove(pos);
            }
            else
            {
                // 无交互悬停：仍路由给引导编辑器更新悬停光标（手柄/引导/卡尺不同光标）
                guideEditor.ProcessMouseMove(pos);
            }

            RenderAll();
        }

        private void OnCanvasMouseLeftButtonDown(object? sender, ImageMouseEventArgs e)
        {
            if (_mainImageControl == null) return;
            var pos = e.ImagePosition;

            // 场景1 - 有编辑器正在交互，只路由给该编辑器
            var interacting = GetInteractingEditor();
            if (interacting != null)
            {
                RouteMouseDown(interacting, pos, e);
                return;
            }

            // 场景2 - 绘制模式优先（互斥：激活引导时屏蔽进入空闲，反之亦然，避免弧冲突）
            if (guideEditor.IsDrawingMode)
            {
                var consumed = guideEditor.ProcessMouseDown(pos);
                if (_activeEditor != guideEditor)
                {
                    _activeEditor = guideEditor;
                    maskEditor.EnterIdleMode();   // 屏蔽区域取消绘制/选中
                }
                e.Handled = consumed;
                RenderAll();
                return;
            }
            if (maskEditor.ViewModel.IsDrawing)
            {
                var consumed = maskEditor.ProcessMouseDown(pos);
                if (_activeEditor != maskEditor)
                {
                    _activeEditor = maskEditor;
                    guideEditor.CancelDrawing();  // 引导取消绘制/选中
                }
                e.Handled = consumed;
                RenderAll();
                return;
            }

            // 场景3 - 选择模式 HitTest：屏蔽区域优先命中（视觉上层，更大容差）
            var maskHit = maskEditor.HitTestRegion(pos, tolerance: 8.0);
            if (maskHit != null)
            {
                var consumed = maskEditor.ProcessMouseDown(pos);
                guideEditor.ProcessMouseDown(pos); // 引导取消选中（内部未命中返回 false）
                _activeEditor = maskEditor;
                e.Handled = consumed;
                RenderAll();
                return;
            }

            // 场景4 - 引导主体/手柄命中
            if (guideEditor.ProcessMouseDown(pos))
            {
                maskEditor.DeselectAll();         // 屏蔽取消选中，避免双选中
                _activeEditor = guideEditor;
                e.Handled = true;
                RenderAll();
                return;
            }

            // 场景5 - 都没有命中 → 双方取消选中，允许平移
            maskEditor.DeselectAll();
            e.Handled = false;
            RenderAll();
        }

        /// <summary>
        /// 路由鼠标按下到指定子编辑器
        /// </summary>
        private void RouteMouseDown(object editor, Point pos, ImageMouseEventArgs e)
        {
            _activeEditor = editor;
            bool consumed;
            if (editor == guideEditor)
            {
                consumed = guideEditor.ProcessMouseDown(pos);
            }
            else
            {
                consumed = maskEditor.ProcessMouseDown(pos);
            }
            e.Handled = consumed;
            RenderAll();
        }

        private void OnCanvasMouseLeftButtonUp(object? sender, ImageMouseEventArgs e)
        {
            var interacting = GetInteractingEditor();
            if (interacting != null)
            {
                if (interacting == guideEditor)
                    guideEditor.ProcessMouseUp();
                else
                    maskEditor.ProcessMouseUp();
                RenderAll();
            }
        }

        private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!guideEditor.IsAnyInteractionInProgress && !maskEditor.IsAnyInteractionInProgress)
                return;

            var interacting = GetInteractingEditor();
            if (interacting != null)
            {
                if (interacting == guideEditor)
                    guideEditor.ProcessMouseUp();
                else
                    maskEditor.ProcessMouseUp();
                RenderAll();
            }
        }

        #endregion

        #region 缩放同步

        private void OnViewTransformed(object? sender, ViewTransformEventArgs e)
        {
            _viewportZoom = e.Zoom;
            guideEditor.SetZoom(_viewportZoom);
            maskEditor.CurrentZoom = _viewportZoom;
            RenderAll();
        }

        #endregion

        #region 键盘路由

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Delete：优先删除引导几何，其次屏蔽区域
            if (e.Key == Key.Delete)
            {
                if (guideEditor.HasGuide)
                {
                    guideEditor.HandleDelete();
                    e.Handled = true;
                    RenderAll();
                }
                else if (maskEditor.GetSelectedRegion() != null)
                {
                    maskEditor.HandleDelete();
                    e.Handled = true;
                    RenderAll();
                }
                return;
            }
        }

        #endregion

        #region 渲染

        private void OnMaskRenderRequested() => RequestRender();

        /// <summary>
        /// 渲染节流：合并多次渲染请求为一次 RenderAll
        /// </summary>
        private void RequestRender()
        {
            if (!_renderScheduled)
            {
                _renderScheduled = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _renderScheduled = false;
                    RenderAll();
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// 渲染全部子编辑器内容
        /// 引导由 MeasureEditorControl 内部渲染（MeasureBandOverlay），
        /// 屏蔽区域由 WpfShapeRenderer 合并渲染（对齐 RoiEditorManagerControl.RenderAll）。
        /// </summary>
        private void RenderAll()
        {
            if (_mainImageControl == null) return;

            // 屏蔽区域：合并渲染上下文 + 手柄（对齐 RoiEditorManagerControl.RenderAll）
            var overlayCanvas = _mainImageControl.InteractionOverlayCanvasControl;
            var maskContexts = maskEditor.GetRenderContexts();
            var maskPreview = maskEditor.GetPreviewContext();
            if (maskPreview != null)
                maskContexts.Add(maskPreview);

            _maskRenderer?.Render(maskContexts, new RoiRenderOptions { ShowLabels = true, ShowHandles = false });

            // 清除陈旧屏蔽手柄 + 绘制选中区域手柄
            Roi.Rendering.HandleRenderer.ClearHandles(overlayCanvas);
            if (maskEditor.GetSelectedRegion() != null && !maskEditor.IsDrawingInteraction)
                maskEditor.DrawEditHandles(maskEditor.GetSelectedRegion()!);

            // 引导：由 MeasureEditorControl 内部渲染（测量带 + 引导手柄）
            guideEditor.RenderForManager();
        }

        #endregion
    }
}
