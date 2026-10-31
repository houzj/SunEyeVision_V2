namespace SunEyeVision.Plugin.Contracts;

/// <summary>Immutable captions for declared output identities, independent of values and compatibility.</summary>
public sealed class OutputDisplayNode
{
    public OutputDisplayNode(string fieldId,string displayName,IEnumerable<OutputDisplayNode>? children=null)
    {
        if(string.IsNullOrWhiteSpace(fieldId)||string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Output display requires a field identity and a nonempty caption.");
        FieldId=fieldId;DisplayName=displayName;
        Children=Freeze(children??Array.Empty<OutputDisplayNode>());
    }
    public string FieldId{get;}
    public string DisplayName{get;}
    public IReadOnlyList<OutputDisplayNode> Children{get;}
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
