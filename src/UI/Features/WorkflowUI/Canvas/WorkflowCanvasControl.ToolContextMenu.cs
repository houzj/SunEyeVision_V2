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
        var session=Adapters.ServiceInitializer.SolutionManager.CurrentSession;
        if(tab?.DocumentCanvas==null||session==null||_viewModel==null)return;
        var canvas=tab.DocumentCanvas;
        var catalog=_viewModel.Toolbox;
        bool Current()=>CanEditVmCanvas&&ReferenceEquals(_viewModel.WorkflowTabViewModel.SelectedTab,tab)
            &&ReferenceEquals(CurrentWorkflowTab,tab)&&ReferenceEquals(tab.DocumentCanvas,canvas)
            &&ReferenceEquals(Adapters.ServiceInitializer.SolutionManager.CurrentSession,session);
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
        try
        {
            if(!CanEditVmCanvas||!tool.CanActivate()||tab.DocumentCanvas is not { } canvas)return;
            canvas.CreateNode(tool.ToolId,new CanvasPoint(Math.Clamp(Math.Round(location.X/5)*5,55,4945),Math.Clamp(Math.Round(location.Y/5)*5,15,4985)));
            WorkflowCanvas.Focus();
        }
        catch(Exception error)
        {
            if(_viewModel!=null)_viewModel.StatusText="添加工具失败："+error.Message;
        }
    }
}
