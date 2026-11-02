using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static int Run(string[] args)
    {
        var scenario=args.Single();
        var caseRoot=Environment.GetEnvironmentVariable("SUNEYE_SHUTDOWN_PROBE_ROOT")??throw new InvalidOperationException("Missing isolated probe data root.");
        var root=Path.Combine(caseRoot,"shutdown-"+Guid.NewGuid().ToString("N"));
        var app=new ProbeApp{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
        Console.WriteLine("probe: app initialized");
        Exception? dispatchFailure=null;
        app.DispatcherUnhandledException+=(_,e)=>{dispatchFailure=e.Exception;e.Handled=true;if(scenario!="throw")app.Shutdown();};
        var manager=new PluginManager();manager.LoadPluginsAsync().GetAwaiter().GetResult();
        Console.WriteLine("probe: plugin admission returned");
        if(manager.LastAdmissionError is not null)throw new Exception(manager.LastAdmissionError);
        var admission=manager.DesktopAdmission??throw new Exception("Missing desktop admission.");
        var pin=manager.PinDesktopHost();
        typeof(App).GetField("_pluginManager",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,manager);
        typeof(App).GetField("_desktopDirectoryPin",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,pin);
        var paths=new ApplicationDataLocationService(Path.Combine(root,"locator.json")).Prepare(root);
        typeof(App).GetField("_dataPaths",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,paths);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,admission.Runtime);
        Console.WriteLine("probe: services initialized");
        var theme=new ThemeManager(ServiceInitializer.SolutionManager.Settings);theme.Initialize();ServiceLocator.Instance.Register(theme);
        var main=new MainWindow(admission){WindowState=WindowState.Normal,ShowActivated=false,ShowInTaskbar=false,Left=-20000,Top=-20000,Width=800,Height=600};
        Console.WriteLine("probe: main window constructed");
        app.MainWindow=main;
        if(scenario=="cancel")ServiceInitializer.SolutionManager.CurrentSolution!.Description="dirty-"+Guid.NewGuid();
        Window? failing=null;
        System.ComponentModel.CancelEventHandler fail=(_,e)=>
        {
            if(scenario=="throw") {Console.WriteLine("probe: throwing child closing stack: "+Environment.StackTrace);throw new InvalidOperationException("injected child cleanup failure");}
            e.Cancel=true;
        };
        if(scenario is "failure" or "throw") {failing=new Window{ShowActivated=false,ShowInTaskbar=false,Left=-21000,Top=-21000};failing.Closing+=fail;}
        var lease=scenario is "wait" or "failure" or "throw"?admission.Runtime.AcquireInvocation():null;
        var start=DateTime.UtcNow;bool sawTimeout=false,sawFailure=false,sawCancel=false,closed=false,retryStarted=false;DateTime? verdictAfter=null;
        using var watchdog=new System.Threading.Timer(_=>{Console.Error.WriteLine("probe: watchdog expired while dispatcher did not complete");Environment.Exit(3);},null,TimeSpan.FromSeconds(40),Timeout.InfiniteTimeSpan);
        main.Closed+=(_,_)=>closed=true;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        bool sawTick=false;int lastReport=-1;
        timer.Tick+=(_,_)=>
        {
            if(!sawTick){sawTick=true;Console.WriteLine("probe: dispatcher timer ticked");}
            var seconds=(int)(DateTime.UtcNow-start).TotalSeconds;
            if(seconds%5==0&&seconds!=lastReport)
            {
                lastReport=seconds;
                var fields=typeof(MainWindow).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Where(f=>f.Name is "_isClosingPromptInProgress" or "_closeApproved" or "_shutdownPreparationStarted").Select(f=>f.Name+"="+f.GetValue(main));
                Console.WriteLine("probe: state "+string.Join(",",fields)+"; windows="+string.Join(",",app.Windows.OfType<Window>().Select(w=>w.GetType().Name+":"+w.Title)));
            }
            if(closed){timer.Stop();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);return;}
            foreach(var dialog in app.Windows.OfType<ThemeMessageDialog>().ToArray())
            {
                var message=((TextBlock)dialog.FindName("MessageText")).Text;
                Console.WriteLine("probe: dialog: "+message);
                string button;
                if(message.Contains("仍有结果或插件资源")){sawTimeout=true;if(!main.IsVisible||admission.Runtime.IsActive)throw new Exception("Timeout did not preserve an open retired workspace.");lease!.Dispose();button="OkButton";}
                else if(message.Contains("插件消费者未能全部释放")||message.Contains("子窗口未能全部关闭")){if(retryStarted)throw new Exception("Retry unexpectedly failed: "+message);sawFailure=true;button="OkButton";}
                else if(scenario=="cancel"&&message.Contains("未保存")){sawCancel=true;button="CancelButton";}
                else throw new Exception("Unexpected dialog: "+message);
                ((Button)dialog.FindName(button)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(sawCancel||sawFailure)verdictAfter=DateTime.UtcNow.AddSeconds(1);
            }
            if(verdictAfter is {} due&&DateTime.UtcNow>=due&&main.IsVisible)
            {
                if(!admission.Runtime.IsActive)throw new Exception("Rejected shutdown retired the active generation.");
                if(sawFailure && !ReferenceEquals(lease!.Directory,admission.Runtime))throw new Exception("A failed child close released its in-use resource.");
                if(sawCancel && (bool)typeof(MainWindow).GetField("_isClosingPromptInProgress",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!)
                    throw new Exception("Cancel did not complete the close prompt.");
                if(sawFailure)
                {
                    failing!.Closing-=fail;lease!.Dispose();retryStarted=true;verdictAfter=null;
                    main.Dispatcher.BeginInvoke(new Action(main.Close));
                }
                else {timer.Stop();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
            }
            if((DateTime.UtcNow-start)>TimeSpan.FromSeconds(30))throw new TimeoutException("Shutdown probe timed out.");
        };
        timer.Start();Console.WriteLine("probe: showing window");main.Show();if(failing is not null){failing.Owner=main;failing.Show();}Console.WriteLine("probe: window shown");main.Dispatcher.BeginInvoke(new Action(()=>{Console.WriteLine("probe: calling Close");main.Close();Console.WriteLine("probe: Close returned");}));
        Console.WriteLine("probe: starting dispatcher");
        Dispatcher.Run();
        if(dispatchFailure is not null&&scenario!="throw")throw new Exception("Dispatcher failure.",dispatchFailure);
        if(scenario=="throw"&&(dispatchFailure is null||!sawFailure||!closed))throw new Exception("Child exception did not recover through a guarded retry.");
        if(scenario=="normal"&&!closed)throw new Exception("Normal window did not close.");
        if(scenario=="wait"&&(!sawTimeout||!closed))throw new Exception("Reader timeout/release path did not close.");
        if(scenario=="cancel"&&!sawCancel)throw new Exception("Cancel prompt was not reached.");
        if(scenario=="failure"&&(!sawFailure||!closed))throw new Exception("Cleanup refusal did not recover through a guarded retry.");
        if(closed&&(admission.Runtime.IsActive||manager.RuntimeDirectory is not null))
            throw new Exception("A closed window left the plugin generation admitted.");
        Console.WriteLine("Actual MainWindow shutdown scenario passed: "+scenario);
        return 0;
    }
}

internal sealed class ProbeApp:App
{
    // The probe explicitly assembles the same services; do not start a second product bootstrap.
    protected override void OnStartup(StartupEventArgs e) { }
}
