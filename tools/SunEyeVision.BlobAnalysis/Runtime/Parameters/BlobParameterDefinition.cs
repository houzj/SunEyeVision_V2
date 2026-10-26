using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Versioned tool metadata. It initializes new settings, never patches saved settings.</summary>
public sealed class BlobParameterDefinition
{
    private readonly IReadOnlyDictionary<string, XElement> _fields;
    private readonly XElement[] _judgments;
    public static BlobParameterDefinition Instance { get; } = Load();
    private BlobParameterDefinition(XElement root)
    {
        if(root.Name!="BlobParameters"||(string?)root.Attribute("schemaVersion")!="1")
            throw new InvalidDataException("Unsupported Blob parameter metadata.");
        Shape(root,["schemaVersion"],["Group","Judgments"]);
        foreach(var group in root.Elements("Group"))
        {
            Shape(group,["page","section"],["Parameter"]);
            Required(group,"page");Required(group,"section");
            if(!group.HasElements)throw new InvalidDataException("Empty Blob parameter group.");
            foreach(var field in group.Elements())
            {
                Shape(field,["id","type","default","label","minimum","maximum","decimals","increment"],[]);
                Required(field,"id");Required(field,"type");Required(field,"default");Required(field,"label");
            }
        }
        var ids=root.Elements("Group").Elements("Parameter").Select(x=>Required(x,"id")).ToArray();
        if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)throw new InvalidDataException("Duplicate Blob parameter identity.");
        _fields=new ReadOnlyDictionary<string,XElement>(root.Elements("Group").Elements("Parameter")
            .ToDictionary(x=>Required(x,"id"),x=>x,StringComparer.Ordinal));
        var declared=typeof(BlobSettings).GetProperties(BindingFlags.Public|BindingFlags.Instance)
            .Where(p=>p.Name!=nameof(BlobSettings.Judgments)).ToDictionary(p=>p.Name,p=>p.PropertyType,StringComparer.Ordinal);
        if(!_fields.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(declared.Keys))
            throw new InvalidDataException("Blob XML fields differ from the settings model.");
        foreach(var (id,type) in declared)
        {
            var expected=type==typeof(int)?"Integer":type==typeof(double)?"Number":type==typeof(bool)?"Boolean":type.IsEnum?"Enum":throw new InvalidDataException("Unsupported Blob setting type.");
            if(Required(_fields[id],"type")!=expected)throw new InvalidDataException("Blob XML field type differs: "+id);
            var minimum=Minimum(id);var maximum=Maximum(id);var step=Increment(id);
            if(minimum>maximum||!double.IsFinite(step)||step<=0)throw new InvalidDataException("Invalid Blob editor bounds: "+id);
            var raw=Required(_fields[id],"default");
            try
            {
                var value=type.IsEnum?Enum.Parse(type,raw,false):Convert.ChangeType(raw,type,CultureInfo.InvariantCulture);
                if(type.IsEnum&&!Enum.IsDefined(type,value))throw new InvalidDataException("Unknown Blob enum default: "+id);
                if(type==typeof(int)||type==typeof(double))
                {
                    var number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
                    if(minimum is null||maximum is null||!double.IsFinite(number)||number<minimum||number>maximum)
                        throw new InvalidDataException("Invalid Blob numeric default/bounds: "+id);
                    Required(_fields[id],"increment");Required(_fields[id],"decimals");
                    var decimals=DecimalPlaces(id);
                    if(type==typeof(int)&&(decimals!=0||minimum<int.MinValue||maximum>int.MaxValue
                        ||minimum!=Math.Truncate(minimum.Value)||maximum!=Math.Truncate(maximum.Value)||step!=Math.Truncate(step)))
                        throw new InvalidDataException("Nonintegral Blob integer metadata: "+id);
                }
                else if(_fields[id].Attributes().Any(a=>a.Name.LocalName is "minimum" or "maximum" or "decimals" or "increment"))
                    throw new InvalidDataException("Numeric metadata on nonnumeric Blob field: "+id);
            }
            catch(Exception e)when(e is FormatException or OverflowException or ArgumentException)
            {throw new InvalidDataException("Invalid Blob default: "+id,e);}
        }
        if(root.Elements("Judgments").Count()!=1)throw new InvalidDataException("Exactly one Blob Judgments element is required.");
        Shape(root.Element("Judgments")!,[],["Judgment"]);
        _judgments=root.Element("Judgments")?.Elements("Judgment").ToArray()
            ??throw new InvalidDataException("Blob judgment definitions are missing.");
        if(_judgments.Select(x=>Required(x,"key")).Distinct(StringComparer.Ordinal).Count()!=_judgments.Length)
            throw new InvalidDataException("Duplicate Blob judgment identities.");
        var orders=new HashSet<int>();
        foreach(var item in _judgments)
        {
            Shape(item,["key","name","kind","perItem","enabled","minimum","maximum","allowedMinimum","allowedMaximum","unit","decimals","order"],[]);
            var key=Required(item,"key");Required(item,"name");
            var perItem=Boolean(item,"perItem");Boolean(item,"enabled");
            if(!Enum.TryParse<BlobJudgmentKind>(Required(item,"kind"),false,out var kind)||!Enum.IsDefined(kind))
                throw new InvalidDataException("Unknown Blob judgment kind: "+key);
            var expectedPerItem=key is "per-blob-area" or "per-blob-circularity" or "per-blob-rectangularity";
            var known=expectedPerItem||key is "output-not-empty" or "white-ratio" or "blob-count" or "total-area" or "average-area"
                or "max-area" or "min-area" or "average-circularity" or "average-rectangularity";
            if(!known||perItem!=expectedPerItem||kind!=(key=="output-not-empty"?BlobJudgmentKind.Boolean:BlobJudgmentKind.Range))
                throw new InvalidDataException("Blob judgment identity/scope/kind mismatch: "+key);
            var min=Finite(item,"minimum");var max=Finite(item,"maximum");
            if(Finite(item,"allowedMinimum")>min||min>max||max>Finite(item,"allowedMaximum"))
                throw new InvalidDataException("Invalid Blob judgment bounds: "+key);
            Integer(item,"decimals",0,15);
            if(!orders.Add(Integer(item,"order",0,int.MaxValue)))throw new InvalidDataException("Duplicate Blob judgment order.");
            if(item.Attribute("unit") is null)throw new InvalidDataException("Missing Blob judgment unit: "+key);
        }
        if(_judgments.Length!=12)throw new InvalidDataException("Missing Blob judgment definitions.");
    }
    private static BlobParameterDefinition Load()
    {
        using var stream=typeof(BlobSettings).Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")
            ??throw new InvalidDataException("BlobParameters.xml is missing from the Runtime component.");
        return Read(stream);
    }
    public static BlobParameterDefinition Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});
            return new(XDocument.Load(reader).Root??throw new InvalidDataException("Empty Blob parameter metadata."));
        }
        catch(XmlException e){throw new InvalidDataException("Malformed Blob parameter XML.",e);}
    }
    internal static void Shape(XElement element,string[] attributes,string[] children)
    {
        if(element.Name.NamespaceName.Length!=0||element.Attributes().Any(a=>a.Name.NamespaceName.Length!=0||!attributes.Contains(a.Name.LocalName))
            ||element.Elements().Any(e=>e.Name.NamespaceName.Length!=0||!children.Contains(e.Name.LocalName))
            ||element.Nodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)))
            throw new InvalidDataException("Unknown Blob metadata element/attribute/content: "+element.Name);
    }
    internal static string Required(XElement element,string name)=>(string?)element.Attribute(name) is {} value&&!string.IsNullOrWhiteSpace(value)
        ?value:throw new InvalidDataException("Missing/empty Blob XML attribute: "+name);
    internal static double Finite(XElement element,string name)=>double.TryParse(Required(element,name),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)
        ?n:throw new InvalidDataException("Invalid finite Blob number: "+name);
    internal static int Integer(XElement element,string name,int min,int max)=>int.TryParse(Required(element,name),NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)&&n>=min&&n<=max
        ?n:throw new InvalidDataException("Invalid Blob integer: "+name);
    internal static bool Boolean(XElement element,string name)=>bool.TryParse(Required(element,name),out var value)
        ?value:throw new InvalidDataException("Invalid Blob Boolean: "+name);
    public bool Contains(string id)=>_fields.ContainsKey(id);
    public string Label(string id)=>Required(_fields[id],"label");
    public string Page(string id)=>Required(_fields[id].Parent!,"page");
    public string Section(string id)=>Required(_fields[id].Parent!,"section");
    public T Default<T>(string id)
    {
        if(typeof(BlobSettings).GetProperty(id)?.PropertyType!=typeof(T))throw new InvalidDataException("Requested Blob default type differs: "+id);
        var value=Required(_fields[id],"default");
        if(typeof(T).IsEnum)
        {
            var parsed=Enum.Parse(typeof(T),value,false);
            if(!Enum.IsDefined(typeof(T),parsed))throw new InvalidDataException("Unknown Blob enum default: "+id);
            return (T)parsed;
        }
        var result=(T)Convert.ChangeType(value,typeof(T),CultureInfo.InvariantCulture);
        if(result is double number&&!double.IsFinite(number))throw new InvalidDataException("Non-finite Blob default: "+id);
        return result;
    }
    private double? Number(string id,string attribute)
    {
        if(_fields[id].Attribute(attribute) is not {} item)return null;
        return Finite(_fields[id],attribute);
    }
    public double? Minimum(string id)=>Number(id,"minimum");
    public double? Maximum(string id)=>Number(id,"maximum");
    public int DecimalPlaces(string id)=>_fields[id].Attribute("decimals") is null?0:Integer(_fields[id],"decimals",0,15);
    public double Increment(string id)=>Number(id,"increment")??1;
    public IReadOnlyList<BlobJudgment> CreateJudgments()=>Array.AsReadOnly(_judgments.Select(x=>new BlobJudgment(
        Required(x,"key"),bool.Parse(Required(x,"perItem")),bool.Parse(Required(x,"enabled")),
        double.Parse(Required(x,"minimum"),CultureInfo.InvariantCulture),double.Parse(Required(x,"maximum"),CultureInfo.InvariantCulture),
        Enum.Parse<BlobJudgmentKind>(Required(x,"kind")))).ToArray());
    public IReadOnlyList<XElement> JudgmentMetadata=>Array.AsReadOnly(_judgments.Select(x=>new XElement(x)).ToArray());
}
