using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Foundation.Host;
using SunEyeVision.Plugin.Foundation.Samples;
using SunEyeVision.Plugin.Foundation.HalconSample;
using SunEyeVision.Plugin.Wpf;
[assembly:PluginContract(FoundationContract.Major,FoundationContract.Minor)]

internal static partial class Program
{
    private static readonly List<object> Results=new();private static int Failures,Blocked;
    private static readonly IReadOnlyDictionary<string,object?> Empty=new Dictionary<string,object?>();
    static RunIdentity Id()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"node",Guid.NewGuid());
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Throws<T>(Action action) where T:Exception {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    static async Task Case(string id,string name,Func<Task> run)
    {
        try{await run().WaitAsync(TimeSpan.FromSeconds(20));Results.Add(new{id,name,status="passed"});Console.WriteLine($"PASS {id} {name}");}
        catch(NativeUnavailable e){Blocked++;Results.Add(new{id,name,status="blocked",error=e.Message});Console.WriteLine($"BLOCKED {id} {e.Message}");}
        catch(Exception e){Failures++;Results.Add(new{id,name,status="failed",error=e.ToString()});Console.WriteLine($"FAIL {id} {e.Message}");}
    }
    static Task Sync(Action action){action();return Task.CompletedTask;}
    static async Task<int> Main(string[] args)
    {
        string report=args.Length>0?args[0]:"plugin-report.json";
        await StructuredProjectionCases();
        await StructuredCases();
        await BindingCases();
        await Case("FT-A02-count", "Declared sequence count rejects zero/many without inferring schema",()=>Sync(()=>{
            var type=TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64),CountConstraint.Exactly(1));
            var port=new Port("one",type);
            Throws<ArgumentException>(()=>new InputValues(new[]{port},new Dictionary<string,object?>{{"one",Array.Empty<double>()}}));
            Throws<ArgumentException>(()=>new InputValues(new[]{port},new Dictionary<string,object?>{{"one",new[]{1d,2d}}}));
            var values=new InputValues(new[]{port},new Dictionary<string,object?>{{"one",new[]{1d}}});
            Require(values.Get<double[]>("one").Single()==1d,"singleton lost");
            Throws<ArgumentOutOfRangeException>(()=>new CountConstraint(2,1));
            Require(type!=TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)),"count omitted from declaration identity");
        }));
        await Case("FT-M03-declaration", "Record declarations freeze fields and distinguish semantic geometry",()=>Sync(()=>{
            var fields=new[]{new Port("x",TypeRef.ScalarOf(ScalarKind.Float64))};
            var record=TypeRef.RecordOf("point-data",fields);var fingerprint=record.Fingerprint;
            fields[0]=new Port("changed",DataType.Text);
            Require(record.Fields.Single().Id=="x"&&record.Fingerprint==fingerprint,"mutable declaration escaped");
            var frame=new CoordinateFrame("image:test",CoordinateDomain.Image,CoordinateUnit.Pixel);
            Require(TypeRef.GeometryOf(GeometryKind.Point,frame)!=record,"geometry confused with structural record");
            Require(TypeRef.GeometryOf(GeometryKind.Circle,frame)!=TypeRef.GeometryOf(GeometryKind.Ellipse,frame),"geometry kinds collapsed");
            Throws<ArgumentException>(()=>TypeRef.RecordOf("duplicate",new[]{new Port("x",DataType.Text),new Port("x",DataType.Number)}));
        }));
        await Case("A01a","Contracts has no UI/host/native references",()=>Sync(()=>{
            var names=typeof(IToolSettings<>).Assembly.GetReferencedAssemblies().Select(a=>a.Name!).ToArray();
            Require(names.All(n=>n.StartsWith("System.")||n=="System.Runtime"),string.Join(",",names));
            Require(typeof(IToolDefinition<,>).GetMethods().All(m=>m.Name!="Run"),"dual runtime interface");
        }));
        await Case("A02a","Missing/null/empty/wrong type are distinct",()=>Sync(()=>{
            var ports=new[]{new Port("optional",DataType.Text,false,true),new Port("array",DataType.NumberArray)};
            var x=new InputValues(ports,new Dictionary<string,object?>{{"array",Array.Empty<double>()}});
            Require(!x.Contains("optional")&&x.Get<double[]>("array").Length==0,"missing/empty");
            var n=new InputValues(ports,new Dictionary<string,object?>{{"array",Array.Empty<double>()},{"optional",null}});Require(n.Contains("optional")&&n.Get<string?>("optional")==null,"null");
            Throws<ArgumentException>(()=>new InputValues(ports,Empty));Throws<ArgumentException>(()=>new InputValues(ports,new Dictionary<string,object?>{{"array",new[]{1,2}}}));
        }));
        await Case("A02b","Stable IDs reject undeclared inputs and duplicate schema",()=>Sync(()=>{
            Throws<ArgumentException>(()=>new ToolSchema(new[]{new Port("x",DataType.Number),new Port("x",DataType.Number)},Array.Empty<Port>()));
            Throws<ArgumentException>(()=>new InputValues(Array.Empty<Port>(),new Dictionary<string,object?>{{"display-name",1d}}));
        }));
        await Case("A03a","Input arrays frozen at ingress and copied at egress",()=>Sync(()=>{
            double[] source={1,2};var x=new InputValues(new[]{new Port("a",DataType.NumberArray)},new Dictionary<string,object?>{{"a",source}});source[0]=99;x.Get<double[]>("a")[1]=88;
            Require(x.Get<double[]>("a").SequenceEqual(new[]{1d,2d}),"mutable input escaped");
            ((double[])x.CopyValues()["a"]!)[0]=5;Require(x.Get<double[]>("a")[0]==1,"copy escaped");
        }));
        await Case("A03b","Settings freeze before scheduled invocation",async()=>{
            await using var host=new NodeHost<PointSettings,DistanceOutput>(new PointPairDefinition());double[] coordinates={0,0,3,4};var h=host.Start(new(coordinates),Empty,Id());coordinates[2]=90;
            using var result=await h.Completion;Require(result.Status==RunStatus.Completed&&result.Values!.Get<double>("distance")==5,"settings not frozen");
        });
        await Case("A04a","Dynamic script schema exists without executing script",()=>Sync(()=>{
            var schema=ScriptSchema.Describe(new("answer",DataType.Number));Require(schema.Outputs.Single().Id=="answer","schema");
            Require(BindingValidator.Validate(new("n","answer",DataType.Number),schema)==null,"valid ref");
            Require(BindingValidator.Validate(new("n","gone",DataType.Number),schema)?.Code=="missing-output","deleted ref");
            Require(BindingValidator.Validate(new("n","answer",DataType.Text),schema)?.Code=="type-changed","type ref");
            Require(schema.Fingerprint!=ScriptSchema.Describe(new("answer",DataType.Text)).Fingerprint,"fingerprint");
        }));
        await Case("A04c","Schema fingerprint cannot collide through port-ID delimiters",()=>Sync(()=>{
            var one=new ToolSchema(new[]{new Port("a:Number:True:False;I:b",DataType.Number)},Array.Empty<Port>());
            var two=new ToolSchema(new[]{new Port("a",DataType.Number),new Port("b",DataType.Number)},Array.Empty<Port>());
            Require(one.Fingerprint!=two.Fingerprint,"different structures share a fingerprint");
        }));
        await Case("A04b","Discovery consumes schema without creating model execution session",()=>Sync(()=>{
            var model=new ModelDefinition();var settings=new ModelSettings(new[]{1d});
            var schema=model.Describe(settings);model.Validate(settings);model.Capabilities(settings);
            Require(schema.Outputs.Count==1&&model.Created==0&&model.Calls==0&&model.Disposed==0,"schema discovery started runtime resources");
        }));
        await Case("A05a","Cooperative waiting cancellation drains and releases",async()=>{
            var tool=new WaitDefinition();var broker=new ResourceBroker();await using var host=new NodeHost<WaitSettings,NumberOutput>(tool,broker);var h=host.Start(new(),Empty,Id());await tool.Entered.Task;
            Require(await h.StopAndWaitAsync(TimeSpan.FromSeconds(2)),"not drained");using var result=await h.Completion;Require(result.Status==RunStatus.Cancelled&&broker.HeldCount==0,"wrong cancel");
        });
        await Case("A05b","Noncooperative wait is not safe-stopped or disposed early",async()=>{
            var tool=new WaitDefinition();var broker=new ResourceBroker();var host=new NodeHost<WaitSettings,NumberOutput>(tool,broker);var h=host.Start(new(false),Empty,Id());await tool.Entered.Task;
            Require(!await h.StopAndWaitAsync(TimeSpan.FromMilliseconds(30)),"false safe completion");Require(broker.HeldCount==1&&!tool.Disposed,"early release");
            var close=host.DisposeAsync().AsTask();Require(!close.IsCompleted&&!tool.Disposed,"early dispose");tool.Release.TrySetResult(1);await close;using var result=await h.Completion;
            Require(result.Status==RunStatus.Cancelled&&broker.HeldCount==0&&tool.Disposed,"late result leaked");await host.DisposeAsync();
        });
        await Case("A05c","Foreign cancellation exception is failure",async()=>{
            await using var host=new NodeHost<TestSettings,MapOutput>(new TestDefinition("foreign-cancel"));using var x=await host.Start(new(),Empty,Id()).Completion;Require(x.Status==RunStatus.Failed,"foreign exception classified cancelled");
        });
        await Case("A05d","Foreign token still fails when this invocation was also stopped",async()=>{
            var definition=new ForeignAfterStopDefinition();await using var host=new NodeHost<TestSettings,NumberOutput>(definition);
            var run=host.Start(new(),Empty,Id());await definition.Entered.Task;run.RequestStop();definition.Release.TrySetResult();
            using var result=await run.Completion;Require(result.Status==RunStatus.Failed,"foreign cancellation hidden by local stop");
        });
        await Case("A06a","Business NG differs from execution failure",async()=>{
            await using var host=new NodeHost<TestSettings,MapOutput>(new TestDefinition("ng"));using var x=await host.Start(new(),Empty,Id()).Completion;Require(x.Status==RunStatus.Completed&&x.Judgment==BusinessJudgment.Fail,"NG collapsed into failure");
        });
        await Case("A06b","Result identity belongs to host and rejects other generation",async()=>{
            var id=Id();await using var host=new NodeHost<PointSettings,DistanceOutput>(new PointPairDefinition());using var x=await host.Start(new(new[]{0d,0,3,4}),Empty,id).Completion;
            Require(x.Identity==id&&new PublicationGate(id.DocumentSessionId,id.RunId).Accepts(x)&&!new PublicationGate(id.DocumentSessionId,Guid.NewGuid()).Accepts(x),"publication identity");
        });
        await Case("A06c","Output schema rejects missing and incorrect types",async()=>{
            foreach(var mode in new[]{"wrong-output","missing-output"}){await using var host=new NodeHost<TestSettings,MapOutput>(new TestDefinition(mode));using var x=await host.Start(new(),Empty,Id()).Completion;Require(x.Status==RunStatus.Failed,mode);}
        });
        await Case("A07a","Retain input transfers a distinct ownership reference",async()=>{
            var before=ImageLease.LiveBuffers;var image=ImageLease.CreateGray8(2,1,new byte[]{1,2});await using var host=new NodeHost<TestSettings,MapOutput>(new TestDefinition("retain"));
            using var result=await host.Start(new(),new Dictionary<string,object?>{{"image",image.View}},Id()).Completion;image.Dispose();var output=result.Values!.Get<IReadOnlyImage>("image");Require(output.CopyPixels().SequenceEqual(new byte[]{1,2}),"input released output");
            result.Dispose();result.Dispose();Require(ImageLease.LiveBuffers==before,"retained image leaked");Throws<ObjectDisposedException>(()=>output.CopyPixels());
        });
        await Case("A07b","Raw borrowed output rejected; partially created output reclaimed",async()=>{
            int before=ImageLease.LiveBuffers;using var image=ImageLease.CreateGray8(1,1,new byte[]{5});
            foreach(var mode in new[]{"raw-image","create-throw"}){await using var host=new NodeHost<TestSettings,MapOutput>(new TestDefinition(mode));using var x=await host.Start(new(),new Dictionary<string,object?>{{"image",image.View}},Id()).Completion;Require(x.Status==RunStatus.Failed,mode);Require(ImageLease.LiveBuffers==before+1,"partial output leak");}
            Require(image.View.CopyPixels()[0]==5,"borrowed input broken");
        });
        await Case("A07c","Unused created image reclaimed after successful scalar result",async()=>{
            var before=ImageLease.LiveBuffers;await using var host=new NodeHost<TestSettings,MapOutput>(new TestDefinition("unused-image"));using var x=await host.Start(new(),Empty,Id()).Completion;Require(x.Status==RunStatus.Completed&&ImageLease.LiveBuffers==before,"unused allocation leaked");
        });
        await Case("A08a","Session rejects reentry and common device key serializes sessions",async()=>{
            var broker=new ResourceBroker();var a=new WaitDefinition();var b=new WaitDefinition();await using var h1=new NodeHost<WaitSettings,NumberOutput>(a,broker);await using var h2=new NodeHost<WaitSettings,NumberOutput>(b,broker);
            var x=h1.Start(new(),Empty,Id());await a.Entered.Task;Throws<InvalidOperationException>(()=>h1.Start(new(),Empty,Id()));var y=h2.Start(new(),Empty,Id());
            Require(broker.HeldCount==1&&!b.Entered.Task.IsCompleted,"resource overlap");a.Release.TrySetResult(1);using var ax=await x.Completion;await b.Entered.Task;Require(broker.HeldCount==1,"second lock");b.Release.TrySetResult(2);using var by=await y.Completion;Require(broker.HeldCount==0,"resource not released");
        });
        await Case("A08b","Cancelling a resource waiter does not release another holder",async()=>{
            var broker=new ResourceBroker();var a=new WaitDefinition();var b=new WaitDefinition();await using var h1=new NodeHost<WaitSettings,NumberOutput>(a,broker);await using var h2=new NodeHost<WaitSettings,NumberOutput>(b,broker);
            var x=h1.Start(new(),Empty,Id());await a.Entered.Task;var y=h2.Start(new(),Empty,Id());y.RequestStop();using var bx=await y.Completion;Require(bx.Status==RunStatus.Cancelled&&broker.HeldCount==1&&!b.Entered.Task.IsCompleted,"waiter affected holder");a.Release.TrySetResult(1);using var ax=await x.Completion;
        });
        await Case("A08c","Stateful model reused in session and disposed exactly once",async()=>{
            var tool=new ModelDefinition();var host=new NodeHost<ModelSettings,NumberOutput>(tool);using var x=await host.Start(new(new[]{2d,3}),Empty,Id()).Completion;using var y=await host.Start(new(new[]{4d}),Empty,Id()).Completion;
            Require(x.Values!.Get<double>("value")==5&&y.Values!.Get<double>("value")==4&&tool.Created==1&&tool.Calls==2,"model reuse");await host.DisposeAsync();await host.DisposeAsync();Require(tool.Disposed==1,"model disposal");Throws<ObjectDisposedException>(()=>host.Start(new(new[]{1d}),Empty,Id()));
        });
        await Case("A09a","Constant/binding switch preserves zero, false and null",()=>Sync(()=>{
            var session=new ParameterEditSession(new[]{new Port("n",DataType.Number),new Port("b",DataType.Boolean),new Port("t",DataType.Text,true,true)},new Dictionary<string,ParameterSetting>{{"n",new ConstantSetting(0d)},{"b",new ConstantSetting(false)},{"t",new ConstantSetting(null)}});
            foreach(var (id,kind) in new[]{("n",DataType.Number),("b",DataType.Boolean),("t",DataType.Text)}){Require(session.Set(id,new ReferenceSetting(new("source","value",kind))),"binding");Require(session.RestoreConstant(id),"restore");}
            Require(session.TryCommit()&&((ConstantSetting)session.Committed["n"]).Value is 0d&&((ConstantSetting)session.Committed["b"]).Value is false&&((ConstantSetting)session.Committed["t"]).Value is null,"value switch");
        }));
        await Case("A09b","Invalid draft blocks entire edit commit; cancel preserves committed values",()=>Sync(()=>{
            var session=new ParameterEditSession(new[]{new Port("n",DataType.Number)},new Dictionary<string,ParameterSetting>{{"n",new ConstantSetting(4d)}});
            Require(!session.SetText("n","oops")&&!session.TryCommit(),"invalid committed");Require(((ConstantSetting)session.Committed["n"]).Value is 4d,"invalid changed model");session.Cancel();Require(!session.HasInvalidDraft,"cancel draft");Require(session.SetText("n","7.5")&&session.TryCommit()&&((ConstantSetting)session.Committed["n"]).Value is 7.5d,"valid commit");
        }));
        await Case("A09c","Actual WPF control edits via UI-neutral session",async()=>{
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var thread=new Thread(()=>{try{
                var session=new ParameterEditSession(new[]{new Port("n",DataType.Number)},new Dictionary<string,ParameterSetting>{{"n",new ConstantSetting(1d)}});var editor=new ParameterEditorControl(session,"n");
                Require(editor.Input.Text=="1","initial committed value not displayed");
                editor.Input.Text="bad";editor.Commit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Require(!editor.LastCommitSucceeded&&editor.Input.Text=="bad","draft lost");editor.Input.Text="2.5";editor.Commit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Require(editor.LastCommitSucceeded&&((ConstantSetting)session.Committed["n"]).Value is 2.5d,"WPF commit");done.SetResult();
            }catch(Exception e){done.SetException(e);}});thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task;
        });
        await Case("A10a","Shared point kernel: golden, degenerate, huge finite and NaN",()=>Sync(()=>{
            var f=SunEyeVision.Tool.Kernels.PointPairMath.Measure(0,0,3,4);Require(f.Distance==5&&f.DeltaX==3&&f.DeltaY==4,"3-4-5");Require(SunEyeVision.Tool.Kernels.PointPairMath.Measure(1,1,1,1).Distance==0,"degenerate");Require(double.IsFinite(SunEyeVision.Tool.Kernels.PointPairMath.Measure(0,0,1e200,1e200).Distance),"hypot overflow");Throws<InvalidOperationException>(()=>SunEyeVision.Tool.Kernels.PointPairMath.Measure(0,0,double.NaN,0));
        }));
        // Deliberately a real HALCON call. Missing runtime/license is reported as blocked, never a simulated pass.
        try{
            await Case("A10b","Real Halcon threshold uses production shared source, exact pixels",async()=>{
                int before=ImageLease.LiveBuffers;using var image=ImageLease.CreateGray8(4,1,new byte[]{0,127,128,255});await using var host=new NodeHost<ThresholdSettings,ImageOutput>(new ThresholdDefinition());using var result=await host.Start(new(128),new Dictionary<string,object?>{{"image",image.View}},Id()).Completion;
                if(result.Status!=RunStatus.Completed){var error=string.Join(";",result.Diagnostics.Select(d=>d.Message));if(error.Contains("license",StringComparison.OrdinalIgnoreCase)||error.Contains("Unable to load",StringComparison.OrdinalIgnoreCase))throw new NativeUnavailable(error);throw new Exception(error);}
                Require(result.Values!.Get<IReadOnlyImage>("image").CopyPixels().SequenceEqual(new byte[]{0,0,255,255}),"threshold pixels differ");Require(image.View.CopyPixels().SequenceEqual(new byte[]{0,127,128,255}),"input mutated");result.Dispose();Require(ImageLease.LiveBuffers==before+1,"output resource not reclaimed");
            });
        }catch(NativeUnavailable){ }
        await Case("A11a","Development contract rejects old or missing plugin metadata",()=>Sync(()=>{
            FoundationContract.RequireCurrent(typeof(PointPairDefinition).Assembly);Throws<InvalidOperationException>(()=>FoundationContract.RequireCurrent(typeof(string).Assembly));
            var old=System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OldContractNegativeFixture"),System.Reflection.Emit.AssemblyBuilderAccess.Run);
            old.SetCustomAttribute(new System.Reflection.Emit.CustomAttributeBuilder(typeof(PluginContractAttribute).GetConstructor(new[]{typeof(int),typeof(int)})!,new object[]{2,1}));
            Throws<InvalidOperationException>(()=>FoundationContract.RequireCurrent(old));
        }));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);File.WriteAllText(report,JsonSerializer.Serialize(new{task="PLUGIN-FOUNDATION-01",scope="local development verification; independent acceptance separate",passed=Results.Count-Failures-Blocked,failed=Failures,blocked=Blocked,results=Results,vendorModules=VendorModules()},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"TOTAL {Results.Count} FAILED {Failures} BLOCKED {Blocked}");return Failures==0?0:1;
    }
    private static object[] VendorModules()
    {
        return System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>()
            .Where(m=>m.ModuleName.Contains("halcon",StringComparison.OrdinalIgnoreCase))
            .Select(m=> {using var stream=System.IO.File.OpenRead(m.FileName);return (object)new {name=m.ModuleName,path=m.FileName,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))};}).ToArray();
    }
    private sealed class NativeUnavailable(string message):Exception(message);
}
public sealed record TestSettings():IToolSettings<TestSettings>{public TestSettings Freeze()=>this;}
public sealed record MapOutput(IReadOnlyDictionary<string,object?> Values):IToolOutput{public IReadOnlyDictionary<string,object?> ToValues()=>Values;}
public sealed class TestDefinition(string mode):IToolDefinition<TestSettings,MapOutput>
{
    bool ImageMode=>mode is "retain" or "raw-image" or "create-throw";
    public string ToolId=>"Test."+mode;
    public ToolSchema Describe(TestSettings s)=>new(ImageMode?new[]{new Port("image",DataType.Image)}:Array.Empty<Port>(),new[]{new Port(ImageMode?"image":"value",ImageMode?DataType.Image:DataType.Number)});
    public IReadOnlyList<Diagnostic> Validate(TestSettings s)=>Array.Empty<Diagnostic>();public ToolCapabilities Capabilities(TestSettings s)=>ToolCapabilities.None;
    public IToolSession<TestSettings,MapOutput> CreateSession()=>new Session(mode);
    private sealed class Session(string mode):IToolSession<TestSettings,MapOutput>
    {
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<ToolOutcome<MapOutput>> ExecuteAsync(ToolInvocation<TestSettings> call)
        {
            if(mode=="foreign-cancel")throw new OperationCanceledException("foreign token");
            if(mode=="create-throw"){call.Context.Resources.CreateGray8(1,1,new byte[]{9});throw new InvalidOperationException("fail after resource allocation");}
            if(mode=="unused-image")call.Context.Resources.CreateGray8(1,1,new byte[]{9});
            IReadOnlyDictionary<string,object?> values=mode switch{
                "retain"=>new Dictionary<string,object?>{{"image",call.Context.Resources.RetainInput(call.Inputs.Get<IReadOnlyImage>("image"))}},
                "raw-image"=>new Dictionary<string,object?>{{"image",call.Inputs.Get<IReadOnlyImage>("image")}},
                "wrong-output"=>new Dictionary<string,object?>{{"value","oops"}},"missing-output"=>new Dictionary<string,object?>(),
                _=>new Dictionary<string,object?>{{"value",1d}}};
            return ValueTask.FromResult(ToolOutcome<MapOutput>.Completed(new(values),mode=="ng"?BusinessJudgment.Fail:BusinessJudgment.NotEvaluated));
        }
    }
}

public sealed class ForeignAfterStopDefinition:IToolDefinition<TestSettings,NumberOutput>
{
    public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string ToolId=>"ForeignCancellation.Regression";
    public ToolSchema Describe(TestSettings s)=>new(Array.Empty<Port>(),new[]{new Port("value",DataType.Number)});
    public IReadOnlyList<Diagnostic> Validate(TestSettings s)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities(TestSettings s)=>ToolCapabilities.None;
    public IToolSession<TestSettings,NumberOutput> CreateSession()=>new Session(this);
    private sealed class Session(ForeignAfterStopDefinition owner):IToolSession<TestSettings,NumberOutput>
    {
        public async ValueTask<ToolOutcome<NumberOutput>> ExecuteAsync(ToolInvocation<TestSettings> call)
        {owner.Entered.TrySetResult();await owner.Release.Task;throw new OperationCanceledException(new CancellationToken(true));}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
