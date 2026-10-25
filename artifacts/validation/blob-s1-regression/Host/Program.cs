using System.IO;
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
 [STAThread] static int Main(string[] args)
 {
  if(args.Contains("workflow"))return WorkflowCases.Run().GetAwaiter().GetResult();
  try {
   ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));PluginLoader.MarkAsLoaded();
   const string fixedRoot="D:/MyWork/SunEyeVision_V2/artifacts/worktrees/blob-s1-owner-migration-20261025";
   var sourceJson=File.ReadAllText(fixedRoot+"/artifacts/validation/blob-s1-owner/Host/bin/fixtures/legacy.solution");
   var folder=Path.Combine(AppContext.BaseDirectory,"fixtures");Directory.CreateDirectory(folder);
   var repository=new SolutionRepository(Path.Combine(folder,"recovery"));
   var rows=new List<object>();
   JsonNode Setting(JsonNode document)=>document["Workflows"]![0]!["Nodes"]![1]!["ParamSettings"]!["Settings"]![0]!;
   void EditReference(JsonNode document,Action<JsonNode> edit){var setting=Setting(document);var expression=JsonNode.Parse(setting["ReferenceExpression"]!.GetValue<string>())!;edit(expression);setting["ReferenceExpression"]=expression.ToJsonString();}
   void Case(string name,bool expected,Action<JsonNode> mutate)
   {
    var document=JsonNode.Parse(sourceJson)!;mutate(document);var path=Path.Combine(folder,name+".solution");File.WriteAllText(path,document.ToJsonString());
    var bytes=File.ReadAllBytes(path);var time=File.GetLastWriteTimeUtc(path);var result=repository.LoadCandidate(path);
    var unchanged=bytes.SequenceEqual(File.ReadAllBytes(path))&&time==File.GetLastWriteTimeUtc(path);
    var passed=(result.Candidate is not null)==expected&&unchanged;
    rows.Add(new{name,passed,expectedAccepted=expected,accepted=result.Candidate is not null,unchanged,sourceSha=Convert.ToHexString(SHA256.HashData(bytes)),sourceMtime=time,diagnostics=result.Diagnostics.Select(d=>d.Message).ToArray()});
   }
   if(args.Contains("deep"))
   {
    JsonNode Parameters(JsonNode d)=>d["Workflows"]![0]!["Nodes"]![0]!["Parameters"]!;
    void Variable(JsonNode d)=>d["GlobalVariables"]=new JsonArray(new JsonObject{["Id"]="v1",["Name"]="Value",["Type"]="Integer",["Value"]=new JsonObject{["Type"]="Integer",["Value"]=new JsonArray(7)}});
    Case("unknown-blob-parameter",false,d=>Parameters(d)["future"]=true);
    Case("unknown-judgment-member",false,d=>Parameters(d)["ResultPresentation"]!["Judgments"]![0]!["future"]=true);
    Case("variable-canonical",true,Variable);
    Case("variable-unknown-wrapper",false,d=>{Variable(d);d["GlobalVariables"]![0]!["Value"]!["future"]=true;});
    Case("variable-unknown-producer",false,d=>{Variable(d);d["GlobalVariables"]![0]!["ProducerBinding"]=new JsonObject{["WorkflowId"]="wf1",["NodeId"]="source",["ResultName"]="BlobAreas",["future"]=true};});
    Case("device-declared-settings-dictionary",true,d=>d["Devices"]=new JsonArray(new JsonObject{["Id"]="device1",["Name"]="Camera",["Settings"]=new JsonObject{["UserSetting"]=12}}));
    Case("device-unknown-member",false,d=>d["Devices"]=new JsonArray(new JsonObject{["Id"]="device1",["Name"]="Camera",["future"]=true}));
    File.WriteAllText(Path.Combine(folder,"deep-results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine(JsonSerializer.Serialize(rows));
    return rows.All(row=>JsonSerializer.SerializeToElement(row).GetProperty("passed").GetBoolean())?0:1;
   }
   var outputs=BlobOutputContract.Describe().ToDictionary(o=>o.Name);
   OutputReference Legacy(string name)=>new("source","name:"+name,BindingClrAdapter.DescribeOutput(outputs[name]).Type,scopeId:"wf1",storage:BindingClrAdapter.DescribeOutput(outputs[name]).Storage);
   JsonNode Nested(string name)=>JsonNode.Parse(BindingReferenceSerializer.Serialize(Legacy(name)))!["Reference"]!.DeepClone();
   void Dynamic(JsonNode d,string name,Action<JsonNode>? edit=null)=>EditReference(d,r=>{var index=Nested(name);edit?.Invoke(index);r["Reference"]!["selection"]=new JsonObject{["kind"]="AtField",["reference"]=index};});
   Case("dynamic-index-integer",true,d=>Dynamic(d,"FilteredBlobCount"));
   Case("dynamic-index-float",false,d=>Dynamic(d,"AverageArea"));
   Case("dynamic-index-cross-scope",false,d=>Dynamic(d,"FilteredBlobCount",r=>r["scopeId"]="wf2"));
   Case("dynamic-index-missing-node",false,d=>Dynamic(d,"FilteredBlobCount",r=>r["nodeId"]="missing"));
   Case("unknown-tool",false,d=>d["Workflows"]![0]!["Nodes"]![0]!["ToolType"]="NoApprovedTool");
   Case("invalid-conversion-operation",false,d=>Setting(d)["Conversion"]=777);
   Case("float-to-int-no-conversion",false,d=>{Setting(d)["ParameterName"]="Threshold";});
   Case("float-to-int-explicit-checked",true,d=>{Setting(d)["ParameterName"]="Threshold";Setting(d)["Conversion"]=(int)BindingConversion.Float64ToInt64Checked;});
   File.WriteAllText(Path.Combine(folder,"results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
   Console.WriteLine(JsonSerializer.Serialize(rows));
   return rows.All(row=>JsonSerializer.SerializeToElement(row).GetProperty("passed").GetBoolean())?0:1;
  }catch(Exception fault){Console.Error.WriteLine(fault);return 2;}
 }
}
