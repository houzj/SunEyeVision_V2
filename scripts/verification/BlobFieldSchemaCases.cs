using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

var tool=new BlobTool();var defaults=new BlobSettings();var schema=tool.SettingsSchema;
if(args.Contains("inspect-default")){Console.WriteLine(JsonSerializer.Serialize(new{settings=defaults,issues=defaults.Validate()}));return 0;}
if(args.Contains("export-fields")){Console.WriteLine(JsonSerializer.Serialize(new{schema,inputs=tool.Describe(defaults).Inputs,outputs=BlobSchema.Outputs},new JsonSerializerOptions{WriteIndented=true}));return 0;}
var rows=new List<object>();
void Test(string name,Action action){try{action();rows.Add(new{name,passed=true});}catch(Exception e){rows.Add(new{name,passed=false,error=e.ToString()});}}
void Check(bool value,string error){if(!value)throw new Exception(error);}
void Reject(Action action){try{action();}catch(Exception e)when(e is ArgumentException or InvalidOperationException or JsonException or InvalidDataException or OverflowException){return;}throw new Exception("应拒绝的配置被接受");}
Test("runtime-4-inputs-54-settings-unique-declarations",()=>{Check(tool.Describe(defaults).Inputs.Count==4&&schema.Inputs.Count==54&&schema.Inputs.Select(p=>p.Id).Distinct().Count()==54,"字段数或身份重复");Check(schema.Presentation!.Fields.Select(f=>f.FieldId).ToHashSet().SetEquals(schema.Inputs.Select(p=>p.Id)),"元数据映射不完整");});
Test("XML-defaults-and-private-settings-draft",()=>{var session=new ToolSettingsEditSession(tool,defaults);Check(defaults.Validate().Count==0,"默认值无效");var id=schema.Presentation!.Fields.Single(f=>f.DisplayName==BlobParameterDefinition.Instance.Label("Threshold")).FieldId;session.ApplyFields(new Dictionary<string,object?>{{id,73L}},_=>{});Check(((BlobSettings)session.CaptureSettings()).Threshold==73&&defaults.Threshold==128,"私有草稿污染原值");Reject(()=>session.ApplyFields(new Dictionary<string,object?>{{id,long.MaxValue}},_=>{}));Check(((BlobSettings)session.CaptureSettings()).Threshold==73,"溢出污染有效草稿");});
Test("closed-enums-and-judgment-definition-identity",()=>{Check((defaults with{Type=(BlobThresholdMode)999}).Validate().Count>0,"非法枚举被接受");Check((defaults with{Judgments=defaults.Judgments.Skip(1).ToArray()}).Validate().Count>0,"缺判定定义未拒绝");Check((defaults with{Judgments=defaults.Judgments.Append(defaults.Judgments[0]).ToArray()}).Validate().Count>0,"重判定未拒绝");});
Test("strict-runtime-save-load-without-default-refill",()=>{
    Check(PluginLoader.LoadPlugins(),"插件装载失败");
    var flow=new Workflow("flow","新配置");flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(defaults with{Threshold=73})});var solution=new Solution();solution.Workflows.Add(flow);
    var folder=Path.Combine(Environment.GetEnvironmentVariable("S1_CASE_ROOT")!,"runtime-fields");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"valid.solution");var repository=new SolutionRepository();Check(repository.Save(solution,path),repository.LastError??"保存失败");
    Check(((BlobSettings)repository.LoadCandidate(path).Candidate!.Workflows[0].Nodes[0].RuntimeConfiguration!.CaptureSettings()).Threshold==73,"XML默认覆盖用户值");
    foreach(var member in new[]{"Threshold","SegmentationMode"}){var bad=JsonNode.Parse(File.ReadAllText(path))!;bad["Workflows"]![0]!["Nodes"]![0]!["RuntimeConfiguration"]!["Settings"]!.AsObject().Remove(member);var file=Path.Combine(folder,member+".solution");File.WriteAllText(file,bad.ToJsonString());var bytes=File.ReadAllBytes(file);Check(repository.LoadCandidate(file).Candidate is null&&bytes.SequenceEqual(File.ReadAllBytes(file)),"缺字段被补默认或改写源文件");}
});
Console.WriteLine(JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));return rows.All(row=>JsonSerializer.SerializeToElement(row).GetProperty("passed").GetBoolean())?0:1;
