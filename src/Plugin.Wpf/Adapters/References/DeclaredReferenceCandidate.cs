using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

public sealed record DeclaredReferenceCandidate(string Label,string NodeId,string? ScopeId,Port Output,
    IReadOnlyList<string> MemberPath)
{
    public override string ToString()=>Label;
    public TypeRef SelectedType
    {
        get
        {
            var type=Output.Type;
            foreach(var member in MemberPath)type=Project(type,member);
            return type;
        }
    }
    private static TypeRef Project(TypeRef type,string member)
        =>type.Shape==ValueShape.Sequence?TypeRef.SequenceOf(Project(type.Element!,member),type.Count)
            :type.Shape==ValueShape.Record?type.Fields.Single(field=>field.Id==member).Type
            :throw new ArgumentException("Member projection requires a record declaration.");
    public OutputReference Reference(BindingSelection selection)=>new(NodeId,Output.Id,Output.Type,MemberPath,selection,ScopeId,Output.Storage);
}

