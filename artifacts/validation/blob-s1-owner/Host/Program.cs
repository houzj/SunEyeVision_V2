using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Tool.BlobAnalysis;
using SunEyeVision.Workflow;
internal static class Program
{
 [STAThread] static int Main()
 {
  try {
   ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));
   PluginLoader.MarkAsLoaded(); // Dedicated fixture host registers the real Blob type; not production discovery acceptance.
   var output=BlobOutputContract.Describe().Single(o=>o.Name=="BlobAreas");
   var port=BindingClrAdapter.DescribeOutput(output);
   var reference=new OutputReference("source","name:BlobAreas",port.Type,selection:new BindingSelection.AtConstant(1),scopeId:"wf1",storage:port.Storage);
   var envelope=JsonNode.Parse(BindingReferenceSerializer.Serialize(reference))!;envelope["ContractMajor"]=4;
   var solution=new Solution{Id="legacy",Name="Legacy Blob",SchemaVersion=1};
   var flow=new Workflow{Id="wf1",Name="Workflow one"};
   var source=new AlgorithmNode("source","source","source",new BlobAnalysisTool()){ToolType="BlobAnalysis",Parameters=new BlobAnalysisParameters()};
   var consumer=new AlgorithmNode("consumer","consumer","consumer",new BlobAnalysisTool()){ToolType="BlobAnalysis",Parameters=new BlobAnalysisParameters()};
   consumer.ParamSettings.SetSetting(new ParamSetting{ParameterName="FilterAreaMin",BindingType=BindingType.Binding,SourceNodeId="source",SourceWorkflowId="wf1",SourceFieldId=output.FieldId,SourceProperty="BlobAreas[1]",ReferenceExpression=envelope.ToJsonString()});
   flow.Nodes.Add(source);flow.Nodes.Add(consumer);solution.Workflows.Add(flow);
   var folder=Path.Combine(AppContext.BaseDirectory,"fixtures");Directory.CreateDirectory(folder);
   var repository=new SolutionRepository(Path.Combine(folder,"recovery"));
   var sourceJson=JsonSerializer.Serialize(solution,WorkflowSerializationOptions.Default);
   var path=Path.Combine(folder,"legacy.solution");File.WriteAllText(path,sourceJson);
   var bytes=File.ReadAllBytes(path);var time=File.GetLastWriteTimeUtc(path);
   var loaded=repository.LoadCandidate(path);
   if(loaded.Candidate is null)throw new Exception("Real Repository legacy candidate failed: "+string.Join(";",loaded.Diagnostics.Select(d=>d.JsonPath+":"+d.Message)));
   if(loaded.Candidate.SchemaVersion!=2)throw new Exception("Format was not promoted after owner migration");
   var migrated=loaded.Candidate.Workflows.Single().Nodes.Single(n=>n.Id=="consumer").ParamSettings.GetSetting("FilterAreaMin")!;
   var actual=BindingReferenceSerializer.Deserialize(migrated.ReferenceExpression!);
   if(actual.PortId!=port.Id || actual.ScopeId!="wf1" || actual.Selection is not BindingSelection.AtConstant{Index:1})throw new Exception("Owner did not preserve explicit selected identity");
   if(!bytes.SequenceEqual(File.ReadAllBytes(path))||time!=File.GetLastWriteTimeUtc(path))throw new Exception("Read migration changed source file");
   var cases=new System.Collections.Generic.List<object>();
   void RejectFile(string name,Action<JsonNode> mutate,string? fixtureJson=null)
   {
    var document=JsonNode.Parse(fixtureJson??sourceJson)!;mutate(document);
    var negativePath=Path.Combine(folder,name+".solution");File.WriteAllText(negativePath,document.ToJsonString());
    var oldBytes=File.ReadAllBytes(negativePath);var oldTime=File.GetLastWriteTimeUtc(negativePath);
    var rejected=repository.LoadCandidate(negativePath);
    bool unchanged=oldBytes.SequenceEqual(File.ReadAllBytes(negativePath))&&oldTime==File.GetLastWriteTimeUtc(negativePath);
    cases.Add(new{name,passed=rejected.Candidate is null&&unchanged,candidateRejected=rejected.Candidate is null,unchanged,diagnostics=rejected.Diagnostics.Select(d=>d.Message).ToArray()});
   }
   void EditReference(JsonNode document,Action<JsonNode> edit)
   {
    var parameter=document["Workflows"]![0]!["Nodes"]![1]!["ParamSettings"]!["Settings"]![0]!;
    var expr=JsonNode.Parse(parameter["ReferenceExpression"]!.GetValue<string>())!;edit(expr);parameter["ReferenceExpression"]=expr.ToJsonString();
   }
   RejectFile("unknown-alias",d=>EditReference(d,r=>r["Reference"]!["fieldId"]="name:MissingApprovedAlias"));
   RejectFile("unknown-node",d=>EditReference(d,r=>r["Reference"]!["nodeId"]="missing"));
   RejectFile("cross-scope",d=>EditReference(d,r=>r["Reference"]!["scopeId"]="wf2"));
   RejectFile("changed-type",d=>EditReference(d,r=>r["Reference"]!["expectedType"]!["maximum"]=0));
   RejectFile("unknown-type-member",d=>EditReference(d,r=>r["Reference"]!["expectedType"]!["future"]=true));
   RejectFile("unknown-workflow-member",d=>d["Workflows"]![0]!["future"]=true);
   RejectFile("unknown-node-member",d=>d["Workflows"]![0]!["Nodes"]![0]!["future"]=true);
   RejectFile("connection-cycle",d=>d["Workflows"]![0]!["Connections"]=new JsonArray(new JsonObject{["Id"]="back",["SourceNodeId"]="consumer",["TargetNodeId"]="source"}));
   var savedPath=Path.Combine(folder,"current.solution");
   if(!repository.Save(loaded.Candidate,savedPath))throw new Exception("Actual atomic save failed: "+repository.LastError);
   var firstSaved=File.ReadAllBytes(savedPath);
   var currentJson=System.Text.Encoding.UTF8.GetString(firstSaved);
   var currentLoad=repository.LoadCandidate(savedPath);
   if(currentLoad.Candidate is null)throw new Exception("Canonical current document failed owner validation");
   RejectFile("current-unknown-node",d=>EditReference(d,r=>r["Reference"]!["nodeId"]="missing"),currentJson);
   RejectFile("current-changed-type",d=>EditReference(d,r=>r["Reference"]!["expectedType"]!["maximum"]=0),currentJson);
   RejectFile("current-cannot-contain-legacy4",d=>EditReference(d,r=>r["ContractMajor"]=4),currentJson);
   RejectFile("current-cannot-use-name-alias",d=>EditReference(d,r=>r["Reference"]!["fieldId"]="name:BlobAreas"),currentJson);
   using(var savedDoc=JsonDocument.Parse(firstSaved))if(savedDoc.RootElement.GetProperty("SchemaVersion").GetInt32()!=2)throw new Exception("Save did not write format2");
   loaded.Candidate.Name="Second save";
   if(!repository.Save(loaded.Candidate,savedPath))throw new Exception("Second actual save failed: "+repository.LastError);
   if(!File.ReadAllBytes(repository.GetRecoveryPath(savedPath)).SequenceEqual(firstSaved))throw new Exception("Previous file backup differs");
   if(!bytes.SequenceEqual(File.ReadAllBytes(path))||time!=File.GetLastWriteTimeUtc(path))throw new Exception("Saving candidate changed legacy source file");
   var multi=JsonNode.Parse(sourceJson)!;
   var secondFlow=multi["Workflows"]![0]!.DeepClone();secondFlow["Id"]="wf2";
   var secondSetting=secondFlow["Nodes"]![1]!["ParamSettings"]!["Settings"]![0]!;secondSetting["SourceWorkflowId"]="wf2";
   var secondExpr=JsonNode.Parse(secondSetting["ReferenceExpression"]!.GetValue<string>())!;secondExpr["Reference"]!["scopeId"]="wf2";secondSetting["ReferenceExpression"]=secondExpr.ToJsonString();
   ((JsonArray)multi["Workflows"]!).Add(secondFlow);
   var multiPath=Path.Combine(folder,"duplicate-nodeids-in-different-workflows.solution");File.WriteAllText(multiPath,multi.ToJsonString());
   var multiLoad=repository.LoadCandidate(multiPath);if(multiLoad.Candidate?.Workflows.Count!=2)throw new Exception("Workflow scope isolation rejected: "+string.Join(";",multiLoad.Diagnostics.Select(d=>d.Message)));
   File.WriteAllText(Path.Combine(folder,"negative-results.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
   if(cases.Any(c=>!JsonSerializer.SerializeToElement(c).GetProperty("passed").GetBoolean()))throw new Exception("Negative cases failed; see negative-results.json");
   Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",stage="real Repository format1 binding4 owner mapping to format2 binding5 candidate",sourceSha=Convert.ToHexString(SHA256.HashData(bytes)),diagnostics=loaded.Diagnostics.Select(d=>d.Code),scope="real Blob declarations and SDK catalog, not new Runtime producer discovery/UI/Release"}));
   return 0;
  }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
 }
}


