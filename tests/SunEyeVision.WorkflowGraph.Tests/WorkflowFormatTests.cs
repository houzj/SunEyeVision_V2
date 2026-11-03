using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class WorkflowFormatTests
{
 [Theory] [InlineData("RuntimeConfiguration")] [InlineData("ParamSettings")] [InlineData("Parameters")]
 public void Legacy_node_fields_are_rejected_without_mutating_active_session(string field)
 {
  using var f=new SessionGraphFixture();f.Add();var path=f.Save();var root=JsonNode.Parse(File.ReadAllText(path))!;root["Workflows"]![0]!["Nodes"]![0]![field]=new JsonObject();
  var invalid=Path.Combine(f.CaseDirectory,"invalid-"+field+".solution");File.WriteAllText(invalid,root.ToJsonString());var before=SHA256.HashData(File.ReadAllBytes(invalid));
  Assert.ThrowsAny<Exception>(()=>f.Manager.Open(invalid,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));Assert.Same(f.Session,f.Manager.CurrentSession);Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(invalid)));
 }
}
