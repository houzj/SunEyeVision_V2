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
    private static int _failed;
    private static readonly List<object> Reports=new();
    private static readonly IReadOnlyDictionary<string,string> Names=new Dictionary<string,string>
    {
        ["Threshold"]="LowThreshold",["UpperThreshold"]="HightThreshold",["BlobCountLimit"]="FindNum",
        ["FilterAreaMin"]="MinArea",["FilterAreaMax"]="MaxArea",
        ["FilterCircularityMin"]="MinCircularity",["FilterCircularityMax"]="MaxCircularity",
        ["FilterRectangularityMin"]="MinRectangularity",["FilterRectangularityMax"]="MaxRectangularity"
    };
    [STAThread] private static int Main()
    {
        try
        {
            Require(PluginLoader.LoadPlugins(),"正式插件加载失败");
            Case("真实九字段稳定名称、VM默认和面积整数存储",()=>
            {
                var c=RuntimeToolConfiguration.Create(BlobTool.Id);
                object[] expected={100L,150L,100L,10L,999999999L,0.1,1d,0.1,1d};var index=0;
                foreach(var name in Names.Keys)
                {
                    var id=Setting(name);Require(c.Fields.Read(id) is ConstantSetting constant&&Equals(constant.Value,expected[index++]),"九字段默认或规范值类型错误："+name);
                    if(name is "FilterAreaMin" or "FilterAreaMax")
                    {
                        var port=c.SettingsSchema.Inputs.Single(p=>p.Id==id);
                        Require(port.Type==TypeRef.ScalarOf(ScalarKind.Int64)&&port.Storage==NumericStorage.Int32,"面积字段没有声明Int32整数");
                    }
                }
            });
            Case("整数面积两端边界接受并经真实宿主保存重开",()=>
            {
                using var f=new Fixture(documentHistory:true);
                Commit(f,new(){[Setting("FilterAreaMin")]=1L,[Setting("FilterAreaMax")]=999999999L});
                var reopened=f.SaveReopen("integer-boundary");var c=reopened.Workflows[0].Nodes[0].RuntimeConfiguration!;
                Require(c.Fields.Read(Setting("FilterAreaMin")) is ConstantSetting{Value:1L}&&c.Fields.Read(Setting("FilterAreaMax")) is ConstantSetting{Value:999999999L},"整数边界保存改变");
            });
            Case("整数面积越界小数与倒置提案拒绝且正式九字段不半写",()=>
            {
                using var f=new Fixture(areaFilter:true);var before=f.Node.RuntimeConfiguration;var version=f.Host.Capture().Version;
                foreach(var bad in new object[]{0L,1000000000L,1.5})
                    Reject(()=>Commit(f,new(){[Setting("UpperThreshold")]=149L,[Setting("FilterAreaMin")]=bad}),"绑定阈值草稿开启面积过滤后错误接纳MinArea="+bad);
                Reject(()=>Commit(f,new(){[Setting("FilterAreaMin")]=100L,[Setting("FilterAreaMax")]=99L}));
                Require(ReferenceEquals(before,f.Node.RuntimeConfiguration)&&f.Host.Capture().Version==version,"失败提案发生正式半写");
            });
            Case("阈值仍绑定且面积筛选关闭时已知面积越界拒绝且无半写",()=>
            {
                using var f=new Fixture();var before=f.Node.RuntimeConfiguration;var version=f.Host.Capture().Version;
                foreach(var bad in new object[]{0L,1000000000L})
                    Reject(()=>Commit(f,new(){[Setting("UpperThreshold")]=149L,[Setting("FilterAreaMin")]=bad}),"筛选关闭且阈值仍绑定时错误接纳MinArea="+bad);
                Require(ReferenceEquals(before,f.Node.RuntimeConfiguration)&&f.Host.Capture().Version==version,"禁用字段越界提案发生正式半写");
            });
            Case("九字段显式非空变量数组索引确认与保存重开",()=>
            {
                using var f=new Fixture(documentHistory:true);var variables=f.Host.BeginVariableEdit();
                var integers=new GlobalVariable("九字段整数",new long[]{110,150,3,1,999999999},"Integer[]");
                var numbers=new GlobalVariable("九字段小数",new double[]{0.2,0.9,0.3,0.8},"Double[]");
                variables.Configuration.GlobalVariables.Add(integers);variables.Configuration.GlobalVariables.Add(numbers);
                var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);var changes=new Dictionary<string,ParameterSetting>();var i=0;
                foreach(var name in Names.Keys)
                {
                    var variable=i<5?integers:numbers;var at=i<5?i:i-5;
                    changes[Setting(name)]=new VariableSetting(f.Reference(variable,new BindingSelection.AtConstant(at)));i++;
                }
                draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(changes);
                f.Host.CommitBatch(new[]{draft},variables);
                using(var run=f.Run())Require(run.Success,"九变量索引正式执行失败");
                i=0;foreach(var name in Names.Keys){object expected=i<5?(object)new long[]{110,150,3,1,999999999}[i]:new double[]{0.2,0.9,0.3,0.8}[i-5];Require(Equals(f.Node.RuntimeConfiguration!.ReadCurrentParameter(Setting(name))!.Value,expected),"九字段消费错误："+name);i++;}
                var reopened=f.SaveReopen("nine-bindings");Require(reopened.Workflows[0].Nodes[0].RuntimeConfiguration!.ParameterBindingOrder.Count==9,"九绑定保存重开丢失");
            });
            Case("无控制前序的真实Blob输出不能成为普通阈值来源",()=>
            {
                using var f=new Fixture();var source=new WorkflowNodeBase("source","source","源Blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id)};f.Flow.Nodes.Add(source);f.Host.SynchronizeStructure();
                var port=new BlobTool().Describe().Outputs.Single(p=>p.Id==BlobSchema.Field(11));
                var reference=new OutputReference(source.Id,port.Id,port.Type,null,null,f.Flow.Id,port.Storage);
                var edit=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{f.Threshold,new ReferenceSetting(reference)}});
                var before=f.Node.RuntimeConfiguration;Reject(()=>f.Host.Commit(edit));Require(ReferenceEquals(before,f.Node.RuntimeConfiguration),"无前序引用写入正式配置");
            });
            Case("真实Blob控制前序输出绑定经调度消费并保持稳定来源身份",()=>
            {
                using var f=new Fixture();var source=new WorkflowNodeBase("source","source","源Blob",BlobTool.Id)
                    {RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings{Threshold=200,IsAreaFilterEnabled=false,OutputGrayImage=false,OutputBinaryImage=false,OutputBoundary=false})};
                f.Flow.Nodes.Insert(0,source);
                var edge=new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=f.Node.Id};
                ExecutionConnectionPolicy.EnsureCanAdd(f.Flow.Connections,edge);f.Flow.Connections.Add(edge);f.Host.SynchronizeStructure();
                var port=new BlobTool().Describe().Outputs.Single(p=>p.Id==BlobSchema.Field(11));
                var reference=new OutputReference(source.Id,port.Id,port.Type,null,null,f.Flow.Id,port.Storage);
                var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{f.Threshold,new ReferenceSetting(reference)}});f.Host.Commit(draft);
                using(var run=f.Run())Require(run.Success&&f.Current==0,"真实前序Count没有作为阈值消费");
                source.Name="改名源Blob";
                var reopened=f.SaveReopen("module-source");var c=reopened.Workflows[0].Nodes.Single(n=>n.Id==f.Node.Id).RuntimeConfiguration!;
                Require(c.Fields.Read(f.Threshold) is ReferenceSetting saved&&saved.Reference.NodeId==source.Id&&saved.Reference.ScopeId==f.Flow.Id,"来源保存重开改变身份");
            });
            Case("私有调试回执取消隔离、确认接纳及当前值重开",()=>
            {
                using var f=new Fixture(documentHistory:true);var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);using var owner=new OwnerLease();using var invocation=f.Debug(draft,owner.Owner);
                using(var result=Wait(()=>invocation.ExecuteAsync()))Require(result.IsSuccess,"真实Blob私有调试失败");
                Require(f.Current==37&&invocation.AcceptedParameters?.ReadCurrentParameter(f.Threshold) is {Value:173L},"私有回执或取消隔离错误");
                draft.Node.RuntimeConfiguration=invocation.AcceptedParameters;f.Host.Commit(draft);Require(f.Current==173,"确认没有接纳私有值");
                Require(f.SaveReopen("accepted").Workflows[0].Nodes[0].RuntimeConfiguration!.ReadCurrentParameter(f.Threshold) is {Value:173L},"确认值重开丢失");
            });
            Case("后项索引失败仅保留私有已接纳前缀，正式九字段不变",()=>
            {
                using var f=new Fixture(extraFailure:true);var before=f.Node.RuntimeConfiguration;var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);using var owner=new OwnerLease();using var invocation=f.Debug(draft,owner.Owner);
                Reject(()=>Wait(()=>invocation.ExecuteAsync()));Require(invocation.AcceptedParameters?.ReadCurrentParameter(f.Threshold) is {Value:173L},"失败丢失已接纳前缀");
                Require(ReferenceEquals(before,f.Node.RuntimeConfiguration)&&f.Current==37,"私有失败发生正式半写");
            });
            Case("九参数配置编辑历史与运行值历史分离",()=>
            {
                using var f=new Fixture(documentHistory:true);Commit(f,new(){[Setting("FilterAreaMin")]=2L,[Setting("FilterAreaMax")]=900L});
                f.Host.Undo();Require(f.Node.RuntimeConfiguration!.Fields.Read(Setting("FilterAreaMin")) is ConstantSetting{Value:10L},"撤销没有恢复面积");
                f.Host.Redo();using(var run=f.Run())Require(run.Success,"重做后运行失败");f.Host.Undo();Require(f.Current==173,"撤销配置重放了运行值");
            });
            foreach(var mode in new[]{BlobSegmentationMode.None,BlobSegmentationMode.DoubleThreshold})
            Case("旧模式读取保留、保存确认及生产执行门禁："+mode,()=>
            {
                using var f=new Fixture();var before=f.Node.RuntimeConfiguration;
                    Reject(()=>
                    {
                        var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);
                        draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithSettings(new BlobSettings{SegmentationMode=mode});f.Host.Commit(draft);
                    });
                    f.ExecuteMode(mode);
                    // A materializable stored configuration may retain an unavailable mode; admission must reject it.
                    var (reopened,path)=f.LoadUnavailableMode(mode);
                    using var stored=new Fixture(reopened);
                    Require(((BlobSettings)stored.Node.RuntimeConfiguration!.CaptureSettings()).SegmentationMode==mode,"读取旧配置丢失暂不可用正式模式");
                    var bytes=File.ReadAllBytes(path);var repository=new SolutionRepository();
                    Require(!repository.Save(stored.Document,path)&&File.ReadAllBytes(path).SequenceEqual(bytes),"未就绪模式被重新保存或覆盖既有文件");
                    var edit=stored.Host.BeginEdit(stored.Flow.Id,stored.Node.Id);Reject(()=>stored.Host.Commit(edit),"旧未就绪模式被正式确认");
                    using(var run=stored.Run())Require(!run.Success,"工作流预检接纳暂不可用模式");
                    using var debugOwner=new OwnerLease();
                    Reject(()=>{using var invocation=stored.Debug(edit,debugOwner.Owner);},"私有调试预检接纳暂不可用模式");
                    Require(ReferenceEquals(before,f.Node.RuntimeConfiguration)&&f.Current==37,"模式拒绝污染正式九参数");
            });
            Case("窗口所属正式会话退役后旧草稿和私有回执不能确认",()=>
            {
                using var f=new Fixture(documentHistory:true);var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);using var owner=new OwnerLease();using var invocation=f.Debug(draft,owner.Owner);
                using(var result=Wait(()=>invocation.ExecuteAsync()))Require(result.IsSuccess,"退役前调试失败");
                f.Manager.CloseSolution(saveChanges:false);draft.Node.RuntimeConfiguration=invocation.AcceptedParameters;Reject(()=>f.Host.Commit(draft));Reject(()=>f.Host.BeginEdit(f.Flow.Id,f.Node.Id));
            });
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"blob-parameter-production-report.json"),JsonSerializer.Serialize(new{scope="真实Blob九参数非前台生产链；实际窗口、VS设计器、人工和VM算法另验",passed=_passed,failed=_failed,cases=Reports},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("Blob九参数非前台生产链："+_passed+"组通过，"+_failed+"组失败；不代表真实参数窗口、VS设计器、VM算法或B08整体验收。");return _failed==0?0:1;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Commit(Fixture f,Dictionary<string,object?> values)
    {
        var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldValues(values);Require(draft.Node.RuntimeConfiguration.Fields.Read(f.Threshold) is VariableSetting,"本用例必须保留真实阈值绑定");f.Host.Commit(draft);
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
        public Fixture(long source=173,bool extraFailure=false,bool ordered=false,bool documentHistory=false,bool areaFilter=false,BlobSegmentationMode mode=BlobSegmentationMode.SingleThreshold)
        {
            Flow=Engine.CreateWorkflow(Guid.NewGuid().ToString(),"参数当前值");Document.Workflows.Add(Flow);
            Variable=new GlobalVariable("阈值来源",source,"Integer");Document.GlobalVariables.Add(Variable);
            Alternate=new GlobalVariable("替换来源",37L,"Integer");Document.GlobalVariables.Add(Alternate);
            var configuration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings
                {Threshold=37,SegmentationMode=mode,IsAreaFilterEnabled=areaFilter,OutputGrayImage=false,OutputBinaryImage=false,OutputBoundary=false});
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
        public Fixture(Solution restored)
        {
            Document=restored;Flow=Document.Workflows.Single();Node=Flow.Nodes.Single();Engine.RegisterWorkflow(Flow);
            Variable=Document.GlobalVariables[0];Alternate=Document.GlobalVariables[1];
            _manager=new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Flow.Id+"-loaded-manager"));
            _manager.SetCurrentSolution(Document,registerInCatalog:false);Editing=_manager.Session!.Editing;Host=Editing.Host;
            _executor=new WorkflowExecutionEngine(Engine,new PluginManager(),new CheckLogger());_image=HalImage.CreateFromBytes(new byte[64],8,8,1);
        }
        public (Solution Document,string Path) LoadUnavailableMode(BlobSegmentationMode mode)
        {
            var path=System.IO.Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Flow.Id+"-old-mode-"+mode+".solution");
            var repository=new SolutionRepository();Require(repository.Save(Document,path),repository.LastError??"准备合法方案失败");
            // Explicit external persisted-input fixture. This is not a successful product Save of an unavailable mode.
            var root=JsonNode.Parse(File.ReadAllText(path))!;
            var persisted=Node.RuntimeConfiguration!.WithSettings(new BlobSettings{SegmentationMode=mode});
            root["Workflows"]![0]!["Nodes"]![0]!["RuntimeConfiguration"]=JsonSerializer.SerializeToNode(persisted);
            File.WriteAllText(path,root.ToJsonString());
            var loaded=repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError??"旧配置读取失败");
            return(loaded,path);
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
        public void ExecuteMode(BlobSegmentationMode mode)
        {
            using var resources=new FrozenOutputResources();using var input=_image.CaptureSnapshot(new(Guid.NewGuid(),"mode-admission"));
            var tool=new BlobTool();var session=tool.CreateSession();
            try
            {
                var outcome=session.ExecuteAsync(new(new BlobSettings{SegmentationMode=mode},
                    new InputValues(tool.Describe().Inputs,new Dictionary<string,object?>{{BlobTool.ImageInput,input}}),new ExecutionContext(resources))).GetAwaiter().GetResult();
                using var output=outcome.Output;
                Require(!outcome.Success&&outcome.Diagnostics.Any(d=>d.Code=="BLOB_MODE_NOT_READY"),"插件生产ExecuteAsync没有明确拒绝模式："+mode);
            }
            finally{session.DisposeAsync().GetAwaiter().GetResult();}
        }
        public Solution SaveReopen(string suffix)
        {
            var repository=new SolutionRepository();var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Flow.Id+"-"+suffix+".solution");
            Require(repository.Save(Document,path),repository.LastError??"保存失败");
            return repository.LoadCandidate(path).Candidate??throw new Exception(repository.LastError??"重开失败");
        }
        public void Dispose(){_manager?.CloseSolution(saveChanges:false);_image.Dispose();Document.ExecutionOwner.Retire();}
    }
    private sealed class ExecutionContext(IOutputResources resources):IToolExecutionContext
    {
        public RunIdentity Identity{get;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"blob-mode",Guid.NewGuid());
        public CancellationToken Cancellation=>default;public IOutputResources Resources=>resources;public void Log(string message){}
    }
    private sealed class OwnerLease:IDisposable{public ToolExecutionOwner Owner{get;}=new();public void Dispose()=>Owner.Retire();}
    private static string Setting(string name)
        =>((IToolParameterBindingNames)new BlobTool()).ParameterBindingNames.Single(pair=>pair.Value==Names[name]).Key;
    private static DeclaredOutputSnapshot Result(ExecutionResult result,string node)=>result.NodeResults[node].Result as DeclaredOutputSnapshot??throw new Exception("没有正式Runtime结果");
    private static T Wait<T>(Func<Task<T>> start)
    {
        var previous=SynchronizationContext.Current;var context=new NoPost();SynchronizationContext.SetSynchronizationContext(context);
        try{var task=start();Require(Task.WhenAny(task,Task.Delay(TimeSpan.FromSeconds(30))).GetAwaiter().GetResult()==task,"生产链等待超时");Require(context.Posts==0,"执行等待UI消息泵");return task.GetAwaiter().GetResult();}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }
    private sealed class NoPost:SynchronizationContext{public int Posts;public override void Post(SendOrPostCallback callback,object? state){Interlocked.Increment(ref Posts);throw new Exception("禁止等待UI消息泵");}}
    private static void Reject(Action action,string message="本应拒绝的操作被接纳"){try{action();}catch{return;}throw new Exception(message);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Case(string name,Action action){try{action();_passed++;Reports.Add(new{name,passed=true});Console.WriteLine("通过："+name);}catch(Exception error){_failed++;Reports.Add(new{name,passed=false,error=error.ToString()});Console.Error.WriteLine("失败："+name+"\n"+error);}}
    private sealed class CheckLogger:ILogger
    {
        public ILogger ForSource(string source)=>this;
        public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){}
    }
}

