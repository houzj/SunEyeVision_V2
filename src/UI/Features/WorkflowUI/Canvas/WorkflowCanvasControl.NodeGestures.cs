using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

public partial class WorkflowCanvasControl
{
    private const string CanvasNodeDragFormat = "SunEyeVision.Canvas.NodeMove.Token";
    private sealed class NodeDragSession
    {
        public Rect FrameRect { get; init; }
        public required object Owner { get; init; }
        public required WorkflowTabViewModel Tab { get; init; }
        public required Point Pointer { get; init; }
        public required Dictionary<CanvasNodeProjection, Point> Before { get; init; }
        public required string Token { get; init; }
        public required string Stamp { get; init; }
        public string? ConfigurationStamp { get; init; }
        public Dictionary<CanvasNodeProjection, Point> After { get; } = new();
        public List<FrameworkElement> Ghosts { get; } = new();
        public List<System.Windows.Shapes.Line> AlignLines { get; } = new();
        /// <summary>节点 →（预览快照, 相对中心的偏移）。快照只在拖动开始时建一次，之后只改坐标。</summary>
        public Dictionary<CanvasNodeProjection, (Rectangle Ghost, double Dx, double Dy)> GhostMap { get; } = new();
        /// <summary>预览根容器：装全部节点快照，整体平移只改容器坐标（原件 AdornerPanel 同构）。</summary>
        public DragPreviewAdorner? Preview { get; set; }
        /// <summary>上次吸附落点。吸附后位置没变就整帧跳过，避免每帧重排预览与对齐线。</summary>
        public long LastSnapKey { get; set; } = long.MinValue;
        public bool Copy { get; set; }
    }
    private NodeDragSession? _canvasNodeMove;
    private bool _canvasNodeOleActive;
    private Window? _canvasGestureWindow;

    private static int CanvasIdentity(object? value) => value == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    private string CanvasGraphStamp(WorkflowTabViewModel tab) => $"{tab.CurrentWorkflowId}:{CanvasIdentity(tab.DocumentCanvas)}|"+string.Join(";",
        tab.DocumentCanvas?.Nodes.Select(n => $"{n.Id}:{CanvasIdentity(n.Definition)}:{n.PositionX:R},{n.PositionY:R}:{n.IsEnabled}:{n.Name}:{n.ToolType}:{n.SubroutineId}") ?? Array.Empty<string>()) + "|" +
        string.Join(";", tab.DocumentCanvas?.Connections.Select(c => $"{c.Id}:{CanvasIdentity(c.Definition)}:{c.SourceNodeId}:{c.SourcePort}:{c.TargetNodeId}:{c.TargetPort}") ?? Array.Empty<string>());
    private bool ValidCanvasNodeMove(NodeDragSession session) => CanEditCanvas &&
        ReferenceEquals(DataContext, session.Owner) && ReferenceEquals(_viewModel?.WorkflowTabViewModel.SelectedTab, session.Tab) &&
        CanvasGraphStamp(session.Tab) == session.Stamp;

    private void InitializeCanvasNodeGestures()
    {
        WorkflowCanvas.Focusable = true;
        WorkflowCanvas.AllowDrop = true;
        WorkflowCanvas.PreviewDragOver += CanvasNodeDragOver;
        WorkflowCanvas.PreviewDrop += CanvasNodeDrop;
        WorkflowCanvas.DragLeave += (_, e) => { if (_canvasNodeOleActive && !CanvasDropInside(e.GetPosition(WorkflowCanvas))) HideCanvasNodeGhosts(); };
        WorkflowCanvas.LostMouseCapture += (_, _) => { if (!_canvasNodeOleActive) CancelCanvasGesture(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !CanvasTextInputOwnsKeyboard()) { CancelCanvasGesture(); ClearCanvasWireSelection(); e.Handled = true; } };
        DataContextChanged += (_, _) => CancelCanvasGesture();
        Loaded += (_, _) =>
        {
            if (_canvasGestureWindow != null) _canvasGestureWindow.Deactivated -= CanvasGestureDeactivated;
            _canvasGestureWindow = Window.GetWindow(this);
            if (_canvasGestureWindow != null) _canvasGestureWindow.Deactivated += CanvasGestureDeactivated;
        };
        Unloaded += (_, _) =>
        {
            CancelCanvasGesture();
            if (_canvasGestureWindow != null) _canvasGestureWindow.Deactivated -= CanvasGestureDeactivated;
            _canvasGestureWindow = null;
        };
    }
    private static bool CanvasTextInputOwnsKeyboard()
    {
        DependencyObject? focus = Keyboard.FocusedElement as DependencyObject;
        while (focus != null)
        {
            if (focus is System.Windows.Controls.Primitives.TextBoxBase || focus is ComboBox || focus is PasswordBox) return true;
            focus = focus is Visual || focus is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(focus) : LogicalTreeHelper.GetParent(focus);
        }
        return false;
    }
    private void CanvasGestureDeactivated(object? sender, EventArgs e)
    {
        if (!_canvasNodeOleActive) CancelCanvasGesture();
    }
    private void CancelCanvasGesture()
    {
        // 清状态先于释放捕获，防止 LostMouseCapture 重入。
        CancelCanvasNodeRename();
        CancelCanvasNodeMove();
        _isPanning = false; _isBoxSelecting = false; _isDraggingConnection = false;
        _dragConnectionSourceNode = null; _dragConnectionSourcePort = null;
        if (_dragConnectionSourceBorder != null) SetPortsVisibility(_dragConnectionSourceBorder, false);
        if (_highlightedTargetNodeBorder != null) SetPortsVisibility(_highlightedTargetNodeBorder, false);
        _dragConnectionSourceBorder = null; _highlightedTargetNodeBorder = null;
        _canvasConnectionTarget = null; _canvasConnectionTab = null; _canvasConnectionStamp = null; _canvasConnectionConfigurationStamp = null; _canvasConnectionStarted = false;
        foreach (var port in CanvasPorts(WorkflowCanvas)) port.IsHited = false;
        if (_tempConnectionLine != null) _tempConnectionLine.Visibility = Visibility.Collapsed;
        _tempConnectionGeometry?.Figures.Clear();
        SelectionBox?.EndSelection();
        Cursor = Cursors.Arrow;
        FinishCanvasWireDrag(false);
        if (Mouse.Captured is DependencyObject captured && (captured == WorkflowCanvas || WorkflowCanvas.IsAncestorOf(captured)))
            Mouse.Capture(null);
    }
    private void ArmCanvasNodeMove(CanvasNodeProjection node, Point pointer)
    {
        CancelCanvasNodeMove();
        var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
        if (tab == null || !CanEditCanvas) return;
        _canvasNodeMove = new NodeDragSession
        {
            Owner = DataContext, Tab = tab, Pointer = pointer, Token = Guid.NewGuid().ToString("N"),
            // 原件 DiagramPage_PreviewMouseMove：批量移动用 CurBoxOfItems（框选集合），无框选才退化为选中节点。
            Before = (_canvasBoxSelection.Length > 0 ? _canvasBoxSelection :
                    (tab.DocumentCanvas?.Nodes ?? throw new InvalidOperationException("画布会话未绑定。")).Where(n => n.IsSelected))
                .ToDictionary(n => n, n => new Point(n.PositionX, n.PositionY)),
            Stamp = CanvasGraphStamp(tab), ConfigurationStamp = CanvasConfigurationStamp(tab), FrameRect = NodeFrameRect(tab),
        };
    }
        /// <summary>选中节点包围框（含 10px 命中外扩），对应原件 RectDragPanalRect（ItemsPage.cs:604-609）。</summary>
        private static Rect NodeFrameRect(WorkflowTabViewModel tab)
        {
            var nodes = tab.DocumentCanvas?.Nodes?.Where(n => n.IsSelected).ToArray() ?? Array.Empty<CanvasNodeProjection>();
            if (nodes.Length == 0) return Rect.Empty;
            var left = nodes.Min(n => n.PositionX - n.NodeRect.Width / 2);
            var top = nodes.Min(n => n.PositionY - n.NodeRect.Height / 2);
            var right = nodes.Max(n => n.PositionX + n.NodeRect.Width / 2);
            var bottom = nodes.Max(n => n.PositionY + n.NodeRect.Height / 2);
            var rect = new Rect(left, top, right - left, bottom - top);
            rect.Inflate(10, 10);
            return rect;
        }
    private void StartCanvasNodeMove(MouseEventArgs e)
    {
        var session = _canvasNodeMove;
        if (session == null || !ValidCanvasNodeMove(session)) { CancelCanvasNodeMove(); return; }
        session.Copy = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        _canvasNodeOleActive = true;
        QueryContinueDragEventHandler query = (_, args) => { if (!ValidCanvasNodeMove(session)) args.Action = DragAction.Cancel; };
        WorkflowCanvas.QueryContinueDrag += query;
        try
        {
            DragDrop.DoDragDrop(WorkflowCanvas, new DataObject(CanvasNodeDragFormat, session.Token, false), session.Copy ? DragDropEffects.Copy : DragDropEffects.Move);
        }
        finally
        {
            WorkflowCanvas.QueryContinueDrag -= query;
            _canvasNodeOleActive = false;
            CancelCanvasNodeMove();
        }
        e.Handled = true;
    }
    private bool ReadCanvasNodeMove(DragEventArgs e, out NodeDragSession session)
    {
        session = _canvasNodeMove!;
        return session != null && e.Data.GetDataPresent(CanvasNodeDragFormat, false) &&
            e.Data.GetData(CanvasNodeDragFormat, false) is string token && token == session.Token;
    }
    private bool CanvasDropInside(Point point)
    {
        if (!new Rect(0, 0, WorkflowCanvas.ActualWidth, WorkflowCanvas.ActualHeight).Contains(point)) return false;
        var viewport = FindScrollViewer();
        return viewport == null || new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight)
            .Contains(WorkflowCanvas.TranslatePoint(point, viewport));
    }
    private void CanvasNodeDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CanvasNodeDragFormat, false)) return;
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!ReadCanvasNodeMove(e, out var session) || !ValidCanvasNodeMove(session) ||
            !CanvasNodeDropEffectAllowed(session.Copy, e.KeyStates, e.AllowedEffects, false) || !CanvasDropInside(e.GetPosition(WorkflowCanvas)))
        { HideCanvasNodeGhosts(); return; }
        e.Effects = session.Copy ? DragDropEffects.Copy : DragDropEffects.Move;
        PreviewCanvasNodeMove(session, e.GetPosition(WorkflowCanvas));
    }
    private void CanvasNodeDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CanvasNodeDragFormat, false)) return;
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!ReadCanvasNodeMove(e, out var session) || !ValidCanvasNodeMove(session) ||
            session.ConfigurationStamp == null || session.ConfigurationStamp != CanvasConfigurationStamp(session.Tab) ||
            !CanvasNodeDropEffectAllowed(session.Copy, e.KeyStates, e.AllowedEffects, true) || !CanvasDropInside(e.GetPosition(WorkflowCanvas))) return;
        PreviewCanvasNodeMove(session, e.GetPosition(WorkflowCanvas));
        if (session.After.Count == 1)
        {
            var single = session.After.Single();
            session.After[single.Key] = ResolveCanvasPlacement(single.Value, single.Key.NodeRect.Height);
        }
        var positions = session.After.Select(p => (Node: p.Key, Before: session.Before[p.Key], After: p.Value)).ToArray();
        var insertion = session.After.Count == 1 ? _canvasInsertionWire : null;
        if (session.Copy)
        {
            if (_viewModel?.DuplicateCanvasNodes(session.After)==true)e.Effects=DragDropEffects.Copy;
            return;
        }
        if (insertion != null && positions.Length == 1)
        {
            var canvas = session.Tab.DocumentCanvas;
            if (canvas == null) return;
            try { canvas.InsertNode(positions[0].Node, insertion, new SunEyeVision.Domain.Workflows.CanvasPoint(positions[0].After.X, positions[0].After.Y)); }
            catch (Exception error) { if (_viewModel != null) _viewModel.StatusText = "插线已取消：" + error.Message; return; }
        }
        else if (positions.Any(p => p.Before != p.After))
            session.Tab.DocumentCanvas?.MoveNodes(session.After.ToDictionary(item => item.Key,
                item => new SunEyeVision.Domain.Workflows.CanvasPoint(item.Value.X, item.Value.Y)));
        e.Effects = session.Copy && insertion == null ? DragDropEffects.Copy : DragDropEffects.Move;
    }
    private static bool CanvasNodeDropEffectAllowed(bool copy, DragDropKeyStates keys, DragDropEffects allowed, bool dropping)
    {
        var control = (keys & DragDropKeyStates.ControlKey) != 0;
        return copy == control && (!copy || !dropping || keys == DragDropKeyStates.ControlKey) &&
            (allowed & (copy ? DragDropEffects.Copy : DragDropEffects.Move)) != 0;
    }
    private void PreviewCanvasNodeMove(NodeDragSession session, Point pointer)
    {
        var delta = pointer - session.Pointer;
        if (session.Preview is null && Math.Abs(delta.X) <= 3 && Math.Abs(delta.Y) <= 3)
        { session.After.Clear(); return; }
        var left = session.Before.Min(p => p.Value.X - p.Key.NodeRect.Width / 2);
        var top = session.Before.Min(p => p.Value.Y - p.Key.NodeRect.Height / 2);
delta = new Vector(Math.Round((left + delta.X) / 5) * 5 - left, Math.Round((top + delta.Y) / 5) * 5 - top);
        // 原件 _dragOver 只在吸附后的位置变化时才挪预览；位置没变就整帧跳过，
        // 否则每帧重算 After、重绘对齐线、动视觉树，拖动会明显发飘/卡顿。
        var snapKey = ((long)Math.Round(left + delta.X) * 100000L) + (long)Math.Round(top + delta.Y);
        if (session.LastSnapKey == snapKey) return;
        session.LastSnapKey = snapKey;
        session.After.Clear();
        foreach (var pair in session.Before)
        {
            var node = pair.Key;
            var width = node.NodeRect.Width; var height = node.NodeRect.Height;
            session.After[node] = new Point(
                Math.Clamp(pair.Value.X + delta.X, width / 2, 5000 - width / 2),
                Math.Clamp(pair.Value.Y + delta.Y, height / 2, 5000 - height / 2));
        }

        EnsureCanvasNodePreview(session);
        if (session.Preview is not null)
        {
            // 原件 UIElementAdorner.SetOffsets：只改 TranslateTransform 并刷新 Adorner 层，不触碰画布布局。
            session.Preview.SetOffsets(delta.X, delta.Y);
        }
        if (session.After.Count == 1)
        {
            var single = session.After.Single();
            UpdateCanvasPlacementFeedback(single.Value, single.Key.NodeRect.Height, session.Before.Keys.Select(n => n.Id));
        }
        else ClearCanvasPlacementFeedback();
        UpdateCanvasInsertionWire(pointer, session.Before.Keys.Select(n => n.Id));
    }
    private static Border? FindCanvasNodeVisual(DependencyObject parent, CanvasNodeProjection node)
    {
        if (parent is Border border && border.Name == "HitAreaBorder" && ReferenceEquals(border.Tag, node)) return border;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var result = FindCanvasNodeVisual(VisualTreeHelper.GetChild(parent, i), node);
            if (result != null) return result;
        }
        return null;
    }
    private void HideCanvasNodeGhosts() { RemoveCanvasNodeGhosts(); DisposeCanvasPlacementFeedback(); ClearCanvasInsertionWire(); }
    /// <summary>建预览根容器（只建一次）。原件：VisualTreeHelper.GetDescendantBounds + 0.7 不拉伸 VisualBrush。</summary>
    /// <summary>建拖动预览（只建一次）：挂在 Adorner 层，内容为各节点的 0.7 VisualBrush 快照。
    /// 原件 DiagramItem.xaml.cs:654-672 同样把预览放进 AdornerPanel，不让画布参与布局。</summary>
    private void EnsureCanvasNodePreview(NodeDragSession session)
    {
        if (session.Preview is not null) return;
        var frame = session.FrameRect;
        if (frame.IsEmpty) return;
        var adornerLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(WorkflowCanvas);
        if (adornerLayer is null) return;
        var adorner = new DragPreviewAdorner(WorkflowCanvas);
        adorner.Layer.Opacity = .7;

        foreach (var pair in session.Before)
        {
            var visual = FindCanvasNodeVisual(WorkflowCanvas, pair.Key);
            if (visual == null) continue;
            var bounds = VisualTreeHelper.GetDescendantBounds(visual);
            if (bounds.IsEmpty) continue;
            var ghost = new Rectangle
            {
                Width = bounds.Width, Height = bounds.Height, IsHitTestVisible = false,
                Fill = new VisualBrush(visual) { Stretch = Stretch.None }
            };
            System.Windows.Controls.Canvas.SetLeft(ghost,
                pair.Value.X - pair.Key.NodeRect.Width / 2 - 10 + bounds.X);
            System.Windows.Controls.Canvas.SetTop(ghost,
                pair.Value.Y - pair.Key.NodeRect.Height / 2 - 10 + bounds.Y);
            adorner.Layer.Children.Add(ghost);
        }
        // 原件 DiagramPage.xaml.cs:662-670：批量移动预览是两层——节点快照 + 外包框自身快照。
        // 有外包框（CurBoxOfItems）时才画第二层，单节点拖动没有这一层。
        if (session.Before.Count > 1 && CanvasBoxSelectionRect is { } boxRect)
        {
            var frameGhost = new Rectangle
            {
                Width = boxRect.Width, Height = boxRect.Height,
                IsHitTestVisible = false,
                Fill = new VisualBrush(_canvasSelectionFrame!)
                {
                    Viewbox = new Rect(0, 0, boxRect.Width, boxRect.Height),
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Stretch = Stretch.None
                }
            };
            System.Windows.Controls.Canvas.SetLeft(frameGhost, boxRect.X);
            System.Windows.Controls.Canvas.SetTop(frameGhost, boxRect.Y);
            adorner.Layer.Children.Add(frameGhost);
        }
        adornerLayer.Add(adorner);
        session.Preview = adorner;
    }
    /// <summary>销毁预览根容器与对齐线：三者一起清，避免残留。</summary>
    private void DisposeCanvasNodePreview(NodeDragSession session)
    {
        if (session.Preview is not null)
        {
            // 原件同样从 AdornerLayer 移除，不能从画布 Children 移除（它会一直留在层里叠加）。
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(WorkflowCanvas)?.Remove(session.Preview);
            session.Preview = null;
        }
        foreach (var line in session.AlignLines) WorkflowCanvas.Children.Remove(line);
        session.AlignLines.Clear();
    }
    private void RemoveCanvasNodeGhosts()
    {
        if (_canvasNodeMove == null) return;
        DisposeCanvasNodePreview(_canvasNodeMove);
        _canvasNodeMove.Ghosts.Clear();
    }
    /// <summary>拖动对齐线：原件 DiagramItem.DrawAlignLine（DiagramItem.xaml.cs:855-944）——
    /// 左/右/水平中心、上/下/垂直中心共 6 组；命中的是 LightGreen 1px、虚线 5,2.5、Round join，
    /// 线段从自身边缘外 5px 延伸到目标边缘外 5px。</summary>
    private void CancelCanvasNodeMove() { HideCanvasNodeGhosts(); _canvasNodeMove = null; }
}
