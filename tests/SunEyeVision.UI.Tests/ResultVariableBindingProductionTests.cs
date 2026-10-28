using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
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
        if(Application.Current is null)_=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            Application.Current.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
        var host=typeof(ResultVariableBindingProductionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item=>item.Key=="HostDebugRoot").Value!;
        var prior=AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        Window? owner=null;ImageResultControl? view=null;MainWindowViewModel? main=null;
        try
        {
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(Path.GetFullPath(host))+Path.DirectorySeparatorChar);
            Assert.True(PluginLoader.LoadPlugins(Path.Combine(host,"plugins")));
            var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"result-variable-binding-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(directory);
            var source=new SunEyeVision.UI.Shared.WorkflowNode("source","图像源","图像源","ImageLoad"){RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad")};
            var root=new SunEyeVision.Workflow.Workflow("root","正式根流程");root.Nodes.Add(source);
            var variable=new GlobalVariable("宽度",0L,"Integer"){Id="width"};
            var document=new Solution{Workflows=new(){root}};document.GlobalVariables.Add(variable);
            var manager=ServiceInitializer.SolutionManager;manager.SetCurrentSolution(document,false);
            main=new MainWindowViewModel();main.SelectedNode=source;
            view=new ImageResultControl{DataContext=main.ImageResult};view.Initialize(main);
            ((TabControl)view.FindName("ImageDisplayTabControl")).SelectedIndex=1;
            owner=new Window{Content=view,Width=900,Height=700,Left=-10000,ShowActivated=false};owner.Show();Pump();
            var tree=Assert.IsType<PropertyListView>(view.FindName("ModuleResultTreeView"));
            var session=manager.Session!;var initial=session.History.Position;
            var publication=Assert.IsAssignableFrom<SunEyeVision.Plugin.Wpf.Presenters.Results.IResultPublication>(main.CurrentResultPublication);
            var row=Assert.Single(main.FlatResultRows.Where(item=>item.SourcePropertyName=="output.width"));
            Assert.True(row.IsBindable);Assert.True(tree.BindVariableCommand!.CanExecute(row));
            var verifyBind=SelectVariableInNextDialog(variable);tree.BindVariableCommand.Execute(row);verifyBind();Pump();
            Assert.Equal(initial+1,session.History.Position);Assert.Same(publication,main.CurrentResultPublication);
            Assert.Equal("宽度",row.BindingDisplayName);AssertUnlinkVisible(tree,row);
            var expression=variable.ProducerBinding!.ReferenceExpression;
            var reference=BindingReferenceSerializer.Deserialize(expression);
            Assert.Equal(root.Id,reference.ScopeId);Assert.Equal(source.Id,reference.NodeId);Assert.Equal("output.width",reference.PortId);
            Assert.Equal(source.RuntimeConfiguration.Schema.Outputs.Single(port=>port.Id=="output.width").Type,reference.Type);
            Assert.Empty(reference.MemberFieldPath);
            main.UndoCommand.Execute(null);Pump();Assert.Null(variable.ProducerBinding);Assert.Null(row.BindingDisplayName);
            Assert.Same(publication,main.CurrentResultPublication);Assert.Equal(initial,session.History.Position);
            main.RedoCommand.Execute(null);Pump();Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);Assert.Equal("宽度",row.BindingDisplayName);
            var file=Path.Combine(directory,"accepted.solution");var saved=manager.SaveCurrentDocument(file);Assert.True(saved.FileWritten,saved.Error);
            var reopened=new SolutionRepository().Load(file)!;
            Assert.Equal(expression,Assert.Single(reopened.GlobalVariables).ProducerBinding!.ReferenceExpression);
            Assert.True(tree.UnbindVariableCommand!.CanExecute(row));
            var verifyUnbind=SelectVariableInNextDialog(variable);tree.UnbindVariableCommand.Execute(row);verifyUnbind();Pump();
            Assert.Null(variable.ProducerBinding);Assert.Null(row.BindingDisplayName);Assert.Equal(initial+2,session.History.Position);
            main.UndoCommand.Execute(null);Pump();Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);Assert.Equal("宽度",row.BindingDisplayName);
            var before=session.History.Position;
            var verifyCancel=SelectVariableInNextDialog(variable,cancel:true);tree.BindVariableCommand.Execute(row);verifyCancel();Pump();
            Assert.Equal(before,session.History.Position);Assert.Equal(expression,variable.ProducerBinding!.ReferenceExpression);
            var verifyStale=SelectVariableInNextDialog(variable,beforeAccept:()=>main.CloseResultPresentation());tree.BindVariableCommand.Execute(row);verifyStale();Pump();
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
        Exception? failure=null;var invoked=false;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
        {
            GlobalVariableSelectDialog? dialog=null;
            try
            {
                invoked=true;dialog=Application.Current.Windows.OfType<GlobalVariableSelectDialog>().Single();
                if(cancel){dialog.Close();return;}
                var picker=Assert.IsType<GlobalVariableSelectViewModel>(dialog.DataContext);
                Assert.Contains(variable,picker.FilteredVariables);picker.SelectedVariable=variable;
                beforeAccept?.Invoke();Assert.True(picker.AcceptCommand.CanExecute(null));picker.AcceptCommand.Execute(null);
            }
            catch(Exception error){failure=error;dialog?.Close();}
        }));
        return ()=>{Assert.True(invoked,"正式变量选择窗口未进入模态消息循环");if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();};
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
    private static void Sta(Action action)
    {
        Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception error){failure=error;}}){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(60)),"真实结果绑定生产链超时");
        if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
