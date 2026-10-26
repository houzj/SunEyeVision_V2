using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.Workflow;
using Xunit;
using Node = SunEyeVision.Workflow.WorkflowNodeBase;
using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmFragmentCopyTests
{
    public sealed class Parameters : ToolParameters
    {
        public List<int> Values {get;set;} = new();
        public AvailableDataSource? ImageSource {get;set;}
        public ImageSourceReference? ImageSourceRef {get;set;}
        public string Text {get;set;} = "a.Out";
    }
    private static Node Make(string id, ToolParameters? p = null) => new(id,id,id,"Test") { Parameters = p ?? new Parameters() };
    private static Node New(Node original,string scope) => Make(Guid.NewGuid().ToString());
    private static WorkflowFragment Copy(Flow source,string[] ids,params Flow[] children) => WorkflowFragmentCopy.Prepare(source,ids,"destination",id => children.SingleOrDefault(c => c.Id == id), New);
    private static OutputReference Reference(string node, string? scope = null, BindingSelection? selection = null) => new(node,"stable-field",TypeRef.ScalarOf(ScalarKind.Float64),selection:selection,scopeId:scope);
    [Fact] public void Fragment_has_fresh_identity_deep_configuration_internal_edges_and_typed_index_references()
    {
        var a = Make("a",new Parameters { Values = new() {1,2} }); var b = Make("b"); var external = Make("external");
        b.ParamSettings.SetSetting(new ParamSetting { ParameterName="input",BindingType=BindingType.Binding,
            SourceNodeId="a",SourceWorkflowId="source",ReferenceExpression=BindingReferenceSerializer.Serialize(Reference("a","source",new BindingSelection.AtField(Reference("external","source")))) });
        b.ParamSettings.SetSetting(ParamSetting.CreateConstant("list",new List<int>{7}));
        var source = new Flow("source","flow"); source.Nodes.Add(a); source.Nodes.Add(b); source.Nodes.Add(external);
        source.Connections.Add(new() { Id="internal",SourceNodeId="a",TargetNodeId="b",SourcePort="RightPort",TargetPort="LeftPort",ManualPathPoints=new[]{new Point(1,2),new Point(3,4)} });
        source.Connections.Add(new() { Id="outside",SourceNodeId="external",TargetNodeId="a" });
        var result = Copy(source,new[]{"a","b"});
        var ca = result.Nodes[0]; var cb=result.Nodes[1]; Assert.NotEqual(a.Id,ca.Id); Assert.NotSame(a.Parameters,ca.Parameters);
        Assert.Empty(result.OwnedWorkflows); var edge=Assert.Single(result.Connections); Assert.Equal(ca.Id,edge.SourceNodeId); Assert.Equal(cb.Id,edge.TargetNodeId);
        Assert.Equal("RightPort",edge.SourcePort); Assert.NotSame(source.Connections[0].ManualPathPoints,edge.ManualPathPoints);
        var reference=BindingReferenceSerializer.Deserialize(cb.ParamSettings["input"]!.ReferenceExpression!);
        Assert.Equal(ca.Id,reference.NodeId); Assert.Equal("destination",reference.ScopeId);
        var index=Assert.IsType<BindingSelection.AtField>(reference.Selection).Reference; Assert.Equal("external",index.NodeId); Assert.Equal("source",index.ScopeId);
        Assert.Equal("stable-field",reference.PortId); Assert.Equal("a.Out",((Parameters)ca.Parameters).Text);
        ((Parameters)ca.Parameters).Values.Add(3); Assert.Equal(2,((Parameters)a.Parameters).Values.Count);
        Assert.IsType<List<int>>(cb.ParamSettings["list"]!.ConstantValue).Add(9); Assert.Single(Assert.IsType<List<int>>(b.ParamSettings["list"]!.ConstantValue));
    }
    [Fact] public void Source_references_and_automatic_edge_ownership_follow_copied_graph()
    {
        var a=Make("a"); var b=Make("b",new Parameters { ImageSource=new AvailableDataSource {SourceNodeId="a",PropertyName="Out",AutomaticConnectionId="edge"},
            ImageSourceRef=new ImageSourceReference("a","Out","a","Output") {AutomaticConnectionId="edge"} });
        var flow=new Flow("source","flow"); flow.Nodes.Add(a);flow.Nodes.Add(b);flow.Connections.Add(new(){Id="edge",SourceNodeId="a",TargetNodeId="b"});
        var both=Copy(flow,new[]{"a","b"});var p=(Parameters)both.Nodes[1].Parameters;
        Assert.Equal(both.Nodes[0].Id,p.ImageSource!.SourceNodeId);Assert.Equal("destination",p.ImageSource.SourceWorkflowId);
        Assert.Equal(both.Connections[0].Id,p.ImageSource.AutomaticConnectionId);Assert.Equal(p.ImageSource.SourceNodeId,p.ImageSourceRef!.SourceNodeId);
        var single=(Parameters)Copy(flow,new[]{"b"}).Nodes[0].Parameters;
        Assert.Equal("a",single.ImageSource!.SourceNodeId);Assert.Equal("source",single.ImageSource.SourceWorkflowId);Assert.Null(single.ImageSource.AutomaticConnectionId);
        Assert.Equal("a",((Parameters)b.Parameters).ImageSource!.SourceNodeId);
    }
    [Fact] public void Subprogram_outputs_and_child_input_references_use_independent_owned_scope()
    {
        var child=new Flow("child","child"){IsSubroutine=true}; var inner=Make("inner");child.Nodes.Add(inner);
        inner.ParamSettings.SetSetting(new ParamSetting { ParameterName="image",BindingType=BindingType.Binding,
            ReferenceExpression=BindingReferenceSerializer.Serialize(Reference("__SubroutineInputs__","child")) });
        var parameters=new SubroutineParameters {SubroutineId="child"};
        parameters.Outputs.Add(new SubroutineOutputConfig {Name="out",BindingSource="inner.Out",Source=new AvailableDataSource { SourceNodeId="inner",PropertyName="Out" }});
        parameters.LoopConfig.SourceCompareBinding="inner.Out";
        var owner=Make("owner",parameters);owner.SubroutineId="child";var flow=new Flow("source","root");flow.Nodes.Add(owner);
        var result=Copy(flow,new[]{"owner"},child);var copiedChild=Assert.Single(result.OwnedWorkflows);var copiedOwner=result.Nodes[0];
        Assert.NotEqual(child.Id,copiedChild.Id);Assert.Equal(copiedChild.Id,copiedOwner.SubroutineId);
        var p=Assert.IsType<SubroutineParameters>(copiedOwner.Parameters);Assert.Equal(copiedChild.Id,p.SubroutineId);
        Assert.Equal(copiedChild.Nodes[0].Id+".Out",p.Outputs[0].BindingSource);
        Assert.Equal(copiedChild.Nodes[0].Id,p.Outputs[0].Source!.SourceNodeId);Assert.Equal(copiedChild.Id,p.Outputs[0].Source!.SourceWorkflowId);
        Assert.Equal(copiedChild.Nodes[0].Id+".Out",p.LoopConfig.SourceCompareBinding);
        var input=BindingReferenceSerializer.Deserialize(copiedChild.Nodes[0].ParamSettings["image"]!.ReferenceExpression!);
        Assert.Equal(copiedChild.Id,input.ScopeId);Assert.Equal("__SubroutineInputs__",input.NodeId);
        Assert.Equal("inner.Out",parameters.Outputs[0].BindingSource);
    }
    [Fact] public void Reused_source_subprogram_creates_independent_body_for_each_owner()
    {
        var child=new Flow("child","child");child.Nodes.Add(Make("inner"));var root=new Flow("root","root");
        foreach(var id in new[]{"one","two"}) {var owner=Make(id,new SubroutineParameters{SubroutineId="child"});owner.SubroutineId="child";root.Nodes.Add(owner);}
        var result=Copy(root,new[]{"one","two"},child);
        Assert.Equal(2,result.OwnedWorkflows.Count);Assert.NotEqual(result.Nodes[0].SubroutineId,result.Nodes[1].SubroutineId);
        Assert.NotEqual(result.OwnedWorkflows[0].Nodes[0].Id,result.OwnedWorkflows[1].Nodes[0].Id);
    }
    [Fact] public void Invalid_owned_graph_fails_without_editing_source()
    {
        var root=new Flow("root","root");var owner=Make("owner");owner.SubroutineId="root";root.Nodes.Add(owner);
        Assert.Throws<InvalidOperationException>(()=>Copy(root,new[]{"owner"},root));Assert.Single(root.Nodes);Assert.Equal("root",owner.SubroutineId);
        owner.SubroutineId="missing";Assert.Throws<InvalidOperationException>(()=>Copy(root,new[]{"owner"}));
        owner.SubroutineId=null;root.Nodes.Add(Make("other"));
        Assert.Throws<InvalidOperationException>(()=>WorkflowFragmentCopy.Prepare(root,new[]{"owner","other"},"dest",_=>null,(_,_)=>Make("duplicate")));
    }
    [Fact] public void Paste_command_undo_redo_registers_same_owned_objects_and_ids()
    {
        var root = new Flow("source","root"); var child = new Flow("child","child"); child.Nodes.Add(Make("inner"));
        var owner=Make("owner",new SubroutineParameters {SubroutineId="child"}); owner.SubroutineId="child"; root.Nodes.Add(owner);
        var fragment=Copy(root,new[]{"owner"},child); var target=new Flow("destination","destination");
        var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);engine.RegisterWorkflow(target);
        var solution=new Solution();solution.Workflows.Add(target);
        var history=new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(target.Nodes,target.Connections);
        history.Execute(new PasteWorkflowFragmentCommand(target,fragment,engine,solution));
        var copied=Assert.Single(fragment.OwnedWorkflows); Assert.Same(copied,engine.GetWorkflowById(copied.Id));Assert.Contains(copied,solution.Workflows);
        history.Undo();Assert.Empty(target.Nodes);Assert.Null(engine.GetWorkflowById(copied.Id));Assert.Single(solution.Workflows);
        history.Redo();Assert.Same(fragment.Nodes[0],Assert.Single(target.Nodes));Assert.Same(copied,engine.GetWorkflowById(copied.Id));
    }
    [Fact] public void Paste_validation_failure_rolls_back_nodes_and_owned_registration()
    {
        var target=new Flow("target","target");var child=new Flow("owned","owned");
        var node=Make("new");var bad=new WorkflowFragment(new[]{node},new[]{new WorkflowConnection {Id="self",SourceNodeId=node.Id,TargetNodeId=node.Id}},new[]{child});
        var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);var solution=new Solution();
        var history=new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(target.Nodes,target.Connections);
        Assert.Throws<InvalidOperationException>(()=>history.Execute(new PasteWorkflowFragmentCommand(target,bad,engine,solution)));
        Assert.Empty(target.Nodes);Assert.Empty(target.Connections);Assert.Empty(solution.Workflows);Assert.Null(engine.GetWorkflowById(child.Id));Assert.False(history.CanUndo);
    }

    [Fact] public void Binding_identity_metadata_and_external_explicit_scope_do_not_alias_same_named_local_node()
    {
        var a=Make("a");var b=Make("b",new Parameters {ImageSource=new AvailableDataSource { SourceNodeId="a",SourceWorkflowId="external-flow",PropertyName="Out" }});
        b.Parameters.BindingSourceIdentities["ImageSource"]=new("a.Out","external-flow","a");
        b.ParamSettings.SetSetting(new ParamSetting {ParameterName="external",BindingType=BindingType.Binding,SourceNodeId="a",SourceWorkflowId="external-flow",
            ReferenceExpression=BindingReferenceSerializer.Serialize(Reference("a","external-flow"))});
        var flow=new Flow("source","source");flow.Nodes.Add(a);flow.Nodes.Add(b);var result=Copy(flow,new[]{"a","b"});
        var parameters=(Parameters)result.Nodes[1].Parameters;
        Assert.Equal("a",parameters.ImageSource!.SourceNodeId);Assert.Equal("external-flow",parameters.ImageSource.SourceWorkflowId);
        Assert.Equal("external-flow",parameters.BindingSourceIdentities["ImageSource"].WorkflowId);
        Assert.Equal("a",BindingReferenceSerializer.Deserialize(result.Nodes[1].ParamSettings["external"]!.ReferenceExpression!).NodeId);
    }

    [Fact] public void Collection_listener_failure_cannot_leave_half_added_node_or_subprogram()
    {
        var target=new Flow("target","target");var child=new Flow("owned","owned");
        var node=Make("new");var fragment=new WorkflowFragment(new[]{node},Array.Empty<WorkflowConnection>(),new[]{child});
        target.Nodes.CollectionChanged+=(_,e)=> {if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Add)throw new InvalidOperationException("listener");};
        var engine=new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance);var solution=new Solution();
        Assert.Throws<InvalidOperationException>(()=>new PasteWorkflowFragmentCommand(target,fragment,engine,solution).Execute(null));
        Assert.Empty(target.Nodes);Assert.Empty(solution.Workflows);Assert.Null(engine.GetWorkflowById(child.Id));
    }

    [Fact] public void Same_flow_copy_keeps_unselected_predecessor_incoming_edge_but_never_external_outgoing_edge()
    {
        var a=Make("a");var b=Make("b",new Parameters {ImageSource=new AvailableDataSource{SourceNodeId="a",PropertyName="Out",AutomaticConnectionId="ab"}});var c=Make("c");
        var root=new Flow("flow","flow");root.Nodes.Add(a);root.Nodes.Add(b);root.Nodes.Add(c);
        root.Connections.Add(new(){Id="ab",SourceNodeId="a",TargetNodeId="b",SourcePort="RightPort",TargetPort="LeftPort"});
        root.Connections.Add(new(){Id="bc",SourceNodeId="b",TargetNodeId="c"});
        var fragment=WorkflowFragmentCopy.Prepare(root,new[]{"b"},root.Id,_=>null,New);
        var edge=Assert.Single(fragment.Connections);Assert.Equal("a",edge.SourceNodeId);Assert.Equal(fragment.Nodes[0].Id,edge.TargetNodeId);
        Assert.Equal("RightPort",edge.SourcePort);Assert.Equal("LeftPort",edge.TargetPort);
        Assert.Equal(edge.Id,((Parameters)fragment.Nodes[0].Parameters).ImageSource!.AutomaticConnectionId);
        var command=new PasteWorkflowFragmentCommand(root,fragment,new WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance),null);
        command.Execute(null);Assert.Equal(3,root.Connections.Count);command.Undo();Assert.Equal(2,root.Connections.Count);
        Assert.Empty(Copy(root,new[]{"b"}).Connections);
    }

}
