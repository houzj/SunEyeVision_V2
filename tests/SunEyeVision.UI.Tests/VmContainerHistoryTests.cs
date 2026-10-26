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

    [Fact]
    public void Containers_restore_independent_scale_and_canvas_origin_and_navigation_restores_parent_metadata()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var tab=new WorkflowTabViewModel{Id="root",Name="根流程"};
            var rootNodes=new ObservableCollection<WorkflowNodeBase>();var rootEdges=new ObservableCollection<WorkflowConnection>();
            var childNodes=new ObservableCollection<WorkflowNodeBase>();var childEdges=new ObservableCollection<WorkflowConnection>();
            tab.SetContent(rootNodes,rootEdges);tab.CurrentScale=.5;tab.CaptureViewport(125,75);
            tab.NavigateToSubroutine("child","外层Group","outer-icon","owner-a");tab.SetContent(childNodes,childEdges);
            Assert.Equal(1,tab.CurrentScale);Assert.False(tab.HasSavedViewport);
            tab.CurrentScale=1.5;tab.CaptureViewport(300,150);
            tab.NavigateToSubroutine("grand","内层Group","inner-icon","owner-b");
            Assert.Equal(new[]{"根流程","外层Group","内层Group"},tab.GetNavigationPath());Assert.Equal(new[]{"child","root"},tab.NavigationAncestorIds);
            Assert.Throws<InvalidOperationException>(()=>tab.NavigateToSubroutine("root","错误",null,"owner-wrong"));
            Assert.True(tab.NavigateBack());Assert.Equal("外层Group",tab.EffectiveDisplayName);Assert.Equal("outer-icon",tab.SubroutineIcon);Assert.Equal("owner-a",tab.CurrentOwnerNodeId);
            Assert.True(tab.NavigateBack());tab.SetContent(rootNodes,rootEdges);
            Assert.Equal(.5,tab.CurrentScale);Assert.Equal(new System.Windows.Point(250,150),tab.ViewportCanvasOrigin);Assert.True(tab.HasSavedViewport);Assert.Null(tab.SubroutineIcon);Assert.Null(tab.CurrentOwnerNodeId);
            tab.NavigateToSubroutine("child","外层Group","outer-icon","owner-a");tab.SetContent(childNodes,childEdges);
            Assert.Equal(1.5,tab.CurrentScale);Assert.Equal(new System.Windows.Point(200,100),tab.ViewportCanvasOrigin);
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }

    [Fact]
    public void Restored_canvas_origin_drives_real_scroll_viewer_at_saved_scale()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var tab=new WorkflowTabViewModel{CurrentScale=.5};tab.CaptureViewport(125,75);
            var viewer=new System.Windows.Controls.ScrollViewer{HorizontalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto,Width=200,Height=100,Content=new System.Windows.Controls.Canvas{Width=5000,Height=5000}};
            viewer.Measure(new System.Windows.Size(200,100));viewer.Arrange(new System.Windows.Rect(0,0,200,100));viewer.UpdateLayout();
            var restore=typeof(SunEyeVision.UI.Shell.Controls.Shell.ShellWorkspaceControl).GetMethod("RestoreSavedViewport",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            restore.Invoke(null,new object[]{tab,viewer});viewer.UpdateLayout();
            Assert.Equal(125,viewer.HorizontalOffset);Assert.Equal(75,viewer.VerticalOffset);
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Nodes,WeakReference Edges) BindAbandonedContainer(WorkflowTabViewModel tab)
    {
        var nodes=new ObservableCollection<WorkflowNodeBase>();var edges=new ObservableCollection<WorkflowConnection>();
        tab.SetContent(nodes,edges);tab.CaptureViewport(20,30);tab.CommandManager.Execute(new AddConnectionCommand(edges,Edge("a","b")));
        var references=(new WeakReference(nodes),new WeakReference(edges));
        tab.SetContent(new(),new());return references;
    }
    [Fact]
    public void Abandoned_container_collections_are_not_pinned_by_live_tab_history_or_view_cache()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var tab=new WorkflowTabViewModel();var abandoned=BindAbandonedContainer(tab);
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            Assert.False(abandoned.Nodes.IsAlive);Assert.False(abandoned.Edges.IsAlive);GC.KeepAlive(tab);
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }

}