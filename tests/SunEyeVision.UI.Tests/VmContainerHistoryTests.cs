using System.Collections.ObjectModel;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmContainerHistoryTests
{
    private static WorkflowConnection Edge(string a,string b)=>new(){Id=Guid.NewGuid().ToString("N"),SourceNodeId=a,TargetNodeId=b};
    [Fact]
    public void Returning_to_actual_container_restores_history_without_executing_another_container_command()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var tab=new WorkflowTabViewModel();var rootNodes=new ObservableCollection<WorkflowNodeBase>();var rootEdges=new ObservableCollection<WorkflowConnection>();
            var childNodes=new ObservableCollection<WorkflowNodeBase>();var childEdges=new ObservableCollection<WorkflowConnection>();
            tab.SetContent(rootNodes,rootEdges);var rootHistory=tab.CommandManager;var rootEdge=Edge("a","b");rootHistory.Execute(new AddConnectionCommand(rootEdges,rootEdge));
            tab.SetContent(childNodes,childEdges);var childHistory=tab.CommandManager;var childEdge=Edge("x","y");childHistory.Execute(new AddConnectionCommand(childEdges,childEdge));
            tab.SetContent(rootNodes,rootEdges);Assert.Same(rootHistory,tab.CommandManager);tab.CommandManager.Undo();Assert.Empty(rootEdges);Assert.Same(childEdge,Assert.Single(childEdges));
            tab.SetContent(childNodes,childEdges);Assert.Same(childHistory,tab.CommandManager);tab.CommandManager.Undo();Assert.Empty(childEdges);
            tab.SetContent(rootNodes,rootEdges);tab.CommandManager.Redo();Assert.Same(rootEdge,Assert.Single(rootEdges));
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }
    [Fact]
    public void Content_event_observes_both_new_collections_and_same_binding_does_not_clear_history()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var tab=new WorkflowTabViewModel();var nodes=new ObservableCollection<WorkflowNodeBase>();var edges=new ObservableCollection<WorkflowConnection>();var count=0;
            tab.WorkflowContentChanged+=(_,_)=>{count++;Assert.Same(nodes,tab.WorkflowNodes);Assert.Same(edges,tab.WorkflowConnections);};
            tab.SetContent(nodes,edges);var history=tab.CommandManager;history.Execute(new AddConnectionCommand(edges,Edge("a","b")));tab.SetContent(nodes,edges);
            Assert.Equal(1,count);Assert.Same(history,tab.CommandManager);Assert.True(history.CanUndo);
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }
    [Fact]
    public void Mini_map_replaces_same_id_subscription_and_unload_detaches_old_node()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var shell=new SunEyeVision.UI.Shell.Controls.Shell.ShellWorkspaceControl();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var subscribe=shell.GetType().GetMethod("SubscribeMiniMapNodes",flags)!;
            var first=new WorkflowNode{Id="same",Position=new System.Windows.Point(1,2)};
            var second=new WorkflowNode{Id="same",Position=new System.Windows.Point(3,4)};
            var timer=(System.Windows.Threading.DispatcherTimer)shell.GetType().GetField("_miniMapThrottleTimer",flags)!.GetValue(shell)!;
            subscribe.Invoke(shell,new object[]{new WorkflowNodeBase[]{first}});subscribe.Invoke(shell,new object[]{new WorkflowNodeBase[]{second}});
            timer.Stop();first.Position=new System.Windows.Point(5,6);Assert.False(timer.IsEnabled);
            second.Position=new System.Windows.Point(7,8);Assert.True(timer.IsEnabled);timer.Stop();
            shell.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
            second.Position=new System.Windows.Point(9,10);Assert.False(timer.IsEnabled);
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }

}