using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class ResultVariableBindingProductionTests
{
    [Fact]
    public void Real_runtime_result_binding_unbinding_cancel_history_and_save_reopen_share_one_formal_endpoint()=>Sta(()=>
    {
        Stage("创建同dispatcher Application与主题");
        if(Application.Current is null)_=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            Application.Current.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
        var host=typeof(ResultVariableBindingProductionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item=>item.Key=="HostDebugRoot").Value!;
        var prior=AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        Window? owner=null;ImageResultControl? view=null;MainWindowViewModel? main=null;
        try
        {
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(Path.GetFullPath(host))+Path.DirectorySeparatorChar);
            Stage("加载正式工具组件");
            Assert.True(PluginLoader.LoadPlugins(Path.Combine(host,"plugins")));
            var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"result-variable-binding-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);Stage("初始化正式宿主服务");
            ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(directory);
            Stage("创建正式文档与图像源配置");
            var source=new SunEyeVision.UI.Shared.WorkflowNode("source","图像源","图像源","ImageLoad"){RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad")};
            var root=new SunEyeVision.Workflow.Workflow("root","正式根流程");root.Nodes.Add(source);
            var variable=new GlobalVariable("宽度",0L,"Integer"){Id="width"};
            // Node IDs are local to a workflow. Another root with the same local node ID
            // must neither throw during binding lookup nor acquire this root's producer.
            var other=new SunEyeVision.Workflow.Workflow("other-root","另一正式根流程");
            other.Nodes.Add(new SunEyeVision.UI.Shared.WorkflowNode("source","另一个图像源","另一个图像源","ImageLoad")
                {RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad")});
            var document=new Solution{Workflows=new(){root,other}};document.GlobalVariables.Add(variable);
            var manager=ServiceInitializer.SolutionManager;manager.SetCurrentSolution(document,false);
            Stage("构造真实MainWindowViewModel");main=new MainWindowViewModel();
            Stage("选择真实图像源节点");main.SelectedNode=source;
            view=new ImageResultControl{DataContext=main.ImageResult};view.Initialize(main);
            ((TabControl)view.FindName("ImageDisplayTabControl")).SelectedIndex=1;
            Stage("加载真实结果工作区窗口");owner=new Window{Content=view,Width=900,Height=700,Left=-10000,ShowActivated=false};owner.Show();Pump();
            var tree=Assert.IsType<PropertyListView>(view.FindName("ModuleResultTreeView"));
            var session=manager.Session!;var initial=session.History.Position;
            var publication=Assert.IsAssignableFrom<SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication>(main.CurrentResultPublication);
            var row=Assert.Single(main.FlatResultRows.Where(item=>item.SourcePropertyName=="output.width"));
            Assert.True(row.IsBindable);Assert.True(tree.BindVariableCommand!.CanExecute(row));
            Stage("首次绑定选择与正式接纳");var verifyBind=SelectVariableInNextDialog(variable);tree.BindVariableCommand.Execute(row);verifyBind();Pump();
            Assert.Equal(initial+1,session.History.Position);Assert.Same(publication,main.CurrentResultPublication);
            Assert.Equal("宽度",row.BindingDisplayName);AssertUnlinkVisible(tree,row);
            var expression=variable.ProducerBinding!.ReferenceExpression;
            var reference=BindingReferenceSerializer.Deserialize(expression);
            Assert.Equal(root.Id,reference.ScopeId);Assert.Equal(source.Id,reference.NodeId);Assert.Equal("output.width",reference.PortId);
            Assert.Equal(source.RuntimeConfiguration.Schema.Outputs.Single(port=>port.Id=="output.width").Type,reference.Type);
            Assert.Empty(reference.MemberFieldPath);
            Stage("绑定Undo/Redo同publication");main.UndoCommand.Execute(null);Pump();Assert.Null(variable.ProducerBinding);Assert.Null(row.BindingDisplayName);
            Assert.Same(publication,main.CurrentResultPublication);Assert.Equal(initial,session.History.Position);
            main.RedoCommand.Execute(null);Pump();Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);Assert.Equal("宽度",row.BindingDisplayName);
            Stage("保存与正式重开");var file=Path.Combine(directory,"accepted.solution");var saved=manager.SaveCurrentDocument(file);Assert.True(saved.FileWritten,saved.Error);
            var reopened=new SolutionRepository().Load(file)!;
            Assert.Equal(expression,Assert.Single(reopened.GlobalVariables).ProducerBinding!.ReferenceExpression);
            Assert.True(tree.UnbindVariableCommand!.CanExecute(row));
            Stage("正式解绑与Undo");var verifyUnbind=SelectVariableInNextDialog(variable);tree.UnbindVariableCommand.Execute(row);verifyUnbind();Pump();
            Assert.Null(variable.ProducerBinding);Assert.Null(row.BindingDisplayName);Assert.Equal(initial+2,session.History.Position);
            main.UndoCommand.Execute(null);Pump();Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);Assert.Equal("宽度",row.BindingDisplayName);
            var before=session.History.Position;
            Stage("取消选择");var verifyCancel=SelectVariableInNextDialog(variable,cancel:true);tree.BindVariableCommand.Execute(row);verifyCancel();Pump();
            Assert.Equal(before,session.History.Position);Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);
            Stage("模态期间publication退出拒绝");var verifyStale=SelectVariableInNextDialog(variable,beforeAccept:()=>main.CloseResultPresentation());tree.BindVariableCommand.Execute(row);verifyStale();Pump();
            Assert.Equal(before,session.History.Position);Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);
            Assert.False(main.IsCurrentResultRow(row));Assert.False(tree.BindVariableCommand.CanExecute(row));
            var bad=new ProducerBinding{ReferenceExpression=BindingReferenceSerializer.Serialize(new(source.Id,"missing",reference.Type,scopeId:root.Id))};
            Assert.False(main.TryBindResultVariable(session,bad,variable.Id,out var error));Assert.NotEmpty(error);
            Assert.Equal(before,session.History.Position);Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);
        }
        finally
        {
            view?.Shutdown();if(owner is not null){owner.Content=null;owner.Close();}
            main?.CloseResultPresentation();AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",prior);
        }
    });

    private static Action SelectVariableInNextDialog(GlobalVariable variable,bool cancel=false,Action? beforeAccept=null)
    {
        Exception? failure=null;var invoked=false;var expected=Application.Current.Windows.Cast<Window>().ToHashSet();
        var watch=Stopwatch.StartNew();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(25)};
        timer.Tick+=(_,_)=>
        {
            var windows=Application.Current.Windows.Cast<Window>().Where(window=>!expected.Contains(window)&&window.IsVisible).ToArray();
            try
            {
                var unexpected=windows.FirstOrDefault(window=>window is not GlobalVariableSelectDialog);
                if(unexpected is not null)
                {
                    failure=new InvalidOperationException("正式操作出现非预期对话框："+unexpected.GetType().Name+" / "+unexpected.Title+" / "+string.Join(" | ",Texts(unexpected)));
                    unexpected.Close();return;
                }
                if(!invoked&&windows.OfType<GlobalVariableSelectDialog>().FirstOrDefault(dialog=>dialog.IsLoaded) is {} dialog)
                {
                    invoked=true;
                    if(cancel){dialog.Close();return;}
                    var picker=Assert.IsType<GlobalVariableSelectViewModel>(dialog.DataContext);
                    Assert.Contains(variable,picker.FilteredVariables);picker.SelectedVariable=variable;
                    beforeAccept?.Invoke();Assert.True(picker.AcceptCommand.CanExecute(null));picker.AcceptCommand.Execute(null);
                }
                if(watch.Elapsed>TimeSpan.FromSeconds(10))
                {
                    failure=new TimeoutException("模态阶段未完成："+_stage+"；窗口："+string.Join(",",windows.Select(window=>window.GetType().Name+":"+window.Title)));
                    foreach(var window in windows)window.Close();timer.Stop();
                }
            }
            catch(Exception error){failure=error;foreach(var window in windows)window.Close();timer.Stop();}
        };
        timer.Start();
        return ()=>{timer.Stop();if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();Assert.True(invoked,"正式变量选择窗口未进入可加载状态");};
    }
    private static IEnumerable<string> Texts(DependencyObject parent)
    {
        if(parent is TextBlock text&&!string.IsNullOrWhiteSpace(text.Text))yield return text.Text;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)
            foreach(var value in Texts(VisualTreeHelper.GetChild(parent,index)))yield return value;
    }
    private static void AssertUnlinkVisible(PropertyListView view,FlatResultRow row)
    {
        var tree=Assert.IsType<TreeView>(view.FindName("resultTreeView"));tree.UpdateLayout();Pump();
        var root=Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromIndex(0));root.UpdateLayout();
        var item=Assert.IsType<TreeViewItem>(root.ItemContainerGenerator.ContainerFromItem(row));item.ApplyTemplate();
        var unlink=Assert.IsType<Button>(item.Template.FindName("btnDel",item));
        Assert.Equal(Visibility.Visible,unlink.Visibility);Assert.True(unlink.IsEnabled);
        Assert.Same(row,unlink.CommandParameter);Assert.Same(view.UnbindVariableCommand,unlink.Command);
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static volatile string _stage="尚未进入生产链";
    private static void Stage(string stage)=>_stage=stage;
    private static void Sta(Action action)
    {
        // Reuse the existing persistent real WPF dispatcher. An Application must not be
        // abandoned on a per-test thread while later controls resolve its dynamic resources.
        var operation=Task.Run(()=>ImageThresholdProductionEditorTests.Sta(action));
        try{operation.WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();}
        catch(TimeoutException error){throw new TimeoutException("真实结果绑定生产链超时；最后阶段："+_stage,error);}
    }
}
