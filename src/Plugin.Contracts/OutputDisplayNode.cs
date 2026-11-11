namespace SunEyeVision.Plugin.Contracts;

/// <summary>Immutable captions for declared output identities, independent of values and compatibility.</summary>
public sealed class OutputDisplayNode
{
    public OutputDisplayNode(string fieldId,string displayName,IEnumerable<OutputDisplayNode>? children=null,bool isAuxiliaryImage=false,bool displayInResultPanel=true,bool isModuleExecutionSummary=false)
    {
        if(string.IsNullOrWhiteSpace(fieldId)||string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Output display requires a field identity and a nonempty caption.");
        FieldId=fieldId;DisplayName=displayName;
        Children=Freeze(children??Array.Empty<OutputDisplayNode>());
        IsAuxiliaryImage=isAuxiliaryImage;
        DisplayInResultPanel=displayInResultPanel;
        IsModuleExecutionSummary=isModuleExecutionSummary;
    }
    public string FieldId{get;}
    public string DisplayName{get;}
    public IReadOnlyList<OutputDisplayNode> Children{get;}
    /// <summary>Display after ordinary images and exclude from the owning node's default image choice.</summary>
    public bool IsAuxiliaryImage{get;}
    /// <summary>Controls result-panel presentation only; the declared value remains readable.</summary>
    public bool DisplayInResultPanel{get;}
    /// <summary>该输出是否承载原件工具的模块状态/模块耗时摘要，宿主不再重复追加执行摘要。</summary>
    public bool IsModuleExecutionSummary{get;}
    public static IReadOnlyList<OutputDisplayNode> Freeze(IEnumerable<OutputDisplayNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);var copy=nodes.ToArray();
        if(copy.Any(node=>node is null)||copy.Select(node=>node.FieldId).Distinct(StringComparer.Ordinal).Count()!=copy.Length)
            throw new ArgumentException("Output display siblings require unique declared identities.");
        return Array.AsReadOnly(copy);
    }
    internal static void Validate(IReadOnlyList<OutputDisplayNode> nodes,IReadOnlyList<Port> ports)
    {
        foreach(var node in nodes)
        {
            var port=ports.SingleOrDefault(port=>port.Id==node.FieldId)
                ??throw new ArgumentException("Output caption addresses an undeclared field: "+node.FieldId);
            if(node.Children.Count==0)continue;
            var type=port.Type;while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape!=ValueShape.Record)throw new ArgumentException("Output caption members require a declared record: "+port.Id);
            Validate(node.Children,type.Fields);
        }
    }
}
