using System.IO;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using SunEyeVision.Workflow;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        var prior=VisionImageFactoryLocator.Current;
        try{return Run();}catch(Exception error){Console.Error.WriteLine(error);return 1;}finally{VisionImageFactoryLocator.Current=prior;}
    }
    private static int Run()
    {
        Require(PluginLoader.LoadPlugins(),"正式插件加载失败");
        var manager=new PluginManager();var engine=new WorkflowEngine(new Logger());var executor=new WorkflowExecutionEngine(engine,manager,new Logger());
        var flow=engine.CreateWorkflow("runtime-sdk-image","Runtime图像消费");
        var configuration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings{Threshold=73,IsAreaFilterEnabled=false,OutputGrayImage=false});
        var first=new WorkflowNodeBase("producer","producer","Runtime源",BlobTool.Id){RuntimeConfiguration=configuration};
        var parameters=ToolRegistry.CreateParameters("ImageThreshold");parameters.GetType().GetProperty("Threshold")!.SetValue(parameters,50);
        parameters.GetType().GetProperty("ImageSourceRef")!.SetValue(parameters,new ImageSourceReference(first.Id,BlobSchema.Field(5),"Runtime源","灰图"){SourceWorkflowId=flow.Id});
        var sdk=new WorkflowNodeBase("sdk","sdk","现行二值化","ImageThreshold"){Parameters=parameters};
        var gray=new OutputBindingChangeService().ReadContract(sdk)!.Single(p=>p.Name=="GrayImage");var grayPort=BindingClrAdapter.DescribeOutput(gray);
        var last=new WorkflowNodeBase("consumer","consumer","Runtime再消费",BlobTool.Id){RuntimeConfiguration=configuration.Copy()};
        Bind(last,BlobTool.ImageInput,sdk.Id,grayPort.Id,grayPort.Type,flow.Id);
        Bind(last,BlobTool.InspectionInput,first.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),flow.Id);
        flow.Nodes.Add(first);flow.Nodes.Add(sdk);flow.Nodes.Add(last);
        var bytes=new byte[32*32];for(var y=1;y<5;y++)for(var x=1;x<5;x++)bytes[y*32+x]=100;
        for(var y=20;y<25;y++)for(var x=20;x<25;x++)bytes[y*32+x]=200;
        using var full=HalImage.CreateFromBytes(bytes,32,32,1);using var domain=new HRegion();domain.GenRectangle1(0d,0d,9d,9d);
        using var nativeReduced=full.Unwrap().ReduceDomain(domain);using var input=new HalImage(nativeReduced.Clone());
        var factory=new CopyFactory();VisionImageFactoryLocator.Current=factory;
        Case("稳定图像端口按精确声明准入，拒绝伪造路径、集合目标和数值转换",()=>
        {
            var declared=RuntimeToolResults.Describe(new BlobTool().ResultSchema);
            var allowed=new[]{typeof(IVisionImage)};
            Require(BindingContractValidator.Validate(declared,BlobSchema.Field(5),allowed).State==BindingCompatibility.Conditional,"可空图像端口未保留条件准入");
            Require(BindingContractValidator.Validate(declared,BlobSchema.Field(5)+".Unknown",allowed).State==BindingCompatibility.Incompatible,"伪造图像路径被准入");
            Require(BindingContractValidator.Validate(declared,BlobSchema.Field(5),new[]{typeof(IVisionImage[])}).State==BindingCompatibility.Incompatible,"单图像隐式变为集合");
            Require(BindingContractValidator.Validate(declared,BlobSchema.Field(5),allowed,BindingConversion.Float64ToInt64Checked).State==BindingCompatibility.Incompatible,"图像接受了数值转换");
        });
        ExecutionResult Run(Workflow current)=>WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(current.Id,input)));
        var execution=Run(flow);RuntimeToolResults Source()=>execution.NodeResults[first.Id].Result as RuntimeToolResults??throw new Exception("没有规范Runtime源结果");
        Case("Runtime图像端口拒绝伪造GUID且正常Port读取保留原资源",()=>
        {
            bool rejected=false;
            try{OutputVariableResolver.Resolve(Source(),BlobSchema.Field(5),first.Id,Guid.NewGuid());}
            catch(InvalidOperationException){rejected=true;}
            Require(rejected,"Runtime图像路径绕过了字段身份校验");
            Require(OutputVariableResolver.Resolve(Source(),BlobSchema.Field(5),first.Id) is IVisionImage,"正式图像Port读取失效");
        });
        Case("实际Runtime→现行SDK二值化→Runtime且复用源Region，保留原始帧及实际Domain，无UI回调",()=>
        {
            Require(execution.Success,string.Join(";",execution.Errors));
            var source=(BlobOutput)Source().Output!;var target=(BlobOutput)((RuntimeToolResults)execution.NodeResults[last.Id].Result!).Output!;
            Require(source.TotalArea==16&&target.TotalArea==16,"丢失输入Domain或下游Region");
            using var published=execution.NodeResults[sdk.Id].Result!.DeclaredOutput!.AcquireReader();var sdkImage=published.Values!.Get<IReadOnlyImage>(grayPort.Id);
            using var sdkDomain=sdkImage.EffectiveDomain.AcquireReader();using var sourceRegion=source.Region.AcquireReader();
            Require(sdkImage.Frame==source.GrayImage!.Frame&&sdkImage.Frame==sourceRegion.SourceFrame&&Area(sdkDomain)==100,"复制或保坐标算法重标记了帧/丢失Domain");
        });
        Case("SDK和主显示读取同一按需副本，规范结果对象及发布Owner保持单一",()=>
        {
            var source=Source();var owner=source.Resources;var copies=factory.Copies;var native=source.GetImageView(BlobSchema.Field(5))!;
            Require(ReferenceEquals(native,source.GetImageView(BlobSchema.Field(5)))&&ReferenceEquals(native,source.GetDisplayImages()[BlobSchema.Field(5)])&&factory.Copies==copies&&ReferenceEquals(owner,source.Resources),"重复复制或替换了发布资源Owner");
            Require(source.Values[BlobSchema.Field(5)] is IReadOnlyImage&&ImageFrameProvenance.CaptureAddress(native)==((IReadOnlyImage)source.Values[BlobSchema.Field(5)]!).Frame,"规范值被SDK对象覆盖或原始帧丢失");
        });
        Case("发布读者保留已缓存副本，最后读者退出后释放，输入原图不受影响",()=>
        {
            var source=Source();var reader=source.DeclaredOutput!.AcquireReader();var native=source.GetImageView(BlobSchema.Field(5))!;var second=source.DeclaredOutput.AcquireReader();
            execution.Dispose();reader.Dispose();Require(native.GetPixelData().Length==1024,"结果所有者释放后读者失去副本");second.Dispose();
            RejectDisposed(()=>native.GetPixelData());Require(input.GetPixelData().Length==1024,"释放借用结果损坏原输入");
        });
        var solo=engine.CreateWorkflow("headless-image","不消费SDK视图");var headless=new WorkflowNodeBase("headless","headless","无视图",BlobTool.Id){RuntimeConfiguration=configuration.WithOverrides(new Dictionary<string,object?>{{"OutputGrayImage",true}})};solo.Nodes.Add(headless);
        VisionImageFactoryLocator.Current=null;using var pure=Run(solo);var pureResult=(RuntimeToolResults)pure.NodeResults[headless.Id].Result!;
        Case("无SDK图像工厂仍可执行纯Runtime，SDK读取明确拒绝且不会伪造默认图像",()=>
        {
            Require(pure.Success&&pureResult.Values[BlobSchema.Field(5)] is IReadOnlyImage,"无视图宿主丢失规范输出");
            Reject(()=>pureResult.GetImageView(BlobSchema.Field(5)));Require(pureResult.GetDisplayImages().Count==0,"无视图能力时生成假显示图像");
        });
        using var beforeCopy=pureResult.DeclaredOutput!.AcquireReader();
        Case("副本工厂失败和验证/释放同时失败保留错误，随后可恢复，不污染规范结果",()=>
        {
            VisionImageFactoryLocator.Current=new BrokenFactory(false);Reject(()=>pureResult.GetImageView(BlobSchema.Field(5)));
            VisionImageFactoryLocator.Current=new BrokenFactory(true);
            try{pureResult.GetImageView(BlobSchema.Field(5));throw new Exception("预期副本验证失败");}catch(AggregateException errors){Require(errors.Flatten().InnerExceptions.Count==2,"副本释放覆盖原始验证错误");}
            VisionImageFactoryLocator.Current=factory;var copied=pureResult.GetImageView(BlobSchema.Field(5))!;
            using var captured=((ITypedImageSnapshotSource)copied).CaptureSnapshot(((IReadOnlyImage)pureResult.Values[BlobSchema.Field(5)]!).Frame);
            using var actual=captured.AcquirePixels();using var reader=actual.AcquireDomainReader();Require(Area(reader)==100&&captured.CopyPixels().SequenceEqual(((IReadOnlyImage)pureResult.Values[BlobSchema.Field(5)]!).CopyPixels()),"恢复后的像素或Domain改变");
        });
        Case("先取得的发布读者保留后来生成的副本，未更换资源Owner",()=>
        {
            var native=pureResult.GetImageView(BlobSchema.Field(5))!;pure.Dispose();
            Require(native.GetPixelData().Length==1024,"先取得的读者没有保留后来生成的副本");
            beforeCopy.Dispose();RejectDisposed(()=>native.GetPixelData());
        });
        execution.Dispose();Console.WriteLine($"S3 Runtime图像消费：{_passed}组通过；当前SDK/规范值/Domain/帧/缓存和资源，不代表全部工具或UI验收。");return 0;
    }
    private static long Area(IRegionReadLease region){long count=0;for(var offset=0;offset<region.RunCount;offset+=4096)foreach(var run in region.ReadRuns(offset,4096).Span)count+=run.LastColumnInclusive-run.FirstColumn+1;return count;}
    private static void Bind(WorkflowNodeBase node,string target,string source,string output,TypeRef type,string scope)=>node.ParamSettings.SetSetting(new ParamSetting{ParameterName=target,BindingType=BindingType.Binding,ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(source,output,type,scopeId:scope)),SourceNodeId=source,SourceProperty=output,SourceWorkflowId=scope});
    private static T WithoutUiPump<T>(Func<Task<T>> start){var prior=SynchronizationContext.Current;var context=new NoPost();SynchronizationContext.SetSynchronizationContext(context);try{var task=start();Require(Task.WhenAny(task,Task.Delay(30000)).GetAwaiter().GetResult()==task,"执行等待UI");var result=task.GetAwaiter().GetResult();Require(context.Posts==0,"执行反调UI");return result;}finally{SynchronizationContext.SetSynchronizationContext(prior);}}
    private static void Reject(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("预期明确拒绝");}
    private static void RejectDisposed(Action action){try{action();}catch(ObjectDisposedException){return;}throw new Exception("最后读者退出后资源仍可用");}
    private static void Case(string name,Action action){action();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class NoPost:SynchronizationContext{public int Posts;public override void Post(SendOrPostCallback callback,object? state){Posts++;throw new Exception("禁止UI回调");}}
    private sealed class CopyFactory:IVisionImageFactory,IReadOnlyImageViewFactory
    {public int Copies;private readonly HalImageFactory _factory=new();public bool CanCreateFromFile(string path)=>true;public IVisionImage CreateFromFile(string path)=>_factory.CreateFromFile(path);public IVisionImage CopyImage(IReadOnlyImage image){Copies++;return _factory.CopyImage(image);}}
    private sealed class BrokenFactory(bool invalid):IVisionImageFactory,IReadOnlyImageViewFactory
    {public bool CanCreateFromFile(string path)=>false;public IVisionImage CreateFromFile(string path)=>throw new NotSupportedException();public IVisionImage CopyImage(IReadOnlyImage source)=>invalid?new BrokenImage():throw new InvalidOperationException("copy failure");}
    private sealed class BrokenImage:IVisionImage{public int Width=>1;public int Height=>1;public int Channels=>1;public bool IsEmpty=>true;public byte[] GetPixelData()=>throw new NotSupportedException();public IVisionImage Clone()=>throw new NotSupportedException();public void Dispose()=>throw new InvalidOperationException("copy disposal failure");}
    private sealed class Logger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}}
}
