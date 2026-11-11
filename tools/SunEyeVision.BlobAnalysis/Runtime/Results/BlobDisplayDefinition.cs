using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using static SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Declared Blob table metadata. Runtime appearance belongs only to BlobResultDisplay fields.</summary>
public sealed class BlobDisplayDefinition
{
    public static BlobDisplayDefinition Instance { get; } = Load();
    public ResultProjectionDescriptor Table { get; }

    private BlobDisplayDefinition(XElement root, ToolSchema schema)
    {
        Shape(root,["schemaVersion"],["Table"]);
        if(root.Name!="BlobDisplay"||Required(root,"schemaVersion")!="1")throw new InvalidDataException("Unsupported Blob display metadata.");
        XElement Section(string name)
        {
            var elements=root.Elements(name).ToArray();
            return elements.Length==1?elements[0]:throw new InvalidDataException("Exactly one Blob display section is required: "+name);
        }
        var table=Section("Table");Shape(table,["port","identity"],["Column"]);
        var port=Required(table,"port");
        Table=new(ResultProjectionKind.StandardTable,ResultCardinality.Many,table.Elements().Select(item=>
        {
            Shape(item,["key","label","labelResource","member","numericFormat","unavailable"],[]);
            if(item.Attribute("unavailable") is {} unavailable)
            {
                if(string.IsNullOrWhiteSpace(unavailable.Value)||item.Attribute("member") is not null||item.Attribute("numericFormat") is not null)
                    throw new InvalidDataException("An unavailable display column cannot declare an output member or numeric format.");
                return new ResultColumnDescriptor(Required(item,"key"),Required(item,"label"),null,
                    UnavailableReason:unavailable.Value,DisplayNameResourceKey:(string?)item.Attribute("labelResource"));
            }
            var source=new ResultFieldPath(port,Required(item,"member").Split('/'));
            var useSinglePrecision=(string?)item.Attribute("numericFormat") switch
            {
                null=>false,
                "float32"=>true,
                _=>throw new InvalidDataException("Unsupported Blob numeric display format.")
            };
            return new ResultColumnDescriptor(Required(item,"key"),Required(item,"label"),source,
                Geometry:new(port,["rotated-bounds"]),GeometrySource:new(port,["region"]),UseSinglePrecision:useSinglePrecision,
                DisplayNameResourceKey:(string?)item.Attribute("labelResource"));
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
}
