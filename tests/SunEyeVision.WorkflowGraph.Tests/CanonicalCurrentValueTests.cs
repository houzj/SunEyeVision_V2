using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class CanonicalCurrentValueTests
{
 [Fact] public void Current_formal_field_configuration_survives_save_and_reopen()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(9L)}});f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var path=f.Save();var opened=f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  var actual=opened.FindNode(f.Root.Id,node.Id);Assert.NotNull(actual);Assert.Equal(9L,Assert.IsType<ConstantSetting>(actual.Configuration.Fields.Read("sequence.initial")).Value);
 }
}
