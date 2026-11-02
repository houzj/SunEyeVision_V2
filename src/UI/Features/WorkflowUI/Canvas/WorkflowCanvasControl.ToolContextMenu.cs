using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shell.Controls.Toolbox;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    /// <summary>原件DiagramPage/恢复ToolNavigation：空白右键直接展开分类，在菜单打开点添加。</summary>
    internal void PopulateVmToolContextMenu(ContextMenu menu,Point location)
    {
        while(menu.Items.Count>1)menu.Items.RemoveAt(1);
        var tab=CurrentWorkflowTab;
        var session=Adapters.ServiceInitializer.SolutionManager?.Session;
        if(tab==null||session==null||_viewModel==null)return;
        var nodes=tab.WorkflowNodes;var edges=tab.WorkflowConnections;
        var catalog=_viewModel.Toolbox;
        bool Current()=>CanEditVmCanvas&&ReferenceEquals(_viewModel.WorkflowTabViewModel.SelectedTab,tab)
            &&ReferenceEquals(CurrentWorkflowTab,tab)&&ReferenceEquals(tab.WorkflowNodes,nodes)&&ReferenceEquals(tab.WorkflowConnections,edges)
            &&ReferenceEquals(Adapters.ServiceInitializer.SolutionManager.Session,session);
        menu.Items.Add(new Separator());
        foreach(var group in catalog.Categories)
        {
            var category=new MenuItem{Header=group.Name,Tag=group.Name,IsEnabled=Current(),Opacity=Current()?1:.45,Icon=ToolMenuIcon(group.IconSource,true)};
            category.Items.Add(new MenuItem{Header="加载工具分类",IsEnabled=false});
            category.SubmenuOpened+=(_,args)=>{
                if(!ReferenceEquals(args.Source,category))return;
                category.Items.Clear();
                foreach(var tool in catalog.GetCategoryTools(group.Name))
                {
                    var item=new MenuItem{Header=tool.Name,Tag=tool,IsEnabled=Current()&&tool.CanActivate(),
                        ToolTip=tool.ToolTip,Opacity=tool.IsAvailable?1:.45,Icon=ToolMenuIcon(tool.IconSource,true)};
                    ToolTipService.SetShowOnDisabled(item,true);
                    item.Click+=(_,e)=>{
                        e.Handled=true;if(!Current()||!tool.CanActivate())return;
                        AddVmContextTool(tab,tool,location);
                    };
                    category.Items.Add(item);
                }
                if(category.Items.Count==0)category.Items.Add(new MenuItem{Header="该分类暂无工具",IsEnabled=false});
            };
            menu.Items.Add(category);
        }
    }
    private static Image ToolMenuIcon(string source,bool available)=>new(){Width=20,Height=20,
        Opacity=available?1:.35,Source=new BitmapImage(new Uri(source,UriKind.Absolute))};
    private void AddVmContextTool(WorkflowTabViewModel tab,ToolItem tool,Point location)
    {
        SunEyeVision.UI.Shared.WorkflowNode? node=null;
        try
        {
            if(!CanEditVmCanvas||ToolRegistry.GetToolMetadata(tool.ToolId)==null)return;
            using(var transaction=tab.CommandManager.BeginTransaction())
            {
                node=tab.CreateNode(tool.ToolId,tool.Name);
                node.Position=new Point(Math.Clamp(Math.Round(location.X/5)*5,55,4945),Math.Clamp(Math.Round(location.Y/5)*5,node.Height/2,5000-node.Height/2));
                // 沿正式添加链处理索引、图像自动绑定及预编译，并在全部接受后登记单个历史条目。
                _viewModel!.AddNodeToWorkflow(node);
                transaction.Complete();
            }
            WorkflowCanvas.Focus();
        }
        catch(Exception error)
        {
            if(node!=null&&!tab.WorkflowNodes.Contains(node))
            {
                tab.SequenceManager.ReleaseGlobalIndex(node.GlobalIndex);
                tab.SequenceManager.ReleaseLocalIndex(tab.CurrentWorkflowId,node.ToolType,node.LocalIndex);
            }
            if(_viewModel!=null)_viewModel.StatusText="添加工具失败："+error.Message;
        }
    }
}
