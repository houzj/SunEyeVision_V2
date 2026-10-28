using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Workflow;

/// <summary>A source-scope assignment frozen with the published document. No UI callback or persisted value ownership.</summary>
public sealed class WorkflowGlobalProducerOperation
{
    public WorkflowGlobalProducerOperation(string variableId,string variableName,OutputReference source,Port target,Type valueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableId);ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(valueType);
        if(string.IsNullOrWhiteSpace(source.ScopeId)||source.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("Global producers require an explicit source scope and an All selection.");
        var element=target.Type.Shape==ValueShape.Sequence?target.Type.Element!:target.Type;
        if(element.Shape!=ValueShape.Scalar)throw new ArgumentException("Global variable storage does not own resource or composite values.");
        VariableId=variableId;VariableName=variableName;Source=source;Target=target;ValueType=valueType;
    }
    public string VariableId {get;}
    public string VariableName {get;}
    public OutputReference Source {get;}
    public Port Target {get;}
    public Type ValueType {get;}
    internal string OperationId=>"global-producer:"+VariableId.Length+":"+VariableId;
}

public sealed record WorkflowGlobalProducedValue(string VariableId,string VariableName,object? Value,Diagnostic? Issue);

/// <summary>One batch's supported pure values. Resource targets are rejected before production.</summary>
public sealed class WorkflowGlobalRunValues:IDisposable
{
    private readonly object _gate=new();
    private readonly Dictionary<string,object?> _values=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Diagnostic> _issues=new(StringComparer.Ordinal);
    private bool _disposed;
    public WorkflowGlobalRunValues(IEnumerable<KeyValuePair<string,object?>> initial)
    {foreach(var pair in initial)_values.Add(pair.Key,Copy(pair.Value));}
    private static bool IsScalar(Type type)=>type==typeof(string)||type==typeof(bool)
        ||type==typeof(sbyte)||type==typeof(byte)||type==typeof(short)||type==typeof(ushort)
        ||type==typeof(int)||type==typeof(uint)||type==typeof(long)||type==typeof(float)||type==typeof(double);
    private static object? Copy(object? value)
    {
        if(value is null)return null;
        if(IsScalar(value.GetType()))return value;
        if(value is Array array&&array.GetType().IsSZArray&&IsScalar(array.GetType().GetElementType()!))return array.Clone();
        throw new ArgumentException("运行变量槽仅接纳纯标量及一维纯标量数组，不持有图像、区域、组合或未知对象。",nameof(value));
    }
    public IReadOnlyDictionary<string,object?> Capture()
    {lock(_gate){ObjectDisposedException.ThrowIf(_disposed,this);return new ReadOnlyDictionary<string,object?>(_values.ToDictionary(p=>p.Key,p=>Copy(p.Value),StringComparer.Ordinal));}}
    public IReadOnlyDictionary<string,Diagnostic> Diagnostics
    {get{lock(_gate){ObjectDisposedException.ThrowIf(_disposed,this);return new ReadOnlyDictionary<string,Diagnostic>(new Dictionary<string,Diagnostic>(_issues,StringComparer.Ordinal));}}}
    internal (IReadOnlyDictionary<string,object?> Values,IReadOnlyDictionary<string,Diagnostic> Issues) CaptureState()
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            return (new ReadOnlyDictionary<string,object?>(_values.ToDictionary(p=>p.Key,p=>Copy(p.Value),StringComparer.Ordinal)),
                new ReadOnlyDictionary<string,Diagnostic>(new Dictionary<string,Diagnostic>(_issues,StringComparer.Ordinal)));
        }
    }
    internal void Publish(IReadOnlyList<WorkflowGlobalProducedValue> values,CancellationToken cancellationToken)
    {
        var prepared=values.Select(item=>item with {Value=Copy(item.Value)}).ToArray();
        if(prepared.Select(item=>item.VariableId).Distinct(StringComparer.Ordinal).Count()!=prepared.Length
            ||prepared.Select(item=>item.VariableName).Distinct(StringComparer.Ordinal).Count()!=prepared.Length)
            throw new ArgumentException("A produced batch cannot assign the same variable twice.",nameof(values));
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);cancellationToken.ThrowIfCancellationRequested();
            foreach(var item in prepared)
            {
                _values.Remove(item.VariableName);_issues.Remove(item.VariableId);
                if(item.Issue is {} issue)_issues[item.VariableId]=issue;
                else _values.Add(item.VariableName,item.Value);
            }
        }
    }
    public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;_values.Clear();_issues.Clear();}}
}
