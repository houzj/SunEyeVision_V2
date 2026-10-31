using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>One immutable read of source and index declarations. This is not an execution/value snapshot.</summary>
public sealed class ReferenceCandidates
{
    public IReadOnlyList<DeclaredReferenceCandidate> Sources {get;}
    public IReadOnlyList<DeclaredReferenceCandidate> Indices {get;}
    public IReadOnlyList<VariableSelectionItem> Variables {get;}
    public static ReferenceCandidates Empty {get;}=new(Array.Empty<DeclaredReferenceCandidate>(),Array.Empty<DeclaredReferenceCandidate>());
    public ReferenceCandidates(IEnumerable<DeclaredReferenceCandidate> sources,IEnumerable<DeclaredReferenceCandidate> indices,IEnumerable<VariableSelectionItem>? variables=null)
    {
        Sources=Freeze(sources);Indices=Freeze(indices);
        var rows=(variables??Array.Empty<VariableSelectionItem>()).ToArray();
        if(rows.Any(row=>row is null||row.Reference is null||string.IsNullOrWhiteSpace(row.Name))
            ||rows.Select(row=>(row.Reference.SolutionId,row.Reference.VariableId)).Distinct().Count()!=rows.Length)
            throw new ArgumentException("变量候选缺少身份、显示名称或存在重复身份。");
        Variables=Array.AsReadOnly(rows);
        if(Indices.Any(candidate=>candidate.Output.Type.Shape==ValueShape.Sequence||candidate.SelectedType!=DataType.Integer))
            throw new ArgumentException("索引候选必须声明为可选成单项的整数来源。");
    }
    private static IReadOnlyList<DeclaredReferenceCandidate> Freeze(IEnumerable<DeclaredReferenceCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return Array.AsReadOnly(candidates.Select(candidate=>
        {
            ArgumentNullException.ThrowIfNull(candidate);
            return candidate.Freeze();
        }).ToArray());
    }
}
