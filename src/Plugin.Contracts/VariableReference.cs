namespace SunEyeVision.Plugin.Contracts;

/// <summary>A solution-owned variable identity and expected binding declaration.
/// No node/result identity, current value, name, UI or runtime resource is persisted.</summary>
public sealed class VariableReference
{
    public VariableReference(string solutionId,string variableId,TypeRef type,BindingSelection? selection=null,NumericStorage? storage=null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(variableId);ArgumentNullException.ThrowIfNull(type);
        var element=type.Shape==ValueShape.Sequence?type.Element!:type;
        if(element.Shape!=ValueShape.Scalar && type!=TypeRef.ResourceOf(ResourceKind.Image))
            throw new ArgumentException("Variable references require a scalar, scalar sequence or single image declaration.");
        NumericStorageRules.ValidateDeclaration(new Port(variableId,type,Storage:storage));
        SolutionId=solutionId;VariableId=variableId;Type=type;Storage=NumericStorageRules.Effective(type,storage);Selection=selection??BindingSelection.All;
        if(Selection is not BindingSelection.AllItems&&type.Shape!=ValueShape.Sequence)
            throw new ArgumentException("Variable index selection requires a sequence declaration.");
    }
    public string VariableId {get;}
    public string SolutionId {get;}
    public TypeRef Type {get;}
    public NumericStorage? Storage {get;}
    public BindingSelection Selection {get;}
}
public sealed record VariableSetting(VariableReference Reference,BindingConversion Conversion=BindingConversion.Automatic,
    string? RebindingReason=null):ParameterSetting;
