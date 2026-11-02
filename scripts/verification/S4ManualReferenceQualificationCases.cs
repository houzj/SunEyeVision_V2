using System.IO;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.TestFixtures;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Solutions;
using SunEyeVision.Workflow;
using Probe=SunEyeVision.TestFixtures.ManualReferenceProbeDefinitionBase;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    private static readonly List<object> Reports=new();
    [STAThread] private static int Main()
    {
        Require(PluginLoader.LoadPlugins(),"产品Runtime注册失败");
        Register(typeof(ManualReferenceQualifiedProbe),ManualReferenceQualifiedProbe.Id);
        Register(typeof(ManualReferenceUndeclaredProbe),ManualReferenceUndeclaredProbe.Id);
        try
        {
            Case("并列节点不进普通候选，构造引用不能绕过宿主；控制直连后同一提案可确认并实际消费",()=>
            {
                using var f=new Fixture();var draft=f.Draft(f.Ref(f.Source));
                Require(!f.Candidates().Contains(f.Source.Id),"并列源成为普通候选");
                Reject(()=>f.Host.Validate(draft));Reject(()=>f.Host.Commit(draft));
                Require(f.Target.RuntimeConfiguration!.Fields.Read(Probe.NumberInput) is ConstantSetting {Value:777d},"拒绝污染正式值");
                f.Connect(f.Source,f.Target);f.Host.Commit(draft);
                using var run=f.Run();f.Expect(run,5);Require(f.Candidates().Contains(f.Source.Id),"直连来源仍无候选");
            });
            Case("整数设置参数与浮点参数同用普通来源准入，隐藏整数叶不能借类型匹配绕过",()=>
            {
                using var f=new Fixture();var draft=f.Draft(f.Ref(f.Index,"output.index"),Probe.IntegerInput);
                Reject(()=>f.Host.Validate(draft));f.Connect(f.Index,f.Target);f.Host.Commit(draft);
                using var run=f.Run();f.Expect(run,777);
                Require(run.NodeResults[f.Target.Id].Result?.Output is ManualReferenceProbeOutput {Index:1},"整数参数未实际消费来源");
                Require(f.Target.RuntimeConfiguration!.ReadCurrentParameter(Probe.IntegerInput) is {Value:1L},"整数当前值没有沿正式接纳回写");
                var hidden=f.Draft(f.Ref(f.Index,"output.record",new[]{"hidden-index"}),Probe.IntegerInput);
                Reject(()=>f.Host.Validate(hidden));
            });
            Case("数值Schema.Input走一般候选及后端，不能被数值Settings普通资格误拦截",()=>
            {
                using var f=new Fixture();var service=new OutputBindingChangeService();service.Reset(f.Document);
                Require(service.GetCandidateNodeIds(f.Flow.Id,f.Target.Id).Contains(f.Undeclared.Id),"一般输入候选丢失并列来源");
                Require(!service.GetManualParameterCandidateNodeIds(f.Flow.Id,f.Target.Id).Contains(f.Undeclared.Id),"普通设置候选混入并列来源");
                Reject(()=>f.Host.Validate(f.Draft(f.Ref(f.Undeclared))));
                var input=f.Draft(f.Ref(f.Undeclared),Probe.GeneralInput);f.Host.Validate(input);f.Host.Commit(input);
                using var run=f.Run();f.Expect(run,70);
                Require(f.Target.RuntimeConfiguration!.ReadCurrentParameter(Probe.NumberInput) is {Value:777d},"工具输入污染了同类型设置字段当前值");
            });
            Case("传递控制闭包包含祖先，不包含自身、后继及无关并列节点",()=>
            {
                using var f=new Fixture();f.Connect(f.Source,f.Bridge);f.Connect(f.Bridge,f.Target);f.Connect(f.Target,f.Index);
                var ids=f.Candidates();Require(ids.Contains(f.Source.Id)&&ids.Contains(f.Bridge.Id),"多跳前驱缺失");
                Require(!ids.Contains(f.Target.Id)&&!ids.Contains(f.Index.Id)&&!ids.Contains(f.Parallel.Id),"候选含自身、后继或并列");
                f.Accept(f.Ref(f.Source));using var run=f.Run();f.Expect(run,5);
            });
            Case("已有数据订阅及保存JSON不能为自己创造控制前序",()=>
            {
                using var f=new Fixture();var proposal=f.Target.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting> {
                    [Probe.NumberInput]=new ReferenceSetting(f.Ref(f.Source))});
                var persisted=JsonSerializer.Deserialize<RuntimeToolConfiguration>(JsonSerializer.Serialize(proposal))!;
                var draft=f.Host.BeginEdit(f.Flow.Id,f.Target.Id);draft.Node.RuntimeConfiguration=persisted;
                Reject(()=>f.Host.Validate(draft));Reject(()=>f.Host.Commit(draft));
                f.Target.RuntimeConfiguration=persisted;
                var injectedHost=new NodeConfigurationEditHost(f.Document);
                var query=new OutputBindingChangeService();query.Reset(f.Document);
                Require(!query.GetManualParameterCandidateNodeIds(f.Flow.Id,f.Target.Id).Contains(f.Source.Id),"数据依赖自授普通来源资格");
                var issues=OutputBindingChangeService.GetExecutionInputIssues(f.Flow,f.Flow.Id);
                Require(issues.TryGetValue(f.Target.Id,out var failures)&&failures.Any(issue=>issue.InputKey==Probe.NumberInput),"持久化注入没有明确输入失效");
                using var run=f.Run(host:injectedHost);Require(!run.Success,"运行层接受了无控制前序的持久化注入");
            });
            Case("逐叶资格支持真实record.allowed，拒绝hidden成员、未列顶层输出及未声明资格Runtime",()=>
            {
                using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Undeclared,f.Target);
                foreach(var reference in new[]{f.Ref(f.Source,"output.hidden"),f.Ref(f.Source,"output.record",new[]{"hidden"}),f.Ref(f.Undeclared)})
                {
                    var draft=f.Draft(reference);var json=JsonSerializer.Serialize(draft.Node.RuntimeConfiguration);
                    draft.Node.RuntimeConfiguration=JsonSerializer.Deserialize<RuntimeToolConfiguration>(json)!;
                    Reject(()=>f.Host.Validate(draft));Reject(()=>f.Host.Commit(draft));
                }
                f.Accept(f.Ref(f.Source,"output.record",new[]{"allowed"}));using var run=f.Run();f.Expect(run,25);
            });
            Case("字段索引同样要求控制前序和逐叶资格，合法member索引实际选中第二项",()=>
            {
                using var f=new Fixture();f.Connect(f.Source,f.Target);
                var indexed=f.Ref(f.Source,"output.values",selection:new BindingSelection.AtField(f.Ref(f.Index,"output.index")));
                var draft=f.Draft(indexed);Reject(()=>f.Host.Validate(draft));
                f.Connect(f.Index,f.Target);
                f.Host.Commit(draft);using(var run=f.Run())f.Expect(run,15);
                var hidden=f.Ref(f.Source,"output.values",selection:new BindingSelection.AtField(f.Ref(f.Source,"output.record",new[]{"hidden-index"})));
                Reject(()=>f.Host.Commit(f.Draft(hidden)));
                var allowed=f.Ref(f.Source,"output.values",selection:new BindingSelection.AtField(f.Ref(f.Source,"output.record",new[]{"index"})));
                f.Accept(allowed);using(var run=f.Run())f.Expect(run,15);
            });
            Case("VariableSetting内AtField输出索引经过同一普通资格，变量值身份不套控制前序",()=>
            {
                using var f=new Fixture();var variable=f.AddVariable("array",new double[]{41,42},"Double[]");
                var selection=new BindingSelection.AtField(f.Ref(f.Index,"output.index"));
                var setting=new VariableSetting(f.VariableReference(variable,selection));
                var draft=f.Draft(setting);Reject(()=>f.Host.Validate(draft));f.Connect(f.Index,f.Target);f.Host.Commit(draft);
                using var run=f.Run();f.Expect(run,42);
                var hidden=new VariableSetting(f.VariableReference(variable,new BindingSelection.AtField(f.Ref(f.Index,"output.record",new[]{"hidden-index"}))));
                Reject(()=>f.Host.Validate(f.Draft(hidden)));
            });
            Case("两层AtField递归索引必须逐级核控制范围，模块和变量序列同用正式编译执行链",()=>
            {
                foreach(var variableSource in new[]{false,true})
                {
                    using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Index,f.Target);
                    var inner=f.Ref(f.Index,"output.indices",selection:new BindingSelection.AtField(f.Ref(f.Parallel,"output.index")));
                    var outer=new BindingSelection.AtField(inner);
                    ParameterSetting proposal;
                    if(variableSource)
                    {
                        var values=f.AddVariable("recursive-array",new double[]{41,42},"Double[]");
                        proposal=new VariableSetting(f.VariableReference(values,outer));
                    }
                    else proposal=new ReferenceSetting(f.Ref(f.Source,"output.values",selection:outer));
                    var draft=f.Draft(proposal);Reject(()=>f.Host.Validate(draft));
                    f.Connect(f.Parallel,f.Target);f.Host.Commit(draft);
                    using var run=f.Run();f.Expect(run,variableSource?42:15);
                }
            });
            Case("AtVariable整数标量与独立生产者可用，无控制前序的变量生产者不被误当普通订阅",()=>
            {
                foreach(var variableSource in new[]{false,true})
                {
                    using var f=new Fixture();f.Connect(f.Source,f.Target);
                    var index=f.AddVariable("index",0L,"Integer",f.Ref(f.Undeclared,"output.index"));
                    var selection=new BindingSelection.AtVariable(f.VariableReference(index));
                    ParameterSetting proposal;
                    if(variableSource)
                    {
                        var values=f.AddVariable("variable-items",new double[]{31,32},"Double[]");
                        proposal=new VariableSetting(f.VariableReference(values,selection));
                    }
                    else proposal=new ReferenceSetting(f.Ref(f.Source,"output.values",selection:selection));
                    f.Host.Commit(f.Draft(proposal));using var run=f.Run();f.Expect(run,variableSource?32:15);
                    Require(!f.Candidates().Contains(f.Undeclared.Id),"独立变量生产者被加入普通控制前序");
                    Reject(()=>new BindingSelection.AtVariable(f.VariableReference(index,new BindingSelection.AtField(f.Ref(f.Source,"output.index")))));
                }
            });
            Case("现有生产者可合法建立无设计初值的Double[]，批次产值和保存重开保持显式数组",()=>
            {
                using var f=new Fixture();var variable=f.AddVariable("produced-array",null,"Double[]",f.Ref(f.Undeclared,"output.values"));
                f.Host.Commit(f.Draft(new VariableSetting(f.VariableReference(variable,new BindingSelection.AtConstant(1)))));
                using(var run=f.Run())f.Expect(run,80);
                Require(variable.Value is null,"批次生产值回写了设计初值");
                var path=Path.Combine(f.Directory,"produced-array.solution");var saved=f.Manager.SaveCurrentDocument(path);
                Require(saved.FileWritten,"合法数组生产者保存失败："+saved.Error);
                var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("合法数组生产者重开失败");
                Require(reopened.GlobalVariables.Single().Type=="Double[]"&&reopened.GlobalVariables.Single().Value is null,"重开缩减数组声明或填造设计值");
                f.Manager.SetCurrentSolution(reopened,registerInCatalog:false);var host=f.Manager.Session!.Editing.Host;
                using var reopenedRun=f.Run(reopened,host);f.Expect(reopenedRun,80);
            });
            Case("显式非空数组正式编辑隔离、拒绝回滚、元数据历史和保存重开贯通",()=>
            {
                using var f=new Fixture();var variable=f.AddVariable("literal-array",new double[]{41,42},"Double[]");
                var illicit=f.Host.BeginVariableEdit();((double[])illicit.Configuration.GlobalVariables.Single().Value!)[0]=999;
                Reject(()=>f.Host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),illicit));
                Require(((double[])variable.Value!)[0]==41,"未经SetValue的数组修改泄漏正式值");
                var wrongShape=f.Host.BeginVariableEdit();Reject(()=>wrongShape.SetValue(variable.Id,43d));
                var nonFinite=f.Host.BeginVariableEdit();Reject(()=>nonFinite.SetValue(variable.Id,new double[]{double.NaN}));
                var edit=f.Host.BeginVariableEdit();edit.Configuration.GlobalVariables.Single().Name="renamed-array";
                var values=new double[]{43,44};edit.SetValue(variable.Id,values);values[0]=999;
                f.Host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),edit);
                Require(((double[])variable.Value!).SequenceEqual(new double[]{43,44}),"数组SetValue借用调用者存储");
                f.Host.Undo();Require(variable.Name=="literal-array"&&((double[])variable.Value!).SequenceEqual(new double[]{43,44}),"Undo回放了数组运行值");
                f.Host.Redo();Require(variable.Name=="renamed-array"&&((double[])variable.Value!).SequenceEqual(new double[]{43,44}),"Redo回放或丢失数组值");
                var rejected=f.Host.BeginVariableEdit();rejected.Configuration.GlobalVariables.Single().Name="must-not-publish";
                rejected.SetValue(variable.Id,new double[]{55,56});rejected.Configuration.GlobalVariables.Single().Type="Integer[]";
                Reject(()=>f.Host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),rejected));
                Require(variable.Name=="renamed-array"&&variable.Type=="Double[]"&&((double[])variable.Value!).SequenceEqual(new double[]{43,44}),"失败批次发布了部分数组配置/值");
                f.Host.Commit(f.Draft(new VariableSetting(f.VariableReference(variable,new BindingSelection.AtConstant(1)))));
                using(var run=f.Run())f.Expect(run,44);
                var path=Path.Combine(f.Directory,"literal-array.solution");Require(f.Manager.SaveCurrentDocument(path).FileWritten,"显式数组保存失败");
                var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("显式数组重开失败");
                Require(reopened.GlobalVariables.Single().Type=="Double[]"&&((double[])reopened.GlobalVariables.Single().Value!).SequenceEqual(new double[]{43,44}),"保存重开改变显式数组基数或值");
                f.Manager.SetCurrentSolution(reopened,registerInCatalog:false);using var restored=f.Run(reopened,f.Manager.Session!.Editing.Host);f.Expect(restored,44);
            });
            Case("已有四类显式纯值数组可经正式宿主创建和保存，不误作原生复杂资源",()=>
            {
                using var f=new Fixture();
                f.AddVariable("integers",new long[]{1,2},"Integer[]");
                f.AddVariable("numbers",new double[]{1.5,2.5},"Double[]");
                f.AddVariable("texts",new string[]{"a,b",""},"String[]");
                f.AddVariable("flags",new bool[]{true,false},"Boolean[]");
                var path=Path.Combine(f.Directory,"pure-array-families.solution");Require(f.Manager.SaveCurrentDocument(path).FileWritten,"纯值数组保存失败");
                var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("纯值数组重开失败");
                Require(reopened.GlobalVariables.Single(variable=>variable.Type=="Integer[]").Value is long[] {Length:2},"整数数组声明或载体改变");
                Require(reopened.GlobalVariables.Single(variable=>variable.Type=="Double[]").Value is double[] {Length:2},"浮点数组声明或载体改变");
                Require(reopened.GlobalVariables.Single(variable=>variable.Type=="String[]").Value is string[] texts&&texts.SequenceEqual(new[]{"a,b",""}),"文本数组元素被拆分或丢失");
                Require(reopened.GlobalVariables.Single(variable=>variable.Type=="Boolean[]").Value is bool[] {Length:2},"布尔数组声明或载体改变");
            });
            Case("普通取消只退出私有提案，正式当前值、引用及文档历史不变",()=>
            {
                using var f=new Fixture();f.Connect(f.Source,f.Target);var version=f.Host.Capture().Version;var position=f.Editing.History.Position;
                using(var editor=new ProductionEditorContext(f.Host,f.Flow.Id,f.Target,()=>true,_=>{},documentEdits:f.Editing))
                {
                    editor.DraftNode.RuntimeConfiguration=editor.DraftNode.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting> {
                        [Probe.NumberInput]=new ReferenceSetting(f.Ref(f.Source))});
                    editor.Discard();
                }
                Require(f.Host.Capture().Version==version&&f.Editing.History.Position==position,"取消发布或写入历史");
                Require(f.Target.RuntimeConfiguration!.Fields.Read(Probe.NumberInput) is ConstantSetting {Value:777d},"取消污染正式参数");
            });
            Case("真实断边保留失效引用，运行和新调试拒绝；图Undo恢复同一关系及实际消费",()=>
            {
                using var f=new Fixture();var edge=f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source));
                using var prior=f.Run();f.Expect(prior,5);var before=f.Relation();
                f.DeleteEdges(edge);Require(f.Relation()==before,"断边删除或改写引用身份");
                Require(OutputBindingChangeService.GetExecutionInputIssues(f.Flow,f.Flow.Id).ContainsKey(f.Target.Id),"断边未显示失效");
                // Test qualification before another root run can stale the otherwise valid published identities.
                Require((prior.NodeResults[f.Source.Id].Result as DeclaredOutputSnapshot)?.ExecutionIdentity?.IsCurrent==true,"断边实验的已发布来源身份已失效，不能据此判定资格拒绝");
                using var debugOwner=new OwnerLease();Reject(()=>{using var unexpected=f.Debug(f.Host.BeginEdit(f.Flow.Id,f.Target.Id),debugOwner.Owner,prior);});
                using(var failed=f.Run())Require(!failed.Success,"断边后仍使用旧输出运行");
                f.Host.Undo();Require(f.Flow.Connections.Any(item=>ReferenceEquals(item,edge))&&f.Relation()==before,"Undo未恢复同一边/引用");
                using var restored=f.Run();f.Expect(restored,5);
            });
            Case("打开后断边的旧草稿不能确认，Undo恢复控制条件后原草稿可继续确认",()=>
            {
                using var f=new Fixture();var edge=f.Connect(f.Source,f.Target);var draft=f.Draft(f.Ref(f.Source));
                f.DeleteEdges(edge);Reject(()=>f.Host.Commit(draft));f.Host.Undo();f.Host.Commit(draft);
                using var run=f.Run();f.Expect(run,5);
            });
            Case("改名及同显示名不同ID保持来源身份，整体保存重开沿真实宿主继续消费",()=>
            {
                using var f=new Fixture();f.Connect(f.Source,f.Target);f.Connect(f.Parallel,f.Target);f.Accept(f.Ref(f.Source));
                f.Commands.Execute(new RenameCanvasNodeCommand(f.Source,"同名来源",null,()=>{}));
                f.Parallel.DispName=f.Source.DispName;f.Parallel.Name=f.Source.Name;f.Host.SynchronizeStructure();
                Require(((ReferenceSetting)f.Target.RuntimeConfiguration!.Fields.Read(Probe.NumberInput)).Reference.NodeId==f.Source.Id,"改名重绑定到同名对象");
                using(var run=f.Run())f.Expect(run,5);
                var path=Path.Combine(f.Directory,"qualified.solution");var service=new SolutionApplicationService(f.Manager);
                var saved=service.Save(path,()=>f.Editing.AcceptAll());Require(saved.FileWritten,"保存失败："+saved.Error);
                var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("资格配置重开失败");
                f.Manager.SetCurrentSolution(reopened,registerInCatalog:false);
                var host=f.Manager.Session!.Editing.Host;var node=reopened.Workflows.Single(flow=>flow.Id==f.Flow.Id).Nodes.Single(node=>node.Id==f.Target.Id);
                var draft=host.BeginEdit(f.Flow.Id,node.Id);host.Validate(draft);
                Require(((ReferenceSetting)node.RuntimeConfiguration!.Fields.Read(Probe.NumberInput)).Reference.NodeId==f.Source.Id,"保存重开改变稳定来源ID");
                using var reopenedRun=f.Run(reopened,host);f.Expect(reopenedRun,5);
            });
            Case("冻结运行保留捕获时控制资格，下一次正式捕获不能沿已断边继续消费",()=>
            {
                using var f=new Fixture();var edge=f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source));var publication=f.Publication();
                f.Flow.Connections.Remove(edge);
                using(var frozen=f.Run(captured:publication))f.Expect(frozen,5);
                f.Host.SynchronizeStructure();using var next=f.Run();Require(!next.Success,"新运行沿用旧控制资格");
            });
            Case("已捕获私有调试冻结资格及读者，断边后的新捕获拒绝旧已发布结果",()=>
            {
                using var f=new Fixture();var edge=f.Connect(f.Source,f.Target);f.Accept(f.Ref(f.Source));using var run=f.Run();f.Expect(run,5);
                var draft=f.Host.BeginEdit(f.Flow.Id,f.Target.Id);using var owner=new OwnerLease();using var invocation=f.Debug(draft,owner.Owner,run);
                f.DeleteEdges(edge);using(var result=Wait(()=>invocation.ExecuteAsync()))Require(result.IsSuccess,"冻结调试因正式断边被重新解释");
                Require((run.NodeResults[f.Source.Id].Result as DeclaredOutputSnapshot)?.ExecutionIdentity?.IsCurrent==true,"新调试拒绝前的原来源身份已失效");
                Reject(()=>{using var unexpected=f.Debug(f.Host.BeginEdit(f.Flow.Id,f.Target.Id),owner.Owner,run);});
            });
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"manual-reference-report.json"),JsonSerializer.Serialize(
                new {scope="普通数值参数来源完整生产回归；IMAGE、Group、原厂全部动态资格及VS/人工另验",passed=_passed,failed=Reports.Count-_passed,cases=Reports},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"普通数值来源资格：{_passed}通过，{Reports.Count-_passed}失败；不是B08整体或S3算法验收。");
            return _passed==Reports.Count?0:1;
        }
        finally {ToolRegistry.UnregisterTool(ManualReferenceQualifiedProbe.Id);ToolRegistry.UnregisterTool(ManualReferenceUndeclaredProbe.Id);}
    }
    private static void Register(Type type,string id)
    {
        var metadata=new ToolComponentMetadata("ManualReference.Runtime.Tests."+id,id,ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor);
        using var registration=ToolRegistry.PrepareRegistration(Array.Empty<Type>(),new[]{new ToolComponentRegistration(metadata,type)}).EnterCommit();registration.Publish();
    }
    private sealed class Fixture:IDisposable
    {
        public readonly string CaseId=Guid.NewGuid().ToString("N");
        public string Directory {get;}
        public readonly ManualReferenceProbeState State=new();
        public readonly WorkflowEngine Engine=new(new Logger());
        public readonly WorkflowExecutionEngine Runner;
        public readonly Solution Document=new(){Name="普通来源资格"};
        public readonly SunEyeVision.Workflow.Workflow Flow;
        public readonly WorkflowNodeBase Source,Bridge,Target,Index,Parallel,Undeclared;
        public readonly SolutionManager Manager;
        public readonly DocumentEditSession Editing;
        public NodeConfigurationEditHost Host=>Editing.Host;
        public readonly CommandManager Commands;
        public Fixture()
        {
            Directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,CaseId);System.IO.Directory.CreateDirectory(Directory);
            Probe.States[CaseId]=State;Flow=Engine.CreateWorkflow("flow-"+CaseId,"资格工作流");Document.Workflows.Add(Flow);
            Source=Add("source",5);Bridge=Add("bridge",11);Index=Add("index",1);Parallel=Add("parallel",90);
            Undeclared=Add("undeclared",70,toolId:ManualReferenceUndeclaredProbe.Id);Target=Add("target",0,source:false);
            Manager=new SolutionManager(Path.Combine(Directory,"manager"));Manager.SetCurrentSolution(Document,registerInCatalog:false);
            Editing=Manager.Session!.Editing;_=Host;Runner=new(Engine,new PluginManager(),new Logger());
            Commands=new(Flow.Nodes,Flow.Connections);
            Commands.BindDocumentHistory(Editing.History,Flow.Id,()=>Host.AcceptDocumentGraphHistory(),()=>NodeConfigurationEditHost.DocumentHistorySignature(Document));
        }
        private WorkflowNodeBase Add(string id,double value,bool source=true,string toolId=ManualReferenceQualifiedProbe.Id)
        {
            var node=new WorkflowNodeBase(id,id,id,toolId){RuntimeConfiguration=RuntimeToolConfiguration.Create(toolId).WithSettings(
                new ManualReferenceProbeSettings{CaseId=CaseId,Label=id,Source=source,Value=value,Index=1})};
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
        public NodeConfigurationEditDraft Draft(OutputReference reference,string field=Probe.NumberInput)=>Draft(new ReferenceSetting(reference),field);
        public NodeConfigurationEditDraft Draft(ParameterSetting setting,string field=Probe.NumberInput)
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
            Require(result.NodeResults[Target.Id].Result?.Output is ManualReferenceProbeOutput output&&output.Value==value,"消费值不符，期望"+value);
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
