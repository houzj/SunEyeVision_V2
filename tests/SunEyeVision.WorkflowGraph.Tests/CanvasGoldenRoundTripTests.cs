using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SunEyeVision.Domain.Workflows;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class CanvasGoldenRoundTripTests
{
 [Fact] public void V4_roundtrip_retains_manual_path_and_coordinates()
 {
  using var f=new SessionGraphFixture();var a=f.Add();var b=f.Add("BlobAnalysis",240,100);var edge=f.Connect(a,b);
  var points=new[]{new CanvasPoint(10,20),new CanvasPoint(90,20),new CanvasPoint(90,100),new CanvasPoint(240,100)};
  var edit=f.Session.PrepareGraphEdit(f.Root.Id);edit.EditConnectionPath(edge,points);f.Session.CommitGraphEdit(edit,"折线");var path=f.Save();
  var opened=f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);var saved=Assert.Single(opened.GetConnections(f.Root.Id));
  Assert.Equal(points,saved.ManualPathPoints);Assert.Equal(new CanvasPoint(10,20),opened.GetNodes(f.Root.Id).Single(n=>n.Id==a.Id).Position);
 }
 [Fact] public void V3_golden_is_rejected_without_modifying_source_or_active_session()
 {
  using var f=new SessionGraphFixture();var path=Path.Combine(f.CaseDirectory,"old.solution");File.Copy(Path.Combine(AppContext.BaseDirectory,"manual-path-v3.golden.solution"),path);
  var before=SHA256.HashData(File.ReadAllBytes(path));Assert.ThrowsAny<Exception>(()=>f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));
  Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(path)));Assert.Same(f.Session,f.Manager.CurrentSession);
 }
 [Theory] [InlineData("PositionX")] [InlineData("PositionY")] [InlineData("Width")] [InlineData("Height")]
 public void Nonfinite_v4_layout_is_rejected_and_source_and_active_session_are_preserved(string field)
 {
  using var f=new SessionGraphFixture();f.Add();var path=f.Save();var text=File.ReadAllText(path);var root=JsonNode.Parse(text)!;root["Workflows"]![0]!["Nodes"]![0]![field]=JsonNode.Parse("1e999");
  var invalid=Path.Combine(f.CaseDirectory,"invalid.solution");File.WriteAllText(invalid,root.ToJsonString());var before=SHA256.HashData(File.ReadAllBytes(invalid));
  Assert.ThrowsAny<Exception>(()=>f.Manager.Open(invalid,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));Assert.Same(f.Session,f.Manager.CurrentSession);Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(invalid)));
 }
}
