using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Tool.BlobAnalysis;
using SunEyeVision.Workflow;

if(args.Contains("inspect-default")){var config=new BlobAnalysisParameters();Console.WriteLine(JsonSerializer.Serialize(new{judgments=config.ResultPresentation.Judgments.Select(item=>item.Key),graphics=config.ResultPresentation.Graphics.Select(item=>item.Key),texts=config.ResultPresentation.Texts.Select(item=>item.Key),issues=config.ValidateStoredConfiguration()}));return 0;}
if(args.Contains("export-fields"))
{
    var defaults=BlobInputContract.CreateDefaults();
    var fields=BlobInputContract.Fields.Select(field=>new{
        field.LogicalKey,field.LegacyPath,field.Port,field.Presentation,
        defaultKind=defaults[field.Port.Id] is UnsetSetting?"Unset":"Constant",
        defaultValue=defaults[field.Port.Id] is ConstantSetting literal?literal.Value:null,
        unsetMeaning="Absent optional setting; distinct from null, empty sequence and empty Region",
        unitPolicy="Only coordinate frames declare units in TypeRef; scalar physical units are not encoded by this Contracts version"
    });
    Console.WriteLine(JsonSerializer.Serialize(new{specSha="94DA12876AB19360F681378B1FFAB2D2139330EDF1701121CCFBADC752A8ADD3",fields,groups=BlobInputContract.Groups,legacyStyleAliases=BlobInputContract.LegacyStyleAliases,outputs=BlobInputContract.Describe().Outputs},new JsonSerializerOptions{WriteIndented=true}));
    return 0;
}

var rows=new List<object>();void Check(bool ok,string error){if(!ok)throw new Exception(error);}
void Test(string name,Action run){try{run();rows.Add(new{name,passed=true});}catch(Exception error){rows.Add(new{name,passed=false,error=error.ToString()});}}
ToolSchema? schema=null;
Test("explicit-117-schema-33-groups-79-leaves-30-outputs",()=>{schema=BlobInputContract.Describe();Check(schema.Inputs.Count==117&&schema.Outputs.Count==30&&BlobInputContract.Groups.Count==33&&BlobInputContract.Fields.Count(f=>f.LogicalKey.StartsWith("presentation."))==79,"field coverage differs");var ids=BlobInputContract.Groups.SelectMany(group=>group.PortIds).ToArray();Check(ids.Length==117&&ids.Distinct().Count()==117&&ids.ToHashSet().SetEquals(schema.Inputs.Select(port=>port.Id)),"group-to-port map incomplete");});
Test("defaults-are-valid-and-resources-unset",()=>{var contract=schema??BlobInputContract.Describe();var defaults=BlobInputContract.CreateDefaults();Check(defaults.Count==117,"defaults incomplete");var draft=new ParameterEditSession(contract.Inputs,defaults);Check(!draft.HasInvalidDraft,"default draft invalid");Check(defaults[BlobInputContract.Fields.Single(field=>field.LogicalKey=="input.image").Port.Id] is UnsetSetting,"image default is not Unset");Check(defaults[BlobInputContract.Fields.Single(field=>field.LogicalKey=="input.inspection.local").Port.Id] is UnsetSetting,"inspection default is not Unset");});
Test("stored-pure-default-config-valid",()=>Check(BlobInputContract.ValidateStoredConfiguration(new()).Count==0,"default persisted config rejected"));
Test("closed-enums-reject-direct-draft-and-runtime-config",()=>{var config=new BlobAnalysisParameters{Type=(ThresholdType)999,SortMode=(BlobSortMode)999};Check(config.ValidateStoredConfiguration().Count==2&&!config.Validate().IsValid,"invalid stored/runtime choices accepted");var draft=new ParameterEditSession((schema??BlobInputContract.Describe()).Inputs,BlobInputContract.CreateDefaults());var typePort=BlobInputContract.Fields.Single(f=>f.LogicalKey=="setting.Type").Port;Check(!draft.Set(typePort.Id,new ConstantSetting("future-mode")),"draft escaped choices");});
Test("missing-presentation-entry-rejected",()=>{var config=new BlobAnalysisParameters();config.ResultPresentation.Graphics.RemoveAt(0);Check(config.ValidateStoredConfiguration().Count!=0,"missing entry silently ignored");});
Test("rgba-neutral-record-uint8",()=>{var color=PresentationDeclarations.Color(10,20,30,40);Check(color.Get<long>("a")==40&&color.Get<long>("r")==10,"RGBA mapping differs");var failed=false;try{new RecordValue(PresentationDeclarations.Rgba,new Dictionary<string,object?>{{"r",256L},{"g",0L},{"b",0L},{"a",255L}});}catch(Exception error) when(error is ArgumentException or OverflowException){failed=true;}Check(failed,"RGBA exceeded UInt8 constraint");});
Test("roi-original-declaration-fingerprint-and-roundtrip",()=>{var frame=new CoordinateFrame("image:input",CoordinateDomain.Image,CoordinateUnit.Pixel);var point=TypeRef.GeometryOf(GeometryKind.Point,frame);var original=TypeRef.RecordOf("sdk.roi-shape",new[]{new Port("kind",DataType.Text),new Port("center",point,false),new Port("direction-x",DataType.Number,false),new Port("direction-y",DataType.Number,false),new Port("radius",DataType.Number,false),new Port("inner-radius",DataType.Number,false),new Port("outer-radius",DataType.Number,false),new Port("width",DataType.Number,false),new Port("height",DataType.Number,false),new Port("angle-degrees",DataType.Number,false),new Port("start-radians",DataType.Number,false),new Port("end-radians",DataType.Number,false),new Port("major-arc",DataType.Boolean,false),new Port("vertices",TypeRef.SequenceOf(point),false),new Port("closed",DataType.Boolean,false)});Check(original.Fingerprint==RoiDeclarations.Shape.Fingerprint&&ReferenceEquals(RoiShapeValueAdapter.Declaration,RoiDeclarations.Shape),"ROI declaration changed");var reference=new OutputReference("roi","roi",RoiDeclarations.Sequence,scopeId:"flow");Check(BindingReferenceSerializer.Deserialize(BindingReferenceSerializer.Serialize(reference)).Type==TypeRef.SequenceOf(original),"ROI serializer roundtrip changed");});
Test("real-repository-load-closed-choice-rejects-original-unchanged",()=>{ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));PluginLoader.MarkAsLoaded();var folder=Path.Combine(AppContext.BaseDirectory,"fixtures");Directory.CreateDirectory(folder);var solution=new Solution{Id="field-map",SchemaVersion=2};var flow=new Workflow("flow","flow");flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=new BlobAnalysisParameters()});solution.Workflows.Add(flow);var document=JsonNode.Parse(JsonSerializer.Serialize(solution,WorkflowSerializationOptions.Default))!;document["Workflows"]![0]!["Nodes"]![0]!["Parameters"]!["Type"]=999;var path=Path.Combine(folder,"invalid-choice.solution");File.WriteAllText(path,document.ToJsonString());var bytes=File.ReadAllBytes(path);var time=File.GetLastWriteTimeUtc(path);var result=new SolutionRepository(Path.Combine(folder,"recovery")).LoadCandidate(path);Check(result.Candidate is null&&bytes.SequenceEqual(File.ReadAllBytes(path))&&time==File.GetLastWriteTimeUtc(path),"Load did not preserve/reject invalid choice");});
Test("unknown-and-duplicate-presentation-keys-rejected",()=>{var unknown=new BlobAnalysisParameters();unknown.ResultPresentation.Judgments.Add(new(){Key="future-business-rule"});Check(unknown.ValidateStoredConfiguration().Count!=0,"unknown judgment accepted");var duplicate=new BlobAnalysisParameters();duplicate.ResultPresentation.Graphics.Add(duplicate.ResultPresentation.Graphics[0]);Check(duplicate.ValidateStoredConfiguration().Count!=0,"duplicate graphic key accepted");});
Test("declarative-unset-defaults-prepare-publish-ack",()=>{var contract=schema??BlobInputContract.Describe();var document=Guid.NewGuid();var node=new BindingNodeId("flow","blob");var scope=new BindingScopeSnapshot(node,document,0,Array.Empty<BindingSource>(),Array.Empty<BindingNodeId>(),Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());var identity=new EditScopeIdentity(document,Guid.NewGuid(),node,0);var draft=new ParameterEditSession(contract.Inputs,BlobInputContract.CreateDefaults(),new ParameterBindingService(),scope,identity);Check(draft.Committed.Values.All(value=>value is not UnsetSetting),"declarative Unset leaked into committed settings");Check(draft.TryPrepare(identity,scope,out var proposal,out _),"default prepare rejected");draft.AcceptPublished(proposal!,draft.Committed,scope,false);Check(draft.Identity==identity&&!draft.HasInvalidDraft,"no-op default acknowledgement changed state");});
Test("integer-physical-overflow-is-invalid-draft",()=>{var contract=schema??BlobInputContract.Describe();var threshold=BlobInputContract.Fields.Single(field=>field.LogicalKey=="setting.Threshold").Port;var draft=new ParameterEditSession(contract.Inputs,BlobInputContract.CreateDefaults());Check(!draft.Set(threshold.Id,new ConstantSetting(long.MaxValue))&&draft.HasInvalidDraft,"Int32 overflow escaped draft validation");});
Test("style-aliases-cover-original-canonical-leaves",()=>{Check(BlobInputContract.LegacyStyleAliases.Count==2,"style aliases missing");foreach(var list in BlobInputContract.LegacyStyleAliases.Values)Check(list.Count==7&&list.All(id=>BlobInputContract.Fields.Any(field=>field.Port.Id==id&&field.LogicalKey.StartsWith("presentation.graphics."))),"style alias lost canonical leaves");});
Test("integrated-save-rejects-closed-choice-before-target-or-backup-change",()=>{
    var folder=Path.Combine(AppContext.BaseDirectory,"integrated-save");Directory.CreateDirectory(folder);
    var repository=new SolutionRepository(Path.Combine(folder,"recovery"));
    var solution=new Solution{Id="save-fields",SchemaVersion=2};var flow=new Workflow("flow","flow");
    var parameters=new BlobAnalysisParameters();flow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=parameters});solution.Workflows.Add(flow);
    var path=Path.Combine(folder,"field.solution");Check(repository.Save(solution,path),"initial valid Save failed");
    solution.Name="second-valid";Check(repository.Save(solution,path),"second valid Save failed");
    var backup=repository.GetRecoveryPath(path);var bytes=File.ReadAllBytes(path);var oldBackup=File.ReadAllBytes(backup);var targetTime=File.GetLastWriteTimeUtc(path);var backupTime=File.GetLastWriteTimeUtc(backup);
    parameters.Type=(ThresholdType)999;
    Check(!repository.Save(solution,path),"invalid choice Save succeeded");
    Check(bytes.SequenceEqual(File.ReadAllBytes(path))&&oldBackup.SequenceEqual(File.ReadAllBytes(backup))&&targetTime==File.GetLastWriteTimeUtc(path)&&backupTime==File.GetLastWriteTimeUtc(backup),"rejected Save changed target or prior backup");
    Check(repository.LoadCandidate(path).Candidate is not null,"last valid disk state cannot reopen");
});
Console.WriteLine(JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
return rows.All(row=>JsonSerializer.SerializeToElement(row).GetProperty("passed").GetBoolean())?0:1;
