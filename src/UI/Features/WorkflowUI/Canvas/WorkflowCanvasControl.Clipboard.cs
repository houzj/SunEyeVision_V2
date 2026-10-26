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
        menu.Opened += (_, _) => paste.IsEnabled = CanEditVmCanvas && _viewModel?.CanPasteCanvasSelection == true;
        paste.Click += (_, _) =>
        {
            if (CanEditVmCanvas && _viewModel?.PasteCanvasSelection(_vmPastePoint) == true)
            { WorkflowCanvas.Focus(); _vmBoxSelectionActive = VmSelectedNodes().Length > 1; DrawVmSelectionFrame(); }
        };
        WorkflowCanvas.ContextMenuOpening += (_, _) => _vmPastePoint = Mouse.GetPosition(WorkflowCanvas);
        WorkflowCanvas.ContextMenu = menu;
    }
    private void NodeContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditVmCanvas || sender is not MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: WorkflowNode node } } }) return;
        if (!node.IsSelected) { ClearAllSelections(); node.IsSelected = true; }
        _viewModel?.CopyCanvasSelection();
    }
}
