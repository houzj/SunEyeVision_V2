using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private Point _vmPastePoint;
    private void InitializeVmCanvasClipboard()
    {
        var menu = new ContextMenu();
        var paste = new MenuItem { Header = "粘贴", InputGestureText = "Ctrl+V" }; menu.Items.Add(paste);
        paste.Click += (_, _) => { if(CanEditVmCanvas) _viewModel?.PasteCanvasSelection(_vmPastePoint); };
        menu.Opened += (_, _) => {
            paste.IsEnabled=CanEditVmCanvas && _viewModel?.CanPasteCanvasSelection==true;
            PopulateVmToolContextMenu(menu,_vmPastePoint);
        };
        WorkflowCanvas.ContextMenuOpening += (_, _) => _vmPastePoint = Mouse.GetPosition(WorkflowCanvas);
        WorkflowCanvas.ContextMenu = menu;
    }
    private void NodeContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if(!CanEditVmCanvas || sender is not MenuItem item ||
            ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement { DataContext: CanvasNodeProjection node })return;
        if(!node.IsSelected)
        {
            foreach(var current in CurrentWorkflowTab?.DocumentCanvas?.Nodes.AsEnumerable() ?? Enumerable.Empty<CanvasNodeProjection>())current.IsSelected=ReferenceEquals(current,node);
        }
        _viewModel?.CopyCanvasSelection();
    }
}
