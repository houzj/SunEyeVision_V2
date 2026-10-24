using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

public sealed record DeclaredOutputField(string Name,string DisplayName,string VariableType,Type? ClrType,Guid? FieldId,string? Group,bool DisplayInResultPanel);

/// <summary>One admitted invocation's frozen declaration and outcome; selection never means latest result.</summary>
public sealed class DeclaredOutputSnapshot
{
    private readonly InputValues? _values;
    private readonly ResultResourceOwner? _resources;
    private DeclaredOutputSnapshot(ToolSchema schema,long revision,RunIdentity identity,ExecutionStatus status,
        InputValues? values,ResultResourceOwner? resources,IEnumerable<Diagnostic> diagnostics,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true)
    {
        Schema=schema;SchemaRevision=revision;Identity=identity;Status=status;_values=values;_resources=resources;
        Diagnostics=Array.AsReadOnly(diagnostics.ToArray());
        Fields=new System.Collections.ObjectModel.ReadOnlyDictionary<string,DeclaredOutputField>(
            fields is null?schema.Outputs.ToDictionary(port=>port.Id,port=>new DeclaredOutputField(port.Id,port.Id,"",null,null,null,true))
                :fields.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal));
        SourceWorkflowId=sourceWorkflowId;IsComplete=complete;
    }
    public ToolSchema Schema {get;}
    public long SchemaRevision {get;}
    public RunIdentity Identity {get;}
    public ExecutionStatus Status {get;}
    public IReadOnlyList<Diagnostic> Diagnostics {get;}
    public IReadOnlyDictionary<string,DeclaredOutputField> Fields {get;}
    public string? SourceWorkflowId {get;}
    public bool IsComplete {get;}
    public static DeclaredOutputSnapshot NotExecuted(ToolSchema schema,long revision,RunIdentity identity,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true)
        =>new(schema,revision,identity,ExecutionStatus.NotExecuted,null,null,Array.Empty<Diagnostic>(),fields,sourceWorkflowId,complete);
    public static DeclaredOutputSnapshot WithoutValues(ToolSchema schema,long revision,RunIdentity identity,ExecutionStatus status,IEnumerable<Diagnostic> diagnostics,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true)
    {
        if(status is ExecutionStatus.Success or ExecutionStatus.PartialSuccess or ExecutionStatus.Running)
            throw new ArgumentException("A completed successful output requires frozen values.",nameof(status));
        return new(schema,revision,identity,status,null,null,diagnostics,fields,sourceWorkflowId,complete);
    }
    public static DeclaredOutputSnapshot Publish(ToolResults result,RunIdentity identity,IReadOnlyDictionary<string,object?>? values,
        IReadOnlyDictionary<string,DeclaredOutputField>? fields=null,string? sourceWorkflowId=null,bool complete=true)
    {
        ArgumentNullException.ThrowIfNull(result);
        var schema=result.BindingOutputSchema??throw new InvalidOperationException("The invocation has no frozen output declaration.");
        if(result.ExecutionIdentity is null||result.ExecutionIdentity.ToInvocationIdentity(identity.NodeId)!=identity)
            throw new InvalidOperationException("Output identity differs from the admitted invocation.");
        if(result.DeclaredOutput is not null)throw new InvalidOperationException("The invocation output was already frozen.");
        var frozen=result.IsSuccess?new InputValues(schema.Outputs,values??throw new ArgumentNullException(nameof(values))):null;
        var diagnostics=string.IsNullOrEmpty(result.ErrorMessage)?Array.Empty<Diagnostic>():new[]{new Diagnostic("execution",result.ErrorMessage)};
        var output=new DeclaredOutputSnapshot(schema,result.BindingSchemaRevision,identity,result.Status,frozen,result.Resources,diagnostics,fields,sourceWorkflowId,complete);
        result.PublishDeclaredOutput(output);
        return output;
    }
    public DeclaredOutputReader AcquireReader()
        =>new(this,_values,_resources?.Retain());
}

/// <summary>A reader retains the original result resource owner. Borrowed values are valid until this reader closes.</summary>
public sealed class DeclaredOutputReader : IDisposable
{
    private readonly object _gate=new();
    private InputValues? _values;
    private IDisposable? _lease;
    private bool _closed;
    internal DeclaredOutputReader(DeclaredOutputSnapshot snapshot,InputValues? values,IDisposable? lease)
    {Snapshot=snapshot;_values=values;_lease=lease;}
    public DeclaredOutputSnapshot Snapshot {get;}
    public InputValues? Values
    {
        get {lock(_gate){ObjectDisposedException.ThrowIf(_closed,this);return _values;}}
    }
    public void Dispose()
    {
        IDisposable? lease;
        lock(_gate){if(_closed)return;_closed=true;_values=null;lease=_lease;_lease=null;}
        lease?.Dispose();
    }
}
