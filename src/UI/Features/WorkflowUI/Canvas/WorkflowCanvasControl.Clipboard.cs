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
        var paste = new MenuItem { Header = "粘贴", InputGestureText = "Ctrl+V", IsEnabled = false, ToolTip = "节点复制尚未接入正式会话事务" }; menu.Items.Add(paste);
        menu.Opened += (_, _) => {
            PopulateVmToolContextMenu(menu,_vmPastePoint);
        };
        WorkflowCanvas.ContextMenuOpening += (_, _) => _vmPastePoint = Mouse.GetPosition(WorkflowCanvas);
        WorkflowCanvas.ContextMenu = menu;
    }
    private void NodeContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null) _viewModel.StatusText = "节点复制尚未接入正式会话事务，暂不可用。";
    }
}
