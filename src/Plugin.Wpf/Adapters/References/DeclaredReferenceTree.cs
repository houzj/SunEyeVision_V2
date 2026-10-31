using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>Declaration paths form the tree; captions never determine identity or binding.</summary>
internal static class DeclaredReferenceTree
{
    private sealed class Branch(string name)
    {
        internal readonly string Name=name;
        internal readonly Dictionary<string,Branch> Children=new(StringComparer.Ordinal);
        internal DeclaredReferenceCandidate? Candidate;
        internal ReferenceTreeNode Freeze()=>new(Name,
            Array.AsReadOnly(Children.Values.Select(child=>child.Freeze()).ToArray()),
            Children.Count==0?Candidate:null);
    }
    internal static IReadOnlyList<ReferenceTreeNode> Build(IEnumerable<DeclaredReferenceCandidate> candidates,bool includeClear)
    {
        var roots=new List<ReferenceTreeNode>();
        if(includeClear)roots.Add(new("无",Array.Empty<ReferenceTreeNode>(),Clear:true));
        foreach(var group in candidates.GroupBy(candidate=>(candidate.ScopeId,candidate.NodeId)).OrderBy(group=>group.First().NodeDisplayName))
        {
            var outputs=new Dictionary<string,Branch>(StringComparer.Ordinal);
            foreach(var candidate in group)
            {
                if(!outputs.TryGetValue(candidate.Output.Id,out var branch))
                    outputs.Add(candidate.Output.Id,branch=new(candidate.OutputDisplayName));
                for(var i=0;i<candidate.MemberPath.Count;i++)
                {
                    var id=candidate.MemberPath[i];
                    if(!branch.Children.TryGetValue(id,out var child))
                        branch.Children.Add(id,child=new(candidate.MemberDisplayNames.Count==0?id:candidate.MemberDisplayNames[i]));
                    branch=child;
                }
                if(branch.Candidate is not null)throw new ArgumentException("来源声明包含重复的输出成员身份。");
                branch.Candidate=candidate;
            }
            roots.Add(new(group.First().NodeDisplayName,Array.AsReadOnly(outputs.Values.Select(branch=>branch.Freeze()).ToArray())));
        }
        return roots.AsReadOnly();
    }
    internal static IEnumerable<ReferenceTreeNode> Descendants(IEnumerable<ReferenceTreeNode> roots)
    {
        foreach(var node in roots)
        {
            yield return node;
            foreach(var child in Descendants(node.EnumEntrysA))yield return child;
        }
    }
    internal static bool Matches(DeclaredReferenceCandidate candidate,Port target,BindingConversion conversion,bool allowIndex)
        =>MatchesKind(candidate.SelectedType,target.Type,conversion)
            ||allowIndex&&candidate.SelectedType.Shape==ValueShape.Sequence
                &&MatchesKind(candidate.SelectedType.Element!,target.Type,conversion);
    internal static bool Matches(VariableReference candidate,Port target,BindingConversion conversion,bool allowIndex)
        =>MatchesKind(candidate.Type,target.Type,conversion)
            &&(candidate.Type.Shape!=ValueShape.Sequence||allowIndex||CanAdapt(candidate.Type,target.Type,conversion));
    internal static bool RequiresSelectionRepair(TypeRef type,Port target,BindingSelection selection,BindingConversion conversion)
    {
        if(type.Shape!=ValueShape.Sequence)return false;
        var valid=selection is BindingSelection.AllItems?CanAdapt(type,target.Type,conversion):
            (selection is not BindingSelection.AtConstant index||type.Count.Maximum is not {} maximum||index.Index<maximum)
                &&type.Count.Maximum!=0&&CanAdapt(type.Element!,target.Type,conversion);
        return !valid&&(CanAdapt(type,target.Type,conversion)
            ||type.Count.Maximum!=0&&CanAdapt(type.Element!,target.Type,conversion));
    }
    private static bool CanAdapt(TypeRef type,TypeRef target,BindingConversion conversion)
        =>MatchesKind(type,target,conversion)
            &&(type.Shape==ValueShape.Sequence?type.Count:CountConstraint.Exactly(1))
                .CompatibilityWith(target.Shape==ValueShape.Sequence?target.Count:CountConstraint.Exactly(1))!=BindingCompatibility.Incompatible;
    private static bool MatchesKind(TypeRef type,TypeRef target,BindingConversion conversion)
    {
        if(type.Shape==ValueShape.Sequence)type=type.Element!;
        var expected=target.Shape==ValueShape.Sequence?target.Element!:target;
        if(type.Shape!=expected.Shape)return false;
        return type.Shape switch
        {
            ValueShape.Scalar=>type.Scalar==expected.Scalar
                ||conversion==BindingConversion.Float64ToInt64Checked&&type.Scalar==ScalarKind.Float64&&expected.Scalar==ScalarKind.Int64,
            ValueShape.Resource=>type.Resource==expected.Resource,
            ValueShape.Geometry=>type.Geometry==expected.Geometry,
            _=>type==expected
        };
    }
}
