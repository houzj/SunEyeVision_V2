using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        try
        {
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            Require(PluginLoader.LoadPlugins(Path.Combine(product,"plugins")),"正式插件加载失败");
            var manager=new PluginManager();
            var engine=new WorkflowEngine(new CheckLogger());
            var flow=engine.CreateWorkflow("runtime-scheduled","Runtime调度");
            var initial=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings
                {Threshold=73,IsAreaFilterEnabled=false,OutputGrayImage=false,OutputBinaryImage=false,OutputBoundary=false});
            var first=new WorkflowNodeBase("first","first","第一个",BlobTool.Id){RuntimeConfiguration=initial};
            var second=new WorkflowNodeBase("second","second","第二个",BlobTool.Id){RuntimeConfiguration=initial.Copy()};
            Bind(second,BlobTool.ImageInput,first.Id,BlobSchema.Field(5),TypeRef.ResourceOf(ResourceKind.Image),flow.Id);
            Bind(second,BlobTool.InspectionInput,first.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),flow.Id);
            flow.Nodes.Add(first);flow.Nodes.Add(second);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            var pixels=new byte[64];for(var y=1;y<5;y++)for(var x=1;x<5;x++)pixels[y*8+x]=100;
            using var image=HalImage.CreateFromBytes(pixels,8,8,1);
            ExecutionResult Run(Workflow current,CancellationToken token=default,IReadOnlyDictionary<string,object?>? overrides=null)
                =>WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(current.Id,image,
                    nodeParameterOverrides:overrides is null?null:new Dictionary<string,IReadOnlyDictionary<string,object?>>{{first.Id,overrides}},sourceInputs:SourceInputs(current)),token));
            Case("实际调度器运行两个中立Blob，语义引用生成依赖与隐藏灰图输出需求，ROI/图像保持同一物理帧",()=>
            {
                using var run=Run(flow);Require(run.Success,string.Join(";",run.Errors));
                var a=Result(run,first.Id);var b=Result(run,second.Id);
                Require(a.Output is BlobOutput&&b.Output is BlobOutput,"调度器使用了SDK Blob结果而非实际Runtime模型");
                var left=(BlobOutput)a.Output!;var right=(BlobOutput)b.Output!;
                Require(left.Count==1&&right.TotalArea==16&&left.GrayImage is not null&&right.GrayImage is null,
                    "新配置/ROI/输出需求未进入实际算法");
                using var region=right.Region.AcquireReader();Require(region.SourceFrame==left.GrayImage.Frame,"资源被重标记为另一个物理帧");
                Require(a.DeclaredOutput is not null&&b.DeclaredOutput is not null&&run.DeclaredOutputs.Count==2,"中立输出未发布进实际运行快照");
            });
            Case("运行开始后修改设计设置仅影响下一次运行，覆盖参数也只修改本次快照",()=>
            {
                EventHandler<NodeExecutionStatus> edit=(_,status)=>{if(status.NodeId==first.Id&&status.Status==NodeStatus.Running)first.RuntimeConfiguration=initial.WithOverrides(new Dictionary<string,object?>{{"Threshold",200}});};
                executor.NodeStatusChanged+=edit;
                try{using var frozen=Run(flow);Require(frozen.Success&&((BlobOutput)Result(frozen,second.Id).Output!).TotalArea==16,"设计编辑污染当前执行快照");}
                finally{executor.NodeStatusChanged-=edit;}
                using(var next=Run(flow))Require(next.Success&&((BlobOutput)Result(next,second.Id).Output!).Count==0,"下一次运行没有采用已修改设置");
                first.RuntimeConfiguration=initial;
                using(var changed=Run(flow,overrides:new Dictionary<string,object?>{{"Threshold",200}}))
                    Require(changed.Success&&((BlobOutput)Result(changed,second.Id).Output!).Count==0,"运行覆盖未生效");
                using(var normal=Run(flow))Require(normal.Success&&((BlobOutput)Result(normal,second.Id).Output!).TotalArea==16,"覆盖污染了正式设置");
            });
            Case("设置字段订阅进入本次唯一模型，运行期间的设计编辑不污染快照",()=>
            {
                var threshold=Setting("Threshold");
                second.RuntimeConfiguration=initial.WithFieldValues(new Dictionary<string,object?>{{threshold,200L}});
                Bind(second,threshold,first.Id,BlobSchema.Field(11),DataType.Integer,flow.Id);
                EventHandler<NodeExecutionStatus> edit=(_,status)=>{if(status.NodeId==first.Id&&status.Status==NodeStatus.Running)second.ParamSettings.RemoveSetting(threshold);};
                executor.NodeStatusChanged+=edit;
                try
                {
                    using var current=Run(flow);Require(current.Success&&((BlobOutput)Result(current,second.Id).Output!).TotalArea==16,
                        "订阅结果未替换本次阈值或设计修改污染已冻结绑定");
                }
                finally{executor.NodeStatusChanged-=edit;}
                try
                {
                    Require(((BlobSettings)second.RuntimeConfiguration.CaptureSettings()).Threshold==200,"订阅值回写设计模型");
                    using(var next=Run(flow))Require(next.Success&&((BlobOutput)Result(next,second.Id).Output!).Count==0,"下一次未采用已移除订阅后的正式值");
                    Bind(second,threshold,first.Id,BlobSchema.Field(12),DataType.Number,flow.Id);
                    second.ParamSettings.GetSetting(threshold)!.Conversion=BindingConversion.Float64ToInt64Checked;
                    using(var converted=Run(flow))Require(converted.Success&&((BlobOutput)Result(converted,second.Id).Output!).TotalArea==16,"显式数值转换未进入设置模型");
                    var document=new Solution{Name="设置字段订阅"};document.Workflows.Add(flow);
                    var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"runtime-setting-binding.solution");
                    var repository=new SolutionRepository();Require(repository.Save(document,path),repository.LastError??"设置引用保存失败");
                    var restored=repository.LoadCandidate(path).Candidate?.Workflows.Single()??throw new Exception(repository.LastError);
                    Require(restored.Nodes[1].ParamSettings.GetSetting(threshold)?.Conversion==BindingConversion.Float64ToInt64Checked
                        &&((BlobSettings)restored.Nodes[1].RuntimeConfiguration!.CaptureSettings()).Threshold==200,"保存丢失设置引用或回写订阅值");
                    engine.RegisterWorkflow(restored);
                    try{using var replay=Run(restored);Require(replay.Success&&((BlobOutput)Result(replay,second.Id).Output!).TotalArea==16,"重开后设置订阅未执行");}
                    finally{engine.RegisterWorkflow(flow);}
                    using var forced=WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image,
                        nodeBindingOverrides:new Dictionary<string,IReadOnlyDictionary<string,object?>>{{second.Id,new Dictionary<string,object?>{{threshold,200L}}}})));
                    Require(forced.Success&&((BlobOutput)Result(forced,second.Id).Output!).Count==0
                        &&second.ParamSettings.GetSetting(threshold)!.BindingType==BindingType.Binding,"临时常量覆盖没有屏蔽本次引用或污染正式引用");
                }
                finally{second.RuntimeConfiguration=initial;second.ParamSettings.RemoveSetting(threshold);}
            });
            Case("非法设置订阅与重复常量存储拒绝消费并可恢复",()=>
            {
                var minimum=Setting("FilterCircularityMin");
                Bind(second,minimum,first.Id,BlobSchema.Field(12),DataType.Number,flow.Id);
                try
                {
                    using var invalid=Run(flow);Require(!invalid.Success&&invalid.NodeResults[first.Id].Success&&!invalid.NodeResults[second.Id].Success,"越界设置订阅仍进入算法");
                }
                finally{second.ParamSettings.RemoveSetting(minimum);}
                var mode=Setting("SegmentationMode");Bind(second,mode,first.Id,BlobSchema.Field(10),DataType.Text,flow.Id);
                try
                {
                    using var invalid=Run(flow);Require(!invalid.Success&&invalid.NodeResults[first.Id].Success&&!invalid.NodeResults[second.Id].Success,"常量专用字段接受订阅");
                }
                finally{second.ParamSettings.RemoveSetting(mode);}
                var threshold=Setting("Threshold");second.ParamSettings.SetSetting(ParamSetting.CreateConstant(threshold,100L));
                try
                {
                    using var invalid=Run(flow);Require(!invalid.Success&&invalid.NodeResults[first.Id].Success&&!invalid.NodeResults[second.Id].Success,"重复设置常量未拒绝");
                    var document=new Solution{Name="拒绝重复常量"};document.Workflows.Add(flow);
                    var repository=new SolutionRepository();Require(!repository.Save(document,Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"invalid-settings.solution")),"重复设置常量可持久化");
                }
                finally{second.ParamSettings.RemoveSetting(threshold);}
                using var normal=Run(flow);Require(normal.Success&&((BlobOutput)Result(normal,second.Id).Output!).TotalArea==16,"非法提案污染正式设置或下一次运行");
            });
            Case("未知输出引用只阻断消费节点，修正后同一工作流可继续执行",()=>
            {
                Bind(second,BlobTool.InspectionInput,first.Id,"missing-port",TypeRef.ResourceOf(ResourceKind.Region),flow.Id);
                using(var failed=Run(flow))Require(!failed.Success&&failed.NodeResults[first.Id].Success&&!failed.NodeResults[second.Id].Success,"非法绑定未在消费节点准入时拒绝");
                Bind(second,BlobTool.InspectionInput,first.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),flow.Id);
                using var recovery=Run(flow);Require(recovery.Success&&((BlobOutput)Result(recovery,second.Id).Output!).TotalArea==16,"失败后不能恢复");
            });
            Case("正式图像源Runtime可供Runtime消费，但另一物理帧的ROI明确拒绝",()=>
            {
                var mixed=engine.CreateWorkflow("runtime-mixed","混合来源帧");
                var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source.png");
                var bitmap=BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,pixels,8);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var target=File.Create(file))png.Save(target);
                var source=new WorkflowNodeBase("file","file","文件","ImageLoad"){RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad")};
                var sourcePolicy=ImageSourcePolicy.Read(source)!;sourcePolicy.FilePaths=new(){file};sourcePolicy.CurrentIndex=0;sourcePolicy.SNInitialValue=1;sourcePolicy.ApplyTo(source);
                var original=new WorkflowNodeBase("original","original","工作流图像",BlobTool.Id){RuntimeConfiguration=initial};
                var consumer=new WorkflowNodeBase("consumer","consumer","混合ROI",BlobTool.Id){RuntimeConfiguration=initial};
                var imagePort=source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image");
                Bind(consumer,BlobTool.ImageInput,source.Id,imagePort.Id,imagePort.Type,mixed.Id);
                Bind(consumer,BlobTool.InspectionInput,original.Id,BlobSchema.RegionPort,TypeRef.ResourceOf(ResourceKind.Region),mixed.Id);
                mixed.Nodes.Add(source);mixed.Nodes.Add(original);mixed.Nodes.Add(consumer);
                using(var mismatch=Run(mixed))Require(!mismatch.Success&&mismatch.NodeResults[source.Id].Success&&mismatch.NodeResults[original.Id].Success&&!mismatch.NodeResults[consumer.Id].Success
                    &&mismatch.NodeResults[consumer.Id].ErrorMessages.Any(message=>message.Contains("source frame",StringComparison.OrdinalIgnoreCase)),
                    "另一物理帧的ROI被忽略/重标记，或真实图像源Runtime生产者未运行");
                consumer.ParamSettings.RemoveSetting(BlobTool.InspectionInput);
                using var valid=Run(mixed);Require(valid.Success&&((BlobOutput)Result(valid,consumer.Id).Output!).TotalArea==16,"图像源Runtime冻结图像不能进入Runtime输入");
            });
            Case("保存重开保留Runtime设置及两个稳定输入引用，不写SDK参数",()=>
            {
                var document=new Solution{Name="Runtime调度保存"};document.Workflows.Add(flow);
                var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"runtime-workflow.solution");
                var repository=new SolutionRepository();Require(repository.Save(document,path),repository.LastError??"保存失败");
                var json=JsonNode.Parse(File.ReadAllText(path))!;
                Require(json["Workflows"]![0]!["Nodes"]!.AsArray().All(n=>n!["Parameters"] is null),"新节点还写出SDK参数");
                var reopened=repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError);
                var restored=reopened.Workflows.Single();
                Require(restored.Nodes[1].ParamSettings.Settings.Count==2&&((BlobSettings)restored.Nodes[0].RuntimeConfiguration!.CaptureSettings()).Threshold==73,"重开覆盖用户设置或丢失绑定");
                engine.RegisterWorkflow(restored);
                try{using var replay=Run(restored);Require(replay.Success&&((BlobOutput)Result(replay,second.Id).Output!).TotalArea==16,"重开工作流不能实际执行");}
                finally{engine.RegisterWorkflow(flow);}
            });
            Case("已取得的结果读取租约独立于运行结果所有者，最后租约释放后关闭Runtime输出",()=>
            {
                var run=Run(flow);Require(run.Success,"租约用例执行失败");var result=Result(run,second.Id);var output=(BlobOutput)result.Output!;
                var reader=result.DeclaredOutput!.AcquireReader();run.Dispose();
                Require(reader.Values!.Get<SequenceValue>(BlobSchema.BlobsPort).Count==1,"运行结果释放过早销毁显示/绑定读者");
                using(var region=output.Region.AcquireReader())Require(region.RunCount>0,"租约未保留原生输出");
                reader.Dispose();
                try{output.ToValues();throw new Exception("最后租约释放后输出仍未关闭");}catch(ObjectDisposedException){}
            });
            Case("节点开始后取消可排空准入，下一次运行成功",()=>
            {
                using var cancellation=new CancellationTokenSource();
                EventHandler<NodeExecutionStatus> cancel=(_,status)=>{if(status.NodeId==first.Id&&status.Status==NodeStatus.Running)cancellation.Cancel();};
                executor.NodeStatusChanged+=cancel;
                try{using var run=Run(flow,cancellation.Token);Require(!run.Success&&run.Status==ExecutionStatus.Cancelled,"取消没有进入实际调度终态");}
                finally{executor.NodeStatusChanged-=cancel;}
                Require(flow.ExecutionOwner.ActiveRuns==0,"取消后Owner仍有运行租约");
                using var retry=Run(flow);Require(retry.Success,"取消后不能重跑");
            });
            Case("捕获结果失败与输出/资源清理同时失败时，全部清理并保留三个异常",()=>
            {
                var output=new BrokenOutput();var resources=new BrokenResources();var owner=new ToolExecutionOwner();
                var identity=owner.Begin("broken",Guid.NewGuid(),ToolContract.Version);
                try
                {
                    new RuntimeToolResults("broken",new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>()),identity,
                        new ToolOutcome(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),resources,0);
                    throw new Exception("预期捕获/清理失败");
                }
                catch(AggregateException errors)
                {Require(errors.Flatten().InnerExceptions.Count==3&&output.Closed&&resources.Closed,"清理漏执行或原始错误被覆盖");}
            });
            Console.WriteLine($"S3中立工作流：{_passed}组通过；工具箱/参数页默认入口及完整VM界面尚未完成。");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static IReadOnlyDictionary<string,ImageSourceRunInput> SourceInputs(Workflow flow)
        =>flow.Nodes.Where(node=>node.ToolType=="ImageLoad").ToDictionary(node=>node.Id,node=>
        {
            var policy=ImageSourcePolicy.Read(node)??throw new Exception("图像源没有正式Runtime配置。");
            var address=new SolutionResourceResolver(Environment.GetEnvironmentVariable("S2_CASE_ROOT")).ResolveAddress("local.paths",policy.FilePaths[policy.CurrentIndex]);
            return new ImageSourceRunInput(address,policy.SNInitialValue);
        },StringComparer.Ordinal);
    private static RuntimeToolResults Result(ExecutionResult run,string id)=>run.NodeResults[id].Result as RuntimeToolResults??throw new Exception("没有中立宿主结果："+id);
    private static string Setting(string property)
    {
        var label=BlobParameterDefinition.Instance.Label(property);
        return ((IToolSettingsFields)new BlobTool()).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName==label).FieldId;
    }
    private static void Bind(WorkflowNodeBase consumer,string target,string source,string port,TypeRef type,string scope)
        =>consumer.ParamSettings.SetSetting(new ParamSetting{ParameterName=target,BindingType=BindingType.Binding,
            ReferenceExpression=BindingReferenceSerializer.Serialize(new OutputReference(source,port,type,scopeId:scope)),SourceNodeId=source,SourceProperty=port,SourceWorkflowId=scope});
    private static T WithoutUiPump<T>(Func<Task<T>> start)
    {
        var previous=SynchronizationContext.Current;var context=new NoPost();SynchronizationContext.SetSynchronizationContext(context);
        try{var task=start();Require(task.Wait(TimeSpan.FromSeconds(30)),"执行等待UI消息泵");Require(context.Posts==0,"执行回到UI上下文");return task.GetAwaiter().GetResult();}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }
    private sealed class NoPost:SynchronizationContext
    {public int Posts;public override void Post(SendOrPostCallback callback,object? state){Interlocked.Increment(ref Posts);throw new Exception("UI callback is forbidden.");}}
    private sealed class BrokenOutput:IToolOutput,IDisposable
    {public bool Closed;public IReadOnlyDictionary<string,object?> ToValues()=>throw new InvalidOperationException("capture");public void Dispose(){Closed=true;throw new InvalidOperationException("output cleanup");}}
    private sealed class BrokenResources:IDisposable
    {public bool Closed;public void Dispose(){Closed=true;throw new InvalidOperationException("scope cleanup");}}
    private static void Case(string name,Action test){test();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All)
        {if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}
    }
}
