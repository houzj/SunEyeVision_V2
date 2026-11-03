using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private Point _canvasPastePoint;
    private void InitializeCanvasClipboard()
    {
        var menu = new ContextMenu();
        var paste = new MenuItem { Header = "粘贴", InputGestureText = "Ctrl+V" }; menu.Items.Add(paste);
        paste.Click += (_, _) => { if(CanEditCanvas) _viewModel?.PasteCanvasSelection(_canvasPastePoint); };
        menu.Opened += (_, _) => {
            paste.IsEnabled=CanEditCanvas && _viewModel?.CanPasteCanvasSelection==true;
            PopulateCanvasToolContextMenu(menu,_canvasPastePoint);
        };
        WorkflowCanvas.ContextMenuOpening += (_, _) => _canvasPastePoint = Mouse.GetPosition(WorkflowCanvas);
        WorkflowCanvas.ContextMenu = menu;
    }
    private void NodeContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if(!CanEditCanvas || sender is not MenuItem item ||
            ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement { DataContext: CanvasNodeProjection node })return;
        if(!node.IsSelected)
        {
            foreach(var current in CurrentWorkflowTab?.DocumentCanvas?.Nodes.AsEnumerable() ?? Enumerable.Empty<CanvasNodeProjection>())current.IsSelected=ReferenceEquals(current,node);
        }
        _viewModel?.CopyCanvasSelection();
    }
}
