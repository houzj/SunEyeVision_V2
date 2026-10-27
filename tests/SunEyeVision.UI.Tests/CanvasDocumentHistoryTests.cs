using System.Windows;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class CanvasDocumentHistoryTests
{
    private static (SolutionSession Session,SunEyeVision.Workflow.Workflow Root,CommandManager Commands) Model()
    {
        ToolRegistry.RegisterTool(typeof(RootConfigurationTests.Probe));
        var root=new SunEyeVision.Workflow.Workflow("root","流程1");
        var document=new Solution{Workflows=new(){root},GlobalVariables=new(){new("计数",new long[]{1},"Integer"){Id="count"}}};
        var session=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{document},null)!;var host=session.Editing.Host;
        var commands=new CommandManager(root.Nodes,root.Connections);
        commands.BindDocumentHistory(session.History,root.Id,host.AcceptDocumentGraphHistory,()=>NodeConfigurationEditHost.DocumentHistorySignature(document));
        return(session,root,commands);
    }
    private static WorkflowNode Node(string id)=>new(id,id,id,"RootConfigurationProbe"){Parameters=new RootConfigurationTests.Parameters(),Position=new Point(100,100)};
    [Fact]
    public void Accepted_drop_batch_parameters_and_move_replay_in_one_order_without_rewinding_live_values()
    {
        var (session,root,commands)=Model();var a=Node("a");var b=Node("b");var edge=new WorkflowConnection("e","a","b");
        using(var transaction=commands.BeginTransaction()){
            commands.Execute(new AddNodeCommand(root.Nodes,a));commands.Execute(new AddNodeCommand(root.Nodes,b));
            commands.Execute(new AddConnectionCommand(root.Connections,edge));transaction.Complete();}
        Assert.Single(session.History.Entries);
        var edit=session.Editing.Host.BeginEdit(root.Id,a.Id);((RootConfigurationTests.Parameters)edit.Node.Parameters).Value=14;
        session.Editing.Host.CommitBatch(new[]{edit},null);
        var receipt=session.Editing.Host.LastHistoryReceipt!;Assert.NotEqual(Guid.Empty,receipt.CommitId);
        Assert.Null(receipt.Before.CopyConfiguration().GlobalVariables[0].Value);
        commands.Execute(new MoveNodeCommand(a,new Point(100,100),new Point(220,230)));
        session.Document.GlobalVariables[0].Value=new long[]{99};
        commands.Undo();Assert.Equal(new Point(100,100),a.Position);
        commands.Undo();Assert.Equal(3,((RootConfigurationTests.Parameters)a.Parameters).Value);
        commands.Undo();Assert.Empty(root.Nodes);Assert.Empty(root.Connections);
        commands.Redo();commands.Redo();commands.Redo();
        Assert.Same(a,root.Nodes[0]);Assert.Same(edge,Assert.Single(root.Connections));Assert.Equal(14,((RootConfigurationTests.Parameters)a.Parameters).Value);
        Assert.Equal(99,Assert.IsType<long[]>(session.Document.GlobalVariables[0].Value)[0]);
    }
    [Fact]
    public void Cancelled_drop_has_no_history_and_unregistered_route_edits_block_root_replay()
    {
        var (session,root,commands)=Model();var a=Node("a");
        using(commands.BeginTransaction())commands.Execute(new AddNodeCommand(root.Nodes,a));
        Assert.Empty(root.Nodes);Assert.Empty(session.History.Entries);
        commands.Execute(new AddNodeCommand(root.Nodes,a));
        commands.Execute(new AddNodeCommand(root.Nodes,Node("b")));
        var edge=new WorkflowConnection("external","a","b");root.Connections.Add(edge);
        Assert.False(session.History.CanUndo);root.Connections.Remove(edge);Assert.True(session.History.CanUndo);
        root.Connections.Add(edge);
        commands.Execute(new EditConnectionPathCommand(edge,null,new[]{new Point(1,2),new Point(3,4)},()=>{}));
        edge.ManualPathPoints=new[]{new Point(9,9)};Assert.False(session.History.CanUndo);
        edge.ManualPathPoints=new[]{new Point(1,2),new Point(3,4)};Assert.True(session.History.CanUndo);
        commands.Undo();Assert.Null(edge.ManualPathPoints);
        Assert.Throws<InvalidOperationException>(commands.Clear);
    }
}
