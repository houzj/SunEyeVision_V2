using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmDeleteTransactionTests
{
    private static WorkflowNodeBase Node(string id,string? child=null)=>new(id,id,id,child==null?"Test":"Subroutine") {SubroutineId=child,Parameters=new VmFragmentCopyTests.Parameters{Values=new(){1,2}}};
    private static WorkflowEngine Engine(params Flow[] flows){var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);foreach(var f in flows)engine.RegisterWorkflow(f);return engine;}
    [Fact]
    public void Delete_undo_redo_owns_nested_definitions_and_binding_configuration_as_one_edit()
    {
        var root=new Flow("root","root");var child=new Flow("child","child");var grand=new Flow("grand","grand");
        var group=Node("g",child.Id);var consumer=Node("consumer");root.Nodes.Add(group);root.Nodes.Add(consumer);child.Nodes.Add(Node("nested",grand.Id));grand.Nodes.Add(Node("inner"));
        var engine=Engine(root,child,grand);var variable=new GlobalVariable{ConsumerBindings=new(){new(){WorkflowId=root.Id,NodeId=group.Id}}};
        var solution=new Solution{Workflows=new(){root,child,grand},GlobalVariables=new(){variable}};var refreshed=0;var configured=0;
        var command=new DeleteWorkflowItemsCommand(root,new[]{group},Array.Empty<WorkflowConnection>(),engine,solution,()=>{configured++;((VmFragmentCopyTests.Parameters)consumer.Parameters).Values.Clear();variable.ConsumerBindings.Clear();},()=>refreshed++);
        command.Execute(null);Assert.Single(root.Nodes);Assert.Single(solution.Workflows);Assert.Null(engine.GetWorkflowById(grand.Id));Assert.Empty(variable.ConsumerBindings);
        command.Undo();Assert.Equal(new[]{root,child,grand},solution.Workflows);Assert.Same(child,engine.GetWorkflowById(child.Id));Assert.Equal(new[]{1,2},((VmFragmentCopyTests.Parameters)consumer.Parameters).Values);Assert.Single(variable.ConsumerBindings);
        command.Execute(null);Assert.Single(solution.Workflows);Assert.Empty(((VmFragmentCopyTests.Parameters)consumer.Parameters).Values);Assert.Equal(1,configured);Assert.Equal(3,refreshed);
    }
    [Fact]
    public void Shared_definition_and_descendants_survive_deleting_one_owner()
    {
        var root=new Flow("root","root");var child=new Flow("child","child");var grand=new Flow("grand","grand");var first=Node("a",child.Id);root.Nodes.Add(first);root.Nodes.Add(Node("b",child.Id));child.Nodes.Add(Node("nested",grand.Id));
        var engine=Engine(root,child,grand);var solution=new Solution{Workflows=new(){root,child,grand}};
        new DeleteWorkflowItemsCommand(root,new[]{first},Array.Empty<WorkflowConnection>(),engine,solution,()=>{},()=>{}).Execute(null);
        Assert.Equal(3,solution.Workflows.Count);Assert.Same(grand,engine.GetWorkflowById(grand.Id));
    }
    [Fact]
    public void Configuration_failure_rolls_back_graph_nested_ownership_and_parameters()
    {
        var root=new Flow("root","root");var child=new Flow("child","child");var node=Node("g",child.Id);root.Nodes.Add(node);var engine=Engine(root,child);var solution=new Solution{Workflows=new(){root,child}};
        var command=new DeleteWorkflowItemsCommand(root,new[]{node},Array.Empty<WorkflowConnection>(),engine,solution,()=>{((VmFragmentCopyTests.Parameters)node.Parameters).Values.Clear();throw new InvalidOperationException("reconcile");},()=>{});
        Assert.Throws<InvalidOperationException>(()=>command.Execute(null));Assert.Same(node,Assert.Single(root.Nodes));Assert.Same(child,engine.GetWorkflowById(child.Id));Assert.Equal(new[]{root,child},solution.Workflows);Assert.Equal(2,((VmFragmentCopyTests.Parameters)node.Parameters).Values.Count);
    }
    [Fact]
    public void Real_binding_reconciliation_is_reversible_and_redo_never_restores_deleted_source()
    {
        var root=new Flow("root","root");
        var sourceParameters=new SunEyeVision.Tool.ReceiveData.ReceiveDataParameters();
        var source=Node("source");source.Parameters=sourceParameters;
        var consumer=Node("consumer");consumer.Parameters=new SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters {InputText="source."+sourceParameters.OutputDefinition().BindingKey};
        root.Nodes.Add(source);root.Nodes.Add(consumer);var solution=new Solution{Workflows=new(){root}};var engine=Engine(root);
        var bindings=new OutputBindingChangeService();bindings.Reset(solution);
        Func<WorkflowNodeBase,IReadOnlyList<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable>?> reader=_=>null;
        bindings.StaticContractReader=reader;
        var command=new DeleteWorkflowItemsCommand(root,new[]{source},Array.Empty<WorkflowConnection>(),engine,solution,
            ()=>bindings.Commit(root.Id,source.Id),()=>bindings.AcceptHistoryConfiguration(solution));
        command.Execute(null);
        Assert.Empty(((SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters)consumer.Parameters).InputText);
        Assert.NotEmpty(consumer.Parameters.RebindingRequired);
        command.Undo();Assert.StartsWith("source.",((SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters)consumer.Parameters).InputText);
        Assert.Empty(consumer.Parameters.RebindingRequired);Assert.Same(reader,bindings.StaticContractReader);
        command.Execute(null);Assert.Empty(((SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters)consumer.Parameters).InputText);Assert.NotEmpty(consumer.Parameters.RebindingRequired);
    }

    [Fact]
    public void Focused_edge_delete_does_not_consume_retained_node_selection_and_undo_restores_only_edge()
    {
        var root=new Flow("root","root");var a=Node("a");var b=Node("b");a.IsSelected=true;b.IsSelected=true;root.Nodes.Add(a);root.Nodes.Add(b);
        var edge=new WorkflowConnection{Id="edge",SourceNodeId=a.Id,TargetNodeId=b.Id,IsSelected=true};root.Connections.Add(edge);
        var engine=Engine(root);var solution=new Solution{Workflows=new(){root}};
        var command=new DeleteWorkflowItemsCommand(root,Array.Empty<WorkflowNodeBase>(),new[]{edge},engine,solution,()=>{},()=>{});
        command.Execute(null);Assert.Equal(new[]{a,b},root.Nodes);Assert.All(root.Nodes,n=>Assert.True(n.IsSelected));Assert.Empty(root.Connections);
        command.Undo();Assert.Same(edge,Assert.Single(root.Connections));Assert.Equal(new[]{a,b},root.Nodes);
    }

}