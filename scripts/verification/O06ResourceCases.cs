using System.IO;
using System.Reflection;
using System.Text.Json;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    private static readonly List<object> Outcomes=new();
    [STAThread] private static int Main()
    {
        var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
        var plugins=new PluginManager();plugins.LoadPlugins(Path.Combine(product,"plugins"));PluginLoader.MarkAsLoaded();
        plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(t=>t.Id));
        Check("真实四工具run/declared/pixel三级所有权与输入保留",()=>WorkflowOwners(plugins));
        foreach(var id in new[]{"ImageFilter","ImageThreshold","Morphology"})Check(id+"失败部分输出释放且输入不释放",()=>PartialFailure(id));
        foreach(var async in new[]{false,true})foreach(var expired in new[]{false,true})foreach(var failedCleanup in new[]{false,true})
            Check($"owned迟到结果 async={async} expired={expired} failedCleanup={failedCleanup}",()=>RejectedOwnedResult(async,expired,failedCleanup));
        foreach(var id in new[]{"ImageFilter","ImageThreshold","Morphology"}){Check(id+"真实Results同引用去重与多租约",()=>CountedOutputOwner(id,false));Check(id+"真实Results多图异常仍全部清理",()=>CountedOutputOwner(id,true));}
        foreach(var async in new[]{false,true})foreach(var expired in new[]{false,true})Check($"dual cleanup exceptions async={async} expired={expired}",()=>DualCleanupFailure(async,expired));
        var json=JsonSerializer.Serialize(Outcomes,new JsonSerializerOptions{WriteIndented=true});Console.WriteLine(json);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"resource-results.json"),json);
        return Outcomes.All(o=>JsonSerializer.SerializeToElement(o).GetProperty("passed").GetBoolean())?0:1;
    }
    private static void WorkflowOwners(PluginManager plugins)
    {
        var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("resource-flow","resource-flow");
        WorkflowNodeBase Node(string id,string tool)=>new(id,id,id,tool){Parameters=ToolRegistry.CreateParameters(tool)};
        var filter=Node("filter","ImageFilter");Set(filter.Parameters,"FilterTypeValue","HistogramEqualization");flow.Nodes.Add(filter);
        var threshold=Node("threshold","ImageThreshold");Set(threshold.Parameters,"Method","InRange");Set(threshold.Parameters,"MinThreshold",100);Set(threshold.Parameters,"MaxThreshold",255);Source(threshold,"filter","OutputImage");flow.Nodes.Add(threshold);
        var morphology=Node("morphology","Morphology");Set(morphology.Parameters,"Operation","Open");Set(morphology.Parameters,"KernelShape","Circle");Source(morphology,"threshold","BinaryMask");flow.Nodes.Add(morphology);
        var blob=Node("blob","BlobAnalysis");Set(blob.Parameters,"SegmentationMode","None");Set(blob.Parameters,"IsAreaFilterEnabled",false);Source(blob,"morphology","BinaryMask");flow.Nodes.Add(blob);
        var bytes=new byte[32*32];for(int y=6;y<26;y++)for(int x=6;x<26;x++)bytes[y*32+x]=200;
        using var input=HalImage.CreateFromBytes(bytes,32,32,1);var executor=new WorkflowExecutionEngine(engine,plugins,new CheckLogger());
        var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,input)).GetAwaiter().GetResult();
        var readers=new List<DeclaredOutputReader>();var pixels=new List<IImagePixelReadLease>();var typed=new List<IReadOnlyImage>();var native=new List<IVisionImage>();
        try
        {
            Require(run.Success,string.Join(";",run.Errors??new()));
            foreach(var id in new[]{"filter","threshold","morphology","blob"})
            {
                native.AddRange(run.NodeResults[id].Result!.GetDisplayImages().Values.Where(i=>i is not null).Cast<IVisionImage>());
                var reader=run.DeclaredOutputs.Single(o=>o.Identity.NodeId==id).AcquireReader();readers.Add(reader);
                var field=reader.Snapshot.Fields.Single(f=>f.Value.Name==(id=="blob"?"GrayImage":"OutputImage")).Key;
                var image=reader.Values!.Get<IReadOnlyImage>(field);typed.Add(image);pixels.Add(image.AcquirePixels());
            }
            run.Dispose();Require(native.All(image=>image.Width==32),"run.Dispose破坏仍持declared reader的原图");
            foreach(var reader in readers)reader.Dispose();readers.Clear();
            var alive=0;foreach(var image in native){try{if(image.Width==32)alive++;}catch(ObjectDisposedException){}}
            Console.WriteLine($"上游原图总数={native.Count}，run及最后declared reader释放后仍可访问={alive}");
            Require(alive==0,"UPSTREAM_NATIVE_OWNER_LEAK: 原始HalImage未随最后结果租约释放。");
            Require(input.Width==32&&input.GetPixelData().SequenceEqual(bytes),"输出owner错误释放或修改调用者输入");
            foreach(var image in typed)Disposed(()=>image.CopyPixels());
            foreach(var lease in pixels)Require(lease.GetRow(0).Length==32,"已取得pixel lease未独立保活");
            foreach(var lease in pixels){lease.Dispose();Disposed(()=>lease.GetRow(0).ToArray());}
        }
        finally
        {
            run.Dispose();foreach(var reader in readers)reader.Dispose();foreach(var lease in pixels)lease.Dispose();
            // Bound the failure baseline too: explicit fixture cleanup happens after the recorded assertion.
            foreach(var image in native.Distinct(ReferenceEqualityComparer.Instance).Cast<IVisionImage>())image.Dispose();
        }
    }
    private static void PartialFailure(string id)
    {
        using var input=HalImage.CreateFromBytes(Enumerable.Repeat((byte)255,64).ToArray(),8,8,1);
        var parameters=ToolRegistry.CreateParameters(id);parameters.ResolvedInputImage=input;
        Set(parameters,id=="ImageFilter"?"FilterTypeValue":id=="ImageThreshold"?"Method":"Operation","999");
        var tool=ToolRegistry.CreateToolInstance(id)!;var result=(ToolResults)tool.GetType().GetMethod("Run")!.Invoke(tool,new object[]{parameters})!;
        var gray=(IVisionImage?)result.GetType().GetProperty("GrayImage")!.GetValue(result);
        try
        {
            Require(!result.IsSuccess&&gray is not null,"未到创建灰度后失败的真实路径");result.Resources?.Dispose();
            var alive=true;try{_=gray!.Width;}catch(ObjectDisposedException){alive=false;}
            Console.WriteLine(id+"失败后原灰度仍可访问="+alive);Require(!alive,"PARTIAL_NATIVE_OWNER_LEAK: "+id);
            Require(input.Width==8,"失败清理释放了输入");
        }
        finally{result.Resources?.Dispose();gray?.Dispose();}
    }
    private static void RejectedOwnedResult(bool async,bool expired,bool failedCleanup)
    {
        using var cancellation=new CancellationTokenSource();var owner=new ToolExecutionOwner();OwnedResults? produced=null;
        OwnedResults Body()
        {
            produced=new OwnedResults{Status=ExecutionStatus.Success,ThrowOnOwnerRelease=failedCleanup};
            if(expired)owner.RetireNode("resource-flow/owned");else cancellation.Cancel();
            return produced;
        }
        IToolPlugin tool=async?new AsyncOwnedTool(Body):new OwnedTool(Body);
        var request=new ToolExecutionRequest(new ProbeParameters()){ExecutionOwner=owner,WorkflowId="resource-flow",NodeId="owned",CancellationToken=cancellation.Token};
        var executor=new ToolExecutor(new ParameterResolver());var returned=async?executor.ExecuteAsync(tool,request).GetAwaiter().GetResult():executor.Run(tool,request);
        try
        {
            Require(produced is not null,"没有创建带native owner的结果");
            Console.WriteLine($"迟到结果 ownerDispose={produced!.OwnerReleaseCount} legacyDispose={produced.LegacyDisposeCount} returned={returned.Status}");
            Require(produced.OwnerReleaseCount==1&&produced.LegacyDisposeCount==1,"LATE_RETURN_OWNER_LEAK: 丢弃结果未同时释放Resources与IDisposable。");
            Disposed(()=>produced.Image.Width.ToString());
            Require(returned.Status==(failedCleanup?ExecutionStatus.Failed:ExecutionStatus.Cancelled),"清理真实失败被吞作取消或取消被误报失败");
            if(failedCleanup)Require(returned.ErrorMessage?.Contains("OWNED_RELEASE_FAILURE")==true,"清理真实失败消息丢失");
            Require(owner.ActiveRuns==0,"执行owner未排空");
        }
        finally{if(produced is not null){produced.ThrowOnOwnerRelease=false;produced.Resources?.Dispose();}returned.Resources?.Dispose();}
    }
    public sealed class ProbeParameters:ToolParameters { }
    // This is an ownership-boundary proxy, not a substitute algorithm/native execution.
    private sealed class CountingImage(string key,bool throwOnDispose=false):IVisionImage
    {
        private readonly HalImage _native=HalImage.CreateFromBytes(new byte[]{255},1,1,1);
        public int DisposeCount;public bool Released;public Exception Failure{get;}=new InvalidOperationException("COUNTED_DISPOSE_"+key,new IOException("COUNTED_INNER_"+key));
        public int Width=>_native.Width;public int Height=>_native.Height;public int Channels=>_native.Channels;public bool IsEmpty=>_native.IsEmpty;
        public byte[] GetPixelData()=>_native.GetPixelData();public IVisionImage Clone()=>throw new NotSupportedException("Ownership probe never clones.");
        public void Dispose(){DisposeCount++;Released=true;_native.Dispose();if(throwOnDispose)throw Failure;}
        // Deliberately value-equal instances: ownership must deduplicate by reference identity.
        public override bool Equals(object? other)=>other is CountingImage;public override int GetHashCode()=>1;
    }
    private static void CountedOutputOwner(string id,bool throwOnDispose)
    {
        var tool=ToolRegistry.CreateToolInstance(id)!;var result=(ToolResults)Activator.CreateInstance(tool.ResultType)!;
        var slots=new[]{"OutputImage","GrayImage","BinaryMask"}.Select(name=>result.GetType().GetProperty(name)).Where(p=>p is not null).Cast<PropertyInfo>().ToArray();
        using var callerInput=HalImage.CreateFromBytes(new byte[]{128},1,1,1);var values=new List<CountingImage>();
        if(!throwOnDispose){var same=new CountingImage(id+"shared");values.Add(same);foreach(var slot in slots)slot.SetValue(result,same);}
        else for(var i=0;i<slots.Length;i++){var value=new CountingImage(id+i,i<slots.Length-1);values.Add(value);slots[i].SetValue(result,value);}
        var first=result.Resources!.Retain();var last=result.Resources.Retain();result.Resources.Dispose();Require(values.All(v=>v.DisposeCount==0),"owner释放提前清理仍有租约输出");first.Dispose();Require(values.All(v=>v.DisposeCount==0),"倒数第二lease提前清理");
        AggregateException? failure=null;try{last.Dispose();}catch(AggregateException error){failure=error;}
        Require(values.All(v=>v.DisposeCount==1&&v.Released),"同引用多次Dispose或不同引用误合并/后图未清理");
        foreach(var value in values)Disposed(()=>value.Width.ToString());Require(callerInput.GetPixelData().Single()==128,"输出owner释放了未拥有的输入");
        if(throwOnDispose)Require(failure is not null&&failure.Flatten().InnerExceptions.Count==slots.Length-1&&values.Take(slots.Length-1).All(value=>failure.Flatten().InnerExceptions.Any(inner=>ReferenceEquals(inner,value.Failure)&&inner is InvalidOperationException&&inner.InnerException is IOException)),"多图真实清理异常对象/类型/inner聚合丢失");else Require(failure is null,"正常去重释放意外抛错");
        result.Resources.Dispose();first.Dispose();last.Dispose();Require(values.All(v=>v.DisposeCount==1),"重复Dispose重新清理资源");
    }
    private static void DualCleanupFailure(bool async,bool expired)
    {
        using var cancellation=new CancellationTokenSource();var owner=new ToolExecutionOwner();OwnedResults? produced=null;
        OwnedResults Body(){produced=new OwnedResults{Status=ExecutionStatus.Success,ThrowOnOwnerRelease=true,ThrowOnLegacyRelease=true};if(expired)owner.RetireNode("dual-flow/owned");else cancellation.Cancel();return produced;}
        IToolPlugin tool=async?new AsyncOwnedTool(Body):new OwnedTool(Body);var request=new ToolExecutionRequest(new ProbeParameters()){ExecutionOwner=owner,WorkflowId="dual-flow",NodeId="owned",CancellationToken=cancellation.Token};var executor=new ToolExecutor(new ParameterResolver());var returned=async?executor.ExecuteAsync(tool,request).GetAwaiter().GetResult():executor.Run(tool,request);
        try{Require(produced is not null&&produced.OwnerReleaseCount==1&&produced.LegacyDisposeCount==1,"owner异常跳过legacy或重复清理");Disposed(()=>produced!.Image.Width.ToString());Require(returned.Status==ExecutionStatus.Failed&&returned.ErrorMessage?.Contains("OWNED_RELEASE_FAILURE")==true&&returned.ErrorMessage.Contains("LEGACY_RELEASE_FAILURE"),"双清理异常被吞/降级取消");Require(owner.ActiveRuns==0,"异常后执行owner未排空");var diagnostics=returned.ErrorMessage+"\n"+returned.ErrorStackTrace;File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,$"dual-cleanup-async{async}-expired{expired}.txt"),diagnostics);Require(new[]{"System.InvalidOperationException","System.IO.IOException","OWNER_INNER_DETAILS","System.ArgumentException","System.FormatException","LEGACY_INNER_DETAILS"}.All(diagnostics.Contains),"清理Failed保留顶层消息但丢失异常类型/完整inner链");}
        finally{if(produced is not null){produced.ThrowOnOwnerRelease=false;produced.ThrowOnLegacyRelease=false;produced.Resources?.Dispose();}returned.Resources?.Dispose();}
    }
    public sealed class OwnedResults:ToolResults,IDisposable
    {
        public HalImage Image{get;}=HalImage.CreateFromBytes(new byte[]{255},1,1,1);
        public bool ThrowOnLegacyRelease;public bool ThrowOnOwnerRelease;public int OwnerReleaseCount;public int LegacyDisposeCount;
        public OwnedResults()=>Resources=new ResultResourceOwner(()=>{OwnerReleaseCount++;Image.Dispose();if(ThrowOnOwnerRelease)throw new InvalidOperationException("OWNED_RELEASE_FAILURE",new IOException("OWNER_INNER_DETAILS"));});
        public void Dispose(){LegacyDisposeCount++;if(ThrowOnLegacyRelease)throw new ArgumentException("LEGACY_RELEASE_FAILURE",new FormatException("LEGACY_INNER_DETAILS"));}
    }
    private class OwnedTool(Func<OwnedResults> body):IToolPlugin<ProbeParameters,OwnedResults>{public OwnedResults Run(ProbeParameters parameters)=>body();}
    private sealed class AsyncOwnedTool(Func<OwnedResults> body):OwnedTool(body),IAsyncToolPlugin{public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request){await Task.Yield();return Run((ProbeParameters)request.Parameters);}}
    private static void Source(WorkflowNodeBase target,string node,string property)=>target.Parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(target.Parameters,new ImageSourceReference(node,property,node,property));
    private static void Set(object p,string key,object value){var property=p.GetType().GetProperty(key)!;property.SetValue(p,property.PropertyType.IsEnum?Enum.Parse(property.PropertyType,(string)value):value);}
    private static void Disposed(Action action){try{action();}catch(ObjectDisposedException){return;}throw new Exception("已释放资源仍可访问。");}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Check(string name,Action action){try{action();Outcomes.Add(new{name,passed=true});Console.WriteLine("通过："+name);}catch(Exception error){Outcomes.Add(new{name,passed=false,error=error.ToString()});Console.WriteLine("失败："+name+" "+error.Message);}}
    private sealed class CheckLogger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message);}}
}
