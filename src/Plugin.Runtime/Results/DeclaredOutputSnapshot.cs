using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

public sealed record DeclaredOutputField(string Name,string DisplayName,string VariableType,Type? ClrType,Guid? FieldId,string? Group,bool DisplayInResultPanel);

/// <summary>One invocation's only frozen result. It owns the actual output and resource scope;
/// consumers borrow declared values without creating SDK images or another result model.</summary>
public sealed class DeclaredOutputSnapshot : IDisposable
{
    private readonly InputValues? _values;
    private readonly ResultResourceOwner? _resources;
    private DeclaredOutputSnapshot(ToolSchema schema,long revision,RunIdentity identity,ExecutionStatus status,
        InputValues? values,ResultResourceOwner? resources,IEnumerable<Diagnostic> diagnostics,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true,
        ToolExecutionIdentity? executionIdentity=null,BusinessJudgment judgment=BusinessJudgment.NotEvaluated,double elapsed=0,IToolOutput? output=null)
    {
        if(schema.Inputs.Count!=0 || revision<0)throw new ArgumentException("A result requires the captured output-only declaration and revision.");
        if(executionIdentity is not null&&executionIdentity.ToInvocationIdentity(identity.NodeId)!=identity)
            throw new ArgumentException("Result identity differs from its admitted invocation.");
        Schema=schema;SchemaRevision=revision;Identity=identity;Status=status;_values=values;_resources=resources;
        ExecutionIdentity=executionIdentity;Judgment=judgment;ExecutionTimeMs=elapsed;Output=output;
        Diagnostics=Array.AsReadOnly(diagnostics.ToArray());
        Fields=new System.Collections.ObjectModel.ReadOnlyDictionary<string,DeclaredOutputField>(
            fields is null?schema.Outputs.ToDictionary(port=>port.Id,port=>new DeclaredOutputField(port.Id,port.Id,"",null,null,null,true))
                :fields.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal));
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
        var owner=new ResultResourceOwner(()=>Release(outcome.Output as IDisposable,resources));
        try
        {
            if(!executionIdentity.TryCaptureResult())throw new InvalidOperationException("This invocation is stale or its result was already captured.");
            var status=outcome.Success?ExecutionStatus.Success:
                executionIdentity.CancellationToken.IsCancellationRequested?ExecutionStatus.Cancelled:ExecutionStatus.Failed;
            var outputSchema=new ToolSchema(Array.Empty<Port>(),schema.Outputs);
            var values=outcome.Success?new InputValues(outputSchema.Outputs,
                outcome.Output?.ToValues()??throw new ArgumentException("A successful invocation has no output.")):null;
            return new(outputSchema,revision,executionIdentity.ToInvocationIdentity(nodeId),status,values,owner,
                outcome.Diagnostics,fields,sourceWorkflowId,complete,executionIdentity,outcome.Judgment,elapsed,outcome.Output);
        }
        catch(Exception failure)
        {
            try{owner.Dispose();}catch(Exception cleanup){throw new AggregateException("Result capture and cleanup failed.",failure,cleanup);}
            throw;
        }
    }
    private static void Release(IDisposable? output,IDisposable resources)
    {
        var errors=new List<Exception>();
        try{output?.Dispose();}catch(Exception error){errors.Add(error);}
        try{resources.Dispose();}catch(Exception error){errors.Add(error);}
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
    public DeclaredOutputReader AcquireReader()=>new(this,_values,_resources?.Retain());
    public IDisposable Retain()=>_resources?.Retain()??EmptyLease.Instance;
    public void Dispose()=>_resources?.Dispose();
    private sealed class EmptyLease:IDisposable
    {internal static readonly EmptyLease Instance=new();public void Dispose(){} }
}

public sealed class DeclaredOutputReader : IDisposable
{
    private readonly object _gate=new();
    private InputValues? _values;
    private IDisposable? _lease;
    private bool _closed;
    internal DeclaredOutputReader(DeclaredOutputSnapshot snapshot,InputValues? values,IDisposable? lease)
    {Snapshot=snapshot;_values=values;_lease=lease;}
    public DeclaredOutputSnapshot Snapshot {get;}
    public InputValues? Values {get{lock(_gate){ObjectDisposedException.ThrowIf(_closed,this);return _values;}}}
    public void Dispose()
    {
        IDisposable? lease;
        lock(_gate){if(_closed)return;_closed=true;_values=null;lease=_lease;_lease=null;}
        lease?.Dispose();
    }
}
