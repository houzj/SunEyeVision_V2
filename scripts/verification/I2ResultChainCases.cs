using System.Reflection;
using System.IO;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

internal static class S2DocumentEditingCases
{
    private static readonly List<object> Results=new();
    private static int _failed;
    [STAThread] private static int Main()
    {
        Case("Runtime result/identity/lease implementation has no SDK or WPF assembly dependency",()=>
        {
            var forbidden=new[]{"SunEyeVision.Plugin.SDK","SunEyeVision.UI","PresentationFramework","PresentationCore","WindowsBase","System.Xaml"};
            Require(!typeof(DeclaredOutputSnapshot).Assembly.GetReferencedAssemblies().Any(a=>forbidden.Contains(a.Name)),"Runtime contains a platform dependency");
        });
        Case("Capture freezes once and preserves the exact canonical image and physical domain without a pixel copy",()=>
        {
            using var f=new Fixture(true);
            using var reader=f.Snapshot.AcquireReader();
            Require(f.Output.Reads==1,"ToValues was called more than once");
            Require(ReferenceEquals(reader.Values!.Get<IReadOnlyImage>("image"),f.Image),"Result replaced the original image");
            Require(f.Image!.Copies==0,"Publication copied image pixels");
            Require(f.Snapshot.Identity==f.Identity.ToInvocationIdentity("node"),"Invocation identity changed");
        });
        Case("Independent readers outlive both the result owner and the complete run",()=>
        {
            using var f=new Fixture(true);
            var run=new ExecutionResult{NodeResults=new Dictionary<string,NodeExecutionResult>{{"node",new(){Result=f.Snapshot,Success=true}}}};
            run.CaptureDeclaredOutputs(new[]{f.Snapshot});
            var a=run.AcquireDeclaredOutput(f.Snapshot.Identity);var b=run.AcquireDeclaredOutput(f.Snapshot.Identity);
            f.Snapshot.Dispose();run.Dispose();a.Dispose();a.Dispose();
            Require(f.Output.Disposals==0&&b.Values!.Get<IReadOnlyImage>("image").CopyPixels().Length==2,"A live reader lost its pixels");
            b.Dispose();Require(f.Output.Disposals==1&&f.Scope.Disposals==1,"Original ownership was not released once");
            Throws<ObjectDisposedException>(()=>f.Snapshot.AcquireReader());
            Throws<ObjectDisposedException>(()=>f.Image!.CopyPixels());
        });
        Case("Mutable input leaves cannot change an already frozen result",()=>
        {
            var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
            var input=new[]{1d,2d};var output=new ProbeOutput(new(){{"values",input}});
            using var snapshot=DeclaredOutputSnapshot.Capture(new(Array.Empty<Port>(),new[]{new Port("values",TypeRef.SequenceOf(DataType.Number))}),
                7,identity,"node",new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),new Scope(),0);
            input[0]=99;using var reader=snapshot.AcquireReader();
            Require(reader.Values!.Read("values").GetElement(0).GetScalar<double>()==1,"The frozen result changed");
        });
        Case("One original admission cannot publish a second result through a refined identity",()=>
        {
            using var f=new Fixture();var output=new ProbeOutput(new(){{"value",7L}});var scope=new Scope();
            var refined=f.Identity.WithContractIdentity("5.0-refined");
            Throws<InvalidOperationException>(()=>DeclaredOutputSnapshot.Capture(f.Schema,0,refined,"node",
                new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),scope,0));
            Require(output.Disposals==1&&scope.Disposals==1,"Rejected duplicate leaked its owners");
            using var reader=f.Snapshot.AcquireReader();Require(reader.Values!.Get<long>("value")==42,"Duplicate damaged the first result");
        });
        Case("Capture failure preserves the original error and tries both failing cleanups",()=>
        {
            var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
            var output=new ProbeOutput(new()){ReadError=true,DisposeError=true};var scope=new Scope{DisposeError=true};
            var failure=Throws<AggregateException>(()=>DeclaredOutputSnapshot.Capture(ScalarSchema(),0,identity,"node",
                new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),scope,0));
            Require(failure.Flatten().InnerExceptions.Count==3,"Capture/cleanup failures were lost");
            Require(output.Disposals==1&&scope.Disposals==1,"Cleanup did not try both owners once");
        });
        Case("Retired admission rejects capture and still releases its incoming owners",()=>
        {
            var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");owner.Retire();
            var output=new ProbeOutput(new(){{"value",1L}});var scope=new Scope();
            Throws<InvalidOperationException>(()=>DeclaredOutputSnapshot.Capture(ScalarSchema(),0,identity,"node",
                new(true,output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),scope,0));
            Require(output.Disposals==1&&scope.Disposals==1,"Stale output leaked");
        });
        Case("Failed invocation has no prior values and retains its actual diagnostic and identity",()=>
        {
            var owner=new ToolExecutionOwner();var identity=owner.Begin("scope/node",Guid.NewGuid(),"5.0");
            using var snapshot=DeclaredOutputSnapshot.Capture(ScalarSchema(),4,identity,"node",
                new(false,null,BusinessJudgment.NotEvaluated,new[]{new Diagnostic("failure","actual failure")}),new Scope(),0);
            using var reader=snapshot.AcquireReader();Require(reader.Values is null&&snapshot.Status==ExecutionStatus.Failed
                &&snapshot.ErrorMessage=="actual failure"&&snapshot.SchemaRevision==4,"Failure was replaced by old success values");
        });
        Case("Pending UI notification cancels and releases without a UI message pump",()=>
        {
            using var f=new Fixture();using var pending=Dispatch(f.Snapshot);f.Snapshot.Dispose();f.Cancellation.Cancel();
            Require(f.Output.Disposals==1&&Claim(pending) is null,"Cancellation left an unclaimed notification alive");
        });
        Case("Claimed notification survives cancellation until its actual consumer releases",()=>
        {
            using var f=new Fixture();using var pending=Dispatch(f.Snapshot);f.Snapshot.Dispose();
            using var active=Claim(pending)??throw new Exception("The notification could not be claimed");
            f.Cancellation.Cancel();Require(f.Output.Disposals==0,"Cancellation released an active consumer");
            using(var reader=f.Snapshot.AcquireReader())Require(reader.Values!.Get<long>("value")==42,"Claimed values were lost");
            active.Dispose();Require(f.Output.Disposals==1,"Claimed owner did not release");
        });
        Case("Retirement drains pending notifications even when another retirement observer throws",()=>
        {
            using var f=new Fixture();f.Owner.DomainRetired+=_=>throw new InvalidOperationException("observer failure");
            using var pending=Dispatch(f.Snapshot);f.Snapshot.Dispose();
            Throws<AggregateException>(()=>f.Owner.Retire());
            Require(f.Output.Disposals==1&&Claim(pending) is null,"A failing observer blocked later cleanup");
        });
        Case("Disposing an unclaimed notification repeatedly releases exactly once",()=>
        {
            using var f=new Fixture();using var pending=Dispatch(f.Snapshot);f.Snapshot.Dispose();
            pending.Dispose();pending.Dispose();Require(f.Output.Disposals==1&&f.Scope.Disposals==1,"Notification was released twice");
        });
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"i2-result-chain-report.json"),
            JsonSerializer.Serialize(new{count=Results.Count,failures=_failed,scope="Actual formal Runtime snapshots and UI notification leases; not full UI/Blob acceptance",results=Results}));
        Console.WriteLine($"I2冻结结果及通知生命周期：{Results.Count-_failed}通过，{_failed}失败。");
        return _failed==0?0:1;
    }
    private static ToolSchema ScalarSchema()=>new(Array.Empty<Port>(),new[]{new Port("value",DataType.Integer)});
    private static IDisposable Dispatch(DeclaredOutputSnapshot snapshot)
    {
        var path=Path.Combine(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!,"SunEyeVision.UI.dll");
        var type=Assembly.LoadFrom(path).GetType("SunEyeVision.UI.Services.Workflow.ResultDispatchLease",true)!;
        return (IDisposable)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{snapshot},null)!;
    }
    private static IDisposable? Claim(IDisposable pending)=>(IDisposable?)pending.GetType().GetMethod("Claim",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pending,null);
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static T Throws<T>(Action action) where T:Exception
    {try{action();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    private static void Case(string name,Action test)
    {
        try{test();Results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
        catch(Exception error){_failed++;Results.Add(new{name,passed=false,error=error.ToString()});Console.WriteLine("FAIL "+name+": "+error);}
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly ToolExecutionOwner Owner=new();
        internal readonly CancellationTokenSource Cancellation=new();
        internal readonly ToolExecutionIdentity Identity;
        internal readonly ToolSchema Schema;
        internal readonly ProbeOutput Output;
        internal readonly Scope Scope;
        internal readonly DeclaredOutputSnapshot Snapshot;
        internal readonly CountingImage? Image;
        internal Fixture(bool image=false)
        {
            Identity=Owner.Begin("scope/node",Guid.NewGuid(),"5.0",Cancellation.Token);
            var values=new Dictionary<string,object?>{{"value",42L}};
            var ports=new List<Port>{new("value",DataType.Integer)};
            if(image)
            {
                var resources=new FrozenOutputResources();Image=new(resources.CreateGray8(2,1,new byte[]{9,17}));
                Scope=new(resources);values.Add("image",Image);ports.Add(new("image",DataType.Image));
            }
            else Scope=new();
            Schema=new(Array.Empty<Port>(),ports);Output=new(values);
            Snapshot=DeclaredOutputSnapshot.Capture(Schema,3,Identity,"node",new(true,Output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),Scope,1);
        }
        public void Dispose(){Snapshot.Dispose();Cancellation.Dispose();}
    }
    private sealed class ProbeOutput(Dictionary<string,object?> values):IToolOutput,IDisposable
    {
        internal int Reads,Disposals;internal bool ReadError,DisposeError;
        public IReadOnlyDictionary<string,object?> ToValues(){Reads++;if(ReadError)throw new InvalidOperationException("capture failure");return values;}
        public void Dispose(){Disposals++;if(DisposeError)throw new InvalidOperationException("output cleanup failure");}
    }
    private sealed class Scope(IDisposable? owned=null):IDisposable
    {
        internal int Disposals;internal bool DisposeError;
        public void Dispose(){Disposals++;owned?.Dispose();if(DisposeError)throw new InvalidOperationException("scope cleanup failure");}
    }
    private sealed class CountingImage(IReadOnlyImage image):IReadOnlyImage
    {
        internal int Copies;
        public int Width=>image.Width;public int Height=>image.Height;public ImageLayout Layout=>image.Layout;
        public ImageFrameAddress Frame=>image.Frame;public IReadOnlyRegion EffectiveDomain=>image.EffectiveDomain;
        public byte[] CopyPixels(){Copies++;return image.CopyPixels();}
        public IImagePixelReadLease AcquirePixels()=>image.AcquirePixels();
    }
}
