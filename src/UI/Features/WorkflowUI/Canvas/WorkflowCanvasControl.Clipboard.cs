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
        // 原件空白右键顺序：粘贴→导入组合→模块搜索→导入流程→工具箱分组（DiagramPage.xaml:66-224）。
        // 导入组合待 Group 范围决定，本批接"模块搜索"到现有搜索控件。
        var search = new MenuItem { Header = "模块搜索", InputGestureText = "Ctrl+F" }; menu.Items.Add(search);
        search.Click += (_, _) => _viewModel?.RequestWorkflowSearch();
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
