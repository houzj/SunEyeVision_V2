using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using Xunit;
namespace SunEyeVision.UI.Tests;

internal static class UiRuntime
{
    private static PluginManager? _plugins;
    private static IDisposable? _desktop;
    internal static string ProductRoot => Path.GetFullPath(typeof(UiRuntime).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item=>item.Key=="HostDebugRoot").Value!);
    internal static string Cases => Path.GetFullPath(Path.Combine(ProductRoot,"..","..","_validation","UiSessionTests"));
    internal static DesktopToolAdmission Admission
    {
        get
        {
            System.Windows.Application.Current.Dispatcher.VerifyAccess();
            if(_plugins is null)
            {
                AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",ProductRoot);
                _plugins=new PluginManager();_plugins.LoadPluginsAsync().GetAwaiter().GetResult();
                if(_plugins.LastAdmissionError is {} error)throw new InvalidOperationException(error);
                _desktop=_plugins.PinDesktopHost();
                var paths=ApplicationDataPaths.Open(Path.Combine(Cases,"Data"));
                ServiceInitializer.ConfigureDataPaths(paths);ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,_plugins.DesktopAdmission!.Runtime);
            }
            return _plugins.DesktopAdmission??throw new InvalidOperationException("本树正式桌面目录未准入。");
        }
    }
    internal static void Run(Action action)=>SunEyeVision.TestShared.WpfTestDispatcher.Invoke(action);
    internal static void Pump(DispatcherPriority priority=DispatcherPriority.ApplicationIdle)
    {
        var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(priority,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
    }
    internal static void Await(Task task)
    {
        Await(task,DispatcherPriority.ApplicationIdle);
    }
    internal static void Await(Task task,DispatcherPriority priority)
    {
        var deadline=DateTime.UtcNow.AddSeconds(35);
        while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new TimeoutException("实际UI异步操作未排空。");Pump(priority);Thread.Sleep(1);}
        task.GetAwaiter().GetResult();Pump();
    }
    internal static void AwaitEditorReady(SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow window)
    {
        Await(window.AssemblyReadyTask,DispatcherPriority.SystemIdle);
        var deadline=DateTime.UtcNow.AddSeconds(35);
        while(!window.Editor.CanDebug)
        {
            if(DateTime.UtcNow>deadline)throw new TimeoutException("实际参数页未进入可调试状态。");
            Pump(DispatcherPriority.SystemIdle);Thread.Sleep(1);
        }
    }
    internal static object? Call(object target,string method,params object?[] args)
    {
        try{return target.GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Single(item=>item.Name==method&&item.GetParameters().Length==args.Length).Invoke(target,args);}
        catch(TargetInvocationException error) when(error.InnerException is not null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
    }
    internal static T? Field<T>(object target,string name)=>(T?)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    internal static IEnumerable<T> Visuals<T>(DependencyObject parent) where T:DependencyObject
    {
        if(parent is T match)yield return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var nested in Visuals<T>(VisualTreeHelper.GetChild(parent,i)))yield return nested;
    }
}

/// <summary>Real session and original WPF modules. Full MainWindow exit is exercised by the separate real-process probe.</summary>
internal sealed class DocumentUiFixture:IDisposable
{
    internal readonly DocumentSession Session;
    internal readonly WorkflowDefinition Root;
    internal readonly MainWindowViewModel Main;
    internal readonly ShellWorkspaceControl Workspace;
    internal readonly ImageResultControl Results;
    internal readonly Window Host;
    internal readonly string DirectoryPath;
    internal WorkflowTabViewModel Tab=>Main.WorkflowTabViewModel.SelectedTab!;
    internal DocumentCanvasProjection Canvas=>Tab.DocumentCanvas!;
    internal WorkflowCanvasControl View=>Workspace.GetCurrentWorkflowCanvas()??throw new InvalidOperationException("真实画布未加载。");
    internal DocumentUiFixture()
    {
        var admission=UiRuntime.Admission;
        DirectoryPath=Path.Combine(UiRuntime.Cases,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);
        Session=ServiceInitializer.SolutionManager.CreateNew("实际UI会话",discardPendingEdits:true);Root=Session.GetWorkflows().Single();
        Main=new MainWindowViewModel(admission);
        Workspace=new ShellWorkspaceControl();Results=new ImageResultControl();
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        grid.Children.Add(Workspace);Grid.SetColumn(Results,1);grid.Children.Add(Results);
        Host=new Window{Content=grid,DataContext=Main,Width=1100,Height=650,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};
        System.Windows.Application.Current.MainWindow=Host;Main.SolutionDialogOwner=Host;Workspace.DataContext=Main.Workspace;Results.DataContext=Main.ImageResult;
        Workspace.Initialize(Main);Results.Initialize(Main);Host.Show();UiRuntime.Pump();Host.UpdateLayout();
    }
    internal NodeDefinition Add(string tool="ImageLoad",double x=150,double y=120)
    {
        var node=Canvas.CreateNode(tool,new(x,y));UiRuntime.Pump();return node;
    }
    internal CanvasNodeProjection Projection(NodeDefinition node)=>Canvas.Nodes.Single(item=>ReferenceEquals(item.Definition,node));
    internal CanvasConnectionProjection Connect(NodeDefinition source,NodeDefinition target,string from="BottomPort",string to="TopPort")
    {
        Canvas.Connect(new(Guid.NewGuid().ToString("N"),source.Id,target.Id,from,to));UiRuntime.Pump();return Canvas.Connections.Single(item=>item.SourceNodeId==source.Id&&item.TargetNodeId==target.Id);
    }
    internal void Fields(NodeDefinition node,params (string Id,ParameterSetting Value)[] changes)
    {
        var draft=Session.Editing.Host.BeginEdit(Root.Id,node.Id);draft.ApplyFields(changes.ToDictionary(item=>item.Id,item=>item.Value));Session.CommitEdit(DocumentEditProposal.Prepare(Session.Editing.Host,nodes:new[]{draft}));UiRuntime.Pump();
    }
    internal string Image(byte gray=51,int side=2)
    {
        var file=Path.Combine(DirectoryPath,Guid.NewGuid().ToString("N")+".pgm");
        var header=System.Text.Encoding.ASCII.GetBytes($"P5\n{side} {side}\n255\n");
        File.WriteAllBytes(file,header.Concat(Enumerable.Repeat(gray,side*side)).ToArray());
        return file;
    }
    internal void ConfigureImage(NodeDefinition node,params string[] files)=>Fields(node,("local.paths",new ConstantSetting(files)),("local.start",new ConstantSetting(0L)));
    internal void Run()
    {
        var task=(Task)UiRuntime.Call(Main,"ExecuteRunWorkflow")!;UiRuntime.Await(task);
    }
    internal void Save(string? name=null)
    {
        var file=Path.Combine(DirectoryPath,name??"document.solution");
        Session.SaveAs(file,Path.Combine(DirectoryPath,"document.recovery"),DirectoryPath,VariableVocabularyCapture.FromCandidate(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{GlobalVariables=Session.Document.GlobalVariables.Select(variable=>new{variable.Type}).ToArray()})),new System.Text.Json.JsonSerializerOptions(),scope=>new ComplexVariableValueCodec(scope),File.Exists(file)?SunEyeVision.Persistence.TargetExpectation.Match(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))):SunEyeVision.Persistence.TargetExpectation.Missing);UiRuntime.Pump();
    }
    public void Dispose()
    {
        foreach(var editor in System.Windows.Application.Current.Windows.OfType<SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow>().ToArray())UiRuntime.Await(editor.CloseAndDrainAsync());
        Results.Shutdown();Main.ReleasePluginConsumersForShutdown();
        // 与生产关闭链同序：先排空持有活动租约的通讯/相机运行时，ReleaseDocumentHost 才能关闭会话。
        UiRuntime.Await(ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask());
        UiRuntime.Await(ServiceInitializer.DisposeCameraRuntimeAsync().AsTask());
        Main.ReleaseDocumentHost();
        Host.Content=null;Host.Close();ServiceInitializer.SolutionManager.CloseCurrent(discardPendingEdits:true);UiRuntime.Pump();
    }
}
