using System.IO;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
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
            Require(PluginLoader.LoadPlugins(),"插件加载失败");
            var manager=new PluginManager();var engine=new WorkflowEngine(new Logger());var flow=engine.CreateWorkflow("debug-runtime","调试快照");
            var configuration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings{Threshold=73,IsAreaFilterEnabled=false,OutputGrayImage=true});
            var source=new WorkflowNodeBase("source","source","生产者",BlobTool.Id){RuntimeConfiguration=configuration};flow.Nodes.Add(source);
            var executor=new WorkflowExecutionEngine(engine,manager,new Logger());var pixels=new byte[64];
            for(var y=1;y<5;y++)for(var x=1;x<5;x++)pixels[y*8+x]=100;
            using var image=HalImage.CreateFromBytes(pixels,8,8,1);
            ExecutionResult Produce()=>WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image)));
            var consumer=new WorkflowNodeBase("consumer","consumer","编辑节点",BlobTool.Id){RuntimeConfiguration=configuration.Copy()};
            Bind(consumer,BlobTool.ImageInput,source.Id,BlobSchema.Field(5),TypeRef.ResourceOf(ResourceKind.Image),flow.Id);
            Bind(consumer,BlobTool.InspectionInput,source.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),flow.Id);
            using var ownerScope=new OwnerScope();var owner=ownerScope.Owner;
            Case("生产结果释放后调试保留原始图像和ROI帧，冻结草稿且不重跑生产者，无UI回调",()=>
            {
                var run=Produce();var published=(DeclaredOutputSnapshot)run.NodeResults[source.Id].Result!;
                var original=published!.Identity;var frame=((BlobOutput)published.Output!).GrayImage!.Frame;
                flow.Nodes.Add(consumer);int executions=0;EventHandler<NodeExecutionStatus> count=(_,_)=>executions++;executor.NodeStatusChanged+=count;
                try
                {
                    using var invocation=RuntimeDebugInvocation.Capture(flow,consumer,owner,4,id=>id==source.Id?published:null,default);
                    Require(invocation.SourceIdentities.Single()==original&&invocation.Identity.ToInvocationIdentity(consumer.Id).RunId!=original.RunId,"来源身份被重标记");
                    consumer.RuntimeConfiguration=configuration.WithOverrides(new Dictionary<string,object?>{{"Threshold",200}});consumer.ParamSettings.RemoveSetting(BlobTool.InspectionInput);
                    run.Dispose();var result=WithoutUiPump(invocation.ExecuteAsync);
                    try
                    {
                        Require(result.Output is BlobOutput {TotalArea:16},"草稿修改污染本次冻结值");
                        using var region=((BlobOutput)result.Output!).Region.AcquireReader();Require(region.SourceFrame==frame,"物理帧被替换");
                        using var gray=((BlobOutput)result.Output!).GrayImage!.AcquirePixels();Require(gray.Layout.Width==8,"输入读者释放后输出图像失效");
                        Require(executions==0&&owner.ActiveRuns==0&&result?.SourceWorkflowId==flow.Id,"调试重跑上游或未排空/发布");
                    }
                    finally{result.Dispose();}
                }
                finally{executor.NodeStatusChanged-=count;flow.Nodes.Remove(consumer);consumer.RuntimeConfiguration=configuration;Bind(consumer,BlobTool.InspectionInput,source.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),flow.Id);run.Dispose();}
            });
            using var production=Produce();var current=(DeclaredOutputSnapshot)production.NodeResults[source.Id].Result!;flow.Nodes.Add(consumer);
            Case("设置订阅使用冻结已发布值，正式强类型设置保持原值",()=>
            {
                var field=((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName==BlobParameterDefinition.Instance.Label("Threshold")).FieldId;
                consumer.RuntimeConfiguration=configuration.WithFieldValues(new Dictionary<string,object?>{{field,200L}});
                Bind(consumer,field,source.Id,BlobSchema.Field(11),DataType.Integer,flow.Id);
                try
                {
                    using var request=RuntimeDebugInvocation.Capture(flow,consumer,owner,5,_=>current,default);var result=WithoutUiPump(request.ExecuteAsync);
                    try{Require(result.Output is BlobOutput{TotalArea:16}&&((BlobSettings)consumer.RuntimeConfiguration.CaptureSettings()).Threshold==200,"设置订阅污染正式模型或未进入本次执行");}
                    finally{result.Dispose();}
                }
                finally{consumer.ParamSettings.RemoveSetting(field);consumer.RuntimeConfiguration=configuration;}
            });
            Case("普通工作流解析仍拒绝跨轮结果，只有明确发布快照入口允许选定来源",()=>
            {
                using var reader=current!.AcquireReader();var actual=current;
                var id=owner.Begin(flow.Id+"/consumer",Guid.NewGuid(),ToolContract.Version);try
                {
                    var node=new BindingNodeId(flow.Id,source.Id);var consumerId=new BindingNodeId(flow.Id,consumer.Id);
                    var sourceDeclaration=new BindingSource(node,actual.Identity.ScopeId,actual.Schema,actual.SchemaRevision);
                    var scope=new BindingScopeSnapshot(consumerId,owner.Id,6,new[]{sourceDeclaration},new[]{node},Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
                    var target=configuration.Schema.Inputs.Single(p=>p.Id==BlobTool.ImageInput);var resolver=new ParameterBindingService();
                    var plan=resolver.CompileBinding(BindingReferenceSerializer.Deserialize(consumer.ParamSettings.GetSetting(BlobTool.ImageInput)!.ReferenceExpression!),target,scope).Plan!;
                    var snapshot=new PublishedBindingSnapshot(consumerId,id.ToInvocationIdentity(consumer.Id),6,new[]{new BindingNodeResult(node,actual.Identity,actual.SchemaRevision,actual.Schema.Fingerprint,true,reader.Values!)});
                    Require(!resolver.ResolveBinding(plan,snapshot).Success&&resolver.ResolvePublishedBinding(plan,snapshot).Success,"跨轮规则被放宽或发布快照不能解析");
                }
                finally{id.CompleteCallbacks();}
            });
            Case("缺失/失败/捕获期间替换的来源明确拒绝，不执行生产者",()=>
            {
                Reject(()=>RuntimeDebugInvocation.Capture(flow,consumer,owner,7,_=>null,default));
                var otherDocument=new ToolExecutionOwner();
                try{Reject(()=>RuntimeDebugInvocation.Capture(flow,consumer,owner,7,_=>current,default,publishedOwner:otherDocument));}
                finally{otherDocument.Retire();}
                using(var failedRun=WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id))))
                {
                    Require(!failedRun.Success,"缺少图像的实际生产运行应失败");
                    var failed=failedRun.NodeResults[source.Id].Result??failedRun.DeclaredOutputs.Single(value=>value.Identity.NodeId==source.Id);
                    Reject(()=>RuntimeDebugInvocation.Capture(flow,consumer,owner,7,_=>failed,default));
                }
                int reads=0;Reject(()=>RuntimeDebugInvocation.Capture(flow,consumer,owner,7,_=>++reads==1?current:null,default));
                Require(owner.ActiveRuns==0,"失败捕获留下执行租约");
            });
            Case("关闭/取消发生在捕获后执行前时排空输入读者，下一次可运行",()=>
            {
                using var stop=new CancellationTokenSource();using var request=RuntimeDebugInvocation.Capture(flow,consumer,owner,8,_=>current,stop.Token);
                stop.Cancel();Reject(()=>WithoutUiPump(request.ExecuteAsync));Require(owner.ActiveRuns==0,"取消留下运行租约");
                using var next=RuntimeDebugInvocation.Capture(flow,consumer,owner,8,_=>current,default);var result=WithoutUiPump(next.ExecuteAsync);
                try{Require(result.IsSuccess,"取消后不能重跑");}finally{result.Dispose();}
            });
            Case("缺省工作流图像独立捕获像素及Domain，调试只执行一次",()=>
            {
                var draft=consumer.Clone();draft.ParamSettings=new();using var request=RuntimeDebugInvocation.Capture(flow,draft,owner,9,_=>throw new Exception("无引用不应读取上游"),default,image);
                var result=WithoutUiPump(request.ExecuteAsync);try{Require(result.Output is BlobOutput {TotalArea:16},"缺省图像未进入调试");}finally{result.Dispose();}
                Reject(()=>WithoutUiPump(request.ExecuteAsync));
            });
            Case("画布执行连线与来源引用组成的循环，以及缺少图像来源，均在算法前拒绝",()=>
            {
                var connection=new WorkflowConnection{Id="debug-cycle",SourceNodeId=consumer.Id,TargetNodeId=source.Id};flow.Connections.Add(connection);
                try{Reject(()=>RuntimeDebugInvocation.Capture(flow,consumer,owner,10,_=>current,default));}
                finally{flow.Connections.Remove(connection);}
                var missing=consumer.Clone();missing.ParamSettings=new();Reject(()=>RuntimeDebugInvocation.Capture(flow,missing,owner,10,_=>current,default));
                Require(owner.ActiveRuns==0,"拒绝后仍有调试运行");
            });
            Console.WriteLine($"S3 Runtime调试：{_passed}组通过；已发布输入快照/真实HALCON/资源排空，不代表完整窗口、VM视觉或人工验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Bind(WorkflowNodeBase consumer,string target,string source,string port,TypeRef type,string scope)
        =>consumer.ParamSettings.SetSetting(new ParamSetting{ParameterName=target,BindingType=BindingType.Binding,ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(source,port,type,scopeId:scope)),SourceNodeId=source,SourceProperty=port,SourceWorkflowId=scope});
    private static void Reject(Action action){try{action();}catch(InvalidOperationException){return;}catch(OperationCanceledException){return;}throw new Exception("预期拒绝却成功");}
    private static T WithoutUiPump<T>(Func<Task<T>> start)
    {var previous=SynchronizationContext.Current;var context=new NoPost();SynchronizationContext.SetSynchronizationContext(context);try{var task=start();Require(Task.WhenAny(task,Task.Delay(TimeSpan.FromSeconds(30))).GetAwaiter().GetResult()==task,"算法等待UI");Require(context.Posts==0,"算法反调UI");return task.GetAwaiter().GetResult();}finally{SynchronizationContext.SetSynchronizationContext(previous);}}
    private sealed class NoPost:SynchronizationContext{public int Posts;public override void Post(SendOrPostCallback callback,object? state){Posts++;throw new Exception("禁止UI回调");}}
    private sealed class OwnerScope:IDisposable{public ToolExecutionOwner Owner{get;}=new();public void Dispose()=>Owner.Retire();}
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private sealed class Logger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message);}}
}
