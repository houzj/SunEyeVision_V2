using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class NodeDraftFreezeTests
{
 [Fact] public void Host_input_query_rejects_foreign_same_id_node_instance()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var impostor=new NodeDefinition(node.Id,node.Name,node.ToolType,node.Configuration);
  Assert.Throws<InvalidOperationException>(()=>f.Session.GetNodeHostInputDeclaration(f.Root.Id,impostor));
 }
 [Fact] public void Draft_from_replaced_session_is_rejected_without_modifying_new_session()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(7L)}});
  var proposal=DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft});var replacement=f.Manager.CreateNew("replacement",true);
  Assert.Throws<InvalidOperationException>(()=>replacement.CommitEdit(proposal));Assert.Empty(replacement.GetNodes(replacement.GetWorkflows().Single().Id));
 }
}
