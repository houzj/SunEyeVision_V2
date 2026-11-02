using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob-owned inheritance configuration. Runtime fields remain the only persistent owner.
/// Coordinates are interpreted in the frozen input image's pixel space; angles are degrees.
/// These explicit platform conventions are not evidence of VM native coordinate equivalence.</summary>
public static class BlobRoiInheritance
{
    public const string ModeId="input.roi.inheritance-mode";
    public const string Local="Local",Rectangle="Rectangle",RectangleParameters="RectangleParameters",Annulus="Annulus",AnnulusParameters="AnnulusParameters";
    public const string RectangleSource="input.roi.rectangle",AnnulusSource="input.roi.annulus";
    public static IReadOnlyList<string> RectangleMembers {get;}=Array.AsReadOnly(new[]{"center-x","center-y","width","height","angle-degrees"});
    public static IReadOnlyList<string> AnnulusMembers {get;}=Array.AsReadOnly(new[]{"center-x","center-y","inner-radius","outer-radius","start-degrees","sweep-degrees"});
    public static TypeRef RectangleType {get;}=TypeRef.RecordOf("blob.roi.rectangle",RectangleMembers.Select(id=>new Port(id,DataType.Number)));
    public static TypeRef AnnulusType {get;}=TypeRef.RecordOf("blob.roi.annulus",AnnulusMembers.Select(id=>new Port(id,DataType.Number)));
    public static string Leaf(string mode,string member)=>"input.roi."+(mode==RectangleParameters?"rectangle.":mode==AnnulusParameters?"annulus.":throw new ArgumentException("Unknown inheritance parameter mode."))+member;
    public static IReadOnlyList<Port> Ports {get;}=Array.AsReadOnly(new[]{
        new Port(ModeId,DataType.Text,Required:false,EditModes:FieldEditModes.Constant),
        new Port(RectangleSource,TypeRef.SequenceOf(RectangleType),Required:false,EditModes:FieldEditModes.Reference),
        new Port(AnnulusSource,TypeRef.SequenceOf(AnnulusType),Required:false,EditModes:FieldEditModes.Reference)}
        .Concat(RectangleMembers.Select(id=>new Port(Leaf(RectangleParameters,id),DataType.NumberArray,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference)))
        .Concat(AnnulusMembers.Select(id=>new Port(Leaf(AnnulusParameters,id),DataType.NumberArray,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference))).ToArray());
    public static string ReadMode(FieldSettings fields)=>fields.Read(ModeId) switch{
        UnsetSetting=>Local,ConstantSetting {Value:string mode}=>ValidateMode(mode),_=>throw new ArgumentException("ROI继承方式必须是固定模式。")};
    public static string ValidateMode(string mode)=>mode is Local or Rectangle or RectangleParameters or Annulus or AnnulusParameters?mode:throw new ArgumentException("ROI继承方式无效。");
    public static IReadOnlyList<string> ActiveInputs(FieldSettings fields,IEnumerable<Port> inputs)
    {
        var mode=ReadMode(fields);var own=Ports.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(inputs.Where(port=>!own.Contains(port.Id)||port.Id==ModeId
            ||mode==Rectangle&&port.Id==RectangleSource||mode==Annulus&&port.Id==AnnulusSource
            ||mode==RectangleParameters&&RectangleMembers.Any(member=>port.Id==Leaf(mode,member))
            ||mode==AnnulusParameters&&AnnulusMembers.Any(member=>port.Id==Leaf(mode,member))).Select(port=>port.Id).ToArray());
    }
    public static RecordValue? Materialize(InputValues inputs,RecordValue? local)
    {
        var mode=inputs.Contains(ModeId)?ValidateMode(inputs.Get<string>(ModeId)):Local;
        if(mode==Local)return local;
        var rectangle=mode is Rectangle or RectangleParameters;
        var members=rectangle?RectangleMembers:AnnulusMembers;
        var definition=local??BlobLocalRoi.Empty;BlobLocalRoi.Validate(definition);
        var combined=mode is Rectangle or Annulus;
        var source=combined?inputs.Read(rectangle?RectangleSource:AnnulusSource):null;
        if(source is not null&&source.Declaration!=TypeRef.SequenceOf(rectangle?RectangleType:AnnulusType))throw new ArgumentException("ROI继承组合声明不匹配。");
        var leaves=combined?null:members.ToDictionary(member=>member,member=>inputs.Read(Leaf(mode,member)),StringComparer.Ordinal);
        if(source is {IsMissing:true}||leaves?.Values.Any(reader=>reader.IsMissing)==true)throw new ArgumentException("请选择完整的ROI继承来源。");
        var count=source?.Count??leaves!.Values.Min(reader=>reader.Count);
        if(count==0)throw new ArgumentException("ROI继承来源没有有效数据。");
        var rows=new List<RecordValue>();
        for(var i=0;i<Math.Min(64,count);i++)
        {
            double Number(string member)
            {
                var value=source is not null?source.GetElement(i).GetField(member).GetScalar<double>():leaves![member].GetElement(i).GetScalar<double>();
                if(!double.IsFinite(value))throw new ArgumentException("ROI继承参数必须有限。");return value;
            }
            var values=new Dictionary<string,object?>{{"kind",rectangle?"rectangle":"annulus"},{"center",BlobLocalRoi.Point(Number("center-x"),Number("center-y"))}};
            if(rectangle){values.Add("width",Number("width"));values.Add("height",Number("height"));values.Add("angle-degrees",Number("angle-degrees"));}
            else
            {
                var start=BlobLocalRoi.NormalizeDegrees(Number("start-degrees"))*Math.PI/180d;
                var sweep=Number("sweep-degrees");if(Math.Abs(sweep)>360)throw new ArgumentException("圆环继承角度范围不能超过360度。");
                values.Add("inner-radius",Number("inner-radius"));values.Add("outer-radius",Number("outer-radius"));
                values.Add("start-radians",start);values.Add("end-radians",start+sweep*Math.PI/180d);
            }
            var shape=new RecordValue(BlobLocalRoi.ShapeType,values);BlobLocalRoi.ValidateShape(shape);
            var bytes=new Guid("c8a845d3-1695-4761-ac6b-165fe86dc248").ToByteArray();BitConverter.GetBytes(i).CopyTo(bytes,12);
            rows.Add(BlobLocalRoi.Item(new Guid(bytes),"继承区域"+(i+1),shape));
        }
        return BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit,rows),definition.Get<RecordValue>(BlobLocalRoi.Mask));
    }
    public static IReadOnlyList<Diagnostic> ValidateConfiguration(FieldSettings fields)
    {
        var mode=ReadMode(fields);
        if(mode==Local)return Array.Empty<Diagnostic>();
        var ids=mode switch {
            Rectangle=>new[]{RectangleSource},Annulus=>new[]{AnnulusSource},
            RectangleParameters=>RectangleMembers.Select(member=>Leaf(mode,member)).ToArray(),
            _=>AnnulusMembers.Select(member=>Leaf(mode,member)).ToArray()};
        return Array.AsReadOnly(ids.Where(id=>fields.Read(id) is UnsetSetting)
            .Select(id=>new Diagnostic("BLOB_ROI_SOURCE_REQUIRED","请选择完整的ROI继承来源。",id)).ToArray());
    }
}
