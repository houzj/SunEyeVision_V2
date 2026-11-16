namespace SunEyeVision.Plugin.Contracts;

/// <summary>Immutable captions for declared output identities, independent of values and compatibility.</summary>
/// <remarks>
/// 一条显示行指向「目标端口 + 该端口内的成员路径」：根节点默认指向 <see cref="FieldId"/> 同名端口，
/// 子节点默认继承父节点端口并追加自己的 <see cref="FieldId"/>；需要跨端口分组时显式给出 <see cref="PortId"/>。
/// 每行显示名、顺序与分组都来自本声明，端口声明序不再是结果树的显示顺序。
/// </remarks>
public sealed class OutputDisplayNode
{
    public OutputDisplayNode(string fieldId,string displayName,IEnumerable<OutputDisplayNode>? children=null,bool isAuxiliaryImage=false,bool displayInResultPanel=true,bool isModuleExecutionSummary=false,string? portId=null,IEnumerable<string>? memberPath=null)
    {
        if(string.IsNullOrWhiteSpace(fieldId)||string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Output display requires a field identity and a nonempty caption.");
        if(portId is not null&&string.IsNullOrWhiteSpace(portId))
            throw new ArgumentException("Output display member row requires a declared target port.",nameof(portId));
        var members=(memberPath??Array.Empty<string>()).ToArray();
        if(members.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Output display member path requires nonempty member identities.",nameof(memberPath));
        if(portId is null&&members.Length!=0)
            throw new ArgumentException("Output display member path requires a declared target port.",nameof(memberPath));
        FieldId=fieldId;DisplayName=displayName;
        Children=Freeze(children??Array.Empty<OutputDisplayNode>());
        IsAuxiliaryImage=isAuxiliaryImage;
        DisplayInResultPanel=displayInResultPanel;
        IsModuleExecutionSummary=isModuleExecutionSummary;
        PortId=portId;
        MemberPath=Array.AsReadOnly(members);
    }
    public string FieldId{get;}
    public string DisplayName{get;}
    /// <summary>显式目标端口；为空表示继承父节点端口（顶层节点则为自己同名端口）。</summary>
    public string? PortId{get;}
    /// <summary>相对目标端口的成员路径；允许在路径中先解包序列再进入记录成员。</summary>
    public IReadOnlyList<string> MemberPath{get;}
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
    /// <summary>解析成员声明：记录字段，或显示行使用的几何属性（多边形顶点数/坐标）。</summary>
    public static TypeRef ResolveMember(TypeRef declaration,string member)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if(string.IsNullOrWhiteSpace(member))throw new ArgumentException("Output member identity is required.",nameof(member));
        var type=declaration;while(type.Shape==ValueShape.Sequence)type=type.Element!;
        if(type.Shape==ValueShape.Record)
            return type.Fields.SingleOrDefault(field=>field.Id==member)?.Type
                ??throw new ArgumentException("Output caption addresses an undeclared member: "+member);
        if(type.Shape==ValueShape.Geometry)
            return type.Geometry switch
            {
                GeometryKind.Polygon when member=="count"=>DataType.Integer,
                GeometryKind.Polygon when member is "x" or "y"=>DataType.NumberArray,
                GeometryKind.Point or GeometryKind.OrientedRectangle or GeometryKind.Ellipse when member is "center-x" or "center-y" or "width" or "height" or "angle-degrees"=>DataType.Number,
                _=>throw new ArgumentException("Output caption addresses an unsupported geometry member: "+member)
            };
        throw new ArgumentException("Output caption members require a declared record or geometry: "+member);
    }
    internal static void Validate(IReadOnlyList<OutputDisplayNode> nodes,IReadOnlyList<Port> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        foreach(var node in nodes)Validate(node,ports,null,Array.Empty<string>(),true);
    }
    private static void Validate(OutputDisplayNode node,IReadOnlyList<Port> ports,Port? inheritedPort,IReadOnlyList<string> inheritedPath,bool root)
    {
        var port=node.PortId is {} declared
            ? ports.SingleOrDefault(item=>item.Id==declared)
                ??throw new ArgumentException("Output caption addresses an undeclared port: "+declared)
            : inheritedPort
                ??ports.SingleOrDefault(item=>item.Id==node.FieldId)
                ??throw new ArgumentException("Output caption addresses an undeclared field: "+node.FieldId);
        var path=node.PortId is not null?node.MemberPath.ToArray():root?Array.Empty<string>():inheritedPath.Append(node.FieldId).ToArray();
        var type=port.Type;
        foreach(var member in path)type=ResolveMember(type,member);
        foreach(var child in node.Children)Validate(child,ports,port,path,false);
    }
}
