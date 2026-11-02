using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.Plugin.Contracts;

/// <summary>Explicit consumer semantics. No tool identity or UI state is inferred.</summary>
public enum ParameterReferenceBehavior { StrictExactlyOne, FirstItemWithIndexReadZeroFallback }
public interface IToolParameterReferenceBehavior
{
    IReadOnlyDictionary<string,ParameterReferenceBehavior> ParameterReferenceBehaviors {get;}
}
/// <summary>Pure declaration rules shared by compilation and WPF candidate projection.</summary>
public static class ParameterReferenceRules
{
    public static bool CanUseIndexSource(TypeRef selectedType,ParameterReferenceBehavior behavior)
    {
        if(!Enum.IsDefined(behavior))throw new ArgumentException("Unknown parameter reference behavior.");
        return selectedType==TypeRef.ScalarOf(ScalarKind.Int64)
            ||behavior==ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback&&selectedType.Shape==ValueShape.Sequence
                &&selectedType.Element==TypeRef.ScalarOf(ScalarKind.Int64);
    }
    public static bool UsesFirstItem(TypeRef source,TypeRef target,ParameterReferenceBehavior behavior)
        =>behavior==ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback&&target==TypeRef.ScalarOf(ScalarKind.Int64)
            &&source.Shape==ValueShape.Sequence&&source.Element==TypeRef.ScalarOf(ScalarKind.Int64);
    public static BindingCompatibility CompareValueCount(TypeRef source,TypeRef target,ParameterReferenceBehavior behavior)
    {
        if(!Enum.IsDefined(behavior))throw new ArgumentException("Unknown parameter reference behavior.");
        if(UsesFirstItem(source,target,behavior))return source.Count.Maximum==0?BindingCompatibility.Incompatible:
            source.Count.Minimum>0?BindingCompatibility.Compatible:BindingCompatibility.Conditional;
        return (source.Shape==ValueShape.Sequence?source.Count:CountConstraint.Exactly(1))
            .CompatibilityWith(target.Shape==ValueShape.Sequence?target.Count:CountConstraint.Exactly(1));
    }
    public static void ValidateTarget(Port target,ParameterReferenceBehavior behavior)
    {
        if(!Enum.IsDefined(behavior))throw new ArgumentException("Unknown parameter reference behavior.");
        if(behavior!=ParameterReferenceBehavior.StrictExactlyOne&&(target.Type!=TypeRef.ScalarOf(ScalarKind.Int64)
            ||NumericStorageRules.Effective(target.Type,target.Storage)!=NumericStorage.Int32))
            throw new ArgumentException("First-item parameter behavior requires an Int32 scalar target.");
    }
    public static IReadOnlyDictionary<string,ParameterReferenceBehavior> Freeze(IToolDefinition definition)
    {
        var result=new Dictionary<string,ParameterReferenceBehavior>(StringComparer.Ordinal);
        var declared=definition as IToolSettingsFields;
        foreach(var pair in (definition as IToolParameterReferenceBehavior)?.ParameterReferenceBehaviors??new Dictionary<string,ParameterReferenceBehavior>())
        {
            var target=declared?.SettingsSchema.Inputs.SingleOrDefault(p=>p.Id==pair.Key)
                ??throw new ArgumentException("Parameter reference behavior requires a declared Settings field: "+pair.Key);
            ValidateTarget(target,pair.Value);
            if(pair.Value!=ParameterReferenceBehavior.StrictExactlyOne&&((target.EditModes&FieldEditModes.Reference)==0
                ||definition is not IToolParameterBindingNames names||!names.ParameterBindingNames.ContainsKey(pair.Key)))
                throw new ArgumentException("Reference behavior requires a reference-enabled named parameter: "+pair.Key);
            result.Add(pair.Key,pair.Value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,ParameterReferenceBehavior>(result);
    }
    /// <summary>Validate a model against its current consumer; construction context never grants admission.</summary>
    public static void ValidateIndexSelections(BindingSelection selection,ParameterReferenceBehavior behavior)
    {
        if(selection is BindingSelection.AtField field)
        {
            if(!CanUseIndexSource(SelectedType(field.Reference),behavior))throw new ArgumentException("Dynamic index declaration is not allowed by this consumer.");
            ValidateIndexSelections(field.Reference.Selection,behavior);
        }
        else if(selection is BindingSelection.AtVariable variable)
        {
            if(variable.Reference.Selection is not BindingSelection.AllItems||!CanUseIndexSource(variable.Reference.Type,behavior))
                throw new ArgumentException("Variable index declaration is not allowed by this consumer.");
        }
    }
    internal static TypeRef SelectedType(OutputReference reference)
    {
        var type=reference.Type;var position=0;
        if(reference.Selection is not BindingSelection.AllItems)
        {
            while(type.Shape!=ValueShape.Sequence&&position<reference.MemberFieldPath.Count)type=Project(type,reference.MemberFieldPath[position++]);
            if(type.Shape!=ValueShape.Sequence)throw new ArgumentException("Index selection requires a sequence.");
            type=type.Element!;
        }
        while(position<reference.MemberFieldPath.Count)type=Project(type,reference.MemberFieldPath[position++]);
        return type;
    }
    private static TypeRef Project(TypeRef type,string member)
    {
        if(type.Shape==ValueShape.Sequence)return TypeRef.SequenceOf(Project(type.Element!,member),type.Count);
        return type.Shape==ValueShape.Record?type.Fields.SingleOrDefault(p=>p.Id==member)?.Type
            ??throw new ArgumentException("Index reference member is missing."):throw new ArgumentException("Index member projection requires a record.");
    }
}
