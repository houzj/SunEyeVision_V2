using System.IO;
using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Runtime;
using Xunit;
[assembly:CollectionBehavior(DisableTestParallelization=true)]
namespace SunEyeVision.WorkflowGraph.Tests;
internal sealed class SessionGraphFixture : IDisposable
{
 private static readonly Lazy<RuntimeDirectorySnapshot> Trusted=new(()=>new ReleasePayloadAuthority(Path.GetFullPath(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT") ?? throw new InvalidOperationException("必须显式指定本工作区 S2_PRODUCT_ROOT 正式载荷。"))).PrepareRuntimeDirectory());
 public readonly DocumentSessionManager Manager;
 public readonly DocumentSession Session;
 public readonly WorkflowDefinition Root;
 public readonly string CaseDirectory;
 public static FrozenVariableTypeCatalog Types => new(Array.Empty<KeyValuePair<string,string>>());
 public static JsonSerializerOptions Options => new();
 public SessionGraphFixture()
 {
  var caseRoot=Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? throw new InvalidOperationException("必须显式指定本工作区 S2_CASE_ROOT。");
  CaseDirectory=Path.Combine(Path.GetFullPath(caseRoot),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(CaseDirectory);
  Manager=new DocumentSessionManager(Trusted.Value);Session=Manager.CreateNew("图事务回归");Root=Session.GetWorkflows().Single();
 }
 public NodeDefinition Add(string tool="ImageLoad",double x=10,double y=20)
 { var draft=Session.PrepareGraphEdit(Root.Id);var node=Session.CreateNode(draft,tool,new CanvasPoint(x,y));Session.CommitGraphEdit(draft,"添加节点");return node; }
 public ConnectionDefinition Connect(NodeDefinition from,NodeDefinition to)
 { var edge=new ConnectionDefinition(Guid.NewGuid().ToString("N"),from.Id,to.Id,"RightPort","LeftPort");var draft=Session.PrepareGraphEdit(Root.Id);draft.AddConnection(edge);Session.CommitGraphEdit(draft,"连接节点");return edge; }
 public string Save(string name="document.solution")
 {var path=Path.Combine(CaseDirectory,name);Session.SaveAs(path,Path.Combine(CaseDirectory,"backup"),CaseDirectory,Types,Options,_=>null);return path;}
 public void Dispose()=>Manager.Dispose();
}
