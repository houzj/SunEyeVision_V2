using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views
{
    /// <summary>
    /// Unified region editor manager control.
    /// Centralizes event routing and rendering for two RoiEditorControl instances.
    /// Single WpfShapeRenderer and single event subscription set.
    /// </summary>
    public partial class RoiEditorManagerControl : UserControl
    {
        private ImageControl? _mainImageControl;
        private WpfShapeRenderer? _renderer;

        // Viewport zoom tracking
        private double _viewportZoom = 1.0;

        // ★ Phase 3: 渲染节流
        private bool _renderScheduled = false;

        // ★ Phase 4: 当前激活工具的编辑器引用（用于键盘快捷键路由和分组互斥）
        private RoiEditorControl? _activeEditor;

        public RoiEditorManagerControl()
        {
            InitializeComponent();

            // 设计器也必须创建可视树；仅跳过依赖运行时交互对象的事件关联。
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            // Subscribe to child editors' events
            inspectionsEditor.RenderRequested += OnEditorRenderRequested;
            masksEditor.RenderRequested += OnEditorRenderRequested;

            // ★ Phase 4: 订阅工具激活事件（分组互斥控制）
            inspectionsEditor.ToolActivated += () => OnEditorToolActivated(inspectionsEditor);
            masksEditor.ToolActivated += () => OnEditorToolActivated(masksEditor);
        }

        #region Dependency Properties

        public static readonly DependencyProperty InspectionRoisSourceProperty =
            DependencyProperty.Register(nameof(InspectionRoisSource),
                typeof(ObservableCollection<Roi>),
                typeof(RoiEditorManagerControl),
                new PropertyMetadata(null, OnInspectionSourceChanged));

        public ObservableCollection<Roi>? InspectionRoisSource
        {
            get => (ObservableCollection<Roi>?)GetValue(InspectionRoisSourceProperty);
            set => SetValue(InspectionRoisSourceProperty, value);
        }

        public static readonly DependencyProperty MaskRoisSourceProperty =
            DependencyProperty.Register(nameof(MaskRoisSource),
                typeof(ObservableCollection<Roi>),
                typeof(RoiEditorManagerControl),
                new PropertyMetadata(null, OnMaskSourceChanged));

        public ObservableCollection<Roi>? MaskRoisSource
        {
            get => (ObservableCollection<Roi>?)GetValue(MaskRoisSourceProperty);
            set => SetValue(MaskRoisSourceProperty, value);
        }

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources),
                typeof(ObservableCollection<AvailableDataSource>),
                typeof(RoiEditorManagerControl),
                new PropertyMetadata(null, OnAvailableDataSourcesChanged));

        public ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        #endregion

        #region Property Change Callbacks

        private static void OnInspectionSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorManagerControl control && e.NewValue is ObservableCollection<Roi> source)
            {
                control.inspectionsEditor?.SetRoisSource(source);
                PluginLogger.Info("[RoiEditorManager] Inspection roi collection injected | Count=" + source.Count, "RoiEditorManager");
            }
        }

        private static void OnMaskSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorManagerControl control && e.NewValue is ObservableCollection<Roi> source)
            {
                control.masksEditor?.SetRoisSource(source);
                PluginLogger.Info("[RoiEditorManager] Mask roi collection injected | Count=" + source.Count, "RoiEditorManager");
            }
        }

        private static void OnAvailableDataSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorManagerControl control && e.NewValue is ObservableCollection<AvailableDataSource> sources)
            {
                control.inspectionsEditor.AvailableDataSources = sources;
                control.masksEditor.AvailableDataSources = sources;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set main ImageControl and subscribe events once for centralized routing
        /// </summary>
        public void SetMainImageControl(ImageControl? imageControl)
        {
            if (_mainImageControl == imageControl)
                return;

            // Unsubscribe old events
            if (_mainImageControl != null)
            {
                UnsubscribeImageControlEvents();
                UnsubscribeWindowEvents();
            }

            _mainImageControl = imageControl;

            if (_mainImageControl != null)
            {
                inspectionsEditor.DrawingAvailabilityCheck = EnsureImageAvailable;
                masksEditor.DrawingAvailabilityCheck = EnsureImageAvailable;
                _mainImageControl.OverlayHitTestVisible = false;

                // Pass OverlayCanvas to both editors for handle/rubber band drawing
                var overlayCanvas = _mainImageControl.InteractionOverlayCanvasControl;
                inspectionsEditor.OverlayCanvas = overlayCanvas;
                masksEditor.OverlayCanvas = overlayCanvas;

                // Create single WpfShapeRenderer
                _renderer?.Dispose();
                _renderer = new WpfShapeRenderer(overlayCanvas, VisionLogger.Instance);

                // Sync initial zoom
                _viewportZoom = _mainImageControl.Zoom;
                _renderer.SetViewportTransform(_viewportZoom, new Point(0, 0));
                inspectionsEditor.CurrentZoom = _viewportZoom;
                masksEditor.CurrentZoom = _viewportZoom;

                // Subscribe events once
                SubscribeImageControlEvents();
                SubscribeWindowEvents();

                // Initial render
                RenderAll();

                PluginLogger.Info("[RoiEditorManager] ImageControl set with centralized event routing", "RoiEditorManager");
            }
            else
            {
                inspectionsEditor.DrawingAvailabilityCheck = null;
                masksEditor.DrawingAvailabilityCheck = null;
            }
        }

        /// <summary>
        /// 绘制 ROI 前统一确认图像显示控件已有图像。
        /// 图像为空时阻止工具进入绘制状态，并给出可理解的用户提示。
        /// </summary>
        private bool EnsureImageAvailable()
        {
            if (_mainImageControl?.SourceImage != null)
                return true;

            MessageBox.Show(Window.GetWindow(this),
                "当前图像显示控件中没有图像，请先加载或选择一张图像。",
                "无法绘制区域",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        /// <summary>
        /// 设置位置修正矩阵，透传给两个子编辑器。
        /// 显示正向变换，编辑逆变换，存储不变（始终存参考坐标系）。
        /// </summary>
        public void SetPositionCorrectionMatrix(Execution.Results.AffineMatrix? matrix)
        {
            var matrixInfo = matrix != null
                ? $"M11={matrix.M11:F4} M12={matrix.M12:F4} M21={matrix.M21:F4} M22={matrix.M22:F4} OffsetX={matrix.OffsetX:F2} OffsetY={matrix.OffsetY:F2}"
                : "null";
            PluginLogger.Info($"[RoiEditorManager] SetPositionCorrectionMatrix 传入 | Matrix={matrixInfo}", "RoiEditorManager");

            // ★ 诊断：记录传入矩阵的引用和哈希值，追踪是否每个调用都是同一实例
            var hash = matrix != null ? RuntimeHelpers.GetHashCode(matrix) : 0;

            inspectionsEditor.SetPositionCorrectionMatrix(matrix);
            masksEditor.SetPositionCorrectionMatrix(matrix);

            PluginLogger.Info($"[RoiEditorManager] SetPositionCorrectionMatrix 完成 | MatrixHash=0x{hash:X8} | inspectorEditor矩阵已设置 | maskEditor矩阵已设置", "RoiEditorManager");
        }

        /// <summary>
        /// Clear main overlay, unsubscribe events, dispose renderer
        /// </summary>
        public void ClearMainOverlay()
        {
            try
            {
                UnsubscribeImageControlEvents();
                UnsubscribeWindowEvents();

                if (_renderer != null)
                {
                    _renderer.Clear();
                    _renderer.Dispose();
                    _renderer = null;
                }

                if (_mainImageControl?.InteractionOverlayCanvasControl != null)
                {
                    _mainImageControl.OverlayHitTestVisible = false;
                    _mainImageControl.InteractionOverlayCanvasControl.Children.Clear();
                }

                inspectionsEditor.OverlayCanvas = null;
                masksEditor.OverlayCanvas = null;

                _mainImageControl = null;

                PluginLogger.Info("[RoiEditorManager] Overlay cleared and events unsubscribed", "RoiEditorManager");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("[RoiEditorManager] ClearMainOverlay failed: " + ex.Message, exception: ex);
            }
        }

        #endregion

        #region Event Subscription Management

        private void SubscribeImageControlEvents()
        {
            if (_mainImageControl == null) return;

            _mainImageControl.ImageMouseMove += OnImageMouseMove;
            _mainImageControl.CanvasMouseLeftButtonDown += OnCanvasMouseLeftButtonDown;
            _mainImageControl.CanvasMouseLeftButtonUp += OnCanvasMouseLeftButtonUp;
            _mainImageControl.ViewTransformed += OnViewTransformed;
            // ★ Phase 2: 键盘事件由 Manager 统一拦截路由
            // 使用 Window 级订阅而非 _mainImageControl.PreviewKeyDown，
            // 因为 PreviewKeyDown 是隧道事件，焦点在编辑器上时不会传播到 _mainImageControl
            var window = Window.GetWindow(_mainImageControl);
            if (window != null)
            {
                window.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
            }
        }

        private void UnsubscribeImageControlEvents()
        {
            if (_mainImageControl == null) return;

            _mainImageControl.ImageMouseMove -= OnImageMouseMove;
            _mainImageControl.CanvasMouseLeftButtonDown -= OnCanvasMouseLeftButtonDown;
            _mainImageControl.CanvasMouseLeftButtonUp -= OnCanvasMouseLeftButtonUp;
            _mainImageControl.ViewTransformed -= OnViewTransformed;
        }

        private void SubscribeWindowEvents()
        {
            if (_mainImageControl == null) return;

            var window = Window.GetWindow(_mainImageControl);
            if (window != null)
            {
                window.AddHandler(Window.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowMouseLeftButtonUp));
                window.AddHandler(Window.MouseRightButtonDownEvent, new MouseButtonEventHandler(OnWindowMouseRightButtonDown));
            }
        }

        private void UnsubscribeWindowEvents()
        {
            if (_mainImageControl == null) return;

            var window = Window.GetWindow(_mainImageControl);
            if (window != null)
            {
                window.RemoveHandler(Window.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowMouseLeftButtonUp));
                window.RemoveHandler(Window.MouseRightButtonDownEvent, new MouseButtonEventHandler(OnWindowMouseRightButtonDown));
                window.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
            }
        }

        #endregion

        #region Event Handlers (Single Subscription)

        /// <summary>
        /// 获取当前正在交互的编辑器（绘制/拖拽/拖拽手柄）
        /// </summary>
        private RoiEditorControl? GetInteractingEditor()
        {
            // ★ Phase 2: 优先路由给正在交互的编辑器
            if (inspectionsEditor.IsAnyInteractionInProgress)
                return inspectionsEditor;
            if (masksEditor.IsAnyInteractionInProgress)
                return masksEditor;
            return null;
        }

        /// <summary>
        /// 获取当前有选中区域的编辑器
        /// </summary>
        private RoiEditorControl? GetEditorWithSelection()
        {
            if (inspectionsEditor.GetSelectedRoi() != null)
                return inspectionsEditor;
            if (masksEditor.GetSelectedRoi() != null)
                return masksEditor;
            return null;
        }

        private void OnImageMouseMove(object? sender, ImageMouseEventArgs e)
        {
            var pos = e.ImagePosition;

            // 只路由给正在交互的编辑器，避免另一方意外触发
            if (inspectionsEditor.IsAnyInteractionInProgress)
            {
                inspectionsEditor.ProcessMouseMove(pos);
            }
            else if (masksEditor.IsAnyInteractionInProgress)
            {
                masksEditor.ProcessMouseMove(pos);
            }

            // 无交互时不需要路由 MouseMove（悬停光标由 QueryEditorCursor 独立查询驱动）

            // ProcessMouseMove 内部已移除 RenderRequested，只在这里统一渲染一次
            RenderAll();

            // ★ 悬停光标回归修复：对齐 MeasurementEditorManagerControl，MouseMove 直接驱动元素级 Cursor，
            //   屏蔽区/检测区各自命中自己的手柄/主体光标，空白恢复默认。
            _mainImageControl?.SetHoverCursor(QueryEditorCursor(pos));
        }

        /// <summary>
        /// 统一悬停光标路由（检测 vs 屏蔽双编辑器）：
        /// 绘制模式 → 交互中 → 屏蔽区域命中（视觉上层优先）→ 检测区域命中 → 空白 null。
        /// </summary>
        private Cursor? QueryEditorCursor(Point pos)
        {
            // 绘制模式优先（互斥）
            if (inspectionsEditor.ViewModel.IsDrawing) return inspectionsEditor.QueryCursor(pos);
            if (masksEditor.ViewModel.IsDrawing) return masksEditor.QueryCursor(pos);

            // 交互中（拖动/编辑手柄）
            if (inspectionsEditor.IsAnyInteractionInProgress) return inspectionsEditor.QueryCursor(pos);
            if (masksEditor.IsAnyInteractionInProgress) return masksEditor.QueryCursor(pos);

            // 选择模式：屏蔽区域视觉上层优先（更大容差），其次检测区域
            if (masksEditor.HitTestRoi(pos, tolerance: 8.0) != null)
                return masksEditor.QueryCursor(pos);
            if (inspectionsEditor.HitTestRoi(pos, tolerance: 4.0) != null)
                return inspectionsEditor.QueryCursor(pos);

            return null;
        }

        private void OnCanvasMouseLeftButtonDown(object? sender, ImageMouseEventArgs e)
        {
            if (_mainImageControl == null) return;
            var pos = e.ImagePosition;

            // 场景1 - 有编辑器正在交互，只路由给该编辑器
            var interactingEditor = GetInteractingEditor();
            if (interactingEditor != null)
            {
                interactingEditor.ProcessMouseDown(pos);
                e.Handled = true;
                RenderAll();
                return;
            }

            // 场景2 - 绘制模式优先：先检查是否有编辑器处于绘制模式
            // 即使鼠标位于其他区域上方，也优先执行绘制操作
            if (inspectionsEditor.ViewModel.IsDrawing)
            {
                var consumed = inspectionsEditor.ProcessMouseDown(pos);
                if (_activeEditor != inspectionsEditor)
                {
                    _activeEditor = inspectionsEditor;
                    masksEditor.EnterIdleMode();
                }
                e.Handled = consumed;
                RenderAll();
                return;
            }
            if (masksEditor.ViewModel.IsDrawing)
            {
                var consumed = masksEditor.ProcessMouseDown(pos);
                if (_activeEditor != masksEditor)
                {
                    _activeEditor = masksEditor;
                    inspectionsEditor.EnterIdleMode();
                }
                e.Handled = consumed;
                RenderAll();
                return;
            }

            // 场景3 - 两个编辑器都在选择模式，执行 HitTest
            // 屏蔽区域使用更大容差，使其在检测区域内部更容易被选中
            var inspectionHit = inspectionsEditor.HitTestRoi(pos, tolerance: 4.0);
            var maskHit = masksEditor.HitTestRoi(pos, tolerance: 8.0);

            // 屏蔽区域视觉上层优先命中
            if (maskHit != null)
            {
                masksEditor.ProcessMouseDown(pos);
                inspectionsEditor.DeselectAll();
                _activeEditor = masksEditor;
                e.Handled = true;
                RenderAll();
                return;
            }

            if (inspectionHit != null)
            {
                inspectionsEditor.ProcessMouseDown(pos);
                masksEditor.DeselectAll();
                _activeEditor = inspectionsEditor;
                e.Handled = true;
                RenderAll();
                return;
            }

            // 场景4 - 有选中区域的编辑器（手柄可能超出区域范围，需要额外处理）
            var editorWithSelection = GetEditorWithSelection();
            if (editorWithSelection != null)
            {
                editorWithSelection.ProcessMouseDown(pos);
                if (_activeEditor != editorWithSelection)
                    _activeEditor = editorWithSelection;
                e.Handled = true;
                RenderAll();
                return;
            }

            // 都没有选中区域，双方同时取消选择，允许平移
            inspectionsEditor.DeselectAll();
            masksEditor.DeselectAll();
            e.Handled = false;
            RenderAll();
        }

        private void OnCanvasMouseLeftButtonUp(object? sender, ImageMouseEventArgs e)
        {
            // ★ Phase 2: 只路由给正在交互的编辑器
            var interactingEditor = GetInteractingEditor();
            if (interactingEditor != null)
            {
                interactingEditor.ProcessMouseUp();
                RenderAll();
            }
        }

        private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Only process if any editor has active interaction
            if (!inspectionsEditor.IsAnyInteractionInProgress && !masksEditor.IsAnyInteractionInProgress)
                return;

            var interactingEditor = GetInteractingEditor();
            if (interactingEditor != null)
            {
                interactingEditor.ProcessMouseUp();
                RenderAll();
            }
        }

        private void OnWindowMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // ★ Phase 2: 只路由给正在绘制多边形的编辑器
            if (inspectionsEditor.ViewModel.IsDrawing)
                inspectionsEditor.ProcessRightButtonDown();
            else if (masksEditor.ViewModel.IsDrawing)
                masksEditor.ProcessRightButtonDown();
            RenderAll();
        }

        private void OnViewTransformed(object? sender, ViewTransformEventArgs e)
        {
            _viewportZoom = e.Zoom;

            // Update renderer zoom
            _renderer?.SetViewportTransform(_viewportZoom, new Point(0, 0));

            // Update editor zoom for handle rendering
            inspectionsEditor.CurrentZoom = _viewportZoom;
            masksEditor.CurrentZoom = _viewportZoom;

            RenderAll();
        }

        /// <summary>
        /// ★ Phase 4: 工具激活互斥处理
        /// 当某个编辑器的工具被激活时，强制另一个编辑器进入空闲状态，确保同一时间只有一个编辑器的工具处于激活状态
        /// </summary>
        private void OnEditorToolActivated(RoiEditorControl activatedEditor)
        {
            // 更新活跃编辑器引用（用于键盘快捷键路由）
            _activeEditor = activatedEditor;

            // 互斥逻辑：激活一个编辑器时，强制另一个进入空闲状态（无工具选中）
            if (activatedEditor == inspectionsEditor)
            {
                masksEditor.EnterIdleMode();
            }
            else
            {
                inspectionsEditor.EnterIdleMode();
            }
        }

        /// <summary>
        /// ★ Phase 3: 渲染节流 - 合并多个 RenderRequested 为一次 RenderAll
        /// </summary>
        private void OnEditorRenderRequested()
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
        /// 键盘事件统一拦截路由
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Delete: 路由给有选中区域的编辑器
            if (e.Key == Key.Delete)
            {
                var editor = GetEditorWithSelection();
                if (editor != null)
                {
                    editor.HandleDelete();
                    e.Handled = true;
                    RenderAll();
                }
                return;
            }

            // Ctrl+Z: 撤销 - 路由给有选中区域的编辑器
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var editor = GetEditorWithSelection();
                if (editor != null && editor.ViewModel.CanUndo)
                {
                    editor.ViewModel.Undo();
                    e.Handled = true;
                    RenderAll();
                }
                return;
            }

            // Ctrl+Y: 重做 - 路由给有选中区域的编辑器
            if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var editor = GetEditorWithSelection();
                if (editor != null && editor.ViewModel.CanRedo)
                {
                    editor.ViewModel.Redo();
                    e.Handled = true;
                    RenderAll();
                }
                return;
            }

            // Escape: 路由给正在交互的编辑器；无交互时取消双方选中
            if (e.Key == Key.Escape)
            {
                var interactingEditor = GetInteractingEditor();
                if (interactingEditor != null)
                {
                    interactingEditor.HandleEscape();
                    e.Handled = true;
                    RenderAll();
                }
                else
                {
                    inspectionsEditor.DeselectAll();
                    masksEditor.DeselectAll();
                    e.Handled = true;
                    RenderAll();
                }
                return;
            }

            // 删除/撤销/重做/Escape 以外的快捷键（V/R/C/O/L/P）不处理，由用户通过 UI 工具栏操作
        }

        #endregion

        #region Unified Rendering

        /// <summary>
        /// Combine both editors' render contexts and render with single WpfShapeRenderer
        /// </summary>
        private void RenderAll()
        {
            if (_renderer == null || _mainImageControl == null)
                return;

            var allContexts = new System.Collections.Generic.List<RoiRenderContext>();

            // Add inspection region contexts
            allContexts.AddRange(inspectionsEditor.GetRenderContexts());

            // Add mask region contexts
            allContexts.AddRange(masksEditor.GetRenderContexts());

            // Add preview context from whichever editor is drawing
            var previewCtx = inspectionsEditor.GetPreviewContext();
            if (previewCtx != null)
                allContexts.Add(previewCtx);

            previewCtx = masksEditor.GetPreviewContext();
            if (previewCtx != null)
                allContexts.Add(previewCtx);

            // Render all regions
            _renderer.Render(allContexts, new RoiRenderOptions { ShowLabels = true, ShowHandles = false });

            // ── Always clear stale handles first ──
            Rendering.HandleRenderer.ClearHandles(_mainImageControl.InteractionOverlayCanvasControl);

            // ── Draw handles for selected regions ──
            // ★ 只隐藏绘制新形状时的手柄，拖动/编辑手柄时保持可见（恢复上版行为）
            // ★ 订阅模式编辑器不画本地手柄（叠加层已被 GetRenderContexts 模式过滤清空）
            if (inspectionsEditor.ViewModel.CurrentMode != RoiSourceMode.Subscribe
                && inspectionsEditor.GetSelectedRoi() != null && !inspectionsEditor.IsDrawingInteraction)
                inspectionsEditor.DrawEditHandles(inspectionsEditor.GetSelectedRoi()!);
            if (masksEditor.ViewModel.CurrentMode != RoiSourceMode.Subscribe
                && masksEditor.GetSelectedRoi() != null && !masksEditor.IsDrawingInteraction)
                masksEditor.DrawEditHandles(masksEditor.GetSelectedRoi()!);
        }

        #endregion
    }
}
