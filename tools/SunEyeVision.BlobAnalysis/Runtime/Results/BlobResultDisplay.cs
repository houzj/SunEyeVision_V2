using System.Collections.ObjectModel;
using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob's presentation phase. These fields never materialize BlobSettings or enter an invocation.</summary>
public static class BlobResultDisplay
{
    public const string Graphic="display.blob";
    public const string InnerRectangle="display.inner-rectangle";
    public const string ReferencePoint="display.reference-point", RunPoint="display.run-point";
    public const string Text="display.text";
    public const string Outline="display.outline";
    public const string Inspection="display.inspection",Mask="display.mask",Annulus="display.annulus";
    public static string Field(string group,string role)=>group+"."+role;
    private sealed record Definition(Port Port,FieldPresentation Presentation,object Value);
    private static readonly IReadOnlyList<Definition> Definitions=Build();
    public static ToolSchema Schema {get;}=new(Definitions.Select(item=>item.Port),Array.Empty<Port>(),new ToolPresentation(Definitions.Select(item=>item.Presentation)));
    public static ResultDisplayDescriptor Descriptor {get;}=new(new(BlobSchema.BlobsPort),new(BlobSchema.BlobsPort,new[]{"element-id"}),
        new(BlobSchema.BlobsPort,new[]{"oriented-bounds"}),new(BlobSchema.BlobsPort,new[]{"region"}),
        new(BlobSchema.BlobsPort,new[]{"area"}),new(BlobSchema.BlobsPort,new[]{"judgment-passed"}),new(BlobSchema.BlobsPort,new[]{"outline-points"}),
        new(BlobSchema.BlobsPort,new[]{"inner-rectangle"}), selectedRectangleColor:new(255,255,0));
    public static InputValues Defaults()=>new(Schema.Inputs,Definitions.ToDictionary(item=>item.Port.Id,item=>(object?)item.Value,StringComparer.Ordinal));
    private static IReadOnlyList<Definition> Build()
    {
        var result=new List<Definition>();
        var displayOrder=BlobParameterDefinition.Instance.SettingsSchema.Presentation!.Fields.Max(field=>field.Order)+1;
        void Add(string id,string label,string section,TypeRef type,object value,double? minimum=null,double? maximum=null,double? increment=null,string? dialog=null,bool primary=false,int? decimals=2)
        {
            var modes=id==Field(Text,"content")?FieldEditModes.Constant|FieldEditModes.DisplayExpression:
                id==Field(Text,"x")||id==Field(Text,"y")?FieldEditModes.Constant|FieldEditModes.Reference:FieldEditModes.Constant;
            var port=new Port(id,type,EditModes:modes);
            result.Add(new(port,new(id,label,section,displayOrder+result.Count,type.Shape==ValueShape.Record?FieldEditorKind.Record:FieldEditorKind.Scalar){Layout=new("结果显示",section,
                Minimum:minimum,Maximum:maximum,Increment:increment??1,DecimalPlaces:type==DataType.Number?decimals:0,
                DialogGroup:dialog,DialogTitle:dialog is null?null:dialog switch
                {
                    InnerRectangle=>"最大内接矩形",Inspection=>"检测区域",Mask=>"屏蔽区多边形",Annulus=>"扇环检测区域",
                    ReferencePoint=>"基准点",RunPoint=>"运行点",_=>"Blob"
                },DialogPrimary:primary)},value));
        }
        var ok=PresentationDeclarations.Color(102,255,0);var ng=PresentationDeclarations.Color(255,0,0);
        void GraphicFields(string group,string label,RecordValue color,double opacity=1,double width=2)
        {
            Add(Field(group,"visible"),label,"图像显示",DataType.Boolean,true,dialog:group,primary:true);
            Add(Field(group,"ok"),"OK颜色","图像显示",PresentationDeclarations.Rgba,color,dialog:group);
            Add(Field(group,"ng"),"NG颜色","图像显示",PresentationDeclarations.Rgba,ng,dialog:group);
            Add(Field(group,"opacity"),"透明度","图像显示",DataType.Number,opacity,0,1,.01,group);
            Add(Field(group,"width"),"线宽","图像显示",DataType.Number,width,1,10,1,group);
        }
        GraphicFields(Graphic,"Blob",ok);
        GraphicFields(InnerRectangle,"最大内接矩形",ok);
        GraphicFields(Inspection,"检测区域",PresentationDeclarations.Color(0,0,255));
        Add(Field(Outline,"visible"),"轮廓点","图像显示",DataType.Boolean,true);
        GraphicFields(ReferencePoint,"基准点",ok,width:1);
        GraphicFields(RunPoint,"运行点",ng,width:1);
        Add(Field(Text,"visible"),"文本","图像显示",DataType.Boolean,true);
        GraphicFields(Mask,"屏蔽区多边形",PresentationDeclarations.Color(0,0,255),.5);
        GraphicFields(Annulus,"扇环检测区域",PresentationDeclarations.Color(0,0,255));
        Add(Field(Text,"content"),"内容","文本显示",DataType.Text,"Blob面积:{面积}");
        Add(Field(Text,"ok"),"OK颜色","文本显示",PresentationDeclarations.Rgba,ok);
        Add(Field(Text,"ng"),"NG颜色","文本显示",PresentationDeclarations.Rgba,ng);
        Add(Field(Text,"size"),"字号","文本显示",DataType.Number,10d,6,72,1);
        Add(Field(Text,"opacity"),"透明度","文本显示",DataType.Number,1d,0,1,.1);
        Add(Field(Text,"x"),"位置X","文本显示",DataType.Number,0d,decimals:null);
        Add(Field(Text,"y"),"位置Y","文本显示",DataType.Number,0d,decimals:null);
        return result.AsReadOnly();
    }
    public static ResultDisplayAppearance Capture(FieldSettings fields)
    {
        T Read<T>(string id)=>fields.Get<T>(id);
        double Number(string group,string role)
        {
            var id=Field(group,role);var value=Read<double>(id);var layout=Definitions.Single(item=>item.Port.Id==id).Presentation.Layout!;
            if(!double.IsFinite(value)||value<layout.Minimum||value>layout.Maximum||layout.DecimalPlaces is {} digits&&Math.Round(value,digits,MidpointRounding.ToEven)!=value)
                throw new ArgumentException("显示数值超出范围或精度："+id);
            return value;
        }
        ResultDisplayColor Color(string group,string role)
        {var value=Read<RecordValue>(Field(group,role));return new(checked((byte)value.Get<long>("r")),checked((byte)value.Get<long>("g")),checked((byte)value.Get<long>("b")),checked((byte)value.Get<long>("a")));}
        var expression=fields.Read(Field(Text,"content")) switch {DisplayExpressionSetting configured=>ParseContent(configured.Text,configured.Slots),ConstantSetting {Value:string raw}=>ParseContent(raw),_=>throw new ArgumentException("文本内容设置无效。")};
        var content=expression.Text;
        ParameterSetting Position(string role){var setting=fields.Read(Field(Text,role));if(setting is ReferenceSetting reference){ValidateDisplayReference(reference.Reference,true);return reference;}if(setting is ConstantSetting {Value:double number}&&double.IsFinite(number))return setting;throw new ArgumentException("文本位置必须是有限数或已声明数字来源。");}
        var x=Position("x");var y=Position("y");
        ResultGraphicAppearance Style(string group)=>new(Read<bool>(Field(group,"visible")),Color(group,"ok"),Color(group,"ng"),Number(group,"opacity"),Number(group,"width"));
        var configuredSize = Number(Text,"size");
        // The original bridge reads Int32 from the feature string. Its text renderer uses 10 when that parse yields zero.
        var renderedSize = configuredSize == Math.Truncate(configuredSize) ? configuredSize : 10d;
        return new(Style(Graphic),
            new(Read<bool>(Field(Text,"visible")),content,Color(Text,"ok"),Color(Text,"ng"),renderedSize,Number(Text,"opacity"),x is ConstantSetting {Value:double xv}?xv:0,y is ConstantSetting {Value:double yv}?yv:0,expression,x,y),
            Style(Inspection) with { ShowMeasurements = true },
            Style(Mask) with { Fill = new(255,84,0) },
            Style(Annulus) with { ShowMeasurements = true },
            new(Read<bool>(Field(Outline,"visible")),new(102,255,0),new(255,0,0)),Style(InnerRectangle),
            Style(ReferencePoint) with { FixedJudgment = true, PointLabel = "Fixture BasePoint" },
            Style(RunPoint) with { FixedJudgment = true, PointLabel = "Fixture RunPoint" });
    }
    public static void ValidateDisplayReference(OutputReference reference,bool numeric=false)
    {
        if(reference.Selection is BindingSelection.AtVariable)throw new ArgumentException("显示来源暂不支持变量索引。");
        if(reference.Selection is BindingSelection.AtField index)ValidateDisplayReference(index.Reference);
        var type=ParameterReferenceRules.SelectedType(reference);while(type.Shape==ValueShape.Sequence)type=type.Element!;
        if(type.Shape!=ValueShape.Scalar||type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64 or ScalarKind.Text)||numeric&&type.Scalar==ScalarKind.Text)
            throw new ArgumentException("显示来源只支持整数、浮点或文本。");
    }
    public static DisplayExpressionSetting ParseContent(string content,IEnumerable<DisplayExpressionSlot>? available=null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var slots=new List<DisplayExpressionSlot>();var known=(available??Array.Empty<DisplayExpressionSlot>()).ToArray();
        var position=0;
        while(position<content.Length)
        {
            var start=content.IndexOfAny(new[]{'{','}'},position);if(start<0)break;
            if(content[start]!='{')throw new ArgumentException("文本表达式括号不完整。");
            var end=content.IndexOf('}',start+1);
            if(end<0||content.AsSpan(start+1,end-start-1).Contains('{'))throw new ArgumentException("文本表达式括号不完整。");
            var token=content.Substring(start+1,end-start-1);
            var matches=known.Where(slot=>slot.Token==token).ToArray();
            if(matches.Length>1&&!matches.Skip(1).All(slot=>new DisplayExpressionSetting("",new[]{slot}).SameExpression(new("",new[]{matches[0]}))))
                throw new ArgumentException("文本来源名称不唯一，请重新选择。");
            if(matches.Length!=0)slots.Add(matches[0]);
            else if(System.Text.RegularExpressions.Regex.Match(token,@"^(0|面积|Area)(?::([FfNnGgRr][0-9]?))?$") is {Success:true} area)
                slots.Add(new(token,new ResultFieldPath(BlobSchema.BlobsPort,new[]{"area"}),area.Groups[2].Success?area.Groups[2].Value:null));
            else throw new ArgumentException("文本来源尚未选择或已失效："+token);
            position=end+1;
        }
        foreach(var slot in slots)if(slot.Reference is {} reference)ValidateDisplayReference(reference);
        return new(content,slots);
    }
    public static void ValidateContent(string content)=>_=ParseContent(content);
}
