using System.Collections.Concurrent;
using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Plugin.Foundation.Host;
public enum RunStatus { Completed, Failed, Cancelled }
public sealed class RunEnvelope:IDisposable
{
    private readonly List<ImageLease> _leases;
    internal RunEnvelope(RunIdentity identity,RunStatus status,InputValues? values,BusinessJudgment judgment,IReadOnlyList<Diagnostic> diagnostics,List<ImageLease>? leases=null)
    {Identity=identity;Status=status;Values=values;Judgment=judgment;Diagnostics=Array.AsReadOnly(diagnostics.ToArray());_leases=leases??new();}
    public RunIdentity Identity {get;} public RunStatus Status {get;} public InputValues? Values {get;}
    public BusinessJudgment Judgment {get;}public IReadOnlyList<Diagnostic> Diagnostics {get;}
    private int _disposed;
    public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)==0)foreach(var lease in _leases)lease.Dispose();}
}
public sealed class ResourceBroker
{
    private readonly ConcurrentDictionary<string,SemaphoreSlim> _gates=new(StringComparer.Ordinal);
    private int _held;
    public int HeldCount=>Volatile.Read(ref _held);
    internal async Task<IDisposable> AcquireAsync(string[] keys,CancellationToken token)
    {
        var acquired=new List<SemaphoreSlim>();
        try{foreach(var k in keys.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal)){if(string.IsNullOrWhiteSpace(k))throw new ArgumentException("Resource key empty");var s=_gates.GetOrAdd(k,_=>new(1,1));await s.WaitAsync(token);acquired.Add(s);Interlocked.Increment(ref _held);}return new Release(this,acquired);}
        catch{foreach(var s in acquired){s.Release();Interlocked.Decrement(ref _held);}throw;}
    }
    private sealed class Release(ResourceBroker owner,List<SemaphoreSlim> semaphores):IDisposable
    {private int _disposed;public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;foreach(var s in semaphores){s.Release();Interlocked.Decrement(ref owner._held);}}}
}
public sealed class InvocationHandle
{
    private readonly CancellationTokenSource _cts;
    internal InvocationHandle(CancellationTokenSource cts,Task<RunEnvelope> completion){_cts=cts;Completion=completion;}
    public Task<RunEnvelope> Completion {get;}
    public void RequestStop(){try{_cts.Cancel();}catch(ObjectDisposedException){}}
    public async Task<bool> StopAndWaitAsync(TimeSpan timeout)
    {RequestStop();return await Task.WhenAny(Completion,Task.Delay(timeout))==Completion;}
}
/// <summary>Independent contract verification host, deliberately not a second production workflow engine.</summary>
public sealed class NodeHost<TSettings,TOutput>:IAsyncDisposable
    where TSettings:IToolSettings<TSettings> where TOutput:class,IToolOutput
{
    private readonly IToolDefinition<TSettings,TOutput> _definition;
    private readonly IToolSession<TSettings,TOutput> _session;
    private readonly ResourceBroker _broker;private readonly object _gate=new();
    private InvocationHandle? _active;private bool _closing;private Task? _dispose;
    public NodeHost(IToolDefinition<TSettings,TOutput> definition,ResourceBroker? broker=null)
    {FoundationContract.RequireCurrent(definition.GetType().Assembly);_definition=definition;_broker=broker??new();_session=definition.CreateSession();}
    public InvocationHandle Start(TSettings settings,IReadOnlyDictionary<string,object?> rawInputs,RunIdentity identity)
    {
        lock(_gate)
        {
            if(_closing)throw new ObjectDisposedException("node session");
            if(_active is not null&&!_active.Completion.IsCompleted)throw new InvalidOperationException("Node session is not reentrant");
            var frozen=settings.Freeze();var diagnostics=_definition.Validate(frozen);
            if(diagnostics.Count>0)throw new ArgumentException(string.Join(";",diagnostics.Select(d=>d.Message)));
            var schema=_definition.Describe(frozen);var input=new InputValues(schema.Inputs,rawInputs);
            var keys=_definition.Capabilities(frozen).ResourceKeys.ToArray();
            var borrowed=new List<ImageLease>();
            try{foreach(var image in input.Images())borrowed.Add(ImageLease.Borrow(image));}
            catch{foreach(var l in borrowed)l.Dispose();throw;}
            var cts=new CancellationTokenSource();
            var completion=Task.Run(()=>Invoke(frozen,input,schema,identity,keys,borrowed,cts));
            return _active=new(cts,completion);
        }
    }
    private async Task<RunEnvelope> Invoke(TSettings settings,InputValues input,ToolSchema schema,RunIdentity identity,string[] keys,List<ImageLease> borrowed,CancellationTokenSource cts)
    {
        using var outputs=new OutputResources(input.Images());
        try
        {
            using var resource=await _broker.AcquireAsync(keys,cts.Token);
            var context=new Context(identity,cts.Token,outputs);
            var result=await _session.ExecuteAsync(new(settings,input,context));
            cts.Token.ThrowIfCancellationRequested();
            if(!result.Success)return new(identity,RunStatus.Failed,null,result.Judgment,result.Diagnostics);
            if(result.Output is null)throw new InvalidOperationException("Success without output");
            var diagnosticCopy=(result.Diagnostics??throw new InvalidOperationException("Missing diagnostics collection")).ToArray();
            var values=new InputValues(schema.Outputs,result.Output.ToValues());
            var leases=outputs.Transfer(values.Images());
            return new(identity,RunStatus.Completed,values,result.Judgment,diagnosticCopy,leases);
        }
        catch(OperationCanceledException e) when(cts.IsCancellationRequested && e.CancellationToken==cts.Token){return new(identity,RunStatus.Cancelled,null,BusinessJudgment.NotEvaluated,Array.Empty<Diagnostic>());}
        catch(Exception e){return new(identity,RunStatus.Failed,null,BusinessJudgment.NotEvaluated,new[]{new Diagnostic(e.GetType().Name,e.ToString())});}
        finally{foreach(var l in borrowed)l.Dispose();cts.Dispose();}
    }
    public ValueTask DisposeAsync(){lock(_gate)return new(_dispose??=Close());}
    private async Task Close()
    {
        _closing=true;var pending=_active;
        if(pending is not null&&!pending.Completion.IsCompleted){pending.RequestStop();var result=await pending.Completion;result.Dispose();}
        await _session.DisposeAsync();
    }
    private sealed class Context(RunIdentity id,CancellationToken token,IOutputResources resources):IToolExecutionContext
    {public RunIdentity Identity=>id;public CancellationToken Cancellation=>token;public IOutputResources Resources=>resources;public void Log(string message){} }
}
public sealed class PublicationGate(Guid document,Guid run)
{
    public bool Accepts(RunEnvelope result)=>result.Identity.DocumentSessionId==document&&result.Identity.RunId==run;
}
