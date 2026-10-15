using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 双测量元素编辑器（几何测量工具容器，v4 C2）。
    /// UI = 表格单行三列「卡尺 | 测量工具栏 | 测量工具栏」；
    /// 每个测量工具栏 = 独立完整 MeasureToolBar（图标按钮组），工具用 ConfigureElement 限定
    /// 只显示所需类型（如线-线仅"直线"可见），工具栏 = 该元素画线入口。
    /// 编辑器本体（MeasureEditorControl）作为隐形宿主（Collapsed）承载画布交互/几何/事件；
    /// 参数入口 = 下方唯一"当前卡尺 · 引导参数"区（MeasureGuideParameterPanel），跟随激活元素。
    /// 不改 MeasureEditorControl / MeasureToolBar 本体，既有 LineDetect 等零影响。
    /// </summary>
    public partial class MeasureElementsEditorControl : UserControl
    {
        private ImageControl? _mainImageControl;
        public bool HasImageControl => _mainImageControl != null;
        public System.Windows.Media.Imaging.BitmapSource? DisplayedImage => _mainImageControl?.SourceImage;
        public event EventHandler? DisplayedImageChanged;
        private double _viewportZoom = 1.0;
        private int _activeElementIndex = -1;

        // 画布引导角标（卡尺1蓝① / 卡尺2橙②）：不动共享编辑器渲染，仅容器叠加区分元素
        private System.Windows.Controls.TextBlock? _badge1;
        private System.Windows.Controls.TextBlock? _badge2;

        public MeasureElementsEditorControl()
        {
            InitializeComponent();

            // 设计器同样必须加载控件的 XAML。此前在这里直接 return，导致工具栏、
            // 引导参数区和两个编辑器宿主均未创建，所有使用本控件的测量工具在设计期
            // 只显示一块空白区域。设计期只跳过运行时交互（事件、主图像和叠加层）。
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            // 编辑器选中状态 → 激活（驱动参数区跟随）
            Element1Editor.SelectionChanged += (_, selected) =>
            {
                if (selected) ActivateElement(0);
                else if (_activeElementIndex == 0)
                {
                    ActiveElementIndex = -1;
                    HideGuideParams();
                }
            };
            Element2Editor.SelectionChanged += (_, selected) =>
            {
                if (selected) ActivateElement(1);
                else if (_activeElementIndex == 1)
                {
                    ActiveElementIndex = -1;
                    HideGuideParams();
                }
            };

            // 测量工具栏 = 该元素画线入口（选择类型 → 进入绘制）
            Element1ToolBarControl.MeasureSelectionChanged += (_, type) =>
            {
                if (type == null) return;
                ActivateElement(0);
                Element1Editor.StartDraw(type.Value);
            };
            Element2ToolBarControl.MeasureSelectionChanged += (_, type) =>
            {
                if (type == null) return;
                ActivateElement(1);
                Element2Editor.StartDraw(type.Value);
            };

            // 默认全部类型可见（工具打开后再 ConfigureElement 限定）
            RebindToolBar(Element1ToolBarControl, Element1Editor, DefaultTypes);
            RebindToolBar(Element2ToolBarControl, Element2Editor, DefaultTypes);

            // 参数面板初始指向卡尺1（默认 Collapsed：无引导时与直线查找一致不出现）
            GuideParamsPanel.Editor = Element1Editor;
            GuideParamsExpander.Visibility = Visibility.Collapsed;
        }

        private static readonly MeasureGeometryType[] DefaultTypes =
        {
            MeasureGeometryType.Line, MeasureGeometryType.Circle, MeasureGeometryType.Arc,
        };

        #region Dependency Properties

        /// <summary>当前激活元素索引：0=元素1 / 1=元素2 / -1=无</summary>
        public static readonly DependencyProperty ActiveElementIndexProperty =
            DependencyProperty.Register(nameof(ActiveElementIndex), typeof(int),
                typeof(MeasureElementsEditorControl), new PropertyMetadata(-1));

        public int ActiveElementIndex
        {
            get => (int)GetValue(ActiveElementIndexProperty);
            private set => SetValue(ActiveElementIndexProperty, value);
        }

        /// <summary>元素 1 工具栏是否可见。工具在 XAML 中声明，设计期与运行期共用。</summary>
        public static readonly DependencyProperty Element1VisibleProperty =
            DependencyProperty.Register(nameof(Element1Visible), typeof(bool),
                typeof(MeasureElementsEditorControl), new PropertyMetadata(true, OnElementProfileChanged));

        public bool Element1Visible
        {
            get => (bool)GetValue(Element1VisibleProperty);
            set => SetValue(Element1VisibleProperty, value);
        }

        /// <summary>元素 2 工具栏是否可见。工具在 XAML 中声明，设计期与运行期共用。</summary>
        public static readonly DependencyProperty Element2VisibleProperty =
            DependencyProperty.Register(nameof(Element2Visible), typeof(bool),
                typeof(MeasureElementsEditorControl), new PropertyMetadata(true, OnElementProfileChanged));

        public bool Element2Visible
        {
            get => (bool)GetValue(Element2VisibleProperty);
            set => SetValue(Element2VisibleProperty, value);
        }

        /// <summary>元素 1 的唯一允许几何类型；null 表示显示全部默认类型。</summary>
        public static readonly DependencyProperty Element1GeometryTypeProperty =
            DependencyProperty.Register(nameof(Element1GeometryType), typeof(MeasureGeometryType?),
                typeof(MeasureElementsEditorControl), new PropertyMetadata(null, OnElementProfileChanged));

        public MeasureGeometryType? Element1GeometryType
        {
            get => (MeasureGeometryType?)GetValue(Element1GeometryTypeProperty);
            set => SetValue(Element1GeometryTypeProperty, value);
        }

        /// <summary>元素 2 的唯一允许几何类型；null 表示显示全部默认类型。</summary>
        public static readonly DependencyProperty Element2GeometryTypeProperty =
            DependencyProperty.Register(nameof(Element2GeometryType), typeof(MeasureGeometryType?),
                typeof(MeasureElementsEditorControl), new PropertyMetadata(null, OnElementProfileChanged));

        public MeasureGeometryType? Element2GeometryType
        {
            get => (MeasureGeometryType?)GetValue(Element2GeometryTypeProperty);
            set => SetValue(Element2GeometryTypeProperty, value);
        }

        private static void OnElementProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((MeasureElementsEditorControl)d).ApplyElementProfile();

        private void ApplyElementProfile()
        {
            SetElementSlotVisible(0, Element1Visible);
            SetElementSlotVisible(1, Element2Visible);

            if (Element1GeometryType is { } element1Type)
                ConfigureElement(0, element1Type);
            else
                RebindToolBar(Element1ToolBarControl, Element1Editor, DefaultTypes);

            if (Element2GeometryType is { } element2Type)
                ConfigureElement(1, element2Type);
            else
                RebindToolBar(Element2ToolBarControl, Element2Editor, DefaultTypes);
        }

        #endregion

        #region 元素转发（宿主直接操作编辑器 / 工具栏）

        /// <summary>元素1 编辑器宿主（画布交互/几何/事件）</summary>
        public MeasureEditorControl Element1 => Element1Editor;

        /// <summary>元素2 编辑器宿主</summary>
        public MeasureEditorControl Element2 => Element2Editor;

        /// <summary>
        /// 限定元素类型：编辑器宿主 + 测量工具栏只显示 allowed 类型。
        /// allowed 空 = 直线/圆/圆弧全可用。
        /// </summary>
        public void ConfigureElement(int index, params MeasureGeometryType[] allowed)
        {
            if (index < 0 || index > 1) return;
            var types = allowed is { Length: > 0 } ? allowed : DefaultTypes;

            var editor = index == 0 ? Element1Editor : Element2Editor;
            var toolbar = index == 0 ? Element1ToolBarControl : Element2ToolBarControl;

            editor.SetAllowedGeometryTypes(types);
            editor.LockGeometryType(types[0]);
            RebindToolBar(toolbar, editor, types);
        }

        /// <summary>
        /// 显示/隐藏指定元素槽位的工具栏。
        /// 用于"点元素仅参数订阅"的工具（如点-线/点-圆/点-点）：
        /// 槽位1 = 点（参数订阅，无引导入口，工具栏隐藏），
        /// 槽位2 = 线/圆（保留引导）。
        /// 隐藏后该元素无可触发绘制的按钮，事件路由也不会消费（编辑器 LockGeometryType
        /// 仍生效，但工具栏全部不可见 → 用户无法切换交互状态）。
        /// index 0 = 元素1，1 = 元素2。
        /// </summary>
        public void SetElementSlotVisible(int index, bool visible)
        {
            var toolbar = index == 0 ? Element1ToolBarControl : Element2ToolBarControl;
            if (toolbar == null) return;
            toolbar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>测量工具栏只展示 allowed 类型项（从编辑器卡尺工具项过滤，同一项对象复用）</summary>
        private static void RebindToolBar(MeasureToolBar toolbar, MeasureEditorControl editor, MeasureGeometryType[] allowed)
        {
            var filtered = new ObservableCollection<MeasureToolItem>(
                editor.CaliberTools.Where(i => allowed.Contains(i.GeometryType)));
            toolbar.MeasureTools = filtered;
        }

        /// <summary>清除元素测量工具栏的选中态（绘制完成后调用，回到选择模式）</summary>
        public void ClearElementToolSelection(int index)
        {
            if (index == 0) Element1ToolBarControl.ClearSelection();
            else if (index == 1) Element2ToolBarControl.ClearSelection();
        }

        /// <summary>设置位置修正矩阵（两个编辑器宿主同源修正）</summary>
        public void SetPositionCorrectionMatrix(Execution.Results.AffineMatrix? matrix)
        {
            Element1Editor.SetPositionCorrectionMatrix(matrix);
            Element2Editor.SetPositionCorrectionMatrix(matrix);
            // 矩阵变化立即刷新角标（UpdateBadge 需按修正后的显示坐标重锚）
            RenderAll();
        }

        #endregion

        #region 生命周期：绑定 / 解绑

        public void SetMainImageControl(ImageControl? imageControl)
        {
            if (_mainImageControl == imageControl)
                return;

            CancelActiveEdits();

            if (_mainImageControl != null)
            {
                UnsubscribeEvents();
            }

            _mainImageControl = imageControl;

            if (_mainImageControl != null)
            {
                _mainImageControl.OverlayHitTestVisible = false;

                var overlayCanvas = _mainImageControl.InteractionOverlayCanvasControl;
                Element1Editor.SetOverlayCanvas(overlayCanvas);
                Element2Editor.SetOverlayCanvas(overlayCanvas);

                EnsureBadges(overlayCanvas);

                _viewportZoom = _mainImageControl.Zoom;
                Element1Editor.SetZoom(_viewportZoom);
                Element2Editor.SetZoom(_viewportZoom);

                SubscribeEvents();

                // 首次绑定即渲染一次：若两个子编辑器已有引导（宿主参数恢复），
                // 角标/测量带应立即可见，无需等待鼠标进入图像区才刷新。
                RenderAll();

                PluginLogger.Info("[MeasureElementsEditor] ImageControl set with centralized routing", "MeasureElementsEditor");
            }
        }

        /// <summary>宿主在恢复/变更引导或卡尺数据后调用：立即重画测量带与 ①② 角标（含首次展示）。</summary>
        public void RefreshOverlay() => RenderAll();

        public void ClearMainOverlay()
        {
            try
            {
                CancelActiveEdits();
                UnsubscribeEvents();

                if (_mainImageControl?.InteractionOverlayCanvasControl != null)
                {
                    _mainImageControl.OverlayHitTestVisible = false;
                    _mainImageControl.InteractionOverlayCanvasControl.Children.Clear();
                }

                Element1Editor.SetOverlayCanvas(null);
                Element2Editor.SetOverlayCanvas(null);

                RemoveBadges(_mainImageControl?.InteractionOverlayCanvasControl);
                _mainImageControl = null;

                PluginLogger.Info("[MeasureElementsEditor] Overlay cleared and events unsubscribed", "MeasureElementsEditor");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("[MeasureElementsEditor] ClearMainOverlay failed: " + ex.Message, exception: ex);
            }
        }

        #endregion

        #region 事件订阅

        private void SubscribeEvents()
        {
            if (_mainImageControl == null) return;
            DependencyPropertyDescriptor.FromProperty(ImageControl.SourceImageProperty, typeof(ImageControl))
                .AddValueChanged(_mainImageControl, OnDisplayedImageChanged);
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
            DependencyPropertyDescriptor.FromProperty(ImageControl.SourceImageProperty, typeof(ImageControl))
                .RemoveValueChanged(_mainImageControl, OnDisplayedImageChanged);
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

        #region 激活互斥

        /// <summary>Discard unfinished edits before changing image or binding context.</summary>
        public void CancelActiveEdits()
        {
            Element1Editor.CancelDrawing();
            Element2Editor.CancelDrawing();
        }

        private void OnDisplayedImageChanged(object? sender, EventArgs e)
        {
            CancelActiveEdits();
            DisplayedImageChanged?.Invoke(this, EventArgs.Empty);
            RenderAll();
        }

        private void ActivateElement(int index)
        {
            _activeElementIndex = index;
            ActiveElementIndex = index;
            if (index == 0)
            {
                Element2Editor.CancelDrawing();
                BindGuideParamsPanel(0);
            }
            else
            {
                Element1Editor.CancelDrawing();
                BindGuideParamsPanel(1);
            }
        }

        private void BindGuideParamsPanel(int index)
        {
            if (GuideParamsPanel == null || GuideParamsExpander == null) return;
            var editor = index == 0 ? Element1Editor : Element2Editor;
            if (GuideParamsPanel.Editor != editor)
                GuideParamsPanel.Editor = editor;
            else
                GuideParamsPanel.RefreshFromEditor();

            // 对齐直线查找：已有引导才显示参数折叠框（无引导=Collapsed）
            GuideParamsExpander.Visibility = editor.HasGuide
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void HideGuideParams()
        {
            if (GuideParamsExpander != null)
                GuideParamsExpander.Visibility = Visibility.Collapsed;
        }

        private MeasureEditorControl? GetInteractingEditor()
        {
            if (Element1Editor.IsAnyInteractionInProgress) return Element1Editor;
            if (Element2Editor.IsAnyInteractionInProgress) return Element2Editor;
            return null;
        }

        #endregion

        #region 事件路由（绘制模式优先 → 交互中 → 命中1 → 命中2 → 空白取消）

        private Cursor? QueryEditorCursor(Point pos)
        {
            if (Element1Editor.IsDrawingMode || Element1Editor.IsAnyInteractionInProgress)
                return Element1Editor.QueryCursor(pos);
            if (Element2Editor.IsDrawingMode || Element2Editor.IsAnyInteractionInProgress)
                return Element2Editor.QueryCursor(pos);

            var c1 = Element1Editor.QueryCursor(pos);
            return c1 ?? Element2Editor.QueryCursor(pos);
        }

        private void OnImageMouseMove(object? sender, ImageMouseEventArgs e)
        {
            var pos = e.ImagePosition;
            var interacting = GetInteractingEditor();
            if (interacting != null)
                interacting.ProcessMouseMove(pos);
            else
                Element1Editor.ProcessMouseMove(pos);

            RenderAll();
            _mainImageControl?.SetHoverCursor(QueryEditorCursor(pos));
        }

        private void OnCanvasMouseLeftButtonDown(object? sender, ImageMouseEventArgs e)
        {
            if (_mainImageControl == null) return;
            var pos = e.ImagePosition;

            var interacting = GetInteractingEditor();
            if (interacting != null)
            {
                var consumed = interacting.ProcessMouseDown(pos);
                e.Handled = consumed;
                RenderAll();
                return;
            }

            if (Element1Editor.IsDrawingMode)
            {
                var consumed = Element1Editor.ProcessMouseDown(pos);
                if (consumed) ActivateElement(0);
                e.Handled = consumed;
                RenderAll();
                return;
            }
            if (Element2Editor.IsDrawingMode)
            {
                var consumed = Element2Editor.ProcessMouseDown(pos);
                if (consumed) ActivateElement(1);
                e.Handled = consumed;
                RenderAll();
                return;
            }

            if (Element1Editor.ProcessMouseDown(pos))
            {
                ActivateElement(0);
                e.Handled = true;
                RenderAll();
                return;
            }

            if (Element2Editor.ProcessMouseDown(pos))
            {
                ActivateElement(1);
                e.Handled = true;
                RenderAll();
                return;
            }

            Element1Editor.CancelDrawing();
            Element2Editor.CancelDrawing();
            ActiveElementIndex = -1;
            HideGuideParams();
            e.Handled = false;
            RenderAll();
        }

        private void OnCanvasMouseLeftButtonUp(object? sender, ImageMouseEventArgs e)
        {
            var interacting = GetInteractingEditor();
            interacting?.ProcessMouseUp();
            if (interacting != null) RenderAll();
        }

        private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var interacting = GetInteractingEditor();
            if (interacting == null) return;
            interacting.ProcessMouseUp();
            RenderAll();
        }

        #endregion

        #region 缩放同步

        private void OnViewTransformed(object? sender, ViewTransformEventArgs e)
        {
            _viewportZoom = e.Zoom;
            Element1Editor.SetZoom(_viewportZoom);
            Element2Editor.SetZoom(_viewportZoom);
            RenderAll();
        }

        #endregion

        #region 键盘路由

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;

            if (Element1Editor.IsGuideSelected)
            {
                Element1Editor.HandleDelete();
                e.Handled = true;
                RenderAll();
            }
            else if (Element2Editor.IsGuideSelected)
            {
                Element2Editor.HandleDelete();
                e.Handled = true;
                RenderAll();
            }
        }

        #endregion

        #region 渲染

        private void RenderAll()
        {
            if (_mainImageControl == null) return;
            var overlay = _mainImageControl.InteractionOverlayCanvasControl;

            // ★ 渲染前先清空共享手柄（Tag=EditHandle），对齐 MeasurementEditorManagerControl.RenderAll：
            //   删除引导/取消选中后由本拍兜底清残留，再让激活方重画自己的手柄。
            RoiEditor.Rendering.HandleRenderer.ClearHandles(overlay);

            Element1Editor.RenderForManager();
            Element2Editor.RenderForManager();
            UpdateBadges();
        }

        #endregion

        #region 画布引导角标（卡尺1①蓝 / 卡尺2②橙，跟随引导移动）

        private void EnsureBadges(Canvas overlay)
        {
            if (overlay == null || _badge1 != null) return;
            _badge1 = CreateBadge("①", SunEyeVision.Plugin.SDK.UI.Constants.ColorConstants.InspectionBlueBrush);
            _badge2 = CreateBadge("②", SunEyeVision.Plugin.SDK.UI.Constants.ColorConstants.MaskOrangeBrush);
            overlay.Children.Add(_badge1);
            overlay.Children.Add(_badge2);
        }

        private void RemoveBadges(Canvas? overlay)
        {
            if (overlay != null)
            {
                if (_badge1 != null) overlay.Children.Remove(_badge1);
                if (_badge2 != null) overlay.Children.Remove(_badge2);
            }
            _badge1 = null;
            _badge2 = null;
        }

        private static TextBlock CreateBadge(string text, System.Windows.Media.Brush foreground)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
            Canvas.SetZIndex(tb, 300);
            return tb;
        }

        /// <summary>角标锚定到引导线中点旁；无引导/非直线引导时隐藏</summary>
        private void UpdateBadges()
        {
            if (_badge1 == null) return;
            UpdateBadge(_badge1, Element1Editor);
            UpdateBadge(_badge2!, Element2Editor);
        }

        private static void UpdateBadge(TextBlock badge, MeasureEditorControl editor)
        {
            if (!editor.HasGuide || editor.GuideGeometryType != MeasureGeometryType.Line)
            {
                badge.Visibility = Visibility.Collapsed;
                return;
            }
            // 位置修正：InteractionModel 存参考系坐标，而 overlay 是当前图像（显示）坐标，
            // 角标需先把直线端点经修正矩阵变换为显示坐标，再求方向与偏移，否则修正后角标脱离起点。
            var start = editor.ReferenceToDisplay(editor.InteractionModel.LineStartPoint);
            var end = editor.ReferenceToDisplay(editor.InteractionModel.LineEndPoint);
            var len = (end - start).Length;
            if (len < 1e-6)
            {
                badge.Visibility = Visibility.Collapsed;
                return;
            }
            // 锚在 P1 沿引导线【反向延长线】外：测量带/主卡尺从 P1 沿引导线正向生长，
            // 永不越过 P1 反向延伸 → 角标放起点之前必然不与卡尺/手柄重叠。
            var dir = (end - start) / len;
            const double off = 24;   // 反向延长距离（起点稍前，避免压到卡尺/字体）
            Canvas.SetLeft(badge, start.X - dir.X * off - 8);
            Canvas.SetTop(badge, start.Y - dir.Y * off - 22);
            badge.Visibility = Visibility.Visible;
        }

        #endregion
    }
}
