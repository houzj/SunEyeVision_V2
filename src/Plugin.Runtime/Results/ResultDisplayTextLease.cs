using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>A scene's scalar text plan over retained, explicitly captured sources. UI-thread confined.</summary>
public sealed class ResultDisplayTextLease : IDisposable
{
    private readonly DeclaredOutputReader _reader;
    private readonly ParameterBindingService _resolver=new();
    private readonly ResultDisplayBindingScope _scope;
    private readonly BindingRunSnapshot _snapshot;
    private readonly ResultTextAppearance _appearance;
    private readonly Dictionary<DisplayExpressionSlot,OutputReference> _selfReferences=new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<OutputReference,DeclaredValueReader> _resolved=new(ReferenceEqualityComparer.Instance);
    private readonly object _retireGate=new();
    private ToolExecutionIdentity[] _watched=Array.Empty<ToolExecutionIdentity>();
    private ToolExecutionOwner[] _owners=Array.Empty<ToolExecutionOwner>();
    private Action? _invalidated;
    private int _disposed,_stale;
    public ResultDisplayTextLease(DeclaredOutputSnapshot result,ResultTextAppearance appearance)
    {
        if(result.DisplaySourceFailure is {} sourceFailure)throw new InvalidOperationException(sourceFailure);
        _appearance=appearance;_reader=result.AcquireReader();
        try
        {
            var consumer=new BindingNodeId(result.SourceWorkflowId??throw new ArgumentException("显示结果缺少流程身份。"),result.Identity.NodeId);
            var declarations=(_reader.DisplaySources?.CaptureDeclarations()??Array.Empty<BindingSource>()).ToList();
            var results=(_reader.DisplaySources?.CaptureResults()??Array.Empty<BindingNodeResult>()).ToList();
            if(declarations.Any(source=>source.Node==consumer))throw new ArgumentException("显示前驱集合不能包含自身。");
            declarations.Add(new(consumer,result.Identity.ScopeId,result.Schema,result.SchemaRevision));
            results.Add(new(consumer,result.Identity,result.SchemaRevision,result.Schema.Fingerprint,result.IsSuccess,_reader.Values??throw new ArgumentException("显示结果没有冻结值。")));
            _scope=new(consumer,result.Identity.DocumentSessionId,0,declarations);
            _snapshot=new(consumer,result.Identity,0,results,results.ToDictionary(source=>source.Node,source=>source.Identity));
            var references=new List<OutputReference>();
            void Add(OutputReference reference)
            {
                references.Add(reference);
                if(reference.Selection is BindingSelection.AtField index)Add(index.Reference);
            }
            if(appearance.Expression is {} expression)
                foreach(var slot in expression.Slots)if(slot.Reference is {} reference)Add(reference);
            if(appearance.PositionX is ReferenceSetting x)Add(x.Reference);
            if(appearance.PositionY is ReferenceSetting y)Add(y.Reference);
            var identities=new List<ToolExecutionIdentity>();
            foreach(var id in references.Select(reference=>new BindingNodeId(reference.ScopeId??consumer.ScopeId,reference.NodeId)).Distinct())
            {
                if(id==consumer||_reader.DisplaySources is null||!_reader.DisplaySources.TryGet(id,out var source,out _))continue;
                if(source!.ExecutionIdentity is {} identity)identities.Add(identity);
                else if(source.IsSuccess)throw new InvalidOperationException("显示来源缺少可验证的运行域身份。");
            }
            _watched=identities.ToArray();
            _owners=_watched.Select(identity=>identity.Owner).Distinct().ToArray();
            foreach(var owner in _owners)owner.DomainRetired+=OnDomainRetired;
            if(!IsCurrent)throw new InvalidOperationException("文字显示来源的节点域已退役。");
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    public bool IsCurrent=>Volatile.Read(ref _disposed)==0&&Volatile.Read(ref _stale)==0&&_watched.All(identity=>identity.IsDomainCurrent);
    public void SetInvalidatedCallback(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        bool call;
        lock(_retireGate){ObjectDisposedException.ThrowIf(_disposed!=0,this);_invalidated=callback;call=!IsCurrent;}
        if(call)callback();
    }
    private void OnDomainRetired(string domain)
    {
        if(!_watched.Any(identity=>identity.Domain==domain))return;
        Action? callback;
        lock(_retireGate){if(_disposed!=0||_stale!=0)return;_stale=1;callback=_invalidated;}
        callback?.Invoke();
    }
    private sealed class TextSourceFault(string code,string message):ArgumentException(message){internal string Code{get;}=code;}
    private DeclaredValueReader Read(OutputReference reference)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        if(_resolved.TryGetValue(reference,out var cached))return cached;
        var type=ParameterReferenceRules.SelectedType(reference);while(type.Shape==ValueShape.Sequence)type=type.Element!;
        if(type.Shape!=ValueShape.Scalar||type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64 or ScalarKind.Text))throw new ArgumentException("显示来源不支持此类型。");
        var target=new Port("display-value",TypeRef.SequenceOf(type),Nullable:true);
        var compiled=_resolver.CompileBinding(reference,target,_scope);
        if(compiled.Plan is null)throw new TextSourceFault(compiled.Diagnostic?.Code??"display-compile",compiled.Diagnostic?.Message??"显示来源不能编译。");
        var value=_resolver.ResolveDisplayBinding(compiled.Plan,_snapshot);
        if(!value.Success)throw new TextSourceFault(value.Diagnostic?.Code??"display-read",value.Diagnostic?.Message??"显示来源不能读取。");
        var reader=new InputValues(new[]{target},new Dictionary<string,object?>{{target.Id,value.Value}}).Read(target.Id);
        _resolved.Add(reference,reader);return reader;
    }
    private object? At(OutputReference reference,int row,bool missingContentIsEmpty=false)
    {
        DeclaredValueReader reader;
        try{reader=Read(reference);}
        catch(TextSourceFault fault)when(missingContentIsEmpty&&fault.Code is "missing-value" or "source-unavailable"){return null;}
        if(reader.IsMissing||reader.IsNull||reader.Count==0)return null;
        reader=reader.GetElement(row<reader.Count?row:0);
        if(reader.IsMissing||reader.IsNull)return null;
        return reader.Declaration.Scalar switch{ScalarKind.Int64=>reader.GetScalar<long>(),ScalarKind.Float64=>reader.GetScalar<double>(),ScalarKind.Text=>reader.GetScalar<string>(),_=>throw new ArgumentException("显示来源类型无效。")};
    }
    public (string Content,double X,double Y) Evaluate(int row)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        if(!IsCurrent)throw new InvalidOperationException("文字显示来源的节点域已退役。");
        if(row<0)throw new ArgumentOutOfRangeException(nameof(row));
        var expression=_appearance.Expression;var text=expression?.Text??_appearance.Content;var any=false;
        if(expression is not null)
        foreach(var slot in expression.Slots)
        {
            OutputReference reference;
            if(slot.Reference is {} mapped)reference=mapped;
            else
            {
                if(!_selfReferences.TryGetValue(slot,out reference!))
                {
                    var path=slot.Self!;var source=_reader.Snapshot;var port=source.Schema.Outputs.Single(output=>output.Id==path.PortId);
                    reference=new(source.Identity.NodeId,port.Id,port.Type,path.MemberFieldPath,scopeId:source.SourceWorkflowId,storage:port.Storage);
                    _selfReferences.Add(slot,reference);
                }
            }
            var value=At(reference,row,true);any|=value is not null;
            if(slot.Format is not null&&value is not null&&value is not (long or double))throw new ArgumentException("显示数值格式只适用于数字来源。");
            text=text.Replace("{"+slot.Token+"}",slot.Format is not null&&value is IFormattable number?number.ToString(slot.Format,CultureInfo.CurrentCulture):Convert.ToString(value,CultureInfo.CurrentCulture)??"",StringComparison.Ordinal);
        }
        if(expression?.Slots.Count>0&&!any)text="";
        double Position(ParameterSetting? setting,double fallback)
        {
            var value=setting switch{null=>fallback,ConstantSetting constant=>constant.Value,ReferenceSetting reference=>At(reference.Reference,row),_=>throw new ArgumentException("显示位置设置无效。")};
            var number=value switch {double d=>d,long i=>(double)i,_=>throw new ArgumentException("显示位置来源没有有效数字。")};
            return double.IsFinite(number)?number:throw new ArgumentException("显示位置必须是有限数。");
        }
        var result=(text,Position(_appearance.PositionX,_appearance.X),Position(_appearance.PositionY,_appearance.Y));
        if(!IsCurrent)throw new InvalidOperationException("文字显示来源在读取期间已退役。");
        return result;
    }
    public void Dispose()
    {
        lock(_retireGate){if(_disposed!=0)return;_disposed=1;_invalidated=null;}
        foreach(var owner in _owners)owner.DomainRetired-=OnDomainRetired;
        _resolved.Clear();_reader.Dispose();
    }
}
