using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmGroupCreationTests
{
    private static WorkflowNodeBase Group(string id)=>new(id,id,id,"Subroutine"){Parameters=new SubroutineParameters()};
    private static (Flow,WorkflowEngine,Solution) Root()
    {var root=new Flow("root","root");var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);engine.RegisterWorkflow(root);return(root,engine,new Solution{Workflows=new(){root}});}
    [Fact] public void Adding_group_creates_consistent_owned_definition_and_default_input_in_same_edit()
    {
        var(root,engine,solution)=Root();var node=Group("owner");var command=new AddGroupNodeCommand(root,node,engine,solution);command.Execute(null);
        Assert.Same(node,Assert.Single(root.Nodes));Assert.True(command.OwnedWorkflow.IsSubroutine);Assert.Same(command.OwnedWorkflow,engine.GetWorkflowById(node.SubroutineId!));
        var parameters=(SubroutineParameters)node.Parameters;Assert.Equal(node.SubroutineId,parameters.SubroutineId);Assert.Equal(node.DispName,parameters.SubroutineName);
        var input=Assert.Single(parameters.Inputs);Assert.Equal("in_1",input.BindingKey);Assert.Equal("Image",input.TypeName);var field=input.FieldId;
        command.Undo();Assert.Empty(root.Nodes);Assert.Single(solution.Workflows);Assert.Null(engine.GetWorkflowById(command.OwnedWorkflow.Id));
        command.Execute(null);Assert.Same(command.OwnedWorkflow,engine.GetWorkflowById(node.SubroutineId!));Assert.Equal(field,Assert.Single(((SubroutineParameters)node.Parameters).Inputs).FieldId);
    }
    [Fact] public void Undo_outer_creation_removes_later_nested_definitions_and_redo_restores_current_contents_and_binding_configuration()
    {
        var(root,engine,solution)=Root();var outer=Group("outer");var add=new AddGroupNodeCommand(root,outer,engine,solution);add.Execute(null);
        var inner=Group("inner");var nested=new AddGroupNodeCommand(add.OwnedWorkflow,inner,engine,solution);nested.Execute(null);
        var consumer=new WorkflowNodeBase("consumer","consumer","consumer","Test"){Parameters=new VmFragmentCopyTests.Parameters{Values=new(){7,8}}};root.Nodes.Add(consumer);
        var constant=new List<int>{1,2};consumer.ParamSettings.SetSetting(ParamSetting.CreateConstant("values",constant));
        var order=solution.Workflows.ToArray();add.Undo();Assert.Same(consumer,Assert.Single(root.Nodes));Assert.Single(solution.Workflows);Assert.Null(engine.GetWorkflowById(nested.OwnedWorkflow.Id));
        ((VmFragmentCopyTests.Parameters)consumer.Parameters).Values.Clear();constant.Add(99); // 正式断源清理及旧集合继续变化。
        add.Execute(null);Assert.Equal(order,solution.Workflows);Assert.Same(inner,Assert.Single(add.OwnedWorkflow.Nodes));Assert.Same(nested.OwnedWorkflow,engine.GetWorkflowById(inner.SubroutineId!));Assert.Equal(new[]{7,8},((VmFragmentCopyTests.Parameters)consumer.Parameters).Values);Assert.Equal(new[]{1,2},Assert.IsType<List<int>>(consumer.ParamSettings.GetSetting("values")!.ConstantValue));
    }
    [Fact] public void Failed_node_publication_cannot_leave_registered_group_definition()
    {
        var(root,engine,solution)=Root();var node=Group("owner");var add=new AddGroupNodeCommand(root,node,engine,solution);
        root.Nodes.CollectionChanged+=(_,e)=>{if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Add)throw new InvalidOperationException("listener");};
        Assert.Throws<InvalidOperationException>(()=>add.Execute(null));Assert.Empty(root.Nodes);Assert.Single(solution.Workflows);Assert.Null(engine.GetWorkflowById(add.OwnedWorkflow.Id));
    }
    [Fact] public void Ownership_rejects_inconsistent_definition_identity_and_preserves_external_owner()
    {
        var(root,engine,solution)=Root();var outer=Group("outer");var add=new AddGroupNodeCommand(root,outer,engine,solution);add.Execute(null);
        var other=Group("other");other.SubroutineId=outer.SubroutineId;root.Nodes.Add(other);add.Undo();Assert.Same(add.OwnedWorkflow,engine.GetWorkflowById(other.SubroutineId!));Assert.Contains(add.OwnedWorkflow,solution.Workflows);
        Assert.Throws<InvalidOperationException>(()=>WorkflowOwnership.CollectExclusiveDescendants(new[]{root,add.OwnedWorkflow,new Flow(add.OwnedWorkflow.Id,"conflict")},root,new[]{other}));
    }
}