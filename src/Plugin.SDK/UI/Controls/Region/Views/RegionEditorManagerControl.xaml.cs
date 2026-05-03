using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    /// <summary>
    /// Unified region editor manager control.
    /// Centralizes event routing and rendering for two RegionEditorControl instances.
    /// Single WpfShapeRenderer and single event subscription set.
    /// </summary>
    public partial class RegionEditorManagerControl : UserControl
    {
        private ImageControl? _mainImageControl;
        private WpfShapeRenderer? _renderer;

        // Viewport zoom tracking
        private double _viewportZoom = 1.0;

        // ★ Phase 3: 渲染节流
        private bool _renderScheduled = false;

        // ★ Phase 4: 当前激活工具的编辑器引用（用于键盘快捷键路由和分组互斥）
        private RegionEditorControl? _activeEditor;

        public RegionEditorManagerControl()
        {
            // 设计时完全跳过初始化，避免 XAML 解析触发 NullReferenceException
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            InitializeComponent();

            // Subscribe to child editors' events
            inspectionsEditor.RenderRequested += OnEditorRenderRequested;
            masksEditor.RenderRequested += OnEditorRenderRequested;

            // ★ Phase 4: 订阅工具激活事件（分组互斥控制）
            inspectionsEditor.ToolActivated += () => OnEditorToolActivated(inspectionsEditor);
            masksEditor.ToolActivated += () => OnEditorToolActivated(masksEditor);
        }

        #region Dependency Properties

        public static readonly DependencyProperty InspectionRegionsSourceProperty =
            DependencyProperty.Register(nameof(InspectionRegionsSource),
                typeof(ObservableCollection<RegionData>),
                typeof(RegionEditorManagerControl),
                new PropertyMetadata(null, OnInspectionSourceChanged));

        public ObservableCollection<RegionData>? InspectionRegionsSource
        {
            get => (ObservableCollection<RegionData>?)GetValue(InspectionRegionsSourceProperty);
            set => SetValue(InspectionRegionsSourceProperty, value);
        }

        public static readonly DependencyProperty MaskRegionsSourceProperty =
            DependencyProperty.Register(nameof(MaskRegionsSource),
                typeof(ObservableCollection<RegionData>),
                typeof(RegionEditorManagerControl),
                new PropertyMetadata(null, OnMaskSourceChanged));

        public ObservableCollection<RegionData>? MaskRegionsSource
        {
            get => (ObservableCollection<RegionData>?)GetValue(MaskRegionsSourceProperty);
            set => SetValue(MaskRegionsSourceProperty, value);
        }

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources),
                typeof(ObservableCollection<AvailableDataSource>),
                typeof(RegionEditorManagerControl),
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
            if (d is RegionEditorManagerControl control && e.NewValue is ObservableCollection<RegionData> source)
            {
                control.inspectionsEditor?.SetRegionsSource(source);
                PluginLogger.Info("[RegionEditorManager] Inspection region collection injected | Count=" + source.Count, "RegionEditorManager");
            }
        }

        private static void OnMaskSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorManagerControl control && e.NewValue is ObservableCollection<RegionData> source)
            {
                control.masksEditor?.SetRegionsSource(source);
                PluginLogger.Info("[RegionEditorManager] Mask region collection injected | Count=" + source.Count, "RegionEditorManager");
            }
        }

        private static void OnAvailableDataSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorManagerControl control && e.NewValue is ObservableCollection<AvailableDataSource> sources)
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

                PluginLogger.Info("[RegionEditorManager] ImageControl set with centralized event routing", "RegionEditorManager");
            }
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

                PluginLogger.Info("[RegionEditorManager] Overlay cleared and events unsubscribed", "RegionEditorManager");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("[RegionEditorManager] ClearMainOverlay failed: " + ex.Message, exception: ex);
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
            _mainImageControl.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void UnsubscribeImageControlEvents()
        {
            if (_mainImageControl == null) return;

            _mainImageControl.ImageMouseMove -= OnImageMouseMove;
            _mainImageControl.CanvasMouseLeftButtonDown -= OnCanvasMouseLeftButtonDown;
            _mainImageControl.CanvasMouseLeftButtonUp -= OnCanvasMouseLeftButtonUp;
            _mainImageControl.ViewTransformed -= OnViewTransformed;
            _mainImageControl.PreviewKeyDown -= OnPreviewKeyDown;
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
            }
        }

        #endregion

        #region Event Handlers (Single Subscription)

        /// <summary>
        /// 获取当前正在交互的编辑器（绘制/拖拽/拖拽手柄）
        /// </summary>
        private RegionEditorControl? GetInteractingEditor()
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
        private RegionEditorControl? GetEditorWithSelection()
        {
            if (inspectionsEditor.GetSelectedRegion() != null)
                return inspectionsEditor;
            if (masksEditor.GetSelectedRegion() != null)
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

            // 无交互时不需要路由 MouseMove

            // ProcessMouseMove 内部已移除 RenderRequested，只在这里统一渲染一次
            RenderAll();
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

            // 场景2 - 选择模式，根据 hit-test 路由（带选择模式过滤）
            var inspectionHit = inspectionsEditor.HitTestRegion(pos);
            var maskHit = masksEditor.HitTestRegion(pos);

            // 判断每个编辑器是否处于选择模式（非绘制且当前工具为 Point）
            bool inspectionSelectMode = !inspectionsEditor.ViewModel.IsDrawing
                                        && inspectionsEditor.CurrentShapeTool == ShapeType.Point;
            bool maskSelectMode = !masksEditor.ViewModel.IsDrawing
                                  && masksEditor.CurrentShapeTool == ShapeType.Point;

            // 仅在编辑器处于选择模式时，才响应对应类型的区域命中
            if (inspectionSelectMode && inspectionHit != null)
            {
                inspectionsEditor.ProcessMouseDown(pos);
                masksEditor.DeselectAll();
                e.Handled = true;
                RenderAll();
                return;
            }

            if (maskSelectMode && maskHit != null)
            {
                masksEditor.ProcessMouseDown(pos);
                inspectionsEditor.DeselectAll();
                e.Handled = true;
                RenderAll();
                return;
            }

            // 如果处于选择模式但点了其他类型的区域，不路由该事件，
            // 让后续的空白点击逻辑取消当前选择

            // 场景3 - 空白点击：先检查是否有编辑器选中了绘制工具
            if (inspectionsEditor.ViewModel.IsDrawing)
            {
                inspectionsEditor.ProcessMouseDown(pos);
                // ★ Phase 4: 同步更新活跃编辑器引用并互斥
                if (_activeEditor != inspectionsEditor)
                {
                    _activeEditor = inspectionsEditor;
                    masksEditor.EnterIdleMode();
                }
            }
            else if (masksEditor.ViewModel.IsDrawing)
            {
                masksEditor.ProcessMouseDown(pos);
                // ★ Phase 4: 同步更新活跃编辑器引用并互斥
                if (_activeEditor != masksEditor)
                {
                    _activeEditor = masksEditor;
                    inspectionsEditor.EnterIdleMode();
                }
            }
            else
            {
                // 两个都没有 active，双方同时取消选择
                inspectionsEditor.DeselectAll();
                masksEditor.DeselectAll();
            }
            e.Handled = true;
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
        private void OnEditorToolActivated(RegionEditorControl activatedEditor)
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

            var allContexts = new System.Collections.Generic.List<RegionRenderContext>();

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
            _renderer.Render(allContexts, new RegionRenderOptions { ShowLabels = true, ShowHandles = false });

            // ── Always clear stale handles first ──
            Rendering.HandleRenderer.ClearHandles(_mainImageControl.InteractionOverlayCanvasControl);

            // ── Draw handles for selected regions ──
            // 检查每个编辑器，有选中区域画手柄
            if (inspectionsEditor.GetSelectedRegion() != null && !inspectionsEditor.IsAnyInteractionInProgress)
                inspectionsEditor.DrawEditHandles(inspectionsEditor.GetSelectedRegion()!);
            else if (masksEditor.GetSelectedRegion() != null && !masksEditor.IsAnyInteractionInProgress)
                masksEditor.DrawEditHandles(masksEditor.GetSelectedRegion()!);
        }

        #endregion
    }
}
