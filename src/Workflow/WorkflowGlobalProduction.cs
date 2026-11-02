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
    private readonly IReadOnlyDictionary<string,string> _names;
    private readonly IReadOnlyDictionary<string,Port> _declarations;
    internal IReadOnlyList<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource> Sources {get;}
    internal bool TryGetDeclaration(string id,out Port declaration)=>_declarations.TryGetValue(id,out declaration!);
    private readonly Dictionary<string,object?> _values=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Diagnostic> _issues=new(StringComparer.Ordinal);
    private bool _disposed;
    public string SolutionId {get;}
    internal IReadOnlyList<BindingVariableSource> CaptureBindingSources()=>_declarations.Select(pair=>
    {
        var source=Sources.Single(item=>item.Id==pair.Key);
        return new BindingVariableSource(new VariableReference(SolutionId,pair.Key,pair.Value.Type,storage:pair.Value.Storage),
            source.HasProducer&&!string.IsNullOrEmpty(source.ProducerNodeId)&&!string.IsNullOrEmpty(source.ProducerWorkflowId)
                ?new BindingNodeId(source.ProducerWorkflowId,source.ProducerNodeId):null);
    }).ToArray();
    internal IReadOnlyList<BindingVariableValue> CaptureBindingValues(RunIdentity consumer)
    {
        var state=CaptureState();
        return CaptureBindingSources().Select(source=>
        {
            var id=source.Reference.VariableId;
            if(state.Issues.TryGetValue(id,out var issue))return new BindingVariableValue(source,consumer,null,issue);
            if(!state.Values.TryGetValue(id,out var raw))return new BindingVariableValue(source,consumer,null,new("variable-unavailable","变量在本批运行尚未产生有效值。",id));
            try
            {
                var value=SunEyeVision.Plugin.SDK.Execution.Parameters.BindingClrAdapter.NormalizeValue(
                    new Port(id,source.Reference.Type,Storage:source.Reference.Storage),raw);
                return new BindingVariableValue(source,consumer,value);
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or OverflowException)
            {return new BindingVariableValue(source,consumer,null,new("variable-value",error.Message,id));}
        }).ToArray();
    }
    public WorkflowGlobalRunValues(string solutionId,IReadOnlyList<GlobalVariable> definitions,IEnumerable<KeyValuePair<string,object?>> initial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionId);SolutionId=solutionId;
        ArgumentNullException.ThrowIfNull(definitions);ArgumentNullException.ThrowIfNull(initial);
        var names=new Dictionary<string,string>(StringComparer.Ordinal);
        var uniqueNames=new HashSet<string>(StringComparer.Ordinal);
        var declarations=new Dictionary<string,Port>(StringComparer.Ordinal);
        var sources=new List<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>();
        foreach(var definition in definitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
            if(!names.TryAdd(definition.Id,definition.Name)||!uniqueNames.Add(definition.Name))
                throw new ArgumentException("运行变量声明的ID与名称必须分别唯一。",nameof(definitions));
            if(GlobalProducerBindingRules.TryDescribeTarget(definition.Type,out var port,out _,out _))declarations.Add(definition.Id,port);
            sources.Add(WorkflowGlobalVariableReceiveService.Describe(definition));
        }
        _names=new ReadOnlyDictionary<string,string>(names);
        _declarations=new ReadOnlyDictionary<string,Port>(declarations);Sources=Array.AsReadOnly(sources.ToArray());
        foreach(var pair in initial)
        {
            if(!_names.ContainsKey(pair.Key))throw new ArgumentException("初始运行值不属于冻结变量声明："+pair.Key,nameof(initial));
            _values.Add(pair.Key,Copy(pair.Value));
        }
    }
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
    {lock(_gate){ObjectDisposedException.ThrowIf(_disposed,this);return new ReadOnlyDictionary<string,object?>(_values.ToDictionary(p=>_names[p.Key],p=>Copy(p.Value),StringComparer.Ordinal));}}
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
        foreach(var item in values)
            if(!_names.TryGetValue(item.VariableId,out var name)||name!=item.VariableName)
                throw new ArgumentException("生产变量身份或名称不属于本批冻结声明。",nameof(values));
        var prepared=values.Select(item=>item with {Value=Copy(item.Value)}).ToArray();
        if(prepared.Select(item=>item.VariableId).Distinct(StringComparer.Ordinal).Count()!=prepared.Length
            ||prepared.Select(item=>item.VariableName).Distinct(StringComparer.Ordinal).Count()!=prepared.Length)
            throw new ArgumentException("A produced batch cannot assign the same variable twice.",nameof(values));
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);cancellationToken.ThrowIfCancellationRequested();
            foreach(var item in prepared)
            {
                _values.Remove(item.VariableId);_issues.Remove(item.VariableId);
                if(item.Issue is {} issue)_issues[item.VariableId]=issue;
                else _values.Add(item.VariableId,item.Value);
            }
        }
    }
    public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;_values.Clear();_issues.Clear();}}
}
