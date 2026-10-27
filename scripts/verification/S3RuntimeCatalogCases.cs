using System.IO;
using System.Text.Json.Nodes;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        try
        {
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(product,"plugins"));
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(m=>m.Id));PluginLoader.MarkAsLoaded();
            var definition=ToolRegistry.CreateRuntime(BlobTool.Id);
            var metadata=ToolRegistry.GetToolMetadata(BlobTool.Id)??throw new Exception("Runtime没有工具目录");
            var rootPresent=File.Exists(Path.Combine(product,"plugins/SunEyeVision.Tool.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.dll"));
            Case("Runtime拥有工具目录，名称图标分类和排序不依赖SDK工具特性",()=>
            {
                var catalog=((IToolCatalogProvider)definition).Catalog;
                Require(metadata.ToolType==definition.GetType()&&metadata.RuntimeCatalog==catalog
                    &&metadata.DisplayName=="Blob分析"&&metadata.Icon=="blob"&&metadata.Category=="定位"&&metadata.Order==10
                    &&ToolRegistry.ToolExists(BlobTool.Id)&&ToolRegistry.GetToolCount()==22,"目录未由Runtime提供");
                if(!rootPresent)Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="SunEyeVision.Tool.BlobAnalysis")
                    &&ToolRegistry.CreateToolInstance(BlobTool.Id) is null,"移除SDK入口后仍加载/创建旧Blob");
            });
            var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("runtime-catalog","目录默认创建");
            var node=WorkflowNodeFactory.CreateAlgorithmNode(BlobTool.Id,"blob","blob","Blob")??throw new Exception("目录不能创建节点");
            flow.Nodes.Add(node);
            using var image=HalImage.CreateFromBytes(Enumerable.Repeat((byte)200,64).ToArray(),8,8,1);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            ExecutionResult Run(Workflow value)=>WithoutUiPump(()=>executor.ExecuteWorkflowAsync(new WorkflowRunRequest(value.Id,image)));
            Case("公共节点工厂默认创建真实Runtime模型并经过调度器运行HALCON",()=>
            {
                Require(node.RuntimeConfiguration?.SettingsType==typeof(BlobSettings)
                    &&node.Parameters.GetType().FullName!="SunEyeVision.Tool.BlobAnalysis.BlobAnalysisParameters","节点仍默认SDK Blob参数");
                using var run=Run(flow);
                Require(run.Success&&run.NodeResults[node.Id].Result is RuntimeToolResults {Output:BlobOutput {Count:1,TotalArea:64}},"默认创建未进入Runtime执行");
            });
            Case("Runtime目录节点修改强类型值后保存重开仍直接执行",()=>
            {
                var field=((IToolSettingsFields)definition).SettingsSchema.Presentation!.Fields.Single(f=>f.DisplayName=="阈值").FieldId;
                node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{field,201L}});
                var solution=new Solution{Name="Runtime目录保存"};solution.Workflows.Add(flow);
                var file=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"runtime-catalog.solution");var repository=new SolutionRepository();
                Require(repository.Save(solution,file),repository.LastError??"保存失败");
                Require(JsonNode.Parse(File.ReadAllText(file))!["Workflows"]![0]!["Nodes"]![0]!["Parameters"] is null,"保存写入SDK参数");
                var restored=repository.LoadCandidate(file).Candidate?.Workflows.Single()??throw new Exception(repository.LastError);
                Require(((BlobSettings)restored.Nodes.Single().RuntimeConfiguration!.CaptureSettings()).Threshold==201,"重开覆盖用户值");
                engine.RegisterWorkflow(restored);
                try{using var run=Run(restored);Require(run.Success&&run.NodeResults[node.Id].Result is RuntimeToolResults {Output:BlobOutput {Count:0}},"重开未按201阈值执行");}
                finally{engine.RegisterWorkflow(flow);}
            });
            Case("目录重复登记幂等，冲突组件拒绝且不污染已发布目录",()=>
            {
                var components=ToolRegistry.GetComponentMetadata();
                var runtime=components.Single(c=>c.ToolId==BlobTool.Id&&c.Role==ToolComponentRole.Runtime);
                var conflict=new ToolComponentMetadata(runtime.ComponentId+"-conflict",runtime.ToolId,ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor);
                try{ToolRegistry.PrepareRegistration(Array.Empty<Type>(),new[]{new ToolComponentRegistration(conflict,definition.GetType())});throw new Exception("冲突未拒绝");}
                catch(InvalidOperationException){}
                Require(ReferenceEquals(metadata,ToolRegistry.GetToolMetadata(BlobTool.Id))&&ToolRegistry.GetToolCount()==22,"失败提案污染目录");
                var registrations=new List<ToolComponentRegistration>{new(runtime,definition.GetType())};
                var repeat=ToolRegistry.PrepareRegistration(Array.Empty<Type>(),registrations);
                using var commit=repeat.EnterCommit();commit.Publish();
                Require(ReferenceEquals(metadata,ToolRegistry.GetToolMetadata(BlobTool.Id))&&ToolRegistry.GetToolCount()==22,"重复登记改变目录");
            });
            Console.WriteLine($"S3 Runtime目录：{_passed}组通过；SDK入口={(rootPresent?"存在":"缺席")}；不代表默认UI窗口、全部VM控件或人工验收完成。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static T WithoutUiPump<T>(Func<Task<T>> start)
    {
        var previous=SynchronizationContext.Current;var blocked=new NoPost();SynchronizationContext.SetSynchronizationContext(blocked);
        try{var task=start();Require(task.Wait(TimeSpan.FromSeconds(30)),"运行等待UI消息泵");Require(blocked.Posts==0,"运行反调UI");return task.GetAwaiter().GetResult();}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }
    private sealed class NoPost:SynchronizationContext
    {public int Posts;public override void Post(SendOrPostCallback callback,object? state){Interlocked.Increment(ref Posts);throw new Exception("UI callback");}}
    private sealed class CheckLogger:ILogger
    {
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){}
        public ILogger ForSource(string source)=>this;
    }
    private static void Case(string name,Action run){run();_passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
