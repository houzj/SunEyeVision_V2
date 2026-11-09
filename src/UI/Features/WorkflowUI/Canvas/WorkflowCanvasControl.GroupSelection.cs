using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private bool _canvasBoxSelectionActive;
    private Rectangle? _canvasSelectionFrame;
    private CanvasNodeProjection[] CanvasSelectedNodes() => CurrentWorkflowTab?.DocumentCanvas?.Nodes.Where(n => n.IsSelected).ToArray() ?? Array.Empty<CanvasNodeProjection>();
    private void ClearCanvasSelectionFrame()
    {
        if (_canvasSelectionFrame != null) WorkflowCanvas.Children.Remove(_canvasSelectionFrame);
        _canvasSelectionFrame = null;
    }
    private void DrawCanvasSelectionFrame()
    {
        ClearCanvasSelectionFrame();
        var nodes = CanvasSelectedNodes();
        if (!_canvasBoxSelectionActive || nodes.Length == 0 || !CanEditCanvas || _isBoxSelecting) return;
        var area = SelectionLayoutCalculator.Bounds(nodes.Select(n => n.NodeRect).ToArray());
        var frame = new Rectangle { Width = area.Width, Height = area.Height, RadiusX = 3, RadiusY = 3,
            Stroke = new SolidColorBrush(Color.FromArgb(127,218,120,15)), StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 2 },
            Fill = new LinearGradientBrush(Color.FromArgb(8,198,87,12), Color.FromArgb(8,253,129,18), 90), Cursor = Cursors.SizeAll };
        System.Windows.Controls.Canvas.SetLeft(frame, area.X); System.Windows.Controls.Canvas.SetTop(frame, area.Y); Panel.SetZIndex(frame, 50);
        frame.MouseLeftButtonDown += (_, e) =>
        {
            if (!CanEditCanvas) return;
            WorkflowCanvas.Focus(); var selected = CanvasSelectedNodes();
            if (selected.Length > 0) ArmCanvasNodeMove(selected[0], e.GetPosition(WorkflowCanvas));
            e.Handled = true;
        };
        frame.MouseMove += (_, e) => { if (_canvasNodeMove != null && !_canvasNodeOleActive && e.LeftButton == MouseButtonState.Pressed) StartCanvasNodeMove(e); };
        frame.MouseLeftButtonUp += (_, _) => { if (!_canvasNodeOleActive) CancelCanvasNodeMove(); };
        frame.ContextMenu = CreateCanvasSelectionMenu(); _canvasSelectionFrame = frame; WorkflowCanvas.Children.Add(frame);
    }
    private ContextMenu CreateCanvasSelectionMenu()
    {
        var menu = new ContextMenu();
        void Add(ItemsControl owner, string name, Action action)
        {
            var item = new MenuItem { Header = name, IsEnabled = CanEditCanvas };
            item.Click += (_, _) => { if (CanEditCanvas) action(); }; owner.Items.Add(item);
        }
        Add(menu, "删除", () => CurrentWorkflowTab?.DocumentCanvas?.Delete(CanvasSelectedNodes(), Array.Empty<CanvasConnectionProjection>()));
        Add(menu, "复制", () => _viewModel?.CopyCanvasSelection());
        var selected = CanvasSelectedNodes();
        if (selected.Any(node => node.IsEnabled)) Add(menu, "禁用", () => SetCanvasSelectionEnabled(false));
        if (selected.Any(node => !node.IsEnabled)) Add(menu, "启用", () => SetCanvasSelectionEnabled(true));
        menu.Items.Add(new Separator());
        var align = new MenuItem { Header = "对齐" }; menu.Items.Add(align);
        foreach (var item in new[] { ("左对齐", NodeAlignment.Left, "left"), ("水平居中", NodeAlignment.CenterX, "center"), ("右对齐", NodeAlignment.Right, "right"),
            ("顶部对齐", NodeAlignment.Top, "top"), ("垂直居中", NodeAlignment.CenterY, "middle"), ("底部对齐", NodeAlignment.Bottom, "bottom") })
        {
            var alignItem = new MenuItem { Header = item.Item1, IsEnabled = CanEditCanvas, Icon = CreateAlignIcon(item.Item3) };
            var alignment = item.Item2;
            alignItem.Click += (_, _) => { if (CanEditCanvas) AlignCanvasSelection(alignment); };
            align.Items.Add(alignItem);
        }
        var layout = new MenuItem { Header = "分布" }; menu.Items.Add(layout);
        Add(layout, "水平分布", () => DistributeCanvasSelection(true)); Add(layout, "垂直分布", () => DistributeCanvasSelection(false));
        return menu;
    }
    /// <summary>原件对齐图标：从已迁入的原件资源取 16px 图标。</summary>
    private static Image CreateAlignIcon(string name)
    {
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/SharedStyles/resources/ic_align_{name}_normal.png", UriKind.Absolute);
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return new Image { Source = bitmap, Width = 16, Height = 16 };
    }
    private void ApplyCanvasSelectionPositions(CanvasNodeProjection[] nodes, Point[] topLeft)
    {
        var tab = CurrentWorkflowTab; if (tab == null || nodes.Length != topLeft.Length || !CanEditCanvas) return;
        var positions = nodes.Select((node, i) => (Node: node, Before: node.Position.ToWpf(),
            After: new Point(topLeft[i].X + node.NodeRect.Width / 2, topLeft[i].Y + node.NodeRect.Height / 2))).ToArray();
        if (positions.Any(p => p.Before != p.After)) tab.DocumentCanvas?.MoveNodes(positions.ToDictionary(p => p.Node, p => new SunEyeVision.Domain.Workflows.CanvasPoint(p.After.X, p.After.Y)));
        DrawCanvasSelectionFrame();
    }
    private void AlignCanvasSelection(NodeAlignment alignment)
    { var nodes = CanvasSelectedNodes(); ApplyCanvasSelectionPositions(nodes, SelectionLayoutCalculator.Align(nodes.Select(n => n.NodeRect).ToArray(), alignment)); }
    private void DistributeCanvasSelection(bool horizontal)
    { var nodes = CanvasSelectedNodes(); ApplyCanvasSelectionPositions(nodes, SelectionLayoutCalculator.Distribute(nodes.Select(n => n.NodeRect).ToArray(), horizontal)); }
    private void SetCanvasSelectionEnabled(bool enabled)
    {
        if (!CanEditCanvas) return;
        var nodes = CanvasSelectedNodes();
        if (nodes.Any(n => n.IsEnabled != enabled)) CurrentWorkflowTab?.DocumentCanvas?.Edit(enabled ? "启用节点" : "禁用节点",
            draft => { foreach (var node in nodes) draft.SetNodeEnabled(node.Definition, enabled); });
    }
    private void InitializeCanvasSelection()
    {
        DataContextChanged += (_, _) => { _canvasBoxSelectionActive = false; ClearCanvasSelectionFrame(); };
        Unloaded += (_, _) => ClearCanvasSelectionFrame();
        PreviewKeyDown += (_, e) =>
        {
            if (CanvasTextInputOwnsKeyboard() || !WorkflowCanvas.IsKeyboardFocusWithin || !CanEditCanvas) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.C or Key.V)
            { if(e.Key==Key.C)_viewModel?.CopyCanvasSelection();else _viewModel?.PasteCanvasSelection(); e.Handled = true; return; }
            if (!_canvasBoxSelectionActive) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.A or Key.F)
            { SetCanvasSelectionEnabled(e.Key == Key.A); e.Handled = true; }
        };
    }
}
