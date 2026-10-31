using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

public sealed record DeclaredReferenceCandidate(string Label,string NodeId,string? ScopeId,Port Output,
    IReadOnlyList<string> MemberPath)
{
    /// <summary>Display metadata only; grouping and binding retain ScopeId/NodeId identity.</summary>
    public string NodeDisplayName { get; init; } = NodeId;
    public string OutputDisplayName { get; init; } = Label;
    public IReadOnlyList<string> MemberDisplayNames { get; init; } = Array.Empty<string>();
    internal DeclaredReferenceCandidate Freeze()
    {
        ArgumentNullException.ThrowIfNull(MemberPath);
        ArgumentNullException.ThrowIfNull(MemberDisplayNames);
        if(string.IsNullOrWhiteSpace(NodeDisplayName)||string.IsNullOrWhiteSpace(OutputDisplayName))
            throw new ArgumentException("来源节点及输出显示名称不能为空。");
        if(MemberDisplayNames.Count!=0&&MemberDisplayNames.Count!=MemberPath.Count
            ||MemberDisplayNames.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("来源成员显示路径与声明身份不匹配。");
        var frozen=this with{MemberPath=Array.AsReadOnly(MemberPath.ToArray()),
            MemberDisplayNames=Array.AsReadOnly(MemberDisplayNames.ToArray())};
        _=frozen.SelectedType;_=frozen.SelectedStorage;_=frozen.Reference(BindingSelection.All);
        return frozen;
    }
    public override string ToString()=>Label;
    public NumericStorage? SelectedStorage=>ProjectStorage(Output.Type,Output.Storage,0).Storage;
    private (TypeRef Type,NumericStorage? Storage) ProjectStorage(TypeRef type,NumericStorage? storage,int position)
    {
        if(position==MemberPath.Count)return(type,NumericStorageRules.Effective(type,storage));
        if(type.Shape==ValueShape.Sequence)
        {
            var projected=ProjectStorage(type.Element!,storage,position);
            return(TypeRef.SequenceOf(projected.Type,type.Count),projected.Storage);
        }
        if(type.Shape!=ValueShape.Record)throw new ArgumentException("Member projection requires a record declaration.");
        var member=type.Fields.Single(field=>field.Id==MemberPath[position]);
        return ProjectStorage(member.Type,member.Storage,position+1);
    }
    public TypeRef SelectedType
    {
        get
        {
            return ProjectType(Output.Type,MemberPath);
        }
    }
    internal static TypeRef ProjectType(TypeRef type,IReadOnlyList<string> members)
    {
        foreach(var member in members)type=Project(type,member);
        return type;
    }
    private static TypeRef Project(TypeRef type,string member)
        =>type.Shape==ValueShape.Sequence?TypeRef.SequenceOf(Project(type.Element!,member),type.Count)
            :type.Shape==ValueShape.Record?type.Fields.Single(field=>field.Id==member).Type
            :throw new ArgumentException("Member projection requires a record declaration.");
    public OutputReference Reference(BindingSelection selection)=>new(NodeId,Output.Id,Output.Type,MemberPath,selection,ScopeId,Output.Storage);
}

