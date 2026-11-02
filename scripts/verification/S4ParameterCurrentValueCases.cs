using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        try
        {
            Require(PluginLoader.LoadPlugins(),"正式插件加载失败");
            Case("正式变量消费更新参数当前值，关系及配置版本保持；解绑保存重开保留消费值",()=>
            {
                using var fixture=new Fixture();var version=fixture.Host.Capture().Version;
                Require(fixture.Current==37,"建立关系时提前消费变量");
                using(var result=fixture.Run())Require(result.Success&&fixture.Count(result)==0,"真实调度未消费173");
                Require(fixture.Current==173&&fixture.Node.RuntimeConfiguration!.Fields.Read(fixture.Threshold) is VariableSetting,"消费值或绑定关系错误");
                Require(fixture.Host.Capture().Version==version,"运行生成了配置编辑版本");
                Reject(()=>fixture.Host.Undo());
                var bound=fixture.SaveReopen("bound");
                Require(bound.Workflows[0].Nodes[0].RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:173L}
                    &&bound.Workflows[0].Nodes[0].RuntimeConfiguration!.Fields.Read(fixture.Threshold) is VariableSetting,"绑定重开丢失当前值或关系");
                var edit=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                var value=fixture.Host.ReadCurrentParameter(edit,fixture.Threshold)??throw new Exception("解绑缺少当前值");
                edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{fixture.Threshold,value}});
                fixture.Host.Commit(edit);
                Require(fixture.Node.RuntimeConfiguration!.Fields.Read(fixture.Threshold) is ConstantSetting {Value:173L},"解绑恢复了旧常量37");
                using(var result=fixture.Run())Require(result.Success&&fixture.Count(result)==0,"解绑后的执行没有沿用173");
                var unbound=fixture.SaveReopen("unbound");
                Require(unbound.Workflows[0].Nodes[0].RuntimeConfiguration!.Fields.Read(fixture.Threshold) is ConstantSetting {Value:173L},"解绑保存重开改变常量");
            });
            Case("未修改的编辑草稿不会覆盖正式运行新值",()=>
            {
                using var fixture=new Fixture();var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                using(var run=fixture.Run())Require(run.Success,"正式运行失败");
                Require(fixture.Host.ReadCurrentParameter(draft,fixture.Threshold) is {Value:173L},"草稿回读仍使用打开时37");
                fixture.Host.Commit(draft);Require(fixture.Current==173,"未改草稿覆盖新值");
            });
            Case("私有变量调试只写回执，取消不改正式值；确认沿唯一入口接纳且不加撤销",()=>
            {
                using var fixture=new Fixture();var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                using var debugOwner=new OwnerLease();
                using var invocation=fixture.Debug(draft,debugOwner.Owner);
                using(var result=Wait(()=>invocation.ExecuteAsync()))Require(result.IsSuccess,"私有调试失败");
                Require(invocation.AcceptedParameters?.ReadCurrentParameter(fixture.Threshold) is {Value:173L},"没有私有参数写回执");
                Require(fixture.Current==37,"私有调试污染正式参数");
                // Leaving the first draft uncommitted is cancellation, not a formal write.
                var accepted=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                accepted.Node.RuntimeConfiguration=invocation.AcceptedParameters;
                fixture.Host.Commit(accepted);Require(fixture.Current==173,"确认未接纳私有当前值");
                Reject(()=>fixture.Host.Undo());
            });
            Case("后项变量索引失败保留此前私有写回执，不改正式配置",()=>
            {
                using var fixture=new Fixture(extraFailure:true);var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                using var owner=new OwnerLease();using var invocation=fixture.Debug(draft,owner.Owner);
                Reject(()=>Wait(()=>invocation.ExecuteAsync()));
                Require(invocation.AcceptedParameters?.ReadCurrentParameter(fixture.Threshold) is {Value:173L},"后项失败丢失前项已接纳值");
                Require(fixture.Current==37,"私有部分失败污染正式节点");
            });
            Case("取消前未执行的快照没有消费参数",()=>
            {
                using var fixture=new Fixture();var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                using var owner=new OwnerLease();using var cancellation=new CancellationTokenSource();
                using var invocation=fixture.Debug(draft,owner.Owner,cancellation.Token);cancellation.Cancel();
                Reject(()=>Wait(()=>invocation.ExecuteAsync()));
                Require(invocation.AcceptedParameters is null&&fixture.Current==37,"取消前快照被当成实际消费");
            });
            Case("同值私有消费仍有接纳代次，拒绝覆盖并发正式不同值",()=>
            {
                using var fixture=new Fixture(source:37);var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                using var owner=new OwnerLease();using var invocation=fixture.Debug(draft,owner.Owner);
                using(var result=Wait(()=>invocation.ExecuteAsync()))Require(result.IsSuccess,"同值私有调试失败");
                draft.Node.RuntimeConfiguration=invocation.AcceptedParameters;
                var variables=fixture.Host.BeginVariableEdit();variables.SetValue(fixture.Variable.Id,173L);
                fixture.Host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),variables);
                using(var result=fixture.Run())Require(result.Success,"并发正式运行失败");
                Reject(()=>fixture.Host.Commit(draft));Require(fixture.Current==173,"同值ABA草稿覆盖了正式173");
            });
            Case("执行完成后的旧参数回调被拒绝，不能延迟覆盖正式值",()=>
            {
                using var fixture=new Fixture();var publication=fixture.Publication();var opening=fixture.Node.RuntimeConfiguration!.Copy();
                SunEyeVision.Plugin.Runtime.ToolExecutionIdentity identity;
                using(var result=fixture.Run(publication))identity=Result(result,fixture.Node.Id).ExecutionIdentity!;
                var accept=typeof(RuntimeParameterValueAcceptance).GetMethod("Accept",BindingFlags.Instance|BindingFlags.NonPublic)!;
                Reject(()=>accept.Invoke(publication.ParameterValues,new object?[]{fixture.Flow.Id,fixture.Node.Id,identity,opening,fixture.Threshold,99L}));
                Require(fixture.Current==173,"已关闭回调覆盖了参数当前值");
            });
            Case("独立绑定队列覆盖改源尾插、删除重绑、复制映射、历史及保存名称排序",()=>
            {
                using var fixture=new Fixture(ordered:true);var a=fixture.Threshold;var b=Setting("BlobCountLimit");var c=Setting("UpperThreshold");
                void Order(params string[] expected)=>Require(fixture.Node.RuntimeConfiguration!.ParameterBindingOrder.SequenceEqual(expected),"绑定队列与预期不符");
                Order(a,b,c);
                var composite=fixture.Node.RuntimeConfiguration!;
                var snapshot=composite.Fields.Select(composite.SettingsSchema.Inputs).Apply(new Dictionary<string,ParameterSetting>
                    {{a,new VariableSetting(fixture.Reference(fixture.Alternate))},{b,new VariableSetting(fixture.Reference(fixture.Alternate))}});
                var reordered=composite.WithSettingsFields(snapshot,new[]{b,a});
                Require(reordered.ParameterBindingOrder.SequenceEqual(new[]{c,b,a}),"完整快照顺序覆盖了组合提案的B/A顺序");
                Reject(()=>composite.WithSettingsFields(snapshot,new[]{a,a}));
                var edit=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{a,new VariableSetting(fixture.Reference(fixture.Alternate))}});
                fixture.Host.Commit(edit);Order(b,c,a);
                edit=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{b,100L}});
                fixture.Host.Commit(edit);Order(c,a);
                edit=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{b,new VariableSetting(fixture.Reference(fixture.Variable))}});
                fixture.Host.Commit(edit);Order(c,a,b);
                var runtime=fixture.Node.RuntimeConfiguration!;
                Require(runtime.Copy().ParameterBindingOrder.SequenceEqual(new[]{c,a,b})&&runtime.MapReferences(reference=>reference).ParameterBindingOrder.SequenceEqual(new[]{c,a,b}),"复制或映射重排队列");
                using(var run=fixture.Run())Require(run.Success,"顺序配置未进入实际执行");Order(c,a,b);
                var reopened=fixture.SaveReopen("ordered");Order(c,a,b);
                Require(reopened.Workflows[0].Nodes[0].RuntimeConfiguration!.ParameterBindingOrder.SequenceEqual(new[]{b,c,a}),"保存重开未按FindNum/HightThreshold/LowThreshold记录排序");
                Require(!new[]{a,b,c}.OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(new[]{b,c,a}),"样本没有区分字段ID与参数名排序");
                fixture.Host.Undo();Order(c,a);fixture.Host.Redo();Order(c,a,b);
            });
            Case("运行按队列逐项接纳，改源移尾后前项失败不会消费后项",()=>
            {
                using var fixture=new Fixture(extraFailure:true);
                using(var run=fixture.Run())Require(!run.Success&&fixture.Current==173,"后项失败没有留下前项173");
                var edit=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{fixture.Threshold,new VariableSetting(fixture.Reference(fixture.Alternate))}});
                fixture.Host.Commit(edit);
                Require(fixture.Node.RuntimeConfiguration!.ParameterBindingOrder.First()==Setting("BlobCountLimit"),"失败项没有排在移尾后的阈值前");
                using(var run=fixture.Run())Require(!run.Success&&fixture.Current==173,"首项失败仍消费了后项37，或按字典执行");
            });
            Case("队列JSON拒绝重复、缺项及未知项，同来源重复投影不重排",()=>
            {
                using var fixture=new Fixture(ordered:true);var configuration=fixture.Node.RuntimeConfiguration!;
                var same=configuration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{fixture.Threshold,configuration.Fields.Read(fixture.Threshold)}});
                Require(same.ParameterBindingOrder.SequenceEqual(configuration.ParameterBindingOrder),"同来源投影移动了队列");
                foreach(var invalid in new[]{new JsonArray(fixture.Threshold,fixture.Threshold,Setting("UpperThreshold")),new JsonArray(fixture.Threshold),new JsonArray("unknown",Setting("BlobCountLimit"),Setting("UpperThreshold"))})
                {
                    var json=JsonSerializer.SerializeToNode(configuration)!;json["ParameterBindingOrder"]=invalid;
                    Reject(()=>json.Deserialize<RuntimeToolConfiguration>());
                }
            });
            Case("同Id同配置替代节点拒绝旧草稿全部出口，新草稿仍可编辑保存",()=>
            {
                using var fixture=new Fixture();var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                var frozen=fixture.Host.Freeze(draft);
                var replacement=new WorkflowNodeBase(fixture.Node.Id,fixture.Node.Name,fixture.Node.DispName,fixture.Node.ToolType)
                    {RuntimeConfiguration=fixture.Node.RuntimeConfiguration!.Copy()};
                fixture.Flow.Nodes[0]=replacement;fixture.Host.SynchronizeStructure();
                Require(fixture.Host.HasPublishedNodeChanged(draft),"同Id替代节点被认作原正式实例");
                Reject(()=>fixture.Host.ReadCurrentParameter(draft,fixture.Threshold));
                Reject(()=>fixture.Host.Freeze(draft));Reject(()=>fixture.Host.RebaseAcceptedDraft(draft));
                Reject(()=>fixture.Host.Validate(frozen));Reject(()=>fixture.Host.Commit(frozen));
                var fresh=fixture.Host.BeginEdit(fixture.Flow.Id,replacement.Id);
                fresh.Node.RuntimeConfiguration=fresh.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{fixture.Threshold,99L}});
                fixture.Host.Commit(fresh);
                Require(replacement.RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:99L},"新实例不能正常确认");
                Require(fixture.Node.RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:37L},"旧实例被修改");
                var reopened=fixture.SaveReopen("replacement");
                Require(reopened.Workflows[0].Nodes[0].RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:99L},"替代实例保存丢失");
            });
            Case("删除期间草稿失效，同一正式实例恢复后保留原草稿而不重绑替代对象",()=>
            {
                using var fixture=new Fixture();var draft=fixture.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id);
                draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{fixture.Threshold,88L}});
                fixture.Flow.Nodes.Remove(fixture.Node);fixture.Host.SynchronizeStructure();
                Reject(()=>fixture.Host.Commit(draft));Reject(()=>fixture.Host.ReadCurrentParameter(draft,fixture.Threshold));
                fixture.Flow.Nodes.Add(fixture.Node);fixture.Host.SynchronizeStructure();
                fixture.Host.Commit(draft);Require(fixture.Current==88,"恢复原实例后丢失待确认参数");
            });
            Case("真实片段复制保留当前值和顺序，正式加入后的编辑历史及保存彼此独立",()=>
            {
                using var fixture=new Fixture(ordered:true,documentHistory:true);using(var result=fixture.Run())Require(result.Success,"复制前运行失败");
                var fragment=WorkflowFragmentCopy.Prepare(fixture.Flow,new[]{fixture.Node.Id},fixture.Flow.Id,
                    id=>fixture.Document.Workflows.SingleOrDefault(flow=>flow.Id==id),
                    (original,scope)=>new WorkflowNodeBase("copy-blob","copy-blob","Blob副本",original.ToolType));
                var copy=fragment.Nodes.Single();
                var commands=new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(fixture.Flow.Nodes,fixture.Flow.Connections);
                commands.BindDocumentHistory(fixture.Editing!.History,fixture.Flow.Id,()=>fixture.Host.AcceptDocumentGraphHistory(),()=>NodeConfigurationEditHost.DocumentHistorySignature(fixture.Document));
                commands.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.PasteWorkflowFragmentCommand(fixture.Flow,fragment,fixture.Engine,fixture.Document));
                Require(copy.RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:173L}
                    &&copy.RuntimeConfiguration.ParameterBindingOrder.SequenceEqual(fixture.Node.RuntimeConfiguration!.ParameterBindingOrder),"真实片段复制丢失参数状态");
                using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(fixture.Host,fixture.Flow.Id,copy,()=>true,_=>{},documentEdits:fixture.Editing);
                context.DraftNode.RuntimeConfiguration=context.DraftNode.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{fixture.Threshold,90L}});
                Require(context.TryCommitEditor(out var error),error);Require(fixture.Current==173,"副本编辑污染来源节点");
                fixture.Host.Undo();Require(copy.RuntimeConfiguration!.Fields.Read(fixture.Threshold) is VariableSetting,"副本撤销未恢复引用");
                fixture.Host.Redo();Require(copy.RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:90L},"副本重做未恢复常量");
                var reopened=fixture.SaveReopen("fragment");
                Require(reopened.Workflows[0].Nodes.Single(node=>node.Id==copy.Id).RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:90L},"副本保存丢失参数");
                fixture.Host.Undo();fixture.Host.Undo();Require(!fixture.Flow.Nodes.Contains(copy),"统一文档历史未撤销真实粘贴");
                fixture.Host.Redo();Require(fixture.Flow.Nodes.Any(node=>ReferenceEquals(node,copy)),"粘贴重做换了正式实例");
            });
            Case("真实参数编辑整体保存、锁文件失败重试及重开切换不复用旧Host",()=>
            {
                using var fixture=new Fixture(documentHistory:true);var manager=fixture.Manager;var host=fixture.Host;
                using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(host,fixture.Flow.Id,fixture.Node,()=>true,_=>{},documentEdits:fixture.Editing);
                var service=new SunEyeVision.UI.Services.Solutions.SolutionApplicationService(manager);
                var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,fixture.Flow.Id+"-editor.solution");
                context.DraftNode.RuntimeConfiguration=context.DraftNode.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{fixture.Threshold,89L}});
                var saved=service.Save(path,()=>fixture.Editing!.AcceptAll());
                Require(saved.FileWritten&&!manager.HasUnsavedChanges&&fixture.Current==89,"整体保存未接纳真实参数编辑："+saved.Error);
                var original=File.ReadAllBytes(path);
                context.DraftNode.RuntimeConfiguration=context.DraftNode.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{fixture.Threshold,91L}});
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    var failed=service.Save(path,()=>fixture.Editing!.AcceptAll());
                    Require(!failed.FileWritten&&manager.HasUnsavedChanges&&fixture.Current==91&&context.IsActive,"保存失败丢弃正式接纳或退役仍打开编辑器");
                }
                Require(File.ReadAllBytes(path).SequenceEqual(original),"失败保存改变原文件");
                saved=service.Save(path,()=>fixture.Editing!.AcceptAll());Require(saved.FileWritten&&!manager.HasUnsavedChanges,"保存重试失败："+saved.Error);
                var reopened=new SolutionRepository().LoadCandidate(path).Candidate??throw new Exception("保存重开失败");
                manager.SetCurrentSolution(reopened,registerInCatalog:false);
                Require(!context.IsActive&&reopened.Workflows[0].Nodes[0].RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:91L},"重开未退役旧编辑器或丢失参数");
                Reject(()=>host.BeginEdit(fixture.Flow.Id,fixture.Node.Id));
                Require(manager.Session!.Editing.Host.BeginEdit(fixture.Flow.Id,fixture.Node.Id).Node.RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:91L},"新会话不能继续编辑");
            });
            Case("运行准入回调重入关闭或替换节点时拒绝已准备参数当前值",()=>
            {
                foreach(var close in new[]{true,false})
                {
                    using var fixture=new Fixture(documentHistory:true);var publication=fixture.Publication();var opening=fixture.Node.RuntimeConfiguration!;
                    var armed=false;
                    var identity=fixture.Document.ExecutionOwner.Begin(fixture.Flow.Id+"/"+fixture.Node.Id,Guid.NewGuid(),"parameter-lifecycle",admission:()=>
                    {
                        if(armed)
                        {
                            if(close)fixture.Editing!.Close();
                            else fixture.Flow.Nodes[0]=new WorkflowNodeBase(fixture.Node.Id,fixture.Node.Name,fixture.Node.DispName,fixture.Node.ToolType){RuntimeConfiguration=opening.Copy()};
                        }
                        return true;
                    });
                    using var run=fixture.Document.ExecutionOwner.EnterRun(identity);armed=true;
                    var accept=typeof(RuntimeParameterValueAcceptance).GetMethod("Accept",BindingFlags.Instance|BindingFlags.NonPublic)!;
                    Reject(()=>accept.Invoke(publication.ParameterValues,new object?[]{fixture.Flow.Id,fixture.Node.Id,identity,opening,fixture.Threshold,99L}));
                    Require(fixture.Current==37&&fixture.Flow.Nodes[0].RuntimeConfiguration!.ReadCurrentParameter(fixture.Threshold) is {Value:37L},"重入退役后仍安装参数候选");
                }
            });
            Case("正式Manager切换退役旧参数Host，取消异常不阻断其余清理或新会话",()=>
            {
                var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"manager-switch");
                var manager=new SolutionManager(directory);var document=manager.CreateUntitledSolution();
                var node=new WorkflowNodeBase("blob","Blob","Blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id)};
                var flow=document.Workflows[0];flow.Nodes.Add(node);
                var session=manager.Session!;var editing=session.Editing;var host=editing.Host;var draft=host.BeginEdit(flow.Id,node.Id);
                var first=new CloseParticipant(editing,"first",()=>throw new InvalidOperationException("取消异常注入"));
                var second=new CloseParticipant(editing,"second");editing.Register(first);editing.Register(second);
                manager.SetCurrentSolution(document,registerInCatalog:false);
                Require(ReferenceEquals(manager.Session,session)&&first.Cancelled==0,"同一正式对象重复设置替换编辑会话");
                var next=manager.CreateUntitledSolution();
                Require(!ReferenceEquals(manager.Session,session)&&ReferenceEquals(manager.CurrentSolution,next),"未切换到新正式方案");
                Require(first.Cancelled==1&&second.Cancelled==1&&editing.LastRefreshError is not null,"取消异常中断清理或未记录");
                Reject(()=>host.Commit(draft));Reject(()=>host.Freeze(draft));Reject(()=>host.ReadCurrentParameter(draft,Setting("Threshold")));
                Reject(()=>editing.Host.BeginEdit(flow.Id,node.Id));Reject(()=>editing.History.Undo());
                editing.Close();Require(first.Cancelled==1&&second.Cancelled==1,"UI重复关闭重复取消");
                manager.CloseSolution(saveChanges:false);
            });
            Case("执行Owner已退役后的通知异常仍完成正式关闭和切换，不遗留可写旧Host",()=>
            {
                foreach(var switching in new[]{true,false})
                {
                    var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"retirement-notification-"+switching);
                    var manager=new SolutionManager(directory);var document=manager.CreateUntitledSolution();var editing=manager.Session!.Editing;var host=editing.Host;
                    _=document.ExecutionOwner.Begin("retirement-domain",Guid.NewGuid(),"parameter-lifecycle");
                    var notified=0;document.ExecutionOwner.DomainRetired+=_=>{notified++;throw new InvalidOperationException("退役通知异常注入");};
                    document.ExecutionOwner.DomainRetired+=_=>notified++;
                    var observed=false;
                    manager.SolutionOpened+=(_,_)=>{Reject(()=>host.Capture());observed=true;};
                    manager.SolutionClosed+=(_,_)=>{Reject(()=>host.Capture());observed=true;};
                    if(switching)
                    {
                        var next=manager.CreateUntitledSolution();Require(ReferenceEquals(manager.CurrentSolution,next),"已退役通知异常未切换正式方案");
                    }
                    else
                    {
                        manager.CloseSolution(saveChanges:false);Require(manager.CurrentSolution is null,"已退役通知异常未关闭正式方案");
                    }
                    Require(notified==2&&observed,"退役异常中断后续通知，或正式通知未在旧Host退役后发生");
                    Reject(()=>host.Capture());Reject(()=>editing.Host.Capture());
                    manager.CloseSolution(saveChanges:false);
                }
            });
            Case("关闭未打开参数页的正式方案不懒建宿主，保存失败仍保留同一编辑会话",()=>
            {
                var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"manager-close");
                var manager=new SolutionManager(directory);var document=manager.CreateUntitledSolution();
                var unopened=manager.Session!;manager.CloseSolution(saveChanges:false);
                Reject(()=>unopened.Editing.Host.Capture());
                document=manager.CreateUntitledSolution();
                var editing=manager.Session!.Editing;manager.CloseSolution(saveChanges:false);
                Reject(()=>editing.Host.Capture());Reject(()=>editing.History.Undo());editing.Close();
                document=manager.CreateUntitledSolution();var session=manager.Session!;editing=session.Editing;var host=editing.Host;
                document.Name="未保存参数会话";Reject(()=>manager.CloseSolution(saveChanges:true));
                Require(ReferenceEquals(manager.Session,session)&&ReferenceEquals(manager.CurrentSolution,document),"保存失败关闭了所属根");
                Require(ReferenceEquals(editing.Host,host),"保存失败退役了仍打开的参数宿主");
                var identity=document.ExecutionOwner.Begin("close-refusal",Guid.NewGuid(),"parameter-lifecycle");
                using(var run=document.ExecutionOwner.EnterRun(identity))
                {
                    Reject(()=>manager.CloseSolution(saveChanges:false));
                    Require(ReferenceEquals(manager.Session,session)&&ReferenceEquals(editing.Host,host),"运行未排空的关闭拒绝退役了仍打开宿主");
                }
                manager.CloseSolution(saveChanges:false);Reject(()=>host.Capture());manager.CloseSolution(saveChanges:false);
            });
            Console.WriteLine("参数当前值与顺序生产链："+_passed+"组通过；不代表原厂多绑定动态差分、String/TS或全Blob验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private sealed class CloseParticipant(DocumentEditSession editing,string key,Action? onCancel=null):IDocumentEditParticipant
    {
        public Guid ScopeId{get;}=Guid.NewGuid();public string ScopeKey=>key;public int Cancelled;
        public bool HasPendingChanges=>false;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)=>throw new NotSupportedException();
        public void Acknowledge(DocumentEditProposal prepared,BindingConfigurationPublication publication)=>throw new NotSupportedException();
        public void Cancel(){Cancelled++;editing.Unregister(ScopeId);onCancel?.Invoke();}
    }
    private sealed class Fixture:IDisposable
    {
        public WorkflowEngine Engine {get;}=new(new CheckLogger());
        public Workflow Flow {get;}
        public Solution Document {get;}=new(){Name="B08参数当前值"};
        public WorkflowNodeBase Node {get;}
        public NodeConfigurationEditHost Host {get;}
        public DocumentEditSession? Editing {get;}
        private readonly SolutionManager? _manager;
        public SolutionManager Manager=>_manager??throw new InvalidOperationException("本样本没有正式Manager会话。");
        public GlobalVariable Variable {get;}
        public GlobalVariable Alternate {get;}
        public string Threshold {get;}=Setting("Threshold");
        private readonly HalImage _image;
        private readonly WorkflowExecutionEngine _executor;
        public long Current=>(long)Node.RuntimeConfiguration!.ReadCurrentParameter(Threshold)!.Value!;
        public Fixture(long source=173,bool extraFailure=false,bool ordered=false,bool documentHistory=false)
        {
            Flow=Engine.CreateWorkflow(Guid.NewGuid().ToString(),"参数当前值");Document.Workflows.Add(Flow);
            Variable=new GlobalVariable("阈值来源",source,"Integer");Document.GlobalVariables.Add(Variable);
            Alternate=new GlobalVariable("替换来源",37L,"Integer");Document.GlobalVariables.Add(Alternate);
            var configuration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings
                {Threshold=37,IsAreaFilterEnabled=false,OutputGrayImage=false,OutputBinaryImage=false,OutputBoundary=false});
            configuration=configuration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Threshold,new VariableSetting(Reference(Variable))}});
            if(ordered)configuration=configuration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Setting("BlobCountLimit"),new VariableSetting(Reference(Variable))},{Setting("UpperThreshold"),new VariableSetting(Reference(Variable))}});
            if(extraFailure)
            {
                var invalid=new GlobalVariable("后项数组",new long[]{100},"Integer[]");Document.GlobalVariables.Add(invalid);
                configuration=configuration.WithFieldSettings(new Dictionary<string,ParameterSetting>{{Setting("BlobCountLimit"),
                    new VariableSetting(Reference(invalid,new BindingSelection.AtConstant(99)))}});
            }
            Node=new WorkflowNodeBase("blob","blob","Blob",BlobTool.Id){RuntimeConfiguration=configuration};Flow.Nodes.Add(Node);
            if(documentHistory)
            {
                _manager=new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Flow.Id+"-manager"));
                _manager.SetCurrentSolution(Document,registerInCatalog:false);Editing=_manager.Session!.Editing;Host=Editing.Host;
            }
            else Host=new NodeConfigurationEditHost(Document);
            _executor=new WorkflowExecutionEngine(Engine,new PluginManager(),new CheckLogger());
            var pixels=new byte[64];for(var y=1;y<5;y++)for(var x=1;x<5;x++)pixels[y*8+x]=100;
            _image=HalImage.CreateFromBytes(pixels,8,8,1);
        }
        public VariableReference Reference(GlobalVariable variable,BindingSelection? selection=null)
        {
            Require(GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out var port,out _,out var reason),reason);
            return new(Document.Id,variable.Id,port.Type,selection,port.Storage);
        }
        public WorkflowPublishedConfiguration Publication()=>WorkflowPublishedConfiguration.Capture(Host.Capture(),Document,Host);
        public ExecutionResult Run(WorkflowPublishedConfiguration? captured=null)
        {
            var publication=captured??Publication();
            using var globals=new WorkflowGlobalRunValues(Document.Id,publication.GlobalDefinitions,
                publication.GlobalDefinitions.Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value)));
            return Wait(()=>_executor.ExecuteWorkflowAsync(new WorkflowRunRequest(Flow.Id,_image)
                {Configuration=publication.Configuration,ExecutionOwner=publication.ExecutionOwner,ProducedGlobals=globals,ParameterValues=publication.ParameterValues}));
        }
        public long Count(ExecutionResult result)=>((BlobOutput)Result(result,Node.Id).Output!).Count;
        public RuntimeDebugInvocation Debug(NodeConfigurationEditDraft draft,ToolExecutionOwner owner,CancellationToken cancellation=default)
            =>RuntimeDebugInvocation.Capture(Flow,draft.Node,owner,Host.Capture().Version,_=>null,cancellation,_image,
                publishedOwner:Document.ExecutionOwner,solutionId:Document.Id,variables:Document.GlobalVariables,sourceWorkflows:Document.Workflows);
        public Solution SaveReopen(string suffix)
        {
            var repository=new SolutionRepository();var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Flow.Id+"-"+suffix+".solution");
            Require(repository.Save(Document,path),repository.LastError??"保存失败");
            return repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError??"重开失败");
        }
        public void Dispose(){_manager?.CloseSolution(saveChanges:false);_image.Dispose();Document.ExecutionOwner.Retire();}
    }
    private sealed class OwnerLease:IDisposable{public ToolExecutionOwner Owner{get;}=new();public void Dispose()=>Owner.Retire();}
    private static string Setting(string name)
        =>((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(field=>field.DisplayName==BlobParameterDefinition.Instance.Label(name)).FieldId;
    private static DeclaredOutputSnapshot Result(ExecutionResult result,string node)=>result.NodeResults[node].Result as DeclaredOutputSnapshot??throw new Exception("没有正式Runtime结果");
    private static T Wait<T>(Func<Task<T>> start)
    {
        var previous=SynchronizationContext.Current;var context=new NoPost();SynchronizationContext.SetSynchronizationContext(context);
        try{var task=start();Require(Task.WhenAny(task,Task.Delay(TimeSpan.FromSeconds(30))).GetAwaiter().GetResult()==task,"生产链等待超时");Require(context.Posts==0,"执行等待UI消息泵");return task.GetAwaiter().GetResult();}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }
    private sealed class NoPost:SynchronizationContext{public int Posts;public override void Post(SendOrPostCallback callback,object? state){Interlocked.Increment(ref Posts);throw new Exception("禁止等待UI消息泵");}}
    private static void Reject(Action action){try{action();}catch{return;}throw new Exception("本应拒绝的操作被接纳");}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){}
    }
}
