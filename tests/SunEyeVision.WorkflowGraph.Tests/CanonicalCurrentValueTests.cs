using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Infrastructure.ImageSources;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class CanonicalCurrentValueTests
{
 [Fact] public void Runtime_bound_parameter_value_survives_graph_history_save_and_reopen()
 {
  using var f=new SessionGraphFixture(); var source=f.Add(); var blob=f.Add("BlobAnalysis",200,20); f.Connect(source,blob);
  var image=Path.Combine(f.CaseDirectory,"sample.bmp");
  using(var writer=new BinaryWriter(File.Create(image)))
  {
   writer.Write((ushort)0x4d42);writer.Write(54+8*8*3);writer.Write(0);writer.Write(54);writer.Write(40);writer.Write(8);writer.Write(8);writer.Write((ushort)1);writer.Write((ushort)24);writer.Write(0);writer.Write(8*8*3);for(int i=0;i<4;i++)writer.Write(0);for(int i=0;i<8*8*3;i++)writer.Write((byte)100);
  }
  var a=f.Session.Editing.Host.BeginEdit(f.Root.Id,source.Id); var paths=a.SettingsSchema.Inputs.Single(p=>p.Id=="local.paths");
  a.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new SequenceValue(paths.Type,new object?[]{image}))}});
  var b=f.Session.Editing.Host.BeginEdit(f.Root.Id,blob.Id);var port=f.Session.GetNodeSchema(f.Root.Id,source.Id)!.Outputs.Single(p=>p.Id=="output.image");
  const string threshold="field:43bda03261df55b88f963bd18f2ba4d9";
  b.ApplyFields(new Dictionary<string,ParameterSetting>{{"input.image",new ReferenceSetting(new OutputReference(source.Id,port.Id,port.Type,new[]{"data"},scopeId:f.Root.Id))},{threshold,new ReferenceSetting(new OutputReference(source.Id,port.Id,port.Type,new[]{"width"},scopeId:f.Root.Id))}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{a,b}));
  using(var factory=new ImageSourceHostInputFactory()) using(var batch=f.Session.CaptureRunBatch(new[]{f.Root.Id},hostFactory:factory)) using(var result=batch.RunAsync(_=>{}).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult()) Assert.True(result.Workflows[f.Root.Id].Success);
  Assert.Equal(8L,blob.Configuration.ReadCurrentParameter(threshold)?.Value);
  var graph=f.Session.PrepareGraphEdit(f.Root.Id);graph.MoveNode(blob,new CanvasPoint(230,40));f.Session.CommitGraphEdit(graph,"移动节点");Assert.True(f.Session.Undo());
  Assert.Equal(8L,blob.Configuration.ReadCurrentParameter(threshold)?.Value);
  var path=f.Save();var opened=f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  Assert.Equal(8L,opened.FindNode(f.Root.Id,blob.Id)!.Configuration.ReadCurrentParameter(threshold)?.Value);
 }
 [Fact] public void Current_formal_field_configuration_survives_save_and_reopen()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(9L)}});f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var path=f.Save();var opened=f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  var actual=opened.FindNode(f.Root.Id,node.Id);Assert.NotNull(actual);Assert.Equal(9L,Assert.IsType<ConstantSetting>(actual.Configuration.Fields.Read("sequence.initial")).Value);
 }
}
