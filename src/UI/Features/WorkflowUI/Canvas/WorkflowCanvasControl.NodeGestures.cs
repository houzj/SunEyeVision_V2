using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

public partial class WorkflowCanvasControl
{
    private const string VmNodeDragFormat = "SunEyeVision.VmNodeMove.Token";
    private sealed class VmNodeMoveSession
    {
        public required object Owner { get; init; }
        public required WorkflowTabViewModel Tab { get; init; }
        public required Point Pointer { get; init; }
        public required Dictionary<WorkflowNode, Point> Before { get; init; }
        public required string Token { get; init; }
        public required string Stamp { get; init; }
        public string? ConfigurationStamp { get; init; }
        public Dictionary<WorkflowNode, Point> After { get; } = new();
        public List<FrameworkElement> Ghosts { get; } = new();
        public bool Copy { get; set; }
    }
    private VmNodeMoveSession? _vmNodeMove;
    private bool _vmNodeOleActive;
    private Window? _vmGestureWindow;

    private static int VmIdentity(object? value) => value == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    private string VmGraphStamp(WorkflowTabViewModel tab) => $"{tab.CurrentWorkflowId}:{VmIdentity(tab.WorkflowNodes)}:{VmIdentity(tab.WorkflowConnections)}|"+string.Join(";",
        tab.WorkflowNodes.Select(n => $"{n.Id}:{VmIdentity(n)}:{n.PositionX:R},{n.PositionY:R}:{n.IsEnabled}:{n.Name}:{n.ToolType}:{n.SubroutineId}:{VmIdentity(n.Parameters)}:{VmIdentity(n.ParamSettings)}")) + "|" +
        string.Join(";", tab.WorkflowConnections.Select(c => $"{c.Id}:{c.SourceNodeId}:{c.SourcePort}:{c.TargetNodeId}:{c.TargetPort}"));
    private bool ValidVmNodeMove(VmNodeMoveSession session) => CanEditVmCanvas &&
        ReferenceEquals(DataContext, session.Owner) && ReferenceEquals(_viewModel?.WorkflowTabViewModel.SelectedTab, session.Tab) &&
        VmGraphStamp(session.Tab) == session.Stamp;

    private void InitializeVmNodeGestures()
    {
        WorkflowCanvas.Focusable = true;
        WorkflowCanvas.AllowDrop = true;
        WorkflowCanvas.PreviewDragOver += VmNodeDragOver;
        WorkflowCanvas.PreviewDrop += VmNodeDrop;
        WorkflowCanvas.DragLeave += (_, e) => { if (_vmNodeOleActive && !VmDropInside(e.GetPosition(WorkflowCanvas))) HideVmNodeGhosts(); };
        WorkflowCanvas.LostMouseCapture += (_, _) => { if (!_vmNodeOleActive) CancelVmCanvasGesture(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !VmTextInputOwnsKeyboard()) { CancelVmCanvasGesture(); ClearVmWireSelection(); e.Handled = true; } };
        DataContextChanged += (_, _) => CancelVmCanvasGesture();
        Loaded += (_, _) =>
        {
            if (_vmGestureWindow != null) _vmGestureWindow.Deactivated -= VmGestureDeactivated;
            _vmGestureWindow = Window.GetWindow(this);
            if (_vmGestureWindow != null) _vmGestureWindow.Deactivated += VmGestureDeactivated;
        };
        Unloaded += (_, _) =>
        {
            CancelVmCanvasGesture();
            if (_vmGestureWindow != null) _vmGestureWindow.Deactivated -= VmGestureDeactivated;
            _vmGestureWindow = null;
        };
    }
    private static bool VmTextInputOwnsKeyboard()
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
    private void VmGestureDeactivated(object? sender, EventArgs e)
    {
        if (!_vmNodeOleActive) CancelVmCanvasGesture();
    }
    private void CancelVmCanvasGesture()
    {
        // 清状态先于释放捕获，防止 LostMouseCapture 重入。
        CancelVmNodeMove();
        _isPanning = false; _isBoxSelecting = false; _isDraggingConnection = false;
        _dragConnectionSourceNode = null; _dragConnectionSourcePort = null;
        if (_dragConnectionSourceBorder != null) SetPortsVisibility(_dragConnectionSourceBorder, false);
        if (_highlightedTargetNodeBorder != null) SetPortsVisibility(_highlightedTargetNodeBorder, false);
        _dragConnectionSourceBorder = null; _highlightedTargetNodeBorder = null;
        _vmConnectionTarget = null; _vmConnectionTab = null; _vmConnectionStamp = null; _vmConnectionConfigurationStamp = null; _vmConnectionStarted = false;
        foreach (var port in VmPorts(WorkflowCanvas)) { port.Visibility = Visibility.Collapsed; port.IsHighlighted = false; }
        if (_tempConnectionLine != null) _tempConnectionLine.Visibility = Visibility.Collapsed;
        _tempConnectionGeometry?.Figures.Clear();
        SelectionBox?.EndSelection();
        Cursor = Cursors.Arrow;
        FinishVmWireDrag(false);
        if (Mouse.Captured is DependencyObject captured && (captured == WorkflowCanvas || WorkflowCanvas.IsAncestorOf(captured)))
            Mouse.Capture(null);
    }
    private void ArmVmNodeMove(WorkflowNode node, Point pointer)
    {
        CancelVmNodeMove();
        var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
        if (tab == null || !CanEditVmCanvas) return;
        _vmNodeMove = new VmNodeMoveSession
        {
            Owner = DataContext, Tab = tab, Pointer = pointer, Token = Guid.NewGuid().ToString("N"),
            Before = tab.WorkflowNodes.OfType<WorkflowNode>().Where(n => n.IsSelected).ToDictionary(n => n, n => n.Position),
            Stamp = VmGraphStamp(tab), ConfigurationStamp = VmConfigurationStamp(tab)
        };
    }
    private void StartVmNodeMove(MouseEventArgs e)
    {
        var session = _vmNodeMove;
        if (session == null || !ValidVmNodeMove(session)) { CancelVmNodeMove(); return; }
        session.Copy = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        _vmNodeOleActive = true;
        QueryContinueDragEventHandler query = (_, args) => { if (!ValidVmNodeMove(session)) args.Action = DragAction.Cancel; };
        WorkflowCanvas.QueryContinueDrag += query;
        try
        {
            DragDrop.DoDragDrop(WorkflowCanvas, new DataObject(VmNodeDragFormat, session.Token, false), session.Copy ? DragDropEffects.Copy : DragDropEffects.Move);
        }
        finally
        {
            WorkflowCanvas.QueryContinueDrag -= query;
            _vmNodeOleActive = false;
            CancelVmNodeMove();
        }
        e.Handled = true;
    }
    private bool ReadVmNodeMove(DragEventArgs e, out VmNodeMoveSession session)
    {
        session = _vmNodeMove!;
        return session != null && e.Data.GetDataPresent(VmNodeDragFormat, false) &&
            e.Data.GetData(VmNodeDragFormat, false) is string token && token == session.Token;
    }
    private bool VmDropInside(Point point)
    {
        if (!new Rect(0, 0, WorkflowCanvas.ActualWidth, WorkflowCanvas.ActualHeight).Contains(point)) return false;
        var viewport = FindScrollViewer();
        return viewport == null || new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight)
            .Contains(WorkflowCanvas.TranslatePoint(point, viewport));
    }
    private void VmNodeDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(VmNodeDragFormat, false)) return;
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!ReadVmNodeMove(e, out var session) || !ValidVmNodeMove(session) ||
            !VmNodeDropEffectAllowed(session.Copy, e.KeyStates, e.AllowedEffects, false) || !VmDropInside(e.GetPosition(WorkflowCanvas)))
        { HideVmNodeGhosts(); return; }
        e.Effects = session.Copy ? DragDropEffects.Copy : DragDropEffects.Move;
        PreviewVmNodeMove(session, e.GetPosition(WorkflowCanvas));
    }
    private void VmNodeDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(VmNodeDragFormat, false)) return;
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!ReadVmNodeMove(e, out var session) || !ValidVmNodeMove(session) ||
            session.ConfigurationStamp == null || session.ConfigurationStamp != VmConfigurationStamp(session.Tab) ||
            !VmNodeDropEffectAllowed(session.Copy, e.KeyStates, e.AllowedEffects, true) || !VmDropInside(e.GetPosition(WorkflowCanvas))) return;
        PreviewVmNodeMove(session, e.GetPosition(WorkflowCanvas));
        if (session.After.Count == 1)
        {
            var single = session.After.Single();
            session.After[single.Key] = ResolveVmPlacement(single.Value, single.Key.NodeRect.Height);
        }
        var positions = session.After.Select(p => (Node: p.Key, Before: session.Before[p.Key], After: p.Value)).ToArray();
        var insertion = session.After.Count == 1 ? _vmInsertionWire : null;
        if (insertion != null && positions.Length == 1)
        {
            if (_viewModel?.InsertNodeIntoConnection(positions[0].Node, insertion, positions[0].After) != true) return;
        }
        else if (session.Copy)
        {
            if (_viewModel?.DuplicateCanvasNodes(session.After) != true) return;
        }
        else if (positions.Any(p => p.Before != p.After))
            _viewModel?.CurrentCommandManager?.Execute(new MoveNodesSnapshotCommand(positions, session.Tab.WorkflowConnections));
        e.Effects = session.Copy && insertion == null ? DragDropEffects.Copy : DragDropEffects.Move;
    }
    private static bool VmNodeDropEffectAllowed(bool copy, DragDropKeyStates keys, DragDropEffects allowed, bool dropping)
    {
        var control = (keys & DragDropKeyStates.ControlKey) != 0;
        return copy == control && (!copy || !dropping || keys == DragDropKeyStates.ControlKey) &&
            (allowed & (copy ? DragDropEffects.Copy : DragDropEffects.Move)) != 0;
    }
    private void PreviewVmNodeMove(VmNodeMoveSession session, Point pointer)
    {
        var delta = pointer - session.Pointer;
        if (session.Ghosts.Count == 0 && Math.Abs(delta.X) <= 3 && Math.Abs(delta.Y) <= 3)
        { session.After.Clear(); return; }
        var left = session.Before.Min(p => p.Value.X - p.Key.NodeRect.Width / 2);
        var top = session.Before.Min(p => p.Value.Y - p.Key.NodeRect.Height / 2);
        delta = new Vector(Math.Round((left + delta.X) / 5) * 5 - left, Math.Round((top + delta.Y) / 5) * 5 - top);
        RemoveVmNodeGhosts();
        session.After.Clear();
        foreach (var pair in session.Before)
        {
            var node = pair.Key;
            var width = node.NodeRect.Width; var height = node.NodeRect.Height;
            var position = new Point(Math.Clamp(pair.Value.X + delta.X, width / 2, 5000 - width / 2),
                Math.Clamp(pair.Value.Y + delta.Y, height / 2, 5000 - height / 2));
            session.After[node] = position;
            var visual = FindVmNodeVisual(WorkflowCanvas, node);
            if (visual == null) continue;
            // 原件DiagramItem_PreviewMouseMove：后代边界+不拉伸的0.7镜像。
            var bounds = VisualTreeHelper.GetDescendantBounds(visual);
            if (bounds.IsEmpty) continue;
            var brush = new VisualBrush(visual) { Stretch = Stretch.None, Opacity = .7 };
            var ghost = new Rectangle { Width = bounds.Width, Height = bounds.Height, Fill = brush, IsHitTestVisible = false };
            System.Windows.Controls.Canvas.SetLeft(ghost, position.X - width / 2 - 10 + bounds.X);
            System.Windows.Controls.Canvas.SetTop(ghost, position.Y - height / 2 - 10 + bounds.Y);
            Panel.SetZIndex(ghost, 9000); WorkflowCanvas.Children.Add(ghost); session.Ghosts.Add(ghost);
        }
        if (session.After.Count == 1)
        {
            var single = session.After.Single();
            UpdateVmPlacementFeedback(single.Value, single.Key.NodeRect.Height, session.Before.Keys.Select(n => n.Id));
        }
        else ClearVmPlacementFeedback();
        UpdateVmInsertionWire(pointer, session.Before.Keys.Select(n => n.Id));
    }
    private static Border? FindVmNodeVisual(DependencyObject parent, WorkflowNode node)
    {
        if (parent is Border border && border.Name == "HitAreaBorder" && ReferenceEquals(border.Tag, node)) return border;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var result = FindVmNodeVisual(VisualTreeHelper.GetChild(parent, i), node);
            if (result != null) return result;
        }
        return null;
    }
    private void HideVmNodeGhosts() { RemoveVmNodeGhosts(); ClearVmPlacementFeedback(); ClearVmInsertionWire(); }
    private void RemoveVmNodeGhosts()
    {
        if (_vmNodeMove == null) return;
        foreach (var ghost in _vmNodeMove.Ghosts) WorkflowCanvas.Children.Remove(ghost);
        _vmNodeMove.Ghosts.Clear();
    }
    private void CancelVmNodeMove() { HideVmNodeGhosts(); _vmNodeMove = null; }
}