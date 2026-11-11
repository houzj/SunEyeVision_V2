using SunEyeVision.Domain.Workflows;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class NodeClipboardTests
{
 [Fact] public void Internal_nodes_and_edges_copy_atomically_and_reopen()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",200,20);var edge=f.Connect(a,b);
  var edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.EditConnectionPath(edge,new[]{new CanvasPoint(80,30),new CanvasPoint(100,30)});f.Session.CommitGraphEdit(edit,"折点");
  var output=f.Session.GetNodeSchema(f.Root.Id,a.Id)!.Outputs.Single(port=>port.Id=="output.image");
  var selected=ParameterSettingCanonical.Serialize(new OutputReference(a.Id,output.Id,output.Type,new[]{"data"},scopeId:f.Root.Id));
  var binary=f.Session.GetNodeSchema(f.Root.Id,b.Id)!.Outputs.Single(port=>port.Id=="field:8ca10000000040008000000000000006");
  var layer=ParameterSettingCanonical.Serialize(new OutputReference(b.Id,binary.Id,binary.Type,new[]{"data"},scopeId:f.Root.Id));
  f.Session.SetNodeDisplayOverlays(f.Root.Id,b,selected,new ImageOverlaySelection(selected,new string?[]{null,layer,null}));
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{a,b});
  var count=f.Session.HistoryEntries.Count;var receipt=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(400,300));
  Assert.Equal(2,receipt.Nodes.Count);var copied=Assert.Single(receipt.Connections);Assert.Equal(4,f.Session.GetNodes(f.Root.Id).Count);
  var copySource=receipt.Nodes.Single(node=>node.ToolType=="ImageLoad");var copyTarget=receipt.Nodes.Single(node=>node.ToolType=="BlobAnalysis");
  var binding=Assert.IsType<ReferenceSetting>(copyTarget.Configuration.Fields.Read("input.image"));
  Assert.Equal(copySource.Id,binding.Reference.NodeId);Assert.Equal(f.Root.Id,binding.Reference.ScopeId);Assert.Equal("connection:"+copied.Id,binding.OriginId);
  Assert.Equal(copySource.Id,ParameterSettingCanonical.Deserialize(copyTarget.SelectedDisplayReference!).NodeId);
  Assert.Equal(copyTarget.SelectedDisplayReference,copyTarget.DisplayOverlays!.Background);
  Assert.Equal(copyTarget.Id,ParameterSettingCanonical.Deserialize(copyTarget.DisplayOverlays.Slots[1]!).NodeId);
  Assert.Equal(new CanvasPoint(470,310),copied.ManualPathPoints![0]);Assert.Equal(count+1,f.Session.HistoryEntries.Count);
  Assert.True(f.Session.Undo());Assert.Equal(2,f.Session.GetNodes(f.Root.Id).Count);Assert.True(f.Session.Redo());Assert.Equal(4,f.Session.GetNodes(f.Root.Id).Count);
  var path=f.Save();var reopened=f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  Assert.Equal(4,reopened.GetNodes(f.Root.Id).Count);Assert.Equal(2,reopened.GetConnections(f.Root.Id).Count);
  Assert.True(copyTarget.DisplayOverlays.SameSelection(reopened.FindNode(f.Root.Id,copyTarget.Id)!.DisplayOverlays));
 }
 [Fact] public void Missing_sources_are_skipped_without_creating_empty_history()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("ImageLoad",200,20);
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{a,b});
  var edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.DeleteNodesAndConnections(new[]{a},Array.Empty<ConnectionDefinition>());f.Session.CommitGraphEdit(edit,"删除来源");
  var partial=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(400,300));Assert.Single(partial.Nodes);
  edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.DeleteNodesAndConnections(new[]{b},Array.Empty<ConnectionDefinition>());f.Session.CommitGraphEdit(edit,"删除来源");
  var before=f.Session.HistoryEntries.Count;var empty=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(500,300));
  Assert.Empty(empty.Nodes);Assert.Equal(before,f.Session.HistoryEntries.Count);
 } [Fact] public void Renaming_copied_source_invalidates_its_original_name_path()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("ImageLoad",200,20);
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{a,b});
  var edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.RenameNode(a,"新名称");f.Session.CommitGraphEdit(edit,"重命名来源");
  var pasted=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(400,300));
  Assert.Single(pasted.Nodes);Assert.Equal(b.ToolType,pasted.Nodes[0].ToolType);
 } [Fact] public void A_new_same_name_node_never_replaces_the_captured_actual_source()
 {
  using var f=new SessionGraphFixture();var source=f.Add();var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{source});
  var remove=f.Session.PrepareGraphEdit(f.Root.Id);remove.DeleteNodesAndConnections(new[]{source},Array.Empty<ConnectionDefinition>());f.Session.CommitGraphEdit(remove,"删除来源");
  var replacement=f.Add();Assert.NotSame(source,replacement);Assert.Equal(source.Name,replacement.Name);
  var before=f.Session.HistoryEntries.Count;Assert.Empty(f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(400,300)).Nodes);
  Assert.Equal(before,f.Session.HistoryEntries.Count);
 }
 [Fact] public void Nonuniform_placement_clears_copied_manual_path_in_same_transaction()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",200,20);var edge=f.Connect(a,b);
  var edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.EditConnectionPath(edge,new[]{new CanvasPoint(80,30),new CanvasPoint(100,30)});f.Session.CommitGraphEdit(edit,"折点");
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{a,b});var positions=new Dictionary<NodeDefinition,CanvasPoint>{{a,new(400,300)},{b,new(700,300)}};
  var before=f.Session.HistoryEntries.Count;var result=f.Session.PasteNodeClipboard(f.Root.Id,fragment,positions[a],positions);
  Assert.Null(Assert.Single(result.Connections).ManualPathPoints);Assert.Equal(before+1,f.Session.HistoryEntries.Count);
 } [Fact] public void Same_scope_external_incoming_connection_is_recreated_and_rebound()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",200,20);var edge=f.Connect(a,b);
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{b});var pasted=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(400,200));
  var copied=Assert.Single(pasted.Connections);Assert.Equal(a.Id,copied.SourceNodeId);Assert.NotEqual(edge.Id,copied.Id);
  var binding=Assert.IsType<ReferenceSetting>(Assert.Single(pasted.Nodes).Configuration.Fields.Read("input.image"));
  Assert.Equal(a.Id,binding.Reference.NodeId);Assert.Equal("connection:"+copied.Id,binding.OriginId);
 }
 [Fact] public void Paste_reads_configuration_after_later_source_edit()
 {
  using var f=new SessionGraphFixture();var source=f.Add();
  void Set(long value){var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,source.Id);draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(value)}});f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));}
  Set(17);var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{source});Set(29);
  var pasted=Assert.Single(f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(400,300)).Nodes);
  Assert.Equal(29L,Assert.IsType<ConstantSetting>(pasted.Configuration.Fields.Read("sequence.initial")).Value);
  Assert.Equal(29L,Assert.IsType<ConstantSetting>(source.Configuration.Fields.Read("sequence.initial")).Value);
 }
 [Fact] public void First_actual_selection_is_the_stable_paste_anchor_even_if_not_leftmost()
 {
  using var f=new SessionGraphFixture();var left=f.Add();var right=f.Add("BlobAnalysis",200,20);f.Connect(left,right);
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{right,left});
  var result=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(500,300));
  var first=Assert.Single(result.Nodes.Where(node=>node.ToolType=="BlobAnalysis"));var other=Assert.Single(result.Nodes.Where(node=>node.ToolType=="ImageLoad"));
  Assert.Equal(new CanvasPoint(500,300),first.Position);Assert.Equal(new CanvasPoint(310,300),other.Position);
 }
 [Fact] public void External_predecessor_retirement_rejects_entire_paste_without_history_change()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",200,20);f.Connect(a,b);
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{b});var remove=f.Session.PrepareGraphEdit(f.Root.Id);
  remove.DeleteNodesAndConnections(new[]{a},Array.Empty<ConnectionDefinition>());f.Session.CommitGraphEdit(remove,"删除前驱");
  var count=f.Session.HistoryEntries.Count;Assert.Throws<InvalidOperationException>(()=>f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(300,300)));
  Assert.Single(f.Session.GetNodes(f.Root.Id));Assert.Empty(f.Session.GetConnections(f.Root.Id));Assert.Equal(count,f.Session.HistoryEntries.Count);
 }
 [Fact] public void Cross_flow_internal_reference_is_remapped_and_external_predecessor_is_rejected()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",200,20);f.Connect(a,b);
  var independent=f.Add("ImageLoad",350,20);f.Connect(a,independent);
  var dormant=f.Add("BlobAnalysis",450,20);var dormantDraft=f.Session.Editing.Host.BeginEdit(f.Root.Id,dormant.Id);
  var output=f.Session.GetNodeSchema(f.Root.Id,b.Id)!.Outputs.Single(port=>port.Id=="field:8ca10000000040008000000000000019");
  const string inactiveField="input.roi.rectangle.center-x";
  dormantDraft.ApplyFields(new Dictionary<string,ParameterSetting>{{inactiveField,new ReferenceSetting(new OutputReference(b.Id,output.Id,output.Type,scopeId:f.Root.Id,storage:output.Storage))}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{dormantDraft}));
  var path=f.Save();var json=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
  var flows=json["Workflows"]!.AsArray();var second=flows[0]!.DeepClone();second["Id"]=Guid.NewGuid().ToString("N");second["Name"]="目标";
  second["Nodes"]=new System.Text.Json.Nodes.JsonArray();second["Connections"]=new System.Text.Json.Nodes.JsonArray();flows.Add(second);
  var multi=Path.Combine(f.CaseDirectory,"multi.solution");File.WriteAllText(multi,json.ToJsonString());
  var session=f.Manager.Open(multi,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  var source=session.GetWorkflows().Single(flow=>flow.Id==f.Root.Id);var target=session.GetWorkflows().Single(flow=>flow.Name=="目标");
  var originalSource=session.FindNode(source.Id,a.Id)!;var originalTarget=session.FindNode(source.Id,b.Id)!;
  var fragment=session.CaptureNodeClipboard(source.Id,new[]{originalSource,originalTarget});
  var result=session.PasteNodeClipboard(target.Id,fragment,new CanvasPoint(200,200));
  var copiedSource=result.Nodes.Single(node=>node.ToolType=="ImageLoad");var copiedTarget=result.Nodes.Single(node=>node.ToolType=="BlobAnalysis");
  var binding=Assert.IsType<ReferenceSetting>(copiedTarget.Configuration.Fields.Read("input.image"));
  Assert.Equal(copiedSource.Id,binding.Reference.NodeId);Assert.Equal(target.Id,binding.Reference.ScopeId);
  Assert.Equal("connection:"+Assert.Single(result.Connections).Id,binding.OriginId);
  var before=session.HistoryEntries.Count;var outside=session.CaptureNodeClipboard(source.Id,new[]{originalTarget});
  Assert.ThrowsAny<Exception>(()=>session.PasteNodeClipboard(target.Id,outside,new CanvasPoint(500,200)));
  Assert.Equal(before,session.HistoryEntries.Count);Assert.Equal(2,session.GetNodes(target.Id).Count);
  var independentNode=session.FindNode(source.Id,independent.Id)!;
  var independentCopy=session.PasteNodeClipboard(target.Id,session.CaptureNodeClipboard(source.Id,new[]{independentNode}),new CanvasPoint(600,200));
  Assert.Single(independentCopy.Nodes);Assert.Empty(independentCopy.Connections);
  var inactive=session.FindNode(source.Id,dormant.Id)!;var inactiveFragment=session.CaptureNodeClipboard(source.Id,new[]{inactive});
  var failure=Assert.Throws<InvalidOperationException>(()=>session.PasteNodeClipboard(target.Id,inactiveFragment,new CanvasPoint(700,200)));
  Assert.Contains(inactiveField,failure.Message);Assert.Equal(3,session.GetNodes(target.Id).Count);
 }
 [Fact] public void Fake_node_and_foreign_session_fragment_are_rejected_without_publication()
 {
  using var f=new SessionGraphFixture();var source=f.Add();var fake=new NodeDefinition(source.Id,source.Name,source.ToolType,source.Configuration);
  Assert.Throws<InvalidOperationException>(()=>f.Session.CaptureNodeClipboard(f.Root.Id,new[]{fake}));
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{source});
  using var other=new SessionGraphFixture();var count=other.Session.HistoryEntries.Count;
  Assert.Throws<InvalidOperationException>(()=>other.Session.PasteNodeClipboard(other.Root.Id,fragment,new CanvasPoint(200,200)));
  Assert.Empty(other.Session.GetNodes(other.Root.Id));Assert.Equal(count,other.Session.HistoryEntries.Count);
 }
 [Fact] public void Nested_index_reference_and_conversion_are_remapped_to_new_actual_source()
 {
  using var f=new SessionGraphFixture();var source=f.Add("BlobAnalysis");var target=f.Add("BlobAnalysis",200,20);f.Connect(source,target);
  var schema=f.Session.GetNodeSchema(f.Root.Id,source.Id)!;
  var array=schema.Outputs.Single(port=>port.Id=="field:8ca10000000040008000000000000019");
  var index=schema.Outputs.Single(port=>port.Id=="field:8ca10000000040008000000000000011");
  var indexReference=new OutputReference(source.Id,index.Id,index.Type,scopeId:f.Root.Id,storage:index.Storage);
  var reference=new OutputReference(source.Id,array.Id,array.Type,selection:new BindingSelection.AtField(indexReference),scopeId:f.Root.Id,storage:array.Storage);
  const string field="field:43bda03261df55b88f963bd18f2ba4d9";var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,target.Id);
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{field,new ReferenceSetting(reference,BindingConversion.Float64ToInt64Checked)}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var result=f.Session.PasteNodeClipboard(f.Root.Id,f.Session.CaptureNodeClipboard(f.Root.Id,new[]{source,target}),new CanvasPoint(400,300));
  var newSource=result.Nodes[0];var newTarget=result.Nodes[1];var copied=Assert.IsType<ReferenceSetting>(newTarget.Configuration.Fields.Read(field));
  Assert.Equal(BindingConversion.Float64ToInt64Checked,copied.Conversion);Assert.Equal(newSource.Id,copied.Reference.NodeId);
  var copiedIndex=Assert.IsType<BindingSelection.AtField>(copied.Reference.Selection);Assert.Equal(newSource.Id,copiedIndex.Reference.NodeId);Assert.Equal(f.Root.Id,copiedIndex.Reference.ScopeId);
 }
 [Fact] public void Display_expression_preserves_text_self_and_retargets_explicit_source()
 {
  using var f=new SessionGraphFixture();var source=f.Add("BlobAnalysis");var target=f.Add("BlobAnalysis",200,20);f.Connect(source,target);
  var output=f.Session.GetNodeSchema(f.Root.Id,source.Id)!.Outputs.Single(port=>port.Id=="field:8ca10000000040008000000000000011");
  var expression=new DisplayExpressionSetting("{前} / {己}",new[]{new DisplayExpressionSlot("前",new OutputReference(source.Id,output.Id,output.Type,scopeId:f.Root.Id,storage:output.Storage)),new DisplayExpressionSlot("己",new ResultFieldPath(output.Id))});
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,target.Id);draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"display.text.content",expression}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var result=f.Session.PasteNodeClipboard(f.Root.Id,f.Session.CaptureNodeClipboard(f.Root.Id,new[]{source,target}),new CanvasPoint(400,300));
  var copied=Assert.IsType<DisplayExpressionSetting>(result.Nodes[1].Configuration.Fields.Read("display.text.content"));
  Assert.Equal(expression.Text,copied.Text);Assert.Equal(result.Nodes[0].Id,copied.Slots[0].Reference!.NodeId);
  Assert.Equal(output.Id,copied.Slots[1].Self!.PortId);
 }
 [Fact] public void Variable_index_keeps_actual_variable_identity_while_output_source_is_remapped()
 {
  using var f=new SessionGraphFixture();using(var variables=f.Session.Editing.Host.BeginVariableEdit())
  {variables.Variables.Add(new GlobalVariable("索引",0L,"Integer"));f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,variables:variables));}
  var variable=Assert.Single(f.Session.Document.GlobalVariables);var source=f.Add("BlobAnalysis");var target=f.Add("BlobAnalysis",200,20);f.Connect(source,target);
  var port=f.Session.GetNodeSchema(f.Root.Id,source.Id)!.Outputs.Single(item=>item.Id=="field:8ca10000000040008000000000000019");
  var index=new VariableReference(f.Session.Document.Id,variable.Id,TypeRef.ScalarOf(ScalarKind.Int64));
  var reference=new OutputReference(source.Id,port.Id,port.Type,selection:new BindingSelection.AtVariable(index),scopeId:f.Root.Id,storage:port.Storage);
  const string field="field:43bda03261df55b88f963bd18f2ba4d9";var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,target.Id);
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{field,new ReferenceSetting(reference,BindingConversion.Float64ToInt64Checked)}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var result=f.Session.PasteNodeClipboard(f.Root.Id,f.Session.CaptureNodeClipboard(f.Root.Id,new[]{source,target}),new CanvasPoint(400,300));
  var copied=Assert.IsType<ReferenceSetting>(result.Nodes[1].Configuration.Fields.Read(field));
  Assert.Equal(result.Nodes[0].Id,copied.Reference.NodeId);Assert.Equal(variable.Id,Assert.IsType<BindingSelection.AtVariable>(copied.Reference.Selection).Reference.VariableId);
 }
 [Fact] public void Variable_identity_is_preserved_and_absent_variable_cannot_be_silently_rebound()
 {
  using var f=new SessionGraphFixture();using(var variables=f.Session.Editing.Host.BeginVariableEdit())
  {variables.Variables.Add(new GlobalVariable("阈值",8L,"Integer"));f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,variables:variables));}
  var actual=Assert.Single(f.Session.Document.GlobalVariables);var node=f.Add("BlobAnalysis");
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);const string field="field:43bda03261df55b88f963bd18f2ba4d9";
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{field,new VariableSetting(new VariableReference(f.Session.Document.Id,actual.Id,TypeRef.ScalarOf(ScalarKind.Int64)))}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var fragment=f.Session.CaptureNodeClipboard(f.Root.Id,new[]{node});var copied=f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(200,200));
  Assert.Equal(actual.Id,Assert.IsType<VariableSetting>(Assert.Single(copied.Nodes).Configuration.Fields.Read(field)).Reference.VariableId);
  Assert.True(f.Session.Undo());
  var remove=f.Session.PrepareGraphEdit(f.Root.Id);remove.DeleteNodesAndConnections(new[]{node},Array.Empty<ConnectionDefinition>());f.Session.CommitGraphEdit(remove,"删除源节点");
  using(var variables=f.Session.Editing.Host.BeginVariableEdit())
  {variables.Variables.Clear();f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,variables:variables));}
  var count=f.Session.HistoryEntries.Count;Assert.Empty(f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(250,250)).Nodes);
  Assert.Equal(count,f.Session.HistoryEntries.Count);Assert.True(f.Session.Undo());Assert.Same(actual,Assert.Single(f.Session.Document.GlobalVariables));
  Assert.True(f.Session.Undo());Assert.Single(f.Session.PasteNodeClipboard(f.Root.Id,fragment,new CanvasPoint(250,250)).Nodes);
 }
 [Fact] public void Inactive_roi_with_variable_index_is_validated_for_target_scope_and_current_variable_type()
 {
  using var f=new SessionGraphFixture();using(var variables=f.Session.Editing.Host.BeginVariableEdit())
  {variables.Variables.Add(new GlobalVariable("索引",0L,"Integer"));f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,variables:variables));}
  var variable=Assert.Single(f.Session.Document.GlobalVariables);var source=f.Add("BlobAnalysis");var target=f.Add("BlobAnalysis",200,20);
  var port=f.Session.GetNodeSchema(f.Root.Id,source.Id)!.Outputs.Single(item=>item.Id=="field:8ca10000000040008000000000000019");
  var index=new VariableReference(f.Session.Document.Id,variable.Id,TypeRef.ScalarOf(ScalarKind.Int64));
  var reference=new OutputReference(source.Id,port.Id,port.Type,selection:new BindingSelection.AtVariable(index),scopeId:f.Root.Id,storage:port.Storage);
  const string field="input.roi.rectangle.center-x";var edit=f.Session.Editing.Host.BeginEdit(f.Root.Id,target.Id);
  edit.ApplyFields(new Dictionary<string,ParameterSetting>{{field,new ReferenceSetting(reference)}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{edit}));
  var types=new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(new Dictionary<string,string>{{"Integer","System.Int64"},{"Double","System.Double"}});
  var original=Path.Combine(f.CaseDirectory,"inactive.solution");f.Session.SaveAs(original,Path.Combine(f.CaseDirectory,"backup"),f.CaseDirectory,types,SessionGraphFixture.Options,_=>null);
  var json=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(original))!;var flows=json["Workflows"]!.AsArray();var second=flows[0]!.DeepClone();
  second["Id"]=Guid.NewGuid().ToString("N");second["Name"]="目标";second["Nodes"]=new System.Text.Json.Nodes.JsonArray();second["Connections"]=new System.Text.Json.Nodes.JsonArray();flows.Add(second);
  var path=Path.Combine(f.CaseDirectory,"inactive-multi.solution");File.WriteAllText(path,json.ToJsonString());
  var session=f.Manager.Open(path,_=>types,_=>null,SessionGraphFixture.Options);var sourceFlow=session.GetWorkflows().Single(flow=>flow.Id==f.Root.Id);var targetFlow=session.GetWorkflows().Single(flow=>flow.Name=="目标");
  var currentTarget=session.FindNode(sourceFlow.Id,target.Id)!;var fragment=session.CaptureNodeClipboard(sourceFlow.Id,new[]{currentTarget});
  var accepted=session.PasteNodeClipboard(sourceFlow.Id,fragment,new CanvasPoint(400,200));Assert.Single(accepted.Nodes);Assert.True(session.Undo());
  var before=session.HistoryEntries.Count;Assert.ThrowsAny<Exception>(()=>session.PasteNodeClipboard(targetFlow.Id,fragment,new CanvasPoint(400,200)));
  Assert.Equal(before,session.HistoryEntries.Count);Assert.Empty(session.GetNodes(targetFlow.Id));
  var remove=session.PrepareGraphEdit(sourceFlow.Id);remove.DeleteNodesAndConnections(new[]{currentTarget},Array.Empty<ConnectionDefinition>());session.CommitGraphEdit(remove,"删除源");
  using(var variables=session.Editing.Host.BeginVariableEdit())
  {variables.Variables.Single().Type="Double";variables.SetValue(variable.Id,0d);session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,variables:variables));}
  before=session.HistoryEntries.Count;Assert.Empty(session.PasteNodeClipboard(sourceFlow.Id,fragment,new CanvasPoint(500,200)).Nodes);
  Assert.Equal(before,session.HistoryEntries.Count);Assert.Single(session.GetNodes(sourceFlow.Id));
  var occupied=session.GetWorkflows().SelectMany(flow=>session.GetNodes(flow.Id)).Select(node=>node.GlobalIndex).ToHashSet();
  var expected=Enumerable.Range(1,occupied.Count+1).First(index=>!occupied.Contains(index));
  var graph=session.PrepareGraphEdit(targetFlow.Id);var created=session.CreateNode(graph,"ImageLoad",new CanvasPoint(600,200));
  Assert.Equal(expected,created.GlobalIndex);session.CommitGraphEdit(graph,"验证编号未泄漏");
 }
}
