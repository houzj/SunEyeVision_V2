using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

public sealed class ParameterBindingService : IBindingPlanCompiler
{
    public BindingCompilation CompileBinding(OutputReference reference,Port target,IBindingCompilationScope scope,BindingConversion conversion=BindingConversion.Automatic)
    {
        try
        {
            NumericStorageRules.ValidateDeclaration(target);
            if(!Enum.IsDefined(conversion))throw new BindingFault("conversion","Unknown conversion operation.");
            var dependencies=new List<BindingNodeId>();
            var source=CompileReference(reference,scope,dependencies);
            var issue=scope.ValidateDependencies(dependencies.Distinct().ToArray());
            if(issue is not null)return new(BindingCompatibility.Incompatible,null,issue);
            var from=source.SelectedType;var to=target.Type;
            var sourceMany=from.Shape==ValueShape.Sequence;var targetMany=to.Shape==ValueShape.Sequence;
            var elementFrom=sourceMany?from.Element!:from;var elementTo=targetMany?to.Element!:to;
            bool conditional=source.Conditional||target.Choices is not null;
            if(elementFrom!=elementTo)
            {
                if(elementFrom.Scalar==ScalarKind.Int64 && elementTo.Scalar==ScalarKind.Float64)
                    conditional|=!NumericStorageRules.IsWidening(ScalarKind.Int64,source.Storage,ScalarKind.Float64,target.Storage);
                else if(!(conversion==BindingConversion.Float64ToInt64Checked && elementFrom.Scalar==ScalarKind.Float64 && elementTo.Scalar==ScalarKind.Int64))
                    throw new BindingFault("incompatible-type","Declared element types are incompatible; an explicit mapping is required.");
                else conditional=true;
            }
            if(elementFrom.Scalar is ScalarKind.Int64 or ScalarKind.Float64 && elementTo.Scalar is ScalarKind.Int64 or ScalarKind.Float64)
                conditional|=!NumericStorageRules.IsWidening(elementFrom.Scalar.Value,source.Storage,elementTo.Scalar.Value,target.Storage);
            var countState=(sourceMany?from.Count:CountConstraint.Exactly(1)).CompatibilityWith(targetMany?to.Count:CountConstraint.Exactly(1));
            if(countState==BindingCompatibility.Incompatible)throw new BindingFault("count","Declared count ranges do not intersect.");
            conditional|=countState==BindingCompatibility.Conditional;
            var root=source.Source.Schema.Outputs.Single(p=>p.Id==reference.PortId);
            conditional|=root.Nullable||!root.Required;
            return new(conditional?BindingCompatibility.Conditional:BindingCompatibility.Compatible,
                new BindingPlan(scope.Consumer,scope.DocumentSessionId,scope.DocumentRevision,source,target,conversion,dependencies),null);
        }
        catch(BindingFault fault){return new(fault.Unresolved?BindingCompatibility.Unresolved:BindingCompatibility.Incompatible,null,new(fault.Code,fault.Message));}
        catch(ArgumentException fault){return new(BindingCompatibility.Incompatible,null,new("invalid-declaration",fault.Message));}
    }

    private static BindingReferencePlan CompileReference(OutputReference reference,IBindingCompilationScope scope,List<BindingNodeId> dependencies)
    {
        var node=new BindingNodeId(reference.ScopeId??scope.Consumer.ScopeId,reference.NodeId);
        if(!scope.TryGetSource(node,out var source))throw new BindingFault("unresolved-source","Source schema is missing or outside the visible scope.",true);
        if(source.Node!=node)throw new BindingFault("source-identity","Schema owner does not match its requested source.");
        var port=source.Schema.Outputs.SingleOrDefault(p=>p.Id==reference.PortId)
            ??throw new BindingFault("missing-output","Declared source field no longer exists.",true);
        if(port.Type!=reference.Type)throw new BindingFault("schema-changed","Source declaration changed; rebind or recompile explicitly.");
        if(NumericStorageRules.Effective(port.Type,port.Storage)!=reference.Storage)
            throw new BindingFault("schema-changed","Source physical numeric declaration changed; rebind explicitly.");
        dependencies.Add(node);
        var selected=port.Type;BindingReferencePlan? index=null;bool conditional=false;
        if(reference.Selection is not BindingSelection.AllItems)
        {
            if(selected.Shape!=ValueShape.Sequence)throw new BindingFault("index-shape","Index selection requires a sequence declaration.");
            if(reference.Selection is BindingSelection.AtConstant fixedIndex)
            {
                if(selected.Count.Maximum is int maximum && fixedIndex.Index>=maximum)
                    throw new BindingFault("index-range","Index is outside the declared maximum count.");
                conditional|=fixedIndex.Index>=selected.Count.Minimum;
            }
            else if(reference.Selection is BindingSelection.AtField dynamicIndex)
            {
                index=CompileReference(dynamicIndex.Reference,scope,dependencies);
                if(index.SelectedType!=TypeRef.ScalarOf(ScalarKind.Int64))throw new BindingFault("index-type","Dynamic index must be an Int64 scalar.");
                conditional=true;
            }
            else throw new BindingFault("selection","Unknown selection operation.");
            selected=selected.Element!;
        }
        var storage=port.Storage;
        foreach(var member in reference.MemberFieldPath)selected=ProjectType(selected,member,ref conditional,ref storage);
        return new(reference,source,selected,index,conditional,storage);
    }

    private static TypeRef ProjectType(TypeRef type,string field,ref bool conditional,ref NumericStorage? storage)
    {
        if(type.Shape==ValueShape.Sequence)return TypeRef.SequenceOf(ProjectType(type.Element!,field,ref conditional,ref storage),type.Count);
        if(type.Shape!=ValueShape.Record)throw new BindingFault("member-shape","Member projection requires declared record fields.");
        var member=type.Fields.SingleOrDefault(p=>p.Id==field)
            ??throw new BindingFault("missing-member","Declared member field no longer exists.",true);
        conditional|=!member.Required||member.Nullable;
        storage=member.Storage;
        return member.Type;
    }

    public BindingResolution ResolveBinding(BindingPlan plan,IBindingRunSnapshot snapshot)
    {
        try
        {
            if(snapshot.ConsumerIdentity.RequestId==Guid.Empty)
                throw new BindingFault("consumer-unplanned","Consumer has no admitted request identity.");
            if(snapshot.Consumer!=plan.Consumer || snapshot.DocumentRevision!=plan.DocumentRevision || snapshot.ConsumerIdentity.DocumentSessionId!=plan.DocumentSessionId)
                throw new BindingFault("document-changed","Binding plan belongs to another consumer or document revision.");
            var source=ReadReference(plan.Source,snapshot);
            if(source is null)return new(true,ValueRules.Freeze(plan.Target,null),null);
            var from=plan.Source.SelectedType;var target=plan.Target.Type;
            object? value;
            if(target.Shape==ValueShape.Sequence)
            {
                var elements=from.Shape==ValueShape.Sequence?Elements(source):new[]{source};
                if(!target.Count.Accepts(elements.Length))throw new BindingFault("count","Runtime item count violates the target declaration.");
                var elementFrom=from.Shape==ValueShape.Sequence?from.Element!:from;
                var converted=elements.Select((item,index)=>
                {
                    var convertedItem=ConvertElement(item,elementFrom,target.Element!,plan.Conversion,plan.Target.Storage);
                    try{return ValueRules.Freeze(new Port(plan.Target.Id,target.Element!,Storage:plan.Target.Storage),convertedItem);}
                    catch(ArgumentException fault)
                    {throw new BindingFault("invalid-element",$"Field {plan.Target.Id}, item {index}: {fault.Message}");}
                }).ToArray();
                value=target.Element==TypeRef.ScalarOf(ScalarKind.Float64)
                    ?converted.Cast<double>().ToArray():new SequenceValue(target,converted);
            }
            else
            {
                if(from.Shape==ValueShape.Sequence)
                {
                    var elements=Elements(source);
                    if(elements.Length!=1)throw new BindingFault("exactly-one","This consumer requires exactly one item; zero or multiple items cannot be used.");
                    source=elements[0];from=from.Element!;
                }
                value=ConvertElement(source,from,target,plan.Conversion,plan.Target.Storage);
            }
            return new(true,ValueRules.Freeze(plan.Target,value),null);
        }
        catch(BindingFault fault){return new(false,null,new(fault.Code,fault.Message));}
        catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException)
        {return new(false,null,new("invalid-value",fault.Message,plan.Target.Id));}
    }

    private static object? ReadReference(BindingReferencePlan plan,IBindingRunSnapshot snapshot)
    {
        if(!snapshot.TryGetExpectedSourceIdentity(plan.Source.Node,out var expected) || expected.RequestId==Guid.Empty)
            throw new BindingFault("source-unplanned","Source has no admitted request in this frozen dependency snapshot.");
        if(!snapshot.TryGetResult(plan.Source.Node,out var result) || !result.Success)
            throw new BindingFault("source-unavailable","Source did not produce a successful result in this invocation.");
        var identity=snapshot.ConsumerIdentity;
        if(result.Identity!=expected || result.Node!=plan.Source.Node || result.Identity.NodeId!=plan.Source.Node.NodeId || result.Identity.ScopeId!=plan.Source.ExecutionScopeId ||
            result.Identity.DocumentSessionId!=identity.DocumentSessionId || result.Identity.RunId!=identity.RunId)
            throw new BindingFault("run-mismatch","Source result belongs to another node, document session or run.");
        if(result.SchemaRevision!=plan.Source.SchemaRevision || result.SchemaFingerprint!=plan.Source.Schema.Fingerprint)
            throw new BindingFault("schema-changed","Runtime schema differs from the compiled binding plan.");
        if(!result.Values.Contains(plan.Reference.PortId))throw new BindingFault("missing-value","Source did not publish the required field.");
        var root=plan.Source.Schema.Outputs.Single(p=>p.Id==plan.Reference.PortId);
        object? value=ValueRules.Freeze(root,result.Values.Get<object?>(plan.Reference.PortId));
        if(plan.Reference.Selection is not BindingSelection.AllItems)
        {
            if(value is null)throw new BindingFault("index-null","Cannot select an item from null.");
            long index=plan.Reference.Selection is BindingSelection.AtConstant fixedIndex?fixedIndex.Index:
                ReadReference(plan.Index!,snapshot) is long dynamicIndex?dynamicIndex:throw new BindingFault("index-value","Dynamic index is unavailable or is not an Int64 scalar.");
            var elements=Elements(value);
            if(index<0 || index>=elements.Length)throw new BindingFault("index-range","Index is outside this invocation's sequence; no fallback item is used.");
            value=elements[(int)index];
        }
        foreach(var member in plan.Reference.MemberFieldPath)value=ProjectValue(value,member);
        return value;
    }

    private static object? ProjectValue(object? value,string field)
    {
        if(value is RecordValue record)
        {
            if(!record.Contains(field))throw new BindingFault("missing-value","Declared member is missing in this invocation.");
            return record.Get<object?>(field);
        }
        if(value is SequenceValue sequence)return sequence.Select(item=>ProjectValue(item,field)).ToArray();
        if(value is object?[] array)return array.Select(item=>ProjectValue(item,field)).ToArray();
        throw new BindingFault("member-value","Record member projection encountered null or a different shape.");
    }
    private static object?[] Elements(object value)=>value switch
    {
        SequenceValue sequence=>sequence.ToArray(),
        Array array when array.Rank==1=>array.Cast<object?>().ToArray(),
        _=>throw new BindingFault("sequence-value","Runtime value is not a declared sequence.")
    };
    private static object? ConvertElement(object? value,TypeRef from,TypeRef to,BindingConversion conversion,NumericStorage? storage)
    {
        if(value is not null && from.Scalar is ScalarKind.Int64 or ScalarKind.Float64 && to.Scalar is ScalarKind.Int64 or ScalarKind.Float64)
            return NumericStorageRules.ConvertNumber(value,from.Scalar.Value,to.Scalar.Value,conversion,storage);
        if(from==to)return value;
        throw new BindingFault("conversion","No declared conversion operation applies.");
    }
    private sealed class BindingFault(string code,string message,bool unresolved=false):Exception(message)
    {public string Code {get;}=code;public bool Unresolved {get;}=unresolved;}
}
