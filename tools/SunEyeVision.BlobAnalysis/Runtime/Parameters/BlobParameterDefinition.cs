using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Versioned tool metadata. It initializes new settings, never patches saved settings.</summary>
public sealed partial class BlobParameterDefinition
{
    private readonly IReadOnlyDictionary<string, XElement> _fields;
    private readonly IReadOnlyDictionary<string,XElement> _inputs;
    public static BlobParameterDefinition Instance { get; } = Load();
    private BlobParameterDefinition(XElement root)
    {
        if(root.Name!="BlobParameters"||(string?)root.Attribute("schemaVersion")!="1")
            throw new InvalidDataException("Unsupported Blob parameter metadata.");
        Shape(root,["schemaVersion","workflowImageInputId"],["Inputs","Group"]);
        WorkflowImageInputId=Required(root,"workflowImageInputId");
        if(WorkflowImageInputId!=BlobTool.ImageInput)throw new InvalidDataException("Blob workflow image default must name its declared image input.");
        if(root.Elements("Inputs").Count()!=1)throw new InvalidDataException("Exactly one Blob input editor declaration is required.");
        var inputs=root.Element("Inputs")!;Shape(inputs,[],["Input"]);
        foreach(var input in inputs.Elements())
        {Shape(input,["id","label","page","section"],[]);Required(input,"id");Required(input,"label");Required(input,"page");Required(input,"section");}
        if(inputs.Elements().Select(x=>Required(x,"id")).Distinct(StringComparer.Ordinal).Count()!=inputs.Elements().Count())
            throw new InvalidDataException("Duplicate Blob input editor identity.");
        _inputs=new ReadOnlyDictionary<string,XElement>(inputs.Elements().ToDictionary(x=>Required(x,"id"),StringComparer.Ordinal));
        if(!_inputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{BlobTool.ImageInput,BlobTool.InspectionInput,BlobTool.MaskInput,BlobTool.PoseInput}))
            throw new InvalidDataException("Blob input editor declarations differ from the input model.");
        foreach(var group in root.Elements("Group"))
        {
            Shape(group,["page","section","advanced"],["Parameter"]);
            Required(group,"page");Required(group,"section");
            if(group.Attribute("advanced") is not null)Boolean(group,"advanced");
            if(!group.HasElements)throw new InvalidDataException("Empty Blob parameter group.");
            foreach(var field in group.Elements())
            {
                Shape(field,["id","fieldId","allowReference","bindingName","type","default","label","minimum","maximum","decimals","increment","visibleWhen","visibleValue","visibleNegate","pairWith"],["Choice"]);
                Required(field,"id");Required(field,"type");Required(field,"default");Required(field,"label");
                Required(field,"fieldId");Boolean(field,"allowReference");
                if(Boolean(field,"allowReference"))Required(field,"bindingName");
                else if(field.Attribute("bindingName") is not null)throw new InvalidDataException("Non-reference parameters cannot declare binding names.");
                foreach(var choice in field.Elements("Choice")){Shape(choice,["key","label"],[]);Required(choice,"key");Required(choice,"label");}
            }
        }
        var ids=root.Elements("Group").Elements("Parameter").Select(x=>Required(x,"id")).ToArray();
        if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)throw new InvalidDataException("Duplicate Blob parameter identity.");
        _fields=new ReadOnlyDictionary<string,XElement>(root.Elements("Group").Elements("Parameter")
            .ToDictionary(x=>Required(x,"id"),x=>x,StringComparer.Ordinal));
        var settingsTypes=typeof(BlobSettings).GetProperties(BindingFlags.Public|BindingFlags.Instance)
            .Where(p=>p.GetCustomAttribute<BlobNonFieldAttribute>() is null)
            .ToDictionary(p=>p.Name,p=>p.PropertyType,StringComparer.Ordinal);
        var declared=ParameterTypes();
        if(settingsTypes.Keys.Any(id=>!_fields.ContainsKey(id))||_fields.Keys.Any(id=>!declared.ContainsKey(id)))
            throw new InvalidDataException("Blob XML fields differ from the settings and VM parameter models.");
        foreach(var (id,type) in _fields.Keys.Select(id=>(id,declared[id])))
        {
            var expected=type==typeof(int)?"Integer":type==typeof(double)?"Number":type==typeof(bool)?"Boolean":type.IsEnum?"Enum":throw new InvalidDataException("Unsupported Blob setting type.");
            if(Required(_fields[id],"type")!=expected)throw new InvalidDataException("Blob XML field type differs: "+id);
            var choices=_fields[id].Elements("Choice").Select(x=>Required(x,"key")).ToArray();
            if(type.IsEnum?!choices.ToHashSet(StringComparer.Ordinal).SetEquals(Enum.GetNames(type))||choices.Distinct().Count()!=choices.Length:choices.Length!=0)
                throw new InvalidDataException("Blob XML choices differ from the declared enum: "+id);
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
        _bindings=BuildSettingsBindings();
        SettingsSchema=new(_bindings.Select(x=>x.Port),Array.Empty<SunEyeVision.Plugin.Contracts.Port>(),
            new SunEyeVision.Plugin.Contracts.ToolPresentation(_bindings.Select(x=>x.Presentation)));
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
    internal static IReadOnlyDictionary<string,Type> ParameterTypes()
    {
        var settings=typeof(BlobSettings).GetProperties(BindingFlags.Public|BindingFlags.Instance)
            .Where(p=>p.GetCustomAttribute<BlobNonFieldAttribute>() is null)
            .ToDictionary(p=>p.Name,p=>p.PropertyType,StringComparer.Ordinal);
        foreach(var property in typeof(BlobVmFindParameters).GetProperties(BindingFlags.Public|BindingFlags.Instance))
            settings.TryAdd(property.Name,property.PropertyType);
        return settings;
    }
    internal static PropertyInfo? SettingsProperty(string id)=>typeof(BlobSettings).GetProperty(id);
    internal static PropertyInfo? VmProperty(string id)=>typeof(BlobVmFindParameters).GetProperty(id);
    internal static Type ParameterType(string id)=>ParameterTypes().TryGetValue(id,out var type)?type
        :throw new InvalidDataException("Unknown Blob parameter: "+id);
    public string WorkflowImageInputId { get; }
    public SunEyeVision.Plugin.Contracts.FieldPresentation InputPresentation(SunEyeVision.Plugin.Contracts.Port port,int order)
    {
        var input=_inputs[port.Id];
        return new(port.Id,Required(input,"label"),Required(input,"section"),order,
            port.Type.Resource==SunEyeVision.Plugin.Contracts.ResourceKind.Image?SunEyeVision.Plugin.Contracts.FieldEditorKind.Image:
            port.Type.Resource==SunEyeVision.Plugin.Contracts.ResourceKind.Region?SunEyeVision.Plugin.Contracts.FieldEditorKind.Region:SunEyeVision.Plugin.Contracts.FieldEditorKind.Record)
            {Layout=new(Required(input,"page"),Required(input,"section"))};
    }
    public string Label(string id)=>Required(_fields[id],"label");
    public string Page(string id)=>Required(_fields[id].Parent!,"page");
    public string Section(string id)=>Required(_fields[id].Parent!,"section");
    public bool Advanced(string id)=>_fields[id].Parent!.Attribute("advanced") is not null&&Boolean(_fields[id].Parent!,"advanced");
    public T Default<T>(string id)
    {
        if(ParameterType(id)!=typeof(T))throw new InvalidDataException("Requested Blob default type differs: "+id);
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
}
