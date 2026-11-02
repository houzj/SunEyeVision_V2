using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;

internal static class NodeWindowProbe
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);

    internal static int Run()
    {
        Exception? error=null;
        var thread=new Thread(()=>{try{RunOnHiddenDesktop();}catch(Exception ex){error=ex;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error is null)return 0;
        Console.Error.WriteLine(error);return 1;
    }

    private static void RunOnHiddenDesktop()
    {
        var desktop=CreateDesktop("SunEyeNodeProbe-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new Win32Exception(Marshal.GetLastWin32Error(),"隐藏桌面不可用");
        try { RunWindows(); }
        finally { if(desktop!=IntPtr.Zero)CloseDesktop(desktop); }
    }

    private static void RunWindows()
    {
        var manager=new PluginManager();manager.LoadPluginsAsync().GetAwaiter().GetResult();
        if(manager.LastAdmissionError is not null)throw new Exception(manager.LastAdmissionError);
        var directory=manager.RuntimeDirectory??throw new Exception("没有已准入目录。");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
        var root=Path.Combine(AppContext.BaseDirectory,"node-window-probe-"+Guid.NewGuid().ToString("N"));
        var paths=new ApplicationDataLocationService(Path.Combine(root,"locator.json")).Prepare(root);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,directory);
        ServiceInitializer.SolutionManager.CreateUntitledSolution();
        try
        {
            var chain=new NodeOpenDecisionChain();var image=RuntimeToolConfiguration.Create(directory,"ImageLoad");bool created=false;
            foreach(var node in new[]{new WorkflowNodeBase("missing","missing","missing","UnknownTool"),new WorkflowNodeBase("paused","paused","paused","Subroutine"){SubroutineId="child"},new WorkflowNodeBase("mismatch","mismatch","mismatch","BlobAnalysis"){RuntimeConfiguration=image}})
            {
                var context=new NodeOpenContext{RuntimeDirectory=directory};
                if(chain.Execute(node,context,()=>{created=true;return new System.Windows.Controls.UserControl();})||created||context.CreatedWindow is not null||string.IsNullOrWhiteSpace(context.FailureReason))throw new Exception("未准入节点未拒绝打开。");
            }
            foreach(var id in new[]{"ImageLoad","BlobAnalysis"})
            {
                var node=new WorkflowNodeBase("opened-"+id,id,id,id){RuntimeConfiguration=RuntimeToolConfiguration.Create(directory,id),DebugWindowStyle=DebugWindowStyle.Custom};
                var context=new NodeOpenContext{RuntimeDirectory=directory};
                if(!chain.Execute(node,context))throw new Exception("节点打开失败: "+id+" "+context.FailureReason);
                if(context.WindowStyle!=DebugWindowStyle.Custom||context.Metadata?.RuntimeCatalog?.DisplayName!=directory.GetCatalog(id)?.DisplayName)throw new Exception("窗口样式或同代目录丢失: "+id);
                if(context.CreatedWindow is not SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow||context.DebugControl is not RuntimeNodeConfigurationControl control)throw new Exception("未构造真实编辑器: "+id);
                context.CreatedWindow.ShowActivated=false;context.CreatedWindow.ShowInTaskbar=false;context.CreatedWindow.Show();
                var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
                if(!context.CreatedWindow.IsLoaded)throw new Exception("真实窗口未加载: "+id);
                context.CreatedWindow.Close();control.Dispose();Console.WriteLine("隐藏桌面真实窗口通过: "+id);
            }
        }
        finally { app.Shutdown(); }
    }
}
