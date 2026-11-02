using System.Collections.ObjectModel;
using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob's presentation phase. These fields never materialize BlobSettings or enter an invocation.</summary>
public static class BlobResultDisplay
{
    public const string Graphic="display.blob";
    public const string Text="display.text";
    public static string Field(string group,string role)=>group+"."+role;
    private sealed record Definition(Port Port,FieldPresentation Presentation,object Value);
    private static readonly IReadOnlyList<Definition> Definitions=Build();
    public static ToolSchema Schema {get;}=new(Definitions.Select(item=>item.Port),Array.Empty<Port>(),new ToolPresentation(Definitions.Select(item=>item.Presentation)));
    public static ResultDisplayDescriptor Descriptor {get;}=new(new(BlobSchema.BlobsPort),new(BlobSchema.BlobsPort,new[]{"element-id"}),
        new(BlobSchema.BlobsPort,new[]{"rotated-bounds"}),new(BlobSchema.BlobsPort,new[]{"region"}),
        new(BlobSchema.BlobsPort,new[]{"area"}),new(BlobSchema.BlobsPort,new[]{"judgment-passed"}));
    public static InputValues Defaults()=>new(Schema.Inputs,Definitions.ToDictionary(item=>item.Port.Id,item=>(object?)item.Value,StringComparer.Ordinal));
    private static IReadOnlyList<Definition> Build()
    {
        var result=new List<Definition>();
        var displayOrder=BlobParameterDefinition.Instance.SettingsSchema.Presentation!.Fields.Max(field=>field.Order)+1;
        void Add(string id,string label,string section,TypeRef type,object value,double? minimum=null,double? maximum=null,double? increment=null,string? dialog=null,bool primary=false,int? decimals=2)
        {
            var port=new Port(id,type,EditModes:FieldEditModes.Constant);
            result.Add(new(port,new(id,label,section,displayOrder+result.Count,type.Shape==ValueShape.Record?FieldEditorKind.Record:FieldEditorKind.Scalar){Layout=new("结果显示",section,
                Minimum:minimum,Maximum:maximum,Increment:increment??1,DecimalPlaces:type==DataType.Number?decimals:0,
                DialogGroup:dialog,DialogTitle:dialog is null?null:"Blob",DialogPrimary:primary)},value));
        }
        var ok=PresentationDeclarations.Color(102,255,0);var ng=PresentationDeclarations.Color(255,0,0);
        Add(Field(Graphic,"visible"),"Blob","图像显示",DataType.Boolean,true,dialog:Graphic,primary:true);
        Add(Field(Graphic,"ok"),"OK颜色","图像显示",PresentationDeclarations.Rgba,ok,dialog:Graphic);
        Add(Field(Graphic,"ng"),"NG颜色","图像显示",PresentationDeclarations.Rgba,ng,dialog:Graphic);
        Add(Field(Graphic,"opacity"),"透明度","图像显示",DataType.Number,1d,0,1,.01,Graphic);
        Add(Field(Graphic,"width"),"线宽","图像显示",DataType.Number,2d,1,10,1,Graphic);
        Add(Field(Text,"visible"),"文本","图像显示",DataType.Boolean,true);
        Add(Field(Text,"content"),"内容","文本显示",DataType.Text,"Blob Area:{0}");
        Add(Field(Text,"ok"),"OK颜色","文本显示",PresentationDeclarations.Rgba,ok);
        Add(Field(Text,"ng"),"NG颜色","文本显示",PresentationDeclarations.Rgba,ng);
        Add(Field(Text,"size"),"字号","文本显示",DataType.Number,10d,6,72,1,decimals:0);
        Add(Field(Text,"opacity"),"透明度","文本显示",DataType.Number,1d,0,1,.1);
        Add(Field(Text,"x"),"位置X","文本显示",DataType.Number,0d,decimals:null);
        Add(Field(Text,"y"),"位置Y","文本显示",DataType.Number,0d,decimals:null);
        return result.AsReadOnly();
    }
    public static ResultDisplayAppearance Capture(FieldSettings fields)
    {
        var values=fields.Select(Schema.Inputs).Materialize(new Dictionary<string,object?>());
        T Read<T>(string id)=>values.Get<T>(id);
        double Number(string group,string role)
        {
            var id=Field(group,role);var value=Read<double>(id);var layout=Definitions.Single(item=>item.Port.Id==id).Presentation.Layout!;
            if(!double.IsFinite(value)||value<layout.Minimum||value>layout.Maximum||layout.DecimalPlaces is {} digits&&Math.Round(value,digits,MidpointRounding.ToEven)!=value)
                throw new ArgumentException("显示数值超出范围或精度："+id);
            return value;
        }
        ResultDisplayColor Color(string group,string role)
        {var value=Read<RecordValue>(Field(group,role));return new(checked((byte)value.Get<long>("r")),checked((byte)value.Get<long>("g")),checked((byte)value.Get<long>("b")),checked((byte)value.Get<long>("a")));}
        var content=Read<string>(Field(Text,"content"));ValidateContent(content);
        return new(new(Read<bool>(Field(Graphic,"visible")),Color(Graphic,"ok"),Color(Graphic,"ng"),Number(Graphic,"opacity"),Number(Graphic,"width")),
            new(Read<bool>(Field(Text,"visible")),content,Color(Text,"ok"),Color(Text,"ng"),Number(Text,"size"),Number(Text,"opacity"),Number(Text,"x"),Number(Text,"y")));
    }
    public static void ValidateContent(string content)
    {
        if(content is null)throw new ArgumentException("显示内容不能为空值。");
        var remainder=System.Text.RegularExpressions.Regex.Replace(content,@"\{\{ |\}\}","",System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
        remainder=System.Text.RegularExpressions.Regex.Replace(remainder,@"\{0(?::[FfNnGgRr][0-9]?)?\}","");
        if(remainder.Contains('{')||remainder.Contains('}'))throw new ArgumentException("显示内容仅支持面积占位符{0}及有限数值格式。");
        try{_=string.Format(CultureInfo.InvariantCulture,content,0d);}
        catch(FormatException error){throw new ArgumentException("显示内容仅支持面积占位符{0}及其数值格式。",error);}
    }
}



