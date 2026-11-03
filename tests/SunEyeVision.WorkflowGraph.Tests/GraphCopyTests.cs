using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class GraphCopyTests
{
 [Fact] public void Move_clears_manual_path_and_undo_restores_exact_path_and_actual_identity()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",240,100);var edge=f.Connect(a,b);
  var points=new[]{new CanvasPoint(10,20),new CanvasPoint(100,20),new CanvasPoint(100,100),new CanvasPoint(240,100)};
  var edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.EditConnectionPath(edge,points);f.Session.CommitGraphEdit(edit,"设置折线");
  var original=a.Position;var move=f.Session.PrepareGraphEdit(f.Root.Id);move.MoveNode(a,new CanvasPoint(50,60));f.Session.CommitGraphEdit(move,"移动");
  Assert.Null(edge.ManualPathPoints);Assert.True(f.Session.Undo());Assert.Same(a,f.Session.FindNode(f.Root.Id,a.Id));Assert.Equal(original,a.Position);Assert.Equal(points,edge.ManualPathPoints);
  Assert.True(f.Session.Redo());Assert.Equal(new CanvasPoint(50,60),a.Position);Assert.Null(edge.ManualPathPoints);
 }
 [Fact] public void Disconnect_retires_automatic_binding_and_history_restores_it()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",240,100);var edge=f.Connect(a,b);
  var bound=Assert.IsType<ReferenceSetting>(b.Configuration.Fields.Read("input.image"));Assert.Equal(a.Id,bound.Reference.NodeId);
  var draft=f.Session.PrepareGraphEdit(f.Root.Id);draft.DeleteNodesAndConnections(Array.Empty<NodeDefinition>(),new[]{edge});f.Session.CommitGraphEdit(draft,"断开");
  var disconnected=Assert.IsType<ReferenceSetting>(b.Configuration.Fields.Read("input.image"));Assert.False(string.IsNullOrWhiteSpace(disconnected.RebindingReason));
  Assert.True(f.Session.Undo());Assert.Equal(a.Id,Assert.IsType<ReferenceSetting>(b.Configuration.Fields.Read("input.image")).Reference.NodeId);
 }
 [Fact] public void Duplicate_id_instance_cannot_move_or_replace_actual_node()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var impostor=new NodeDefinition(node.Id,node.Name,node.ToolType,node.Configuration);
  var graph=f.Session.PrepareGraphEdit(f.Root.Id);Assert.Throws<InvalidOperationException>(()=>graph.MoveNode(impostor,new CanvasPoint(9,9)));Assert.Same(node,f.Session.FindNode(f.Root.Id,node.Id));
 }
 [Fact] public void Configuration_edit_undo_redo_preserves_real_node_instance()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(7L)}});f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  Assert.Equal(7L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);Assert.True(f.Session.Undo());Assert.Same(node,f.Session.FindNode(f.Root.Id,node.Id));Assert.True(f.Session.Redo());Assert.Equal(7L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);
 }
}
