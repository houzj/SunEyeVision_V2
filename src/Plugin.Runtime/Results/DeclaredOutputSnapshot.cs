using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

public sealed record DeclaredOutputField(string Name,string DisplayName,string VariableType,Type? ClrType,Guid? FieldId,string? Group,bool DisplayInResultPanel);

/// <summary>One invocation's only frozen result. It owns the actual output and resource scope;
/// consumers borrow declared values without creating SDK images or another result model.</summary>
public sealed class DeclaredOutputSnapshot : IDisposable
{
    private readonly object _displayGate=new();
    private ResultDisplaySourceSet? _displaySources;
    private bool _released;
    public string? DisplaySourceFailure {get;private set;}
    public void RejectDisplaySources(string reason){lock(_displayGate){DisplaySourceFailure=reason;}}
    private readonly InputValues? _values;
    private readonly ResultResourceOwner? _resources;
    private readonly ResultResourceOwner? _valueResources;
    private DeclaredOutputSnapshot(ToolSchema schema,long revision,RunIdentity identity,ExecutionStatus status,
        InputValues? values,ResultResourceOwner? resources,IEnumerable<Diagnostic> diagnostics,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true,
        ToolExecutionIdentity? executionIdentity=null,BusinessJudgment judgment=BusinessJudgment.NotEvaluated,double elapsed=0,IToolOutput? output=null)
    {
        if(schema.Inputs.Count!=0 || revision<0)throw new ArgumentException("A result requires the captured output-only declaration and revision.");
        if(executionIdentity is not null&&executionIdentity.ToInvocationIdentity(identity.NodeId)!=identity)
            throw new ArgumentException("Result identity differs from its admitted invocation.");
        Schema=schema;SchemaRevision=revision;Identity=identity;Status=status;_values=values;
        _valueResources=resources;_resources=resources is null?null:new ResultResourceOwner(resources.Dispose);
        ExecutionIdentity=executionIdentity;Judgment=judgment;ExecutionTimeMs=elapsed;Output=output;
        Diagnostics=Array.AsReadOnly(diagnostics.ToArray());
        Fields=new System.Collections.ObjectModel.ReadOnlyDictionary<string,DeclaredOutputField>(
            fields is null?schema.Outputs.ToDictionary(port=>port.Id,port=>new DeclaredOutputField(port.Id,schema.FindOutputDisplay(port.Id)?.DisplayName??port.Id,"",null,null,null,true))
                :fields.ToDictionary(item=>item.Key,item=>schema.FindOutputDisplay(item.Key) is {} display
                    ?item.Value with{DisplayName=display.DisplayName}:item.Value,StringComparer.Ordinal));
        if(Fields.Count!=schema.Outputs.Count || Fields.Keys.Any(key=>!schema.Outputs.Any(port=>port.Id==key)))
            throw new ArgumentException("Result field metadata differs from the captured output declaration.");
        SourceWorkflowId=sourceWorkflowId;IsComplete=complete;
    }
    public ToolSchema Schema {get;}
    public long SchemaRevision {get;}
    public RunIdentity Identity {get;}
    public ToolExecutionIdentity? ExecutionIdentity {get;}
    public ExecutionStatus Status {get;}
    public BusinessJudgment Judgment {get;}
    public double ExecutionTimeMs {get;}
    /// <summary>The original immutable typed output, borrowed under this result's reader or retained run.</summary>
    public IToolOutput? Output {get;}
    public bool IsSuccess=>Status is ExecutionStatus.Success or ExecutionStatus.PartialSuccess;
    public bool IsPass=>IsSuccess&&Judgment!=BusinessJudgment.Fail;
    public string? ErrorMessage=>IsSuccess?null:string.Join("; ",Diagnostics.Select(d=>d.Message));
    public IReadOnlyList<Diagnostic> Diagnostics {get;}
    public IReadOnlyDictionary<string,DeclaredOutputField> Fields {get;}
    public string? SourceWorkflowId {get;}
    public bool IsComplete {get;}

    /// <summary>Ownership of outcome.Output and resources transfers on entry, including failure.</summary>
    public static DeclaredOutputSnapshot Capture(ToolSchema schema,long revision,ToolExecutionIdentity executionIdentity,
        string nodeId,ToolOutcome outcome,IDisposable resources,double elapsed,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true)
    {
        ResultResourceOwner? owner=null;
        try
        {
            owner=new ResultResourceOwner(()=>Release(outcome.Output as IDisposable,resources));
            if(!executionIdentity.TryCaptureResult())throw new InvalidOperationException("This invocation is stale or its result was already captured.");
            var status=outcome.Success?ExecutionStatus.Success:
                executionIdentity.CancellationToken.IsCancellationRequested?ExecutionStatus.Cancelled:ExecutionStatus.Failed;
            var outputSchema=new ToolSchema(Array.Empty<Port>(),schema.Outputs,outputDisplay:schema.OutputDisplay);
            var values=outcome.Success?new InputValues(outputSchema.Outputs,
                outcome.Output?.ToValues()??throw new ArgumentException("A successful invocation has no output.")):null;
            return new(outputSchema,revision,executionIdentity.ToInvocationIdentity(nodeId),status,values,owner,
                outcome.Diagnostics,fields,sourceWorkflowId,complete,executionIdentity,outcome.Judgment,elapsed,outcome.Output);
        }
        catch(Exception failure)
        {
            try{if(owner is null)Release(outcome.Output as IDisposable,resources);else owner.Dispose();}catch(Exception cleanup){throw new AggregateException("Result capture and cleanup failed.",failure,cleanup);}
            throw;
        }
    }
    private static void Release(IDisposable? output,IDisposable resources)
    {
        var errors=new List<Exception>();
        try{output?.Dispose();}catch(Exception error){errors.Add(error);}
        if(!ReferenceEquals(output,resources))try{resources.Dispose();}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("Output and resource cleanup failed.",errors);
    }
    public static DeclaredOutputSnapshot NotExecuted(ToolSchema schema,long revision,RunIdentity identity,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true,ToolExecutionIdentity? executionIdentity=null)
        =>new(schema,revision,identity,ExecutionStatus.NotExecuted,null,null,Array.Empty<Diagnostic>(),fields,sourceWorkflowId,complete,executionIdentity);
    public static DeclaredOutputSnapshot WithoutValues(ToolSchema schema,long revision,RunIdentity identity,ExecutionStatus status,IEnumerable<Diagnostic> diagnostics,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true,ToolExecutionIdentity? executionIdentity=null)
    {
        if(status is ExecutionStatus.Success or ExecutionStatus.PartialSuccess or ExecutionStatus.Running)
            throw new ArgumentException("A completed successful output requires frozen values.",nameof(status));
        return new(schema,revision,identity,status,null,null,diagnostics,fields,sourceWorkflowId,complete,executionIdentity);
    }
    internal DeclaredOutputReader AcquireValueReader()=>new(this,_values,_valueResources?.Retain());
    public void AttachDisplaySources(ResultDisplaySourceSet sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        lock(_displayGate)
        {
            ObjectDisposedException.ThrowIf(_released,this);
            if(_displaySources is not null)
            {
                if(_displaySources.Identities.ToHashSet().SetEquals(sources.Identities))return;
                throw new InvalidOperationException("Display sources were already attached with different invocation identities.");
            }
            if(_resources is null)throw new InvalidOperationException("Only a value-owning result can retain display sources.");
            var lease=sources.Acquire();
            try{_resources.Own(lease);_displaySources=sources;}catch{lease.Dispose();throw;}
        }
    }
    public DeclaredOutputReader AcquireReader()
    {
        lock(_displayGate)
        {
            var resources=_resources?.Retain();ResultDisplaySourceLease? display=null;
            try{display=_displaySources?.Acquire();return new(this,_values,new JoinedLease(resources,display),display);}
            catch{resources?.Dispose();display?.Dispose();throw;}
        }
    }
    public IDisposable Retain()
    {
        lock(_displayGate)
        {
            var resources=_resources?.Retain();
            try{return new JoinedLease(resources,_displaySources?.Acquire());}catch{resources?.Dispose();throw;}
        }
    }
    public void Dispose()
    {
        lock(_displayGate){if(_released)return;_released=true;}
        _resources?.Dispose();
    }
    private sealed class JoinedLease(IDisposable? resources,IDisposable? display):IDisposable
    {
        private IDisposable? _resources=resources,_display=display;
        public void Dispose(){var first=Interlocked.Exchange(ref _display,null);var second=Interlocked.Exchange(ref _resources,null);if(second is not null)Release(first,second);else first?.Dispose();}
    }
    private sealed class EmptyLease:IDisposable
    {internal static readonly EmptyLease Instance=new();public void Dispose(){} }
}

public sealed class DeclaredOutputReader : IDisposable
{
    private readonly object _gate=new();
    private InputValues? _values;
    private IDisposable? _lease;
    private bool _closed;
    internal DeclaredOutputReader(DeclaredOutputSnapshot snapshot,InputValues? values,IDisposable? lease,ResultDisplaySourceLease? displaySources=null)
    {Snapshot=snapshot;_values=values;_lease=lease;DisplaySources=displaySources;}
    public ResultDisplaySourceLease? DisplaySources {get;}
    public DeclaredOutputSnapshot Snapshot {get;}
    public InputValues? Values {get{lock(_gate){ObjectDisposedException.ThrowIf(_closed,this);return _values;}}}
    public void Dispose()
    {
        IDisposable? lease;
        lock(_gate){if(_closed)return;_closed=true;_values=null;lease=_lease;_lease=null;}
        lease?.Dispose();
    }
}
