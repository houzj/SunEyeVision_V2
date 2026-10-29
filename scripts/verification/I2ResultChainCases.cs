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
        Case("A blocked UI keeps one callback and only the latest unclaimed result for a node",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);
            var delivered=new List<int>();var fixtures=new List<Fixture>();
            try
            {
                for(int i=0;i<1000;i++)
                {
                    var f=new Fixture();fixtures.Add(f);int value=i;
                    Enqueue(queue,"node",f.Snapshot,()=>delivered.Add(value));f.Snapshot.Dispose();
                }
                Require(callbacks.Count==1&&PendingCount(queue)==1,"UI queue grew with the number of runs");
                Require(fixtures.Take(999).All(f=>f.Output.Disposals==1),"Replaced pending results still own resources");
                callbacks.Dequeue()();
                Require(delivered.SequenceEqual(new[]{999})&&fixtures.All(f=>f.Output.Disposals==1),"The latest result was not delivered/released exactly once");
            }
            finally{foreach(var f in fixtures)f.Dispose();}
        });
        Case("Cancellation removes a pending result before the blocked UI is pumped",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);using var f=new Fixture();
            int delivered=0;Enqueue(queue,"node",f.Snapshot,()=>delivered++);f.Snapshot.Dispose();f.Cancellation.Cancel();
            Require(PendingCount(queue)==0&&f.Output.Disposals==1,"Cancelled pending entry retained its result");
            callbacks.Dequeue()();Require(delivered==0,"Cancelled result was delivered");
        });
        Case("Reentrant publication replaces an unclaimed node without changing the active reader",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);
            using var a=new Fixture();using var oldB=new Fixture();using var newB=new Fixture();var delivered=new List<string>();
            Enqueue(queue,"a",a.Snapshot,()=>
            {
                delivered.Add("a");Enqueue(queue,"b",newB.Snapshot,()=>delivered.Add("new-b"));newB.Snapshot.Dispose();
                Require(a.Output.Disposals==0&&oldB.Output.Disposals==1,"Replacing pending B damaged active A or retained old B");
            });
            Enqueue(queue,"b",oldB.Snapshot,()=>delivered.Add("old-b"));a.Snapshot.Dispose();oldB.Snapshot.Dispose();
            callbacks.Dequeue()();Require(callbacks.Count==1&&delivered.SequenceEqual(new[]{"a"}),"Reentrant replacement delivered obsolete B");
            callbacks.Dequeue()();Require(delivered.SequenceEqual(new[]{"a","new-b"}),"Latest B was lost");
        });
        Case("Closing a queue releases unclaimed batch entries while a callback keeps its active lease",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);
            using var a=new Fixture();using var b=new Fixture();int delivered=0;
            Enqueue(queue,"a",a.Snapshot,()=>
            {
                delivered++;queue.Dispose();Require(a.Output.Disposals==0&&b.Output.Disposals==1,"Close freed the active reader or retained unclaimed B");
            });
            Enqueue(queue,"b",b.Snapshot,()=>delivered++);a.Snapshot.Dispose();b.Snapshot.Dispose();
            callbacks.Dequeue()();Require(delivered==1&&a.Output.Disposals==1,"Closed batch continued delivery or leaked active ownership");
            using var late=new Fixture();Enqueue(queue,"late",late.Snapshot,()=>delivered++);late.Snapshot.Dispose();
            Require(callbacks.Count==0&&late.Output.Disposals==1,"Closed queue accepted a new result");
        });
        Case("A rejected UI post releases ownership and permits a later successful post",()=>
        {
            var callbacks=new Queue<Action>();bool reject=true;
            using var queue=DispatchQueue(action=>{if(reject)throw new InvalidOperationException("post rejected");callbacks.Enqueue(action);});
            using var old=new Fixture();Throws<AggregateException>(()=>Enqueue(queue,"old",old.Snapshot,()=>{}));old.Snapshot.Dispose();
            Require(old.Output.Disposals==1&&PendingCount(queue)==0,"Rejected post leaked its pending lease");
            reject=false;using var next=new Fixture();int delivered=0;Enqueue(queue,"next",next.Snapshot,()=>delivered++);next.Snapshot.Dispose();
            callbacks.Dequeue()();Require(delivered==1&&next.Output.Disposals==1,"Queue did not recover from a failed post");
        });
        Case("A failing consumer does not skip the remaining node deliveries or their cleanups",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);using var a=new Fixture();using var b=new Fixture();
            int delivered=0;Enqueue(queue,"a",a.Snapshot,()=>throw new InvalidOperationException("consumer failure"));
            Enqueue(queue,"b",b.Snapshot,()=>delivered++);a.Snapshot.Dispose();b.Snapshot.Dispose();
            Throws<AggregateException>(()=>callbacks.Dequeue()());
            Require(delivered==1&&a.Output.Disposals==1&&b.Output.Disposals==1,"Consumer failure leaked or skipped another node");
        });
        Case("The real workflow manager coalesces a blocked synchronization context and drains on disposal",()=>
        {
            var original=SynchronizationContext.Current;var context=new BlockedContext();SynchronizationContext.SetSynchronizationContext(context);
            var manager=new SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager(
                new SunEyeVision.UI.Services.Interaction.DefaultInputProvider(),
                new SunEyeVision.Workflow.WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.PluginLogger.Logger),
                new SunEyeVision.UI.Services.RuntimeInputSnapshotService());
            var fixtures=new List<Fixture>();var owner=new ToolExecutionOwner();int delivered=0;
            manager.NodeExecutionCompleted+=(_,_)=>delivered++;
            try
            {
                var node=new SunEyeVision.UI.Shared.WorkflowNode();
                var notify=manager.GetType().GetMethod("OnNodeExecutionCompleted",BindingFlags.Instance|BindingFlags.NonPublic)!;
                for(int i=0;i<100;i++)
                {
                    var f=new Fixture(owner:owner);fixtures.Add(f);notify.Invoke(manager,new object[]{node,f.Snapshot,false});f.Snapshot.Dispose();
                }
                Require(context.Callbacks.Count==1&&fixtures.Take(99).All(f=>f.Output.Disposals==1),"Production manager still posted one callback per run");
                manager.Dispose();Require(fixtures.All(f=>f.Output.Disposals==1),"Manager disposal retained an unclaimed notification");
                context.Callbacks.Dequeue()();Require(delivered==0,"Disposed manager delivered a pending result");
            }
            finally{manager.Dispose();foreach(var f in fixtures)f.Dispose();SynchronizationContext.SetSynchronizationContext(original);}
        });
        Case("A cleanup error cannot prevent the queue from releasing another node",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);using var a=new Fixture();using var b=new Fixture();
            a.Output.DisposeError=true;Enqueue(queue,"a",a.Snapshot,()=>{});Enqueue(queue,"b",b.Snapshot,()=>{});
            a.Snapshot.Dispose();b.Snapshot.Dispose();Throws<AggregateException>(queue.Dispose);
            Require(a.Output.Disposals==1&&a.Scope.Disposals==1&&b.Output.Disposals==1,"Failing release skipped another owner");
            callbacks.Dequeue()();
        });
        Case("An obsolete admission cannot evict a newer queued result",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);var owner=new ToolExecutionOwner();
            using var old=new Fixture(owner:owner);using var current=new Fixture(owner:owner);var delivered=new List<string>();
            Enqueue(queue,"node",current.Snapshot,()=>delivered.Add("current"));Enqueue(queue,"node",old.Snapshot,()=>delivered.Add("old"));
            old.Snapshot.Dispose();current.Snapshot.Dispose();callbacks.Dequeue()();
            Require(delivered.SequenceEqual(new[]{"current"})&&old.Output.Disposals==1,"Old admission replaced the queued current result");
        });
        Case("Parallel producers retain one latest result per node and share one posted drain",()=>
        {
            var callbacks=new System.Collections.Concurrent.ConcurrentQueue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);
            var fixtures=new System.Collections.Concurrent.ConcurrentBag<Fixture>();int delivered=0;
            try
            {
                Parallel.For(0,4,node=>
                {
                    for(int iteration=0;iteration<250;iteration++)
                    {
                        var f=new Fixture();fixtures.Add(f);
                        Enqueue(queue,"node-"+node,f.Snapshot,()=>Interlocked.Increment(ref delivered));f.Snapshot.Dispose();
                    }
                });
                Require(callbacks.Count==1&&PendingCount(queue)==4,"Parallel producers posted extra drains or lost a node");
                Require(fixtures.Count(f=>f.Output.Disposals==0)==4,"Parallel replacements retained more than the latest four results");
                Require(callbacks.TryDequeue(out var drain),"Missing shared drain");drain!();
                Require(delivered==4&&fixtures.All(f=>f.Output.Disposals==1),"Parallel node delivery lost or leaked a result");
            }
            finally{foreach(var f in fixtures)f.Dispose();}
        });
        Case("Concurrent close preserves a claimed result while releasing the unclaimed remainder",()=>
        {
            var callbacks=new Queue<Action>();using var queue=DispatchQueue(callbacks.Enqueue);using var a=new Fixture();using var b=new Fixture();
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();int delivered=0;
            Enqueue(queue,"a",a.Snapshot,()=>
            {
                using var reader=a.Snapshot.AcquireReader();entered.Set();
                Require(release.Wait(TimeSpan.FromSeconds(5)),"Active callback was not released");
                Require(reader.Values!.Get<long>("value")==42,"Concurrent close invalidated an active reader");
                Interlocked.Increment(ref delivered);
            });
            Enqueue(queue,"b",b.Snapshot,()=>Interlocked.Increment(ref delivered));a.Snapshot.Dispose();b.Snapshot.Dispose();
            var worker=Task.Run(callbacks.Dequeue());
            try
            {
                Require(entered.Wait(TimeSpan.FromSeconds(5)),"Consumer never claimed its result");queue.Dispose();
                Require(a.Output.Disposals==0&&b.Output.Disposals==1,"Concurrent close freed active A or leaked pending B");
            }
            finally{release.Set();Require(worker.Wait(TimeSpan.FromSeconds(5)),"Claimed consumer did not finish");}
            Require(delivered==1&&a.Output.Disposals==1,"Concurrent close delivered old B or leaked A");
        });
        Case("Actual WPF dispatcher shutdown releases pending manager results without pumping them",()=>
        {
            Exception? failure=null;var thread=new Thread(()=>
            {
                try
                {
                    var app=new System.Windows.Application {ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
                    var manager=new SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager(
                        new SunEyeVision.UI.Services.Interaction.DefaultInputProvider(),
                        new SunEyeVision.Workflow.WorkflowEngine(SunEyeVision.Plugin.SDK.Logging.PluginLogger.Logger),
                        new SunEyeVision.UI.Services.RuntimeInputSnapshotService());
                    using var f=new Fixture();int delivered=0;manager.NodeExecutionCompleted+=(_,_)=>delivered++;
                    manager.GetType().GetMethod("OnNodeExecutionCompleted",BindingFlags.Instance|BindingFlags.NonPublic)!
                        .Invoke(manager,new object[]{new SunEyeVision.UI.Shared.WorkflowNode(),f.Snapshot,false});
                    f.Snapshot.Dispose();Require(f.Output.Disposals==0,"Actual dispatcher did not retain pending output");
                    app.Dispatcher.InvokeShutdown();
                    Require(f.Output.Disposals==1&&delivered==0,"Dispatcher shutdown retained or delivered an unclaimed result");
                    manager.Dispose();
                }
                catch(Exception error){failure=error;}
            });
            thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
            Require(thread.Join(TimeSpan.FromSeconds(10)),"Actual dispatcher shutdown did not finish");
            if(failure is not null)throw failure;
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
    private static IDisposable DispatchQueue(Action<Action> post)
    {
        var type=typeof(SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager).Assembly.GetType("SunEyeVision.UI.Services.Workflow.ResultDispatchQueue",true)!;
        return (IDisposable)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{post},null)!;
    }
    private static void Enqueue(IDisposable queue,string key,DeclaredOutputSnapshot result,Action action)
    {
        try{queue.GetType().GetMethod("Enqueue",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(queue,new object[]{key,result,action});}
        catch(TargetInvocationException error) when(error.InnerException is not null)
        {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
    }
    private static int PendingCount(IDisposable queue)
        =>((System.Collections.IDictionary)queue.GetType().GetField("_pending",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(queue)!).Count;
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static T Throws<T>(Action action) where T:Exception
    {try{action();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    private static void Case(string name,Action test)
    {
        try{test();Results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
        catch(Exception error){_failed++;Results.Add(new{name,passed=false,error=error.ToString()});Console.WriteLine("FAIL "+name+": "+error);}
    }
    private sealed class BlockedContext:SynchronizationContext
    {
        internal readonly Queue<Action> Callbacks=new();
        public override void Post(SendOrPostCallback callback,object? state)=>Callbacks.Enqueue(()=>callback(state));
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly ToolExecutionOwner Owner;
        internal readonly CancellationTokenSource Cancellation=new();
        internal readonly ToolExecutionIdentity Identity;
        internal readonly ToolSchema Schema;
        internal readonly ProbeOutput Output;
        internal readonly Scope Scope;
        internal readonly DeclaredOutputSnapshot Snapshot;
        internal readonly CountingImage? Image;
        internal Fixture(bool image=false,ToolExecutionOwner? owner=null)
        {
            Owner=owner??new();
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
