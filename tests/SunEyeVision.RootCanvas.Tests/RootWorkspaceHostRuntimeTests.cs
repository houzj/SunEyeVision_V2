using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>Extracts root-tab/document parts of RootWorkspaceHostTests using real Runtime admission.</summary>
[Collection("VM canvas UI")]
public sealed class RootWorkspaceHostRuntimeTests
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;

    [Fact]
    public void Real_shell_root_copy_history_and_reopen_use_same_admitted_image_source()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            IntPtr desktop=IntPtr.Zero;
            try
            {
                desktop=EnterHiddenDesktop();
                Run();
            }
            catch(Exception error){failure=error;}
            finally{if(desktop!=IntPtr.Zero)CloseDesktop(desktop);}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(60)),"根流程宿主验证超时");
        if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static IntPtr EnterHiddenDesktop()
    {
        var desktop=CreateDesktop("SunEyeVision-RootTest-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop)){var code=Marshal.GetLastWin32Error();throw new System.ComponentModel.Win32Exception(code,"隐藏桌面不可用，Win32="+code);}
        return desktop;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var product=Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD")??throw new InvalidOperationException("缺少正式载荷根目录");
        var caseRoot=Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("缺少隔离测试数据目录");
        var prior=AppContext.BaseDirectory;AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(product)+Path.DirectorySeparatorChar);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        Exception? dispatchFailure=null;app.DispatcherUnhandledException+=(_,e)=>{dispatchFailure=e.Exception;e.Handled=true;};
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        foreach(var name in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+name,UriKind.Relative)});
        var manager=new PluginManager();Window? window=null;MainWindowViewModel? viewModel=null;
        try
        {
            manager.LoadPluginsAsync().GetAwaiter().GetResult();Assert.Null(manager.LastAdmissionError);var admission=Assert.IsType<DesktopToolAdmission>(manager.DesktopAdmission);
            var directory=Path.Combine(caseRoot,"host-"+Guid.NewGuid().ToString("N"));
            var paths=new ApplicationDataLocationService(Path.Combine(directory,"locator.json")).Prepare(directory);
            ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,admission.Runtime);
            var solutions=ServiceInitializer.SolutionManager;solutions.CreateUntitledSolution();viewModel=new MainWindowViewModel(admission);
            var shell=new ShellWorkspaceControl{DataContext=viewModel.WorkflowTabViewModel};shell.Initialize(viewModel);
            var bar=new ShellTopBarControl{DataContext=viewModel.TopBar};var panel=new DockPanel();DockPanel.SetDock(bar,Dock.Top);panel.Children.Add(bar);panel.Children.Add(shell);
            window=new Window{Content=panel,Width=1100,Height=800,ShowActivated=false,ShowInTaskbar=false};window.Show();Pump();
            var document=solutions.CurrentSolution!;var source=Assert.Single(document.Workflows);var sourceTab=Assert.Single(viewModel.WorkflowTabViewModel.Tabs);
            var node=sourceTab.CreateNode("ImageLoad");RootCanvasFixture.SetValue(node,17);viewModel.AddNodeToWorkflow(node);Pump();
            Assert.NotNull(shell.GetCurrentWorkflowCanvas());Assert.Single(source.Nodes);Console.WriteLine("root host: actual shell and Runtime node loaded");
            Assert.True((bool)Invoke(viewModel,"CopyRootWorkspace",sourceTab,null)!);Pump();
            var copy=Assert.Single(document.Workflows.Where(flow=>flow.Id!=source.Id));var copyTab=viewModel.WorkflowTabViewModel.SelectedTab!;
            var copied=Assert.Single(copy.Nodes);Assert.NotEqual(node.Id,copied.Id);Assert.Equal(17,RootCanvasFixture.Value(copied));
            Assert.Same(copyTab,viewModel.WorkflowTabViewModel.SelectedTab);Assert.Equal(2,viewModel.WorkflowTabViewModel.Tabs.Count);
            Invoke(viewModel,"UndoRootWorkspace");Pump();Assert.Single(document.Workflows);Assert.Same(sourceTab,viewModel.WorkflowTabViewModel.SelectedTab);
            Invoke(viewModel,"RedoRootWorkspace");Pump();Assert.Equal(2,document.Workflows.Count);Assert.Contains(document.Workflows,flow=>flow.Id==copy.Id);Console.WriteLine("root host: copy and undo/redo passed");
            var path=Path.Combine(directory,"root-workspace.solution");var save=solutions.SaveCurrentDocument(path);Assert.True(save.FileWritten,save.Error);
            var reopened=new SolutionRepository(directory:admission.Runtime).LoadCandidate(path).Candidate;Assert.NotNull(reopened);
            try{var restored=Assert.Single(reopened!.Workflows.Where(flow=>flow.Id==copy.Id));Assert.Equal(17,RootCanvasFixture.Value(Assert.Single(restored.Nodes)));}
            finally{reopened!.ExecutionOwner.Retire();}Console.WriteLine("root host: saved and reopened with Runtime settings");
            if(dispatchFailure is not null)throw new Exception("根宿主 Dispatcher 异常",dispatchFailure);
        }
        finally
        {
            window?.Close();
            if(viewModel is not null){Invoke(viewModel,"ReleasePluginConsumersForShutdown");Invoke(viewModel,"ReleaseDocumentHost");}
            manager.UnloadPluginsAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();Console.WriteLine("root host: admitted directory drained");
            app.Shutdown();AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",prior);
        }
    }
    private static object? Invoke(object target,string name,params object?[] args)
        =>target.GetType().GetMethod(name,Flags)!.Invoke(target,args);
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
}
