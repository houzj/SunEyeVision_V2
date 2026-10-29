using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using static SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public sealed record BlobGraphicDefinition(string Key, string Label, string StyleName, int Order,
    bool InputSource, ResultFieldPath Source, bool Visible, uint OkColor, uint NgColor,
    double Opacity, double LineWidth, double CrossSize);
public sealed record BlobTextDefinition(string Key, string Label, string StyleName, int Order,
    ResultFieldPath Source, bool Visible, uint OkColor, uint NgColor, double Opacity,
    double FontSize, bool Bold, double X, double Y, int DecimalPlaces, string Unit);

/// <summary>Display metadata refers to declared inputs/outputs, never CLR properties or algorithm callbacks.</summary>
public sealed class BlobDisplayDefinition
{
    public static BlobDisplayDefinition Instance { get; } = Load();
    public IReadOnlyList<BlobGraphicDefinition> Graphics { get; }
    public IReadOnlyList<BlobTextDefinition> Texts { get; }
    public ResultProjectionDescriptor Table { get; }

    private BlobDisplayDefinition(XElement root, ToolSchema schema)
    {
        Shape(root,["schemaVersion"],["Graphics","Texts","Table"]);
        if(root.Name!="BlobDisplay"||Required(root,"schemaVersion")!="1")throw new InvalidDataException("Unsupported Blob display metadata.");
        XElement Section(string name)
        {
            var elements=root.Elements(name).ToArray();
            return elements.Length==1?elements[0]:throw new InvalidDataException("Exactly one Blob display section is required: "+name);
        }
        var graphics=Section("Graphics");Shape(graphics,[],["Graphic"]);
        Graphics=Array.AsReadOnly(graphics.Elements().Select(item=>
        {
            Shape(item,["key","label","styleName","order","scope","port","member","visible","okColor","ngColor","opacity","lineWidth","crossSize"],[]);
            var scope=Required(item,"scope");
            if(scope is not ("Input" or "Output"))throw new InvalidDataException("Unknown Blob display source scope.");
            var source=Source(item);var input=scope=="Input";
            TypeRef type;
            if(input)
            {
                if(source.MemberFieldPath.Count!=0)throw new InvalidDataException("Input display source must be a declared region.");
                type=schema.Inputs.SingleOrDefault(p=>p.Id==source.PortId)?.Type??throw new InvalidDataException("Unknown Blob input display source.");
            }
            else type=ValidateSource(source,schema);
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape!=ValueShape.Geometry&&!(type.Shape==ValueShape.Resource&&type.Resource==ResourceKind.Region))
                throw new InvalidDataException("Blob graphic source must be declared geometry/region.");
            return new BlobGraphicDefinition(Required(item,"key"),Required(item,"label"),Required(item,"styleName"),Integer(item,"order",0,int.MaxValue),
                input,source,Boolean(item,"visible"),Color(item,"okColor"),Color(item,"ngColor"),Bounded(item,"opacity",0,1),
                Bounded(item,"lineWidth",1,10),Bounded(item,"crossSize",1,10000));
        }).OrderBy(x=>x.Order).ToArray());
        var texts=Section("Texts");Shape(texts,[],["Text"]);
        Texts=Array.AsReadOnly(texts.Elements().Select(item=>
        {
            Shape(item,["key","label","styleName","order","port","member","visible","okColor","ngColor","opacity","fontSize","bold","x","y","decimals","unit"],[]);
            var source=Source(item);var type=ValidateSource(source,schema);
            if(type.Shape!=ValueShape.Scalar)throw new InvalidDataException("Blob text source must be a published scalar field.");
            return new BlobTextDefinition(Required(item,"key"),Required(item,"label"),Required(item,"styleName"),Integer(item,"order",0,int.MaxValue),
                source,Boolean(item,"visible"),Color(item,"okColor"),Color(item,"ngColor"),Bounded(item,"opacity",0,1),Bounded(item,"fontSize",1,1000),
                Boolean(item,"bold"),Finite(item,"x"),Finite(item,"y"),Integer(item,"decimals",0,15),Attribute(item,"unit"));
        }).OrderBy(x=>x.Order).ToArray());
        Unique(Graphics.Select(x=>(x.Key,x.Order)),"graphic");Unique(Texts.Select(x=>(x.Key,x.Order)),"text");
        var table=Section("Table");Shape(table,["port","identity"],["Column"]);
        var port=Required(table,"port");
        Table=new(ResultProjectionKind.StandardTable,ResultCardinality.Many,table.Elements().Select(item=>
        {
            Shape(item,["key","label","member","decimals","unit"],[]);
            var source=new ResultFieldPath(port,[Required(item,"member")]);
            return new ResultColumnDescriptor(Required(item,"key"),Required(item,"label"),source,Attribute(item,"unit"),Integer(item,"decimals",0,15),
                new(port,["judgment-passed"]),new(port,["rotated-bounds"]),new(port,["region"]));
        }),new(port),Required(table,"identity"));
        var errors=Table.Validate(schema);
        if(errors.Count!=0||Table.Columns.Count==0)throw new InvalidDataException("Invalid Blob table mapping: "+string.Join("; ",errors.Select(x=>x.Message)));
    }

    private static BlobDisplayDefinition Load()
    {
        using var stream=typeof(BlobTool).Assembly.GetManifestResourceStream("BlobAnalysis.BlobDisplay.xml")
            ??throw new InvalidDataException("BlobDisplay.xml is missing from the Runtime component.");
        return Read(stream,new BlobTool().Describe());
    }
    public static BlobDisplayDefinition Read(Stream stream,ToolSchema schema)
    {
        ArgumentNullException.ThrowIfNull(stream);ArgumentNullException.ThrowIfNull(schema);
        try
        {
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});
            return new(XDocument.Load(reader).Root??throw new InvalidDataException("Empty Blob display metadata."),schema);
        }
        catch(Exception e)when(e is XmlException or ArgumentException)
        {throw new InvalidDataException("Invalid Blob display metadata.",e);}
    }
    private static string Attribute(XElement item,string name)=>(string?)item.Attribute(name)??throw new InvalidDataException("Missing Blob display attribute: "+name);
    private static ResultFieldPath Source(XElement item)
    {
        var member=Attribute(item,"member");
        return new(Required(item,"port"),member.Length==0?Array.Empty<string>():member.Split('/'));
    }
    private static TypeRef ValidateSource(ResultFieldPath source,ToolSchema schema)=>new OutputValueRequest(source.PortId,source.MemberFieldPath).Validate(schema);
    private static uint Color(XElement item,string name)
    {
        var color=Required(item,name);
        return color.Length==7&&color[0]=='#'&&uint.TryParse(color.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var rgb)
            ?0xFF000000u|rgb:throw new InvalidDataException("Invalid Blob RGB color: "+name);
    }
    private static double Bounded(XElement item,string name,double min,double max)
    {
        var value=Finite(item,name);
        return value>=min&&value<=max?value:throw new InvalidDataException("Invalid Blob display bounds: "+name);
    }
    private static void Unique(IEnumerable<(string Key,int Order)> entries,string kind)
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);var orders=new HashSet<int>();
        foreach(var entry in entries)
            if(!keys.Add(entry.Key)||!orders.Add(entry.Order))throw new InvalidDataException("Duplicate Blob "+kind+" identity/order.");
        if(keys.Count==0)throw new InvalidDataException("Missing Blob "+kind+" definitions.");
    }
}
