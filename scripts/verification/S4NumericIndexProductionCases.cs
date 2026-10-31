using System.IO;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.NumericIndexFixtures;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Solutions;
using SunEyeVision.Workflow;
using Probe=SunEyeVision.NumericIndexFixtures.NumericIndexProbeDefinitionBase;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    private static readonly List<object> Reports=new();
    [STAThread] private static int Main()
    {
        Require(PluginLoader.LoadPlugins(),"产品Runtime注册失败");
        Register(typeof(NumericIndexQualifiedProbe),NumericIndexQualifiedProbe.Id);
        Register(typeof(NumericIndexStrictProbe),NumericIndexStrictProbe.Id);
        Register(typeof(NumericIndexSecondProbe),NumericIndexSecondProbe.Id);
        var first=ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback;
        foreach(var id in new[]{NumericIndexQualifiedProbe.Id,NumericIndexSecondProbe.Id})
            Case("跨工具显式字段ALL首项 "+id,()=>{using var f=new Fixture(id);f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source,"output.indices"));using var run=f.Run();ExpectInteger(f,run,37);});
        foreach(var index in new[]{0,1,2})
            Case("零基固定索引 "+index,()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtConstant(index)));using var run=f.Run();ExpectInteger(f,run,new long[]{37,90,173}[index]);});
        Case("严格消费者保持ExactlyOne",()=>{using var f=new Fixture(NumericIndexStrictProbe.Id);f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source,"output.indices"));using var run=f.Run();Require(!run.Success,"Strict错误取首项");Require(!f.State.Trace.Contains("target"),"失败后执行消费者");});
        foreach(var port in new[]{"output.index","output.index-array"})
            Case("动态输出索引 "+port,()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);var at=new BindingSelection.AtField(f.Ref(f.Index,port),first);f.Accept(f.Ref(f.Source,"output.indices",selection:at));using var run=f.Run();ExpectInteger(f,run,90);});
        foreach(var label in new[]{"empty-index"})
            Case("已准入普通索引读取失败回零 "+label,()=>{using var f=new Fixture(indexLabel:label);f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);var at=new BindingSelection.AtField(f.Ref(f.Index,label=="empty-index"?"output.index-array":"output.index"),first);f.Accept(f.Ref(f.Source,"output.indices",selection:at));using var run=f.Run();ExpectInteger(f,run,37);});
        foreach(var label in new[]{"empty-main","big-main"})
            Case("主源空或Int32溢出硬失败 "+label,()=>{using var f=new Fixture(sourceLabel:label);f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source,"output.indices"));using var run=f.Run();Require(!run.Success,"非法主值被回退");Require(!f.State.Trace.Contains("target"),"失败后执行消费者");});
        Case("默认Strict构造与JSON拒绝数组索引",()=>{using var f=new Fixture();var index=f.Ref(f.Index,"output.index-array");Reject(()=>new BindingSelection.AtField(index));var r=f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(index,first));var json=BindingReferenceSerializer.Serialize(r);Reject(()=>BindingReferenceSerializer.Deserialize(json));Require(BindingReferenceSerializer.Deserialize(json,first).Selection is BindingSelection.AtField,"显式上下文未读回");});
        Case("真实全局Int64数组动态索引",()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);var variable=f.AddVariable("index-array",new long[]{1,2},"Integer[]");var at=new BindingSelection.AtVariable(f.VariableReference(variable),first);f.Accept(f.Ref(f.Source,"output.indices",selection:at));using var run=f.Run();ExpectInteger(f,run,90);});
        foreach(var label in new[]{"missing-index","fail"})
            Case("生产者缺必需输出或执行失败不得回退 "+label,()=>{using var f=new Fixture(indexLabel:label);f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index"),first)));using var run=f.Run();Require(!run.Success&&!f.State.Trace.Contains("target"),"生产者失败被回退执行");});
        Case("并列及错误源身份不能被政策授权",()=>{using var f=new Fixture();Reject(()=>f.Host.Commit(f.Draft(f.Ref(f.Source,"output.indices"))));f.Connect(f.Source,f.Target);var wrong=new OutputReference("missing-node","output.indices",Probe.IntegerSequence,null,null,f.Flow.Id);Reject(()=>f.Host.Commit(f.Draft(wrong)));});
        Case("Strict浮点字段与资源目标不获首项政策",()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Host.Commit(f.Draft(f.Ref(f.Source,"output.values"),Probe.NumberInput));using var run=f.Run();Require(!run.Success&&!f.State.Trace.Contains("target"),"浮点字段错误取首项");Reject(()=>ParameterReferenceRules.ValidateTarget(new Port("image",DataType.Image),first));});
        Case("真实保存首次读取拥有目标字段政策并运行",()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first)));var path=Path.Combine(f.Directory,"integer.solution");var saved=new SolutionApplicationService(f.Manager).Save(path,()=>f.Editing.AcceptAll());Require(saved.FileWritten,saved.Error??"保存失败");var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("首次配置读取失败");f.Manager.SetCurrentSolution(reopened,registerInCatalog:false);var host=f.Manager.Session!.Editing.Host;using var run=f.Run(reopened,host);ExpectInteger(f,run,90);});
        Case("真实复制粘贴及撤销重做保留嵌套索引上下文",()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first)));var fragment=WorkflowFragmentCopy.Prepare(f.Flow,new[]{f.Source.Id,f.Index.Id,f.Target.Id},f.Flow.Id,id=>f.Document.Workflows.SingleOrDefault(x=>x.Id==id),(original,scope)=>new WorkflowNodeBase("copy-"+original.Id,"copy-"+original.Id,original.DispName,original.ToolType));f.Commands.Execute(new PasteWorkflowFragmentCommand(f.Flow,fragment,f.Engine,f.Document));var copied=fragment.Nodes.Single(n=>n.Id=="copy-target");Require(((ReferenceSetting)copied.RuntimeConfiguration!.Fields.Read(Probe.IntegerInput)).Reference.NodeId=="copy-source","复制来源未映射");using(var run=f.Run()){Require(run.Success,"复制后运行失败");Require(run.NodeResults[copied.Id].Result?.Output is NumericIndexProbeOutput {Index:90},"复制消费者结果不符");}f.Host.Undo();Require(!f.Flow.Nodes.Contains(copied),"未撤销粘贴");f.Host.Redo();Require(f.Flow.Nodes.Any(n=>ReferenceEquals(n,copied)),"重做改变实例");});
        Case("私有调试使用冻结整数政策且断边新捕获拒绝",()=>{using var f=new Fixture();var edge=f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first)));using var run=f.Run();ExpectInteger(f,run,90);using var owner=new OwnerLease();using var invocation=f.Debug(f.Host.BeginEdit(f.Flow.Id,f.Target.Id),owner.Owner,run);f.DeleteEdges(edge);using(var result=Wait(()=>invocation.ExecuteAsync()))Require(result.IsSuccess,"冻结调试失败");Reject(()=>{using var invalid=f.Debug(f.Host.BeginEdit(f.Flow.Id,f.Target.Id),owner.Owner,run);});});
        Case("取消草稿及已关闭Host不会接纳政策引用",()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);var draft=f.Draft(f.Ref(f.Source,"output.indices"));Require(f.Target.RuntimeConfiguration!.Fields.Read(Probe.IntegerInput) is ConstantSetting {Value:999L},"未确认草稿写入");f.Manager.CloseSolution(false);Reject(()=>f.Host.Commit(draft));});
        Case("SDK实际编译与解析桥保持策略及错误身份硬失败",()=>
        {
            var consumer=new BindingNodeId("scope","consumer");var node=new BindingNodeId("scope","source");var session=Guid.NewGuid();var scopeId=Guid.NewGuid();var runId=Guid.NewGuid();
            var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("values",Probe.IntegerSequence)});var source=new BindingSource(node,scopeId,schema,0);
            var scope=new BindingScopeSnapshot(consumer,session,1,new[]{source},new[]{node},Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
            var reference=new OutputReference("source","values",Probe.IntegerSequence,scopeId:"scope");
            var bridge=new SunEyeVision.Plugin.SDK.Execution.Parameters.ParameterResolver();
            var compilation=((IBindingPlanCompiler)bridge).CompileBinding(reference,new Port("value",DataType.Integer,Storage:NumericStorage.Int32),scope,behavior:first);
            Require(compilation.Plan is {Behavior:ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback},"SDK桥丢失政策");
            var identity=new RunIdentity(session,runId,scopeId,"source",Guid.NewGuid());var targetIdentity=new RunIdentity(session,runId,scopeId,"consumer",Guid.NewGuid());
            var values=new InputValues(schema.Outputs,new Dictionary<string,object?>{{"values",new SequenceValue(Probe.IntegerSequence,new object?[]{37L,90L})}});
            var result=new BindingNodeResult(node,identity,0,schema.Fingerprint,true,values);var expected=new Dictionary<BindingNodeId,RunIdentity>{{node,identity}};
            var snapshot=new BindingRunSnapshot(consumer,targetIdentity,1,new[]{result},expected);
            Require(bridge.ResolveBinding(compilation.Plan!,snapshot) is {Success:true,Value:37L},"SDK实际解析未消费首项");
            foreach(var invalid in new[]{result with {Identity=identity with {RequestId=Guid.NewGuid()}},result with {SchemaFingerprint="wrong"},result with {Success=false}})
                Require(!bridge.ResolveBinding(compilation.Plan!,new BindingRunSnapshot(consumer,targetIdentity,1,new[]{invalid},expected)).Success,"错误身份/Schema/生产失败被回退");
            Require(!bridge.ResolveBinding(compilation.Plan!,new BindingRunSnapshot(consumer,targetIdentity,1,new[]{result},new Dictionary<BindingNodeId,RunIdentity>())).Success,"未准入来源被回退");
        });
        foreach(var indexValue in new long[]{-1,3,2147483648L})
            Case("动态索引负数越界溢出硬失败 "+indexValue,()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);var edit=f.Host.BeginEdit(f.Flow.Id,f.Index.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithSettings(new NumericIndexProbeSettings{CaseId=f.CaseId,Label="index",Source=true,Index=indexValue});f.Host.Commit(edit);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index"),first)));using var run=f.Run();Require(!run.Success&&!f.State.Trace.Contains("target"),"非法成功索引被回退");Require(f.Target.RuntimeConfiguration!.ReadCurrentParameter(Probe.IntegerInput) is {Value:999L},"失败半写当前值");});
        Case("显式政策不能经复制或JSON授权Strict目标",()=>{using var f=new Fixture();var reference=f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first));var strict=RuntimeToolConfiguration.Create(NumericIndexStrictProbe.Id);Reject(()=>strict.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Probe.IntegerInput,new ReferenceSetting(reference)}}));Reject(()=>new BindingSelection.AtVariable(f.VariableReference(f.AddVariable("indices",new long[]{0,1},"Integer[]"))));});
        Case("真实Blob仅五个Int32设置声明政策，其他字段保持Strict",()=>{var config=RuntimeToolConfiguration.Create("BlobAnalysis");var firstFields=config.SettingsSchema.Inputs.Where(p=>config.GetParameterReferenceBehavior(p.Id)==first).ToArray();Require(firstFields.Length==5,"Blob显式字段数量错误");Require(firstFields.All(p=>p.Type==TypeRef.ScalarOf(ScalarKind.Int64)&&NumericStorageRules.Effective(p.Type,p.Storage)==NumericStorage.Int32),"政策扩到非Int32");Require(config.Schema.Inputs.All(p=>config.GetParameterReferenceBehavior(p.Id)==ParameterReferenceBehavior.StrictExactlyOne),"工具Input被扩散");Reject(()=>ParameterReferenceRules.Freeze(new NumericIndexInvalidProbe()));});
        Case("全局主数组与数组索引经VariableSetting真实消费",()=>{using var f=new Fixture();var values=f.AddVariable("values",new long[]{37,90,173},"Integer[]");var indices=f.AddVariable("indices",new long[]{1,2},"Integer[]");f.Host.Commit(f.Draft(new VariableSetting(f.VariableReference(values,new BindingSelection.AtVariable(f.VariableReference(indices),first)))));using var run=f.Run();ExpectInteger(f,run,90);});
        Case("递归AtField索引使用同一目标政策",()=>{using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);var inner=new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first);var index=f.Ref(f.Index,"output.indices",selection:inner);f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(index,first)));using var run=f.Run();Require(!run.Success&&!f.State.Trace.Contains("target"),"递归成功索引90越界被回落");});
        Case("已声明成员整数数组作为动态索引真实消费",()=>
        {
            using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);
            var index=f.Ref(f.Index,"output.record",new[]{"indices"});
            f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(index,first)));
            using var run=f.Run();ExpectInteger(f,run,90);
        });
        Case("递归动态索引正向逐层实际消费",()=>
        {
            using var f=new Fixture(indexLabel:"ordinal");f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);
            var inner=new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first);
            var index=f.Ref(f.Index,"output.indices",selection:inner);
            f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(index,first)));
            using var run=f.Run();ExpectInteger(f,run,90);
        });
        foreach(var change in new[]{"toolId","fieldId"})
            Case("真实保存JSON更换目标身份不能自授权 "+change,()=>
            {
                using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);
                f.Accept(f.Ref(f.Source,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index-array"),first)));
                var path=Path.Combine(f.Directory,"identity-source.solution");var saved=new SolutionApplicationService(f.Manager).Save(path,()=>f.Editing.AcceptAll());
                Require(saved.FileWritten,saved.Error??"合法源文件保存失败");var original=File.ReadAllBytes(path);
                var repository=new SolutionRepository();var valid=repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError??"合法原文件不能读取");
                valid.ExecutionOwner.Retire();
                var json=System.Text.Json.Nodes.JsonNode.Parse(original)!.AsObject();
                var target=json["Workflows"]!.AsArray().SelectMany(flow=>flow!["Nodes"]!.AsArray()).Single(node=>node!["Id"]!.GetValue<string>()==f.Target.Id)!;
                var configuration=target["RuntimeConfiguration"]!.AsObject();
                if(change=="toolId")
                {
                    target["ToolType"]=NumericIndexStrictProbe.Id;configuration["ToolId"]=NumericIndexStrictProbe.Id;
                }
                else
                {
                    var fields=configuration["Fields"]!.AsObject();var reference=fields[Probe.IntegerInput]!.DeepClone();
                    fields[Probe.IntegerInput]=new System.Text.Json.Nodes.JsonObject{["Kind"]="Constant",["Value"]=999L};
                    fields[Probe.NumberInput]=reference;configuration["CurrentValues"]=new System.Text.Json.Nodes.JsonObject();
                    configuration["ParameterBindingOrder"]=new System.Text.Json.Nodes.JsonArray(Probe.NumberInput);
                }
                var altered=Path.Combine(f.Directory,"identity-"+change+".solution");File.WriteAllText(altered,json.ToJsonString());
                var rejected=repository.LoadCandidate(altered);
                Require(rejected.Candidate is null,"替换目标身份后政策被JSON继承");
                Require((repository.LastError??"").Contains(".RuntimeConfiguration: Binding reference contains an invalid or missing declaration.",StringComparison.Ordinal),"拒绝未发生在首次绑定声明读取："+repository.LastError);
                var changedField=change=="toolId"?Probe.IntegerInput:Probe.NumberInput;
                var expression=configuration["Fields"]![changedField]!["Reference"]!.ToJsonString();
                Require(BindingReferenceSerializer.Deserialize(expression,first).Selection is BindingSelection.AtField,"同一引用在显式字段政策下无效");
                Reject(()=>BindingReferenceSerializer.Deserialize(expression,ParameterReferenceBehavior.StrictExactlyOne));
                // Restore only target identity. The exact same parsed/written document and binding remain valid.
                if(change=="toolId")
                {
                    target["ToolType"]=NumericIndexQualifiedProbe.Id;configuration["ToolId"]=NumericIndexQualifiedProbe.Id;
                }
                else
                {
                    var fields=configuration["Fields"]!.AsObject();fields[Probe.IntegerInput]=fields[Probe.NumberInput]!.DeepClone();
                    fields[Probe.NumberInput]=new System.Text.Json.Nodes.JsonObject{["Kind"]="Constant",["Value"]=777d};
                    configuration["ParameterBindingOrder"]=new System.Text.Json.Nodes.JsonArray(Probe.IntegerInput);
                }
                var restoredPath=Path.Combine(f.Directory,"restored-"+change+".solution");File.WriteAllText(restoredPath,json.ToJsonString());
                var restored=repository.LoadCandidate(restoredPath).Candidate??throw new Exception(repository.LastError??"仅恢复目标身份后仍不能读取同源文档");
                restored.ExecutionOwner.Retire();
                Require(File.ReadAllBytes(path).SequenceEqual(original),"读取失败覆盖合法原文件");
                Require(f.Target.RuntimeConfiguration!.Fields.Read(Probe.IntegerInput) is ReferenceSetting,"读取候选失败污染正式配置");
                using var run=f.Run();ExpectInteger(f,run,90);
            });
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"numeric-index-report.json"),JsonSerializer.Serialize(new{passed=_passed,cases=Reports},new JsonSerializerOptions{WriteIndented=true}));
        return Reports.Count==_passed?0:1;
    }
    private static void ExpectInteger(Fixture f,ExecutionResult run,long expected)
    {
        Require(run.Success,"真实执行失败："+string.Join(";",run.Errors));
        Require(run.NodeResults[f.Target.Id].Result?.Output is NumericIndexProbeOutput { } output&&output.Index==expected,"实际整数消费值不符"+expected);
    }
    private static void Register(Type type,string id)
    {
        var metadata=new ToolComponentMetadata("NumericIndex.Runtime.Tests."+id,id,ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor);
        using var registration=ToolRegistry.PrepareRegistration(Array.Empty<Type>(),new[]{new ToolComponentRegistration(metadata,type)}).EnterCommit();registration.Publish();
    }
    private sealed class Fixture:IDisposable
    {
        public readonly string CaseId=Guid.NewGuid().ToString("N");
        public string Directory {get;}
        public readonly NumericIndexProbeState State=new();
        public readonly WorkflowEngine Engine=new(new Logger());
        public readonly WorkflowExecutionEngine Runner;
        public readonly Solution Document=new(){Name="普通来源资格"};
        public readonly SunEyeVision.Workflow.Workflow Flow;
        public readonly WorkflowNodeBase Source,Bridge,Target,Index,Parallel,Undeclared;
        public readonly SolutionManager Manager;
        public readonly DocumentEditSession Editing;
        public NodeConfigurationEditHost Host=>Editing.Host;
        public readonly CommandManager Commands;
        public Fixture(string targetToolId=NumericIndexQualifiedProbe.Id,string sourceLabel="source",string indexLabel="index")
        {
            Directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,CaseId);System.IO.Directory.CreateDirectory(Directory);
            Probe.States[CaseId]=State;Flow=Engine.CreateWorkflow("flow-"+CaseId,"资格工作流");Document.Workflows.Add(Flow);
            Source=Add(sourceLabel,5);Bridge=Add("bridge",11);Index=Add(indexLabel,1);Parallel=Add("parallel",90);
            Undeclared=Add("undeclared",70,toolId:NumericIndexStrictProbe.Id);Target=Add("target",0,source:false,toolId:targetToolId);
            Manager=new SolutionManager(Path.Combine(Directory,"manager"));Manager.SetCurrentSolution(Document,registerInCatalog:false);
            Editing=Manager.Session!.Editing;_=Host;Runner=new(Engine,new PluginManager(),new Logger());
            Commands=new(Flow.Nodes,Flow.Connections);
            Commands.BindDocumentHistory(Editing.History,Flow.Id,()=>Host.AcceptDocumentGraphHistory(),()=>NodeConfigurationEditHost.DocumentHistorySignature(Document));
        }
        private WorkflowNodeBase Add(string id,double value,bool source=true,string toolId=NumericIndexQualifiedProbe.Id)
        {
            var node=new WorkflowNodeBase(id,id,id,toolId){RuntimeConfiguration=RuntimeToolConfiguration.Create(toolId).WithSettings(
                new NumericIndexProbeSettings{CaseId=CaseId,Label=id,Source=source,Value=value,Index=1})};
            Flow.Nodes.Add(node);return node;
        }
        public WorkflowConnection Connect(WorkflowNodeBase source,WorkflowNodeBase target)
        {
            var edge=new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=target.Id};
            ExecutionConnectionPolicy.EnsureCanAdd(Flow.Connections,edge);Flow.Connections.Add(edge);Host.SynchronizeStructure();return edge;
        }
        public void DeleteEdges(params WorkflowConnection[] edges)=>Commands.Execute(new BatchDeleteNodesAndConnectionsCommand(Flow.Nodes,Flow.Connections,new(),edges.ToList()));
        public IReadOnlyList<string> Candidates(){var service=new OutputBindingChangeService();service.Reset(Document);return service.GetManualParameterCandidateNodeIds(Flow.Id,Target.Id);}
        public OutputReference Ref(WorkflowNodeBase source,string port="output.number",IReadOnlyList<string>? path=null,BindingSelection? selection=null)
        {
            var declaration=Probe.OutputSchema.Outputs.Single(item=>item.Id==port);
            return new(source.Id,port,declaration.Type,path,selection,Flow.Id,declaration.Storage);
        }
        public NodeConfigurationEditDraft Draft(OutputReference reference,string field=Probe.IntegerInput)=>Draft(new ReferenceSetting(reference),field);
        public NodeConfigurationEditDraft Draft(ParameterSetting setting,string field=Probe.IntegerInput)
        {
            var draft=Host.BeginEdit(Flow.Id,Target.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{field,setting}});return draft;
        }
        public void Accept(OutputReference reference)=>Host.Commit(Draft(reference));
        public string Relation()=>BindingReferenceSerializer.Serialize(((ReferenceSetting)Target.RuntimeConfiguration!.Fields.Read(Probe.NumberInput)).Reference);
        public GlobalVariable AddVariable(string name,object? value,string type,OutputReference? producer=null)
        {
            var draft=Host.BeginVariableEdit();var variable=new GlobalVariable(name,value,type);
            if(producer is not null)variable.ProducerBinding=new ProducerBinding{ReferenceExpression=BindingReferenceSerializer.Serialize(producer)};
            draft.Configuration.GlobalVariables.Add(variable);Host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),draft);return Document.GlobalVariables.Single(item=>item.Id==variable.Id);
        }
        public VariableReference VariableReference(GlobalVariable variable,BindingSelection? selection=null)
        {
            Require(GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out var port,out _,out var reason),reason);
            return new(Document.Id,variable.Id,port.Type,selection,port.Storage);
        }
        public WorkflowPublishedConfiguration Publication()=>WorkflowPublishedConfiguration.Capture(Host.Capture(),Document,Host);
        public ExecutionResult Run(Solution? document=null,NodeConfigurationEditHost? host=null,WorkflowPublishedConfiguration? captured=null)
        {
            document??=Document;host??=Host;var publication=captured??WorkflowPublishedConfiguration.Capture(host.Capture(),document,host);
            using var globals=new WorkflowGlobalRunValues(document.Id,publication.GlobalDefinitions,
                publication.GlobalDefinitions.Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value)));
            var operations=publication.GlobalDefinitions.Where(variable=>variable.ProducerBinding is not null).Select(variable=>
            {
                Require(GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out var port,out var clr,out var reason),reason);
                return new WorkflowGlobalProducerOperation(variable.Id,variable.Name,variable.ProducerBinding!.SourceReference,port,clr);
            }).ToArray();
            return Wait(()=>Runner.ExecuteWorkflowAsync(new WorkflowRunRequest(Flow.Id){Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,
                ProducedGlobals=globals,GlobalProducers=operations,ParameterValues=publication.ParameterValues}));
        }
        public RuntimeDebugInvocation Debug(NodeConfigurationEditDraft draft,ToolExecutionOwner owner,ExecutionResult published)
            =>RuntimeDebugInvocation.Capture(Flow,draft.Node,owner,Host.Capture().Version,
                id=>published.NodeResults.TryGetValue(id,out var result)?result.Result as DeclaredOutputSnapshot:null,CancellationToken.None,
                publishedOwner:Document.ExecutionOwner,solutionId:Document.Id,variables:Document.GlobalVariables,sourceWorkflows:Document.Workflows);
        public void Expect(ExecutionResult result,double value)
        {
            Require(result.Success,"真实工作流失败："+string.Join(";",result.Errors)+"；执行轨迹："+string.Join(",",State.Trace));
            Require(result.NodeResults[Target.Id].Result?.Output is NumericIndexProbeOutput output&&output.Value==value,"消费值不符，期望"+value);
        }
        public void Dispose(){Manager.CloseSolution(saveChanges:false);Document.ExecutionOwner.Retire();Probe.States.TryRemove(CaseId,out _);}
    }
    private sealed class OwnerLease:IDisposable {public ToolExecutionOwner Owner{get;}=new();public void Dispose()=>Owner.Retire();}
    private static T Wait<T>(Func<Task<T>> start)=>start().WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
    private static void Reject(Action action)
    {
        try{action();}catch(Exception error)when(error is InvalidOperationException or ArgumentException or JsonException){return;}
        throw new Exception("应被资格合同拒绝的操作被接纳");
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Case(string name,Action test)
    {
        try{test();_passed++;Reports.Add(new{name,passed=true});Console.WriteLine("通过："+name);}
        catch(Exception error){Reports.Add(new{name,passed=false,error=error.ToString()});Console.Error.WriteLine("失败："+name+"\n"+error);}
    }
    private sealed class Logger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){}
    }
}



