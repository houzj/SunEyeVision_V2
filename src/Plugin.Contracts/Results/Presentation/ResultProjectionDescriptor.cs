namespace SunEyeVision.Plugin.Contracts.Results.Presentation;

public enum ResultProjectionKind { None, PropertyList, StandardTable, Renderer }
public enum ResultCardinality { One, Many }

/// <summary>Schema-relative existing Port/record identities, not a second type system or runtime object path.</summary>
public sealed class ResultFieldPath
{
    public ResultFieldPath(string portId,IEnumerable<string>? memberFieldPath=null)
    {
        if(string.IsNullOrWhiteSpace(portId))throw new ArgumentException("A declared port is required.");
        PortId=portId;var members=(memberFieldPath??Array.Empty<string>()).ToArray();
        if(members.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Record member identities cannot be empty.");
        MemberFieldPath=Array.AsReadOnly(members);
    }
    public string PortId{get;} public IReadOnlyList<string> MemberFieldPath{get;}
    internal TypeRef Resolve(ToolSchema schema)
    {
        var type=schema.Outputs.SingleOrDefault(port=>port.Id==PortId)?.Type??throw new ArgumentException("Projection output is not declared.");
        foreach(var member in MemberFieldPath)type=Project(type,member);
        return type;
    }
    private static TypeRef Project(TypeRef type,string member)
    {
        if(type.Shape==ValueShape.Sequence)return TypeRef.SequenceOf(Project(type.Element!,member),type.Count);
        if(type.Shape!=ValueShape.Record)throw new ArgumentException("Projection can traverse only declared record members.");
        return type.Fields.SingleOrDefault(field=>field.Id==member)?.Type??throw new ArgumentException("Projection member is not declared.");
    }
}
public sealed record ResultColumnDescriptor(string Key,string DisplayName,ResultFieldPath Value,
    string Unit="",int? DecimalPlaces=null,ResultFieldPath? ValidWhen=null,ResultFieldPath? Geometry=null);

/// <summary>Bounded display declaration. Reads a published snapshot; never runs an algorithm or recomputes judgments.</summary>
public sealed class ResultProjectionDescriptor
{
    public ResultProjectionDescriptor(ResultProjectionKind kind,ResultCardinality cardinality,
        IEnumerable<ResultColumnDescriptor>? columns=null,ResultFieldPath? rows=null,string? elementIdentityFieldId=null,string? rendererKey=null)
    {
        if(!Enum.IsDefined(kind)||!Enum.IsDefined(cardinality))throw new ArgumentException("Unknown projection kind or cardinality.");
        Kind=kind;Cardinality=cardinality;Rows=rows;ElementIdentityFieldId=elementIdentityFieldId;RendererKey=rendererKey;
        Columns=Array.AsReadOnly((columns??Array.Empty<ResultColumnDescriptor>()).ToArray());
        if(Columns.Any(column=>column is null||string.IsNullOrWhiteSpace(column.Key)||column.Value is null||column.DisplayName is null
            ||column.DecimalPlaces is <0 or >15)||Columns.Select(column=>column.Key).Distinct(StringComparer.Ordinal).Count()!=Columns.Count)
            throw new ArgumentException("Columns require unique keys, declared values and bounded formatting.");
        if(kind==ResultProjectionKind.None&&(Columns.Count!=0||rows is not null||elementIdentityFieldId is not null||rendererKey is not null))
            throw new ArgumentException("None has no display sources.");
        if(kind!=ResultProjectionKind.None&&cardinality==ResultCardinality.Many&&(rows is null||string.IsNullOrWhiteSpace(elementIdentityFieldId)))
            throw new ArgumentException("Collection rows require an explicit declared source and stable element identity.");
        if(cardinality==ResultCardinality.One&&(rows is not null||elementIdentityFieldId is not null))throw new ArgumentException("One-row projection reads scalar output sources directly.");
        if((kind==ResultProjectionKind.Renderer)!=!string.IsNullOrWhiteSpace(rendererKey))throw new ArgumentException("Only registered renderer projections declare a renderer key.");
    }
    public ResultProjectionKind Kind{get;} public ResultCardinality Cardinality{get;} public IReadOnlyList<ResultColumnDescriptor> Columns{get;}
    public ResultFieldPath? Rows{get;} public string? ElementIdentityFieldId{get;} public string? RendererKey{get;}
    public IReadOnlyList<Diagnostic> Validate(ToolSchema schema)
    {
        var issues=new List<Diagnostic>();
        try
        {
            if(Rows is not null)
            {
                var rows=Rows.Resolve(schema);
                if(rows.Shape!=ValueShape.Sequence||rows.Element!.Shape!=ValueShape.Record)throw new ArgumentException("Collection rows require a declared sequence of records.");
                var identity=rows.Element.Fields.SingleOrDefault(field=>field.Id==ElementIdentityFieldId);
                if(identity is null||!identity.Required||identity.Nullable||identity.Type.Shape!=ValueShape.Scalar
                    ||identity.Type.Scalar is not (ScalarKind.Int64 or ScalarKind.Text))throw new ArgumentException("Rows require a present immutable integer/text element identity; row number is not identity.");
            }
            TypeRef ResolveCell(ResultFieldPath path)
            {
                var type=path.Resolve(schema);
                if(Rows is null)return type;
                if(path.PortId!=Rows.PortId||!path.MemberFieldPath.Take(Rows.MemberFieldPath.Count).SequenceEqual(Rows.MemberFieldPath)
                    ||type.Shape!=ValueShape.Sequence)throw new ArgumentException("Each row cell must refer to the same declared row collection.");
                return type.Element!;
            }
            foreach(var column in Columns)
            {
                var cell=ResolveCell(column.Value);
                // StandardTable One is exactly one scalar per column. Complex declared values use
                // PropertyList/tree or a registered Renderer, never an implicit first sequence item.
                if(Kind==ResultProjectionKind.StandardTable&&Cardinality==ResultCardinality.One&&cell.Shape!=ValueShape.Scalar)
                    throw new ArgumentException("One-row standard table cells must be scalar; use a declared property/renderer view for complex values.");
                if(column.ValidWhen is not null&&ResolveCell(column.ValidWhen)!=TypeRef.ScalarOf(ScalarKind.Boolean))throw new ArgumentException("Validity is a published Boolean field, not a callback or business calculation.");
                if(column.Geometry is not null&&!ContainsGeometry(ResolveCell(column.Geometry)))throw new ArgumentException("Geometry source must contain declared geometry or a Region resource.");
            }
        }
        catch(ArgumentException exception){issues.Add(new("result-projection",exception.Message));}
        return issues.AsReadOnly();
    }
    private static bool ContainsGeometry(TypeRef type)=>type.Shape==ValueShape.Geometry
        ||type.Shape==ValueShape.Resource&&type.Resource==ResourceKind.Region
        ||type.Element is not null&&ContainsGeometry(type.Element)||type.Fields.Any(field=>ContainsGeometry(field.Type));
}
