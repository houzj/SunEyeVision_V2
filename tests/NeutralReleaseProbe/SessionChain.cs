using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

// Test composition for a single local file, not a replacement desktop cursor/camera service.
internal static class SessionChain
{
    public static void Verify(RuntimeDirectorySnapshot directory, string image)
    {
        var cases=Path.Combine(Path.GetTempPath(),"suneye-neutral-session-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cases);
        try
        {
            using var manager=new DocumentSessionManager(directory);
            var session=manager.CreateNew("无桌面正式会话");
            var flow=session.GetWorkflows().Single();
            var graph=session.PrepareGraphEdit(flow.Id);
            var source=session.CreateNode(graph,"ImageLoad",new CanvasPoint(0,0));
            var blob=session.CreateNode(graph,"BlobAnalysis",new CanvasPoint(200,0));
            graph.AddConnection(new ConnectionDefinition("source-blob",source.Id,blob.Id,"output.image","input.image"));
            session.CommitGraphEdit(graph,"无桌面实际图");
            {
                var input=session.Editing.Host.BeginEdit(flow.Id,source.Id);
                var analysis=session.Editing.Host.BeginEdit(flow.Id,blob.Id);
                var paths=input.SettingsSchema.Inputs.Single(p=>p.Id=="local.paths");
                input.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new SequenceValue(paths.Type,new object?[]{image}))}});
                var output=session.GetNodeSchema(flow.Id,source.Id)!.Outputs.Single(p=>p.Id=="output.image");
                analysis.ApplyFields(new Dictionary<string,ParameterSetting>{{"input.image",new ReferenceSetting(new OutputReference(source.Id,output.Id,output.Type,new[]{"data"},scopeId:flow.Id))}});
                session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{input,analysis}));
            }
            var inputs=new LocalFileFactory(image);
            Run(session,flow.Id,blob.Id,inputs);
            var file=Path.Combine(cases,"session.sol");
            var types=new FrozenVariableTypeCatalog(new Dictionary<string,string>());
            session.Save(file,Path.Combine(cases,"recovery"),types,new JsonSerializerOptions(),expected:TargetExpectation.Missing);
            session=manager.Open(file,_=>types,_=>null,new JsonSerializerOptions());
            flow=session.GetWorkflows().Single();
            Run(session,flow.Id,blob.Id,inputs);
            var blocking=new LocalFileFactory(image,true);
            using(var batch=session.CaptureRunBatch(new[]{flow.Id},hostFactory:blocking))
            {
                var running=batch.RunAsync(_=>{});
                blocking.Started.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                batch.Stop();
                try
                {
                    using var result=running.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                    if(result.Workflows.Values.Any(r=>r.Success)) throw new Exception("取消的输入意外执行成功");
                }
                catch(OperationCanceledException) { }
            }
            if(blocking.Created!=blocking.Disposed || inputs.Created!=inputs.Disposed)
                throw new Exception("宿主输入准备器没有完整释放");
            Console.WriteLine("PASS: 无WPF实际Session编辑、ImageLoad→Blob、reader、保存重开、取消排空");
        }
        finally
        {
            // Only this freshly-created, absolute single-case directory is removed.
            var full=Path.GetFullPath(cases);
            if(Path.GetDirectoryName(full)!=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new Exception("临时目录边界不匹配");
            Directory.Delete(full,true);
        }
    }
    private static void Run(DocumentSession session,string flow,string blob,LocalFileFactory inputs)
    {
        using var batch=session.CaptureRunBatch(new[]{flow},hostFactory:inputs);
        using var result=batch.RunAsync(_=>{}).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        if(!result.Workflows[flow].Success) throw new Exception("无桌面会话正式执行失败: "+string.Join(";",result.Workflows[flow].Errors));
        using var reader=session.AcquirePublishedResult(flow,blob)??throw new Exception("缺少正式Blob reader");
    }
    private sealed class LocalFileFactory(string path,bool block=false):IHostInputFactory
    {
        public int Created,Disposed;
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IHostInputPreparer? Create(HostInputFactoryContext context)
        {
            context.EnsureCurrent();
            if(context.Declaration.CapabilityId!="image-source") return null;
            Created++;
            return new Preparer(this,path,block);
        }
        private sealed class Preparer(LocalFileFactory owner,string path,bool block):IHostInputPreparer
        {
            private int _disposed;
            public string CapabilityId=>"image-source";
            public int CapabilityVersion=>1;
            public IReadOnlyList<string> Services=>new[]{"local-image-read"};
            public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext request)
            {
                if(!request.Settings.Get<SequenceValue>("local.paths").Any(p=>string.Equals(p as string,path,StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("运行输入不属于正式冻结参数");
                request.Cancellation.ThrowIfCancellationRequested();
                owner.Started.TrySetResult();
                if(block) await Task.Delay(Timeout.InfiniteTimeSpan,request.Cancellation).ConfigureAwait(false);
                return new Prepared(path,request);
            }
            public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)==0) Interlocked.Increment(ref owner.Disposed);}
        }
        private sealed class Prepared(string path,HostInputPreparationContext request):IPreparedHostInputs
        {
            public IReadOnlyDictionary<string,object?> Values {get;}=new Dictionary<string,object?>{{"input.local-address",path},{"input.sequence",1L}};
            public IToolExecutionContext BindContext(IToolExecutionContext context)=>new LocalContext(context,path,request);
            public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        }
        private sealed class LocalContext(IToolExecutionContext inner,string path,HostInputPreparationContext request):IToolExecutionContext,ILocalImageReadContext
        {
            public RunIdentity Identity=>inner.Identity;
            public CancellationToken Cancellation=>inner.Cancellation;
            public IOutputResources Resources=>inner.Resources;
            public void Log(string message)=>inner.Log(message);
            public string AcceptLocalImage()
            {
                string? accepted=null;
                if(!request.Gate.TryInvoke(()=>{request.Cancellation.ThrowIfCancellationRequested();accepted=path;}))
                    throw new OperationCanceledException("输入调用已退役",request.Cancellation);
                return accepted??throw new Exception("没有接纳输入");
            }
        }
    }
}
