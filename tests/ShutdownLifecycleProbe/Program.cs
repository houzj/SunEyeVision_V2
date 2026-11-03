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
using SunEyeVision.Infrastructure.Persistence.ApplicationData;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var productRoot=typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item=>item.Key=="HostProductRoot").Value!;
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.GetFullPath(productRoot));
        try { return args.Contains("root-collection-main") ? ContinuousMainProbe.Run(RootCollectionMainProbe.RunWindow) : args.Contains("continuous-main") ? ContinuousMainProbe.Run() : args.Contains("full-startup-probe") ? RunFullStartup(args) : Run(args); }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(IntPtr desktop);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);
    private static int RunFullStartup(string[] args)
    {
        var result=1;Exception? failure=null;
        var thread=new Thread(()=>{try{result=RunFullStartupOnHiddenDesktop();}catch(Exception error){failure=error;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure is not null)throw new Exception("隐藏桌面完整启动失败",failure);
        return result;
    }
    private static int RunFullStartupOnHiddenDesktop()
    {
        var desktop=CreateDesktop("SunEyeVision-Startup-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"隐藏桌面不可用");
        try
        {
            var app=new App();app.InitializeComponent();bool sawMain=false,closed=false;Exception? failure=null;
            app.DispatcherUnhandledException+=(_,e)=>{failure=e.Exception;e.Handled=true;app.Shutdown(2);};
            using var watchdog=new System.Threading.Timer(_=>{Console.Error.WriteLine("full-startup: watchdog expired");Environment.Exit(3);},null,TimeSpan.FromSeconds(75),Timeout.InfiniteTimeSpan);
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
            timer.Tick+=(_,_)=>
            {
                if(!sawMain&&app.MainWindow is MainWindow main&&main.IsVisible&&main.IsLoaded)
                {
                    sawMain=true;Console.WriteLine("full-startup: real App.OnStartup initialized and MainWindow loaded");
                    main.Closed+=(_,_)=>{closed=true;Console.WriteLine("full-startup: MainWindow closed after drain");};
                    main.Close();
                }
            };
            timer.Start();var code=app.Run();timer.Stop();
            if(failure is not null)throw new Exception("完整启动发生 Dispatcher 异常",failure);
            if(!sawMain||!closed)throw new Exception("完整启动未到达主窗口加载并关闭。");
            Console.WriteLine("full-startup: application Run exited "+code);return code;
        }
        finally{CloseDesktop(desktop);}
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
        var locations=new ApplicationDataLocationService(Path.Combine(root,"locator.json"));
        var paths=locations.Prepare(root);locations.Remember(paths);
        typeof(App).GetField("_dataLocations",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,locations);
        typeof(App).GetField("_dataPaths",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,paths);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,admission.Runtime);
        Console.WriteLine("probe: services initialized");
        var theme=new ThemeManager(() => app.StartPreferences.Theme, value => app.StartPreferences.Theme=value);theme.Initialize();ServiceLocator.Instance.Register(theme);
        var session=scenario=="empty"?null:ServiceInitializer.SolutionManager.CreateNew("主窗生命周期定位");
        var file=Path.Combine(root,"main.solution");
        if(session is not null)session.SaveAs(file,Path.Combine(root,"main.recovery"),root,new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new System.Text.Json.JsonSerializerOptions(),scope=>new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(scope));
        var main=new MainWindow(admission){WindowState=WindowState.Normal,ShowActivated=false,ShowInTaskbar=false,Left=-20000,Top=-20000,Width=800,Height=600};
        Console.WriteLine("probe: main window constructed");
        if(scenario=="empty"&&(((MainWindowViewModel)main.DataContext).WorkflowTabViewModel.Tabs.Count!=0||((MainWindowViewModel)main.DataContext).RunWorkflowCommand.CanExecute(null)))throw new Exception("无方案主窗产生了可运行的空模型。");
        app.MainWindow=main;
        if(scenario=="cancel"){var graph=session!.PrepareGraphEdit(session.GetWorkflows().Single().Id);session.CreateNode(graph,"ImageLoad",new(100,100));session.CommitGraphEdit(graph,"触发未保存修改");}
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
        timer.Start();Console.WriteLine("probe: showing window");main.Show();if(failing is not null){failing.Owner=main;failing.Show();}Console.WriteLine("probe: window shown");main.Dispatcher.BeginInvoke(new Action(async ()=>{try{if(scenario is "formal" or "debug")await VerifyFormalMain(main,session!,root,scenario=="debug");if(scenario=="preferences")VerifyPreferenceRegistration(app,session!,paths,root);Console.WriteLine("probe: calling Close");main.Close();Console.WriteLine("probe: Close returned");}catch(Exception error){dispatchFailure=error;timer.Stop();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}}));
        Console.WriteLine("probe: starting dispatcher");
        Dispatcher.Run();
        if(dispatchFailure is not null&&scenario!="throw")throw new Exception("Dispatcher failure.",dispatchFailure);
        if(scenario=="throw"&&(dispatchFailure is null||!sawFailure||!closed))throw new Exception("Child exception did not recover through a guarded retry.");
        if(scenario is "normal" or "formal" or "debug" or "empty" or "preferences"&&!closed)throw new Exception("Normal window did not close.");
        if(scenario=="wait"&&(!sawTimeout||!closed))throw new Exception("Reader timeout/release path did not close.");
        if(scenario=="cancel"&&!sawCancel)throw new Exception("Cancel prompt was not reached.");
        if(scenario=="failure"&&(!sawFailure||!closed))throw new Exception("Cleanup refusal did not recover through a guarded retry.");
        if(closed&&(admission.Runtime.IsActive||manager.RuntimeDirectory is not null))
            throw new Exception("A closed window left the plugin generation admitted.");
        Console.WriteLine("Actual MainWindow shutdown scenario passed: "+scenario);
        return 0;
    }
    private static void VerifyPreferenceRegistration(App app,SunEyeVision.Application.DocumentSession session,ApplicationDataPaths paths,string root)
    {
        var locator=Path.Combine(root,"locator.json");var backup=Path.Combine(root,"locator.saved.json");
        using var opening=System.Text.Json.JsonDocument.Parse(File.ReadAllText(paths.MarkerFile));var id=opening.RootElement.GetProperty("dataId").GetString();
        File.Move(locator,backup);Directory.CreateDirectory(locator);
        try
        {
            var failed=false;try{app.SetStartPageHidden(true);}catch(IOException error){failed=error.Message.Contains("已写入");}
            using var written=System.Text.Json.JsonDocument.Parse(File.ReadAllText(paths.ConfigFile));
            using var marker=System.Text.Json.JsonDocument.Parse(File.ReadAllText(paths.MarkerFile));
            if(!failed||!app.StartPreferences.HideStartPage||!written.RootElement.GetProperty("HideStartPage").GetBoolean()||marker.RootElement.GetProperty("dataId").GetString()!=id||session.HasUnsavedChanges)throw new Exception("偏好已写、目录登记失败的状态被错误回退或污染方案。");
        }
        finally{Directory.Delete(locator);File.Move(backup,locator);}
        app.SetStartPageHidden(false);
        if(new ApplicationDataLocationService(locator).Locate()?.Root!=paths.Root||session.HasUnsavedChanges)throw new Exception("同一隔离locator重试未成功，或污染方案保存状态。");
        Console.WriteLine("preferences: actual locator retained; written preferences survive registration failure; dataId and document clean state unchanged; retry accepted");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject
    {
        if(parent is T match)yield return match;
        for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
            foreach(var nested in Descendants<T>(System.Windows.Media.VisualTreeHelper.GetChild(parent,i)))yield return nested;
    }
    private static async Task VerifyFormalMain(MainWindow main,SunEyeVision.Application.DocumentSession session,string root,bool verifyDebug=false)
    {
        var vm=(MainWindowViewModel)main.DataContext;
        var flow=session.GetWorkflows().Single();
        var graph=session.PrepareGraphEdit(flow.Id);var node=session.CreateNode(graph,"ImageLoad",new(150,120));session.CommitGraphEdit(graph,"创建真实图像节点");
        var source=Path.Combine(root,"source.pgm");File.WriteAllBytes(source,System.Text.Encoding.ASCII.GetBytes("P5\n2 2\n255\n").Concat(new byte[]{0,64,128,255}).ToArray());
        {
            var draft=session.Editing.Host.BeginEdit(flow.Id,node.Id);
            draft.ApplyFields(new Dictionary<string,SunEyeVision.Plugin.Contracts.ParameterSetting>{["local.paths"]=new SunEyeVision.Plugin.Contracts.ConstantSetting(new[]{source}),["local.start"]=new SunEyeVision.Plugin.Contracts.ConstantSetting(0L)});
            session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{draft}));
        }
        vm.SelectedNode=node;
        if(!ReferenceEquals(vm.WorkflowTabViewModel.SelectedTab!.DocumentCanvas!.Nodes.Single().Definition,node))throw new Exception("画布没有持有实际节点。");
        vm.OpenDebugWindowCommand.Execute(node);
        await main.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var editor=System.Windows.Application.Current.Windows.OfType<SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow>().Single();
        if(!editor.Editor.IsLoaded||editor.Editor.Content is null)throw new Exception("真实主窗参数页未加载。");
        var settings=Descendants<SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView>(editor.Editor).Single();
        if(!settings.TrySetFields(new Dictionary<string,SunEyeVision.Plugin.Contracts.ParameterSetting>{{"sequence.initial",new SunEyeVision.Plugin.Contracts.ConstantSetting(9L)}}))throw new Exception("真实主窗SN字段未接受私有输入。");
        if(verifyDebug)
        {
            if(!editor.ExecuteOnce.CanExecute(null))throw new Exception("实际主窗原底栏单次调试未启用。");
            editor.ExecuteOnce.Execute(null);
            var deadline=DateTime.UtcNow.AddSeconds(15);
            while(editor.State.IsRunning){if(DateTime.UtcNow>deadline)throw new TimeoutException("实际参数窗调试未排空。");await Task.Delay(5);}
            var privateResult=(SunEyeVision.Plugin.Runtime.DeclaredOutputSnapshot?)editor.Editor.GetType().GetProperty("CurrentDebugSnapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(editor.Editor);
            if(privateResult is not {IsSuccess:true}||privateResult.Identity.DocumentSessionId==session.ResultOwnerId||session.AcquirePublishedResult(flow.Id,node.Id) is not null||vm.FlatResultRows.Count==0)
                throw new Exception("实际参数窗私有调试结果没有通过原结果区呈现，或污染了正式发布。");
            Console.WriteLine("debug: actual MainWindow -> original bottom command -> stable Session debug -> HALCON -> private result table; formal result remains absent");
        }
        session.Editing.Apply();editor.Close();
        Console.WriteLine("formal: actual session -> canvas selection -> actual parameter window -> receipt -> close");
        var run=(Task)typeof(MainWindowViewModel).GetMethod("ExecuteRunWorkflow",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,null)!;await run;
        using(var reader=session.AcquirePublishedResult(flow.Id,node.Id))
        {
            if(reader is null||!reader.Snapshot.IsSuccess)throw new Exception("正式主窗运行没有实际成功发布："+vm.StatusText);
            var output=reader.Snapshot.Schema.Outputs.Single(port=>port.Id=="output.image");
            var reference=new SunEyeVision.Plugin.Contracts.OutputReference(node.Id,output.Id,output.Type,new[]{"data"},scopeId:flow.Id,storage:output.Storage);
            typeof(MainWindowViewModel).GetMethod("SetFormalDisplaySelection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object?[]{node,SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(reference)});
        }
        if(!session.HasUnsavedChanges)throw new Exception("主窗编辑和显示导航未进入实际保存状态。");
        var saved=(bool)typeof(MainWindowViewModel).GetMethod("SaveCurrentDocumentWithPrompt",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object?[]{main,false})!;
        if(!saved||session.HasUnsavedChanges)throw new Exception("主窗正式保存未完成。");
        var copy=Path.Combine(root,"copy.solution");session.SaveCopy(copy,Path.Combine(root,"copy.recovery"),root,new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new System.Text.Json.JsonSerializerOptions(),scope=>new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(scope));
        var cap=session.AdmitPresentation(flow.Id,node.Id);
        var opened=(Task<bool>)typeof(MainWindowViewModel).GetMethod("OpenExternalSolutionAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object[]{copy,false})!;
        if(!await opened)throw new Exception("主窗打开新文件未成功："+vm.StatusText);
        var current=ServiceInitializer.SolutionManager.CurrentSession!;
        if(ReferenceEquals(current,session)||cap.IsCurrent||vm.SelectedNode is not null||ReferenceEquals(current.FindNode(flow.Id,node.Id),node))throw new Exception("切换没有退役旧会话/节点/呈现准入。");
        if(current.HasUnsavedChanges)throw new Exception("重新打开的正式文件错误标脏。");
        Console.WriteLine("formal: actual host run -> leased published result -> display navigation -> save -> reopen -> old admission retired");
    }

}

internal sealed class ProbeApp:App
{
    // The probe explicitly assembles the same services; do not start a second product bootstrap.
    protected override void OnStartup(StartupEventArgs e) { }
}

