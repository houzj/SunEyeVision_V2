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
using SunEyeVision.UI.Shell.Windows;
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
        Console.WriteLine("window probe: loading plugins");
        var manager=new PluginManager();manager.LoadPluginsAsync().GetAwaiter().GetResult();
        if(manager.LastAdmissionError is not null)throw new Exception(manager.LastAdmissionError);
        var directory=manager.RuntimeDirectory??throw new Exception("没有已准入目录。");
        Console.WriteLine("window probe: plugins admitted");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","_validation","node-window-probe-"+Guid.NewGuid().ToString("N")));
        var paths=new ApplicationDataLocationService(Path.Combine(root,"locator.json")).Prepare(root);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,directory);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var viewModel=new MainWindowViewModel(manager.DesktopAdmission??throw new Exception("缺少桌面准入。"));
        ServiceInitializer.SolutionManager.CreateUntitledSolution();
        if(viewModel.WorkflowTabViewModel.SelectedTab is null)viewModel.NewWorkflowCommand.Execute(null);
        var shell=new Window{Width=620,Height=400,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
        Console.WriteLine("window probe: services ready");
        try
        {
            foreach(var id in new[]{"ImageLoad","BlobAnalysis"})
            {
                Console.WriteLine("window probe: constructing "+id);
                var node=viewModel.WorkflowTabViewModel.SelectedTab!.CreateNode(id);viewModel.AddNodeToWorkflow(node);node.DebugWindowStyle=DebugWindowStyle.Custom;
                viewModel.OpenDebugWindowCommand.Execute(node);
                var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
                var window=app.Windows.OfType<SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("正式打开命令没有显示编辑器: "+id);
                if(window.FindName("ContentHost") is not System.Windows.Controls.ContentControl { Content:RuntimeNodeConfigurationControl control }||SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.GetDraftContext(control) is not ProductionEditorContext {IsValid:true})throw new Exception("正式编辑会话未接入: "+id);
                if(!window.IsLoaded)throw new Exception("真实窗口未加载: "+id);
                Console.WriteLine("window probe: closing "+id);window.Close();Console.WriteLine("window probe: close returned "+id);
                if(window.IsVisible||ServiceInitializer.SolutionManager.Session!.Editing.RegisteredScopeCount!=0)throw new Exception("窗口或编辑会话未释放: "+id);
                Console.WriteLine("隐藏桌面真实窗口通过: "+id);
            }
        }
        finally { app.Shutdown(); }
    }
}
