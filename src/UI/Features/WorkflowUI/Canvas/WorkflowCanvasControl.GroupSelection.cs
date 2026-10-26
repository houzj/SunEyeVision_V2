using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private bool _vmBoxSelectionActive;
    private Rectangle? _vmSelectionFrame;
    private WorkflowNode[] VmSelectedNodes() => CurrentWorkflowTab?.WorkflowNodes.OfType<WorkflowNode>().Where(n => n.IsSelected).ToArray() ?? Array.Empty<WorkflowNode>();
    private void ClearVmSelectionFrame()
    {
        if (_vmSelectionFrame != null) WorkflowCanvas.Children.Remove(_vmSelectionFrame);
        _vmSelectionFrame = null;
    }
    private void DrawVmSelectionFrame()
    {
        ClearVmSelectionFrame();
        var nodes = VmSelectedNodes();
        if (!_vmBoxSelectionActive || nodes.Length == 0 || !CanEditVmCanvas || _isBoxSelecting) return;
        var area = VmSelectionLayout.Bounds(nodes.Select(n => n.NodeRect).ToArray());
        var frame = new Rectangle { Width = area.Width, Height = area.Height, RadiusX = 3, RadiusY = 3,
            Stroke = new SolidColorBrush(Color.FromArgb(127,218,120,15)), StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 2 },
            Fill = new LinearGradientBrush(Color.FromArgb(8,198,87,12), Color.FromArgb(8,253,129,18), 90), Cursor = Cursors.SizeAll };
        System.Windows.Controls.Canvas.SetLeft(frame, area.X); System.Windows.Controls.Canvas.SetTop(frame, area.Y); Panel.SetZIndex(frame, 50);
        frame.MouseLeftButtonDown += (_, e) =>
        {
            if (!CanEditVmCanvas) return;
            WorkflowCanvas.Focus(); var selected = VmSelectedNodes();
            if (selected.Length > 0) ArmVmNodeMove(selected[0], e.GetPosition(WorkflowCanvas));
            e.Handled = true;
        };
        frame.MouseMove += (_, e) => { if (_vmNodeMove != null && !_vmNodeOleActive && e.LeftButton == MouseButtonState.Pressed) StartVmNodeMove(e); };
        frame.MouseLeftButtonUp += (_, _) => { if (!_vmNodeOleActive) CancelVmNodeMove(); };
        frame.ContextMenu = CreateVmSelectionMenu(); _vmSelectionFrame = frame; WorkflowCanvas.Children.Add(frame);
    }
    private ContextMenu CreateVmSelectionMenu()
    {
        var menu = new ContextMenu();
        void Add(ItemsControl owner, string name, Action action)
        {
            var item = new MenuItem { Header = name, IsEnabled = CanEditVmCanvas };
            item.Click += (_, _) => { if (CanEditVmCanvas) action(); }; owner.Items.Add(item);
        }
        Add(menu, "删除", () => { var command = _viewModel?.DeleteSelectedNodesCommand; if (command?.CanExecute(null) == true) command.Execute(null); });
        Add(menu, "复制", () => _viewModel?.CopyCanvasSelection());
        Add(menu, "禁用", () => SetVmSelectionEnabled(false)); Add(menu, "启用", () => SetVmSelectionEnabled(true));
        menu.Items.Add(new Separator());
        var align = new MenuItem { Header = "对齐" }; menu.Items.Add(align);
        foreach (var item in new[] { ("左对齐", VmNodeAlignment.Left), ("水平居中", VmNodeAlignment.CenterX), ("右对齐", VmNodeAlignment.Right),
            ("顶部对齐", VmNodeAlignment.Top), ("垂直居中", VmNodeAlignment.CenterY), ("底部对齐", VmNodeAlignment.Bottom) })
            Add(align, item.Item1, () => AlignVmSelection(item.Item2));
        var layout = new MenuItem { Header = "分布" }; menu.Items.Add(layout);
        Add(layout, "水平分布", () => DistributeVmSelection(true)); Add(layout, "垂直分布", () => DistributeVmSelection(false));
        return menu;
    }
    private void ApplyVmSelectionPositions(WorkflowNode[] nodes, Point[] topLeft)
    {
        var tab = CurrentWorkflowTab; if (tab == null || nodes.Length != topLeft.Length || !CanEditVmCanvas) return;
        var positions = nodes.Select((node, i) => (Node: node, Before: node.Position,
            After: new Point(topLeft[i].X + node.NodeRect.Width / 2, topLeft[i].Y + node.NodeRect.Height / 2))).ToArray();
        if (positions.Any(p => p.Before != p.After)) tab.CommandManager.Execute(new MoveNodesSnapshotCommand(positions, tab.WorkflowConnections));
        DrawVmSelectionFrame();
    }
    private void AlignVmSelection(VmNodeAlignment alignment)
    { var nodes = VmSelectedNodes(); ApplyVmSelectionPositions(nodes, VmSelectionLayout.Align(nodes.Select(n => n.NodeRect).ToArray(), alignment)); }
    private void DistributeVmSelection(bool horizontal)
    { var nodes = VmSelectedNodes(); ApplyVmSelectionPositions(nodes, VmSelectionLayout.Distribute(nodes.Select(n => n.NodeRect).ToArray(), horizontal)); }
    private void SetVmSelectionEnabled(bool enabled)
    {
        if (!CanEditVmCanvas) return;
        var nodes = VmSelectedNodes();
        if (nodes.Any(n => n.IsEnabled != enabled)) CurrentWorkflowTab?.CommandManager.Execute(new SetNodesEnabledCommand(nodes, enabled));
    }
    private void InitializeVmGroupSelection()
    {
        DataContextChanged += (_, _) => { _vmBoxSelectionActive = false; ClearVmSelectionFrame(); };
        Unloaded += (_, _) => ClearVmSelectionFrame();
        PreviewKeyDown += (_, e) =>
        {
            if (VmTextInputOwnsKeyboard() || !WorkflowCanvas.IsKeyboardFocusWithin || !CanEditVmCanvas) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.C)
            { _viewModel?.CopyCanvasSelection(); e.Handled = true; return; }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.V)
            { if (_viewModel?.PasteCanvasSelection() == true) { _vmBoxSelectionActive = VmSelectedNodes().Length > 1; DrawVmSelectionFrame(); } e.Handled = true; return; }
            if (!_vmBoxSelectionActive) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.A or Key.F)
            { SetVmSelectionEnabled(e.Key == Key.A); e.Handled = true; }
        };
    }
}