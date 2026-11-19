using SunEyeVision.Plugin.Contracts;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob运行参数的中性快照。宿主资源和ROI不在此状态中。</summary>

public static class BlobImageRoi
{
    public const string ModeId = "input.roi.area-type";
    public const string ImageId = "input.roi.mask-image";
    public const string OutputEnabledId = "input.roi.output-mask";
    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(ModeId, DataType.Integer, Required:false, EditModes:FieldEditModes.Constant),
        new Port(ImageId, TypeRef.ResourceOf(ResourceKind.Image), Required:false, EditModes:FieldEditModes.Reference),
        // VM OutputMaskImage is a normal RW Boolean with a false default; it has
        // no unset/clear branch, so the original switch does not show the × action.
        // The host keeps this input optional for legacy configurations. The
        // formal editor projects the VM false default and suppresses its clear
        // affordance because the VM switch has no unset state.
        new Port(OutputEnabledId, DataType.Boolean, Required:false, EditModes:FieldEditModes.Constant)
    });

    public static bool IsImageMode(ParameterSetting setting) => setting switch
    {
        UnsetSetting or ConstantSetting { Value:0L } => false,
        ConstantSetting { Value:1L } => true,
        _ => throw new ArgumentException("ROI区域类型必须是图形或图像。")
    };
}

/// <summary>
/// Inputs borrowed from the host's frozen invocation. The host owns their lifetime;
/// constructing this view does not copy or dispose image/region resources.
/// </summary>

public static class BlobLocalRoi
{
    public const string InputId="input.roi.local";
    public const string Inspection="inspection",Mask="mask";
    public const string None="None",FullDomain="FullDomain",Explicit="Explicit";
    public static TypeRef PointType {get;}=TypeRef.RecordOf("blob.local-roi.point",new[]{new Port("x",DataType.Number),new Port("y",DataType.Number)});
    public static TypeRef ShapeType {get;}=TypeRef.RecordOf("blob.local-roi.shape",new[]{
        new Port("kind",DataType.Text),new Port("center",PointType,false),new Port("width",DataType.Number,false),new Port("height",DataType.Number,false),
        new Port("angle-degrees",DataType.Number,false),new Port("inner-radius",DataType.Number,false),new Port("outer-radius",DataType.Number,false),
        new Port("start-radians",DataType.Number,false),new Port("end-radians",DataType.Number,false),new Port("vertices",TypeRef.SequenceOf(PointType),false)});
    public static TypeRef ItemType {get;}=TypeRef.RecordOf("blob.local-roi.item",new[]{new Port("id",DataType.Text),new Port("name",DataType.Text),
        new Port("enabled",DataType.Boolean),new Port("visible",DataType.Boolean),new Port("shape",ShapeType)});
    public static TypeRef ItemsType {get;}=TypeRef.SequenceOf(ItemType);
    public static TypeRef GroupType {get;}=TypeRef.RecordOf("blob.local-roi.group",new[]{new Port("coverage",DataType.Text),new Port("items",ItemsType)});
    public static TypeRef Declaration {get;}=TypeRef.RecordOf("blob.local-roi",new[]{new Port(Inspection,GroupType),new Port(Mask,GroupType)});
    public static Port Port {get;}=new(InputId,Declaration,Required:false,EditModes:FieldEditModes.Constant);
    public static RecordValue Empty {get;}=Root(Group(FullDomain,Array.Empty<RecordValue>()),Group(None,Array.Empty<RecordValue>()));
    public static RecordValue Point(double x,double y)=>new(PointType,new Dictionary<string,object?>{{"x",x},{"y",y}});
    public static RecordValue Item(Guid id,string name,RecordValue shape,bool enabled=true,bool visible=true)
        =>new(ItemType,new Dictionary<string,object?>{{"id",id.ToString("N")},{"name",name},{"shape",shape},{"enabled",enabled},{"visible",visible}});
    public static RecordValue Group(string coverage,IEnumerable<RecordValue> items)
        =>new(GroupType,new Dictionary<string,object?>{{"coverage",coverage},{"items",new SequenceValue(ItemsType,items)}});
    public static RecordValue Root(RecordValue inspection,RecordValue mask)
        =>new(Declaration,new Dictionary<string,object?>{{Inspection,inspection},{Mask,mask}});
    public static IReadOnlyList<RecordValue> Items(RecordValue root,string group)
        =>root.Get<RecordValue>(group).Get<SequenceValue>("items").Cast<RecordValue>().ToArray();
    public static string Coverage(RecordValue root,string group)=>root.Get<RecordValue>(group).Get<string>("coverage");
    public static bool Same(object? left,object? right)=>left is RecordValue a&&right is RecordValue b
        ?a.Declaration==b.Declaration&&a.CopyFields().Count==b.CopyFields().Count&&a.CopyFields().All(pair=>b.Contains(pair.Key)&&Same(pair.Value,b.Get<object?>(pair.Key)))
        :left is SequenceValue x&&right is SequenceValue y?x.Count==y.Count&&x.Zip(y).All(pair=>Same(pair.First,pair.Second)):Equals(left,right);
    public const int MaximumLocalInspectionCount = 16;
    public const int MaximumLocalMaskCount = 8;
    internal const int MaximumInheritedCount = 64;

    public static void Validate(RecordValue root,int maximumInspectionCount=MaximumLocalInspectionCount, bool inheritedMask=false)
    {
        if(root.Declaration!=Declaration)throw new ArgumentException("Blob本地区域声明不匹配。");
        if(maximumInspectionCount is not (MaximumLocalInspectionCount or MaximumInheritedCount))throw new ArgumentException("区域数量合同无效。");
        foreach(var group in new[]{Inspection,Mask})
        {
            var coverage=Coverage(root,group);var rows=Items(root,group);
            if(coverage is not (None or FullDomain or Explicit)||group==Mask&&coverage==FullDomain)
                throw new ArgumentException("区域覆盖状态无效。");
            if(coverage!=Explicit&&rows.Count!=0||coverage==Explicit&&rows.Count==0)
                throw new ArgumentException("显式区域与覆盖状态不一致。");
            if(group==Inspection&&rows.Count>maximumInspectionCount)throw new ArgumentException("检测区域数量超出允许范围。");
            if(group==Mask&&rows.Count>(inheritedMask?MaximumInheritedCount:MaximumLocalMaskCount))
                throw new ArgumentException("屏蔽区数量超出允许范围。");
            var ids=new HashSet<Guid>();
            foreach(var item in rows)
            {
                if(!Guid.TryParseExact(item.Get<string>("id"),"N",out var id)||id==Guid.Empty||!ids.Add(id))throw new ArgumentException("区域身份无效或重复。");
                if(string.IsNullOrWhiteSpace(item.Get<string>("name")))throw new ArgumentException("区域名称不能为空。");
                var shape=item.Get<RecordValue>("shape");
                ValidateShape(shape);
                if(group==Mask)
                {
                    if(shape.Get<string>("kind")!="polygon")throw new ArgumentException("屏蔽区仅支持多边形。");
                    if(shape.Get<SequenceValue>("vertices").Count>BlobMaskInheritance.MaximumVertices)
                        throw new ArgumentException($"每个屏蔽区最多支持 {BlobMaskInheritance.MaximumVertices} 个顶点。");
                }
            }
        }
    }
    public static double NormalizeDegrees(double angle)=>Math.IEEERemainder(angle,360d);
    public static void ValidateShape(RecordValue shape)
    {
        if(shape.Declaration!=ShapeType)throw new ArgumentException("Blob本地形状声明不匹配。");
        var kind=shape.Get<string>("kind");
        string[] fields=kind switch {
            "rectangle"=>new[]{"kind","center","width","height","angle-degrees"},
            "annulus" or "sector"=>new[]{"kind","center","inner-radius","outer-radius","start-radians","end-radians"},
            "polygon"=>new[]{"kind","vertices"},_=>throw new ArgumentException("不支持的本地形状。")};
        if(!shape.CopyFields().Keys.ToHashSet(StringComparer.Ordinal).SetEquals(fields))throw new ArgumentException("形状字段不完整或包含其他类型的字段。");
        double Number(string name){var value=shape.Get<double>(name);if(!double.IsFinite(value))throw new ArgumentException("形状参数必须有限。");return value;}
        void PointChecked(RecordValue point){if(point.Declaration!=PointType||!double.IsFinite(point.Get<double>("x"))||!double.IsFinite(point.Get<double>("y")))throw new ArgumentException("区域坐标必须有限。");}
        if(kind=="polygon")
        {
            var points=shape.Get<SequenceValue>("vertices").Cast<RecordValue>().ToArray();foreach(var point in points)PointChecked(point);
            if(points.Select(point=>(point.Get<double>("x"),point.Get<double>("y"))).Distinct().Count()<3)throw new ArgumentException("多边形至少需要三个不同顶点。");
            return;
        }
        PointChecked(shape.Get<RecordValue>("center"));
        if(kind=="rectangle")
        {if(Number("width")/2<=0||Number("height")/2<=0)throw new ArgumentException("矩形尺寸必须为正。");_=Number("angle-degrees");return;}
        if(Number("inner-radius")<0||Number("outer-radius")<=Number("inner-radius"))throw new ArgumentException("圆环半径无效。");
        var sweep=Number("end-radians")-Number("start-radians");
        if(!double.IsFinite(sweep)||Math.Abs(sweep)<1e-12||Math.Abs(sweep)>2*Math.PI+1e-9||kind=="sector"&&sweep<=0)throw new ArgumentException("形状角度范围无效。");
    }
}

/// <summary>Blind-region subscription uses one comma-delimited coordinate string per polygon.</summary>

public static class BlobMaskInheritance
{
    public const string ModeId = "input.roi.mask.inherit";
    public const string SourceId = "input.roi.mask.polygon-strings";
    internal const int MaximumVertices = 32;
    private const int MaximumPayloadLength = 1023;
    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);
    public static TypeRef Polygons { get; } = TypeRef.SequenceOf(TypeRef.GeometryOf(GeometryKind.Polygon, RoiDeclarations.ImageFrame));
    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(ModeId, DataType.Boolean, false, EditModes:FieldEditModes.Constant),
        new Port(SourceId, PolygonStrings, false, EditModes:FieldEditModes.Reference)
    });
    public static bool IsInherited(ParameterSetting setting) => setting switch
    {
        ConstantSetting {Value:bool enabled} => enabled,
        UnsetSetting => false,
        _ => throw new ArgumentException("屏蔽区创建方式必须是固定值。")
    };

    public static RecordValue? Materialize(InputValues inputs, RecordValue? roi)
    {
        if (!inputs.Contains(ModeId) || !inputs.Get<bool>(ModeId)) return roi;
        var source = inputs.Get<SequenceValue>(SourceId);
        if (source.Count > BlobLocalRoi.MaximumInheritedCount)
            throw new ArgumentException($"继承屏蔽区最多支持 {BlobLocalRoi.MaximumInheritedCount} 个多边形。");
        var rows = source.Cast<string>().Select((polygon,index) =>
        {
            var id = new Guid(0x00a9ef00 + index,0x6985,0x4e44,0xa8,0x82,0xf9,0x16,0x75,0x2a,0x53,0x91);
            var vertices = new SequenceValue(TypeRef.SequenceOf(BlobLocalRoi.PointType), ReadVertices(polygon, index + 1));
            var shape = new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?> {["kind"]="polygon",["vertices"]=vertices});
            BlobLocalRoi.ValidateShape(shape);
            return BlobLocalRoi.Item(id,"继承屏蔽区"+(index+1),shape);
        }).ToArray();
        var inspection = (roi ?? BlobLocalRoi.Empty).Get<RecordValue>(BlobLocalRoi.Inspection);
        return BlobLocalRoi.Root(inspection,BlobLocalRoi.Group(rows.Length==0?BlobLocalRoi.None:BlobLocalRoi.Explicit,rows));
    }

    private static IEnumerable<RecordValue> ReadVertices(string polygon, int index)
    {
        if (polygon.Length > MaximumPayloadLength) throw new ArgumentException($"继承屏蔽区 {index} 的坐标文本超过长度上限。");
        var coordinates = polygon.Split(',');
        if (coordinates.Length < 6 || coordinates.Length > MaximumVertices * 2 || coordinates.Length % 2 != 0)
            throw new ArgumentException($"继承屏蔽区 {index} 需要 3 至 {MaximumVertices} 对坐标。");
        for (var i = 0; i < coordinates.Length; i += 2)
            yield return BlobLocalRoi.Point(Parse(coordinates[i]), Parse(coordinates[i + 1]));

        float Parse(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && float.IsFinite((float)number)) return (float)number;
            throw new ArgumentException($"继承屏蔽区 {index} 包含无效坐标。");
        }
    }

    internal static string Encode(PolygonGeometry polygon)
    {
        var payload = string.Join(",", polygon.Vertices.SelectMany(point => new[] { Format(point.X), Format(point.Y) }));
        if (payload.Length > MaximumPayloadLength) throw new ArgumentException("屏蔽区坐标文本超过订阅长度上限。");
        return payload;

        static string Format(double coordinate)
        {
            var value = (float)coordinate;
            if (!float.IsFinite(value)) throw new ArgumentException("屏蔽区坐标超出单精度范围。");
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }

    internal static SequenceValue Publish(RecordValue? roi, RecordValue? pose)
    {
        var rows = BlobLocalRoi.Items(roi ?? BlobLocalRoi.Empty,BlobLocalRoi.Mask);
        var matrix = pose is null ? null : new[]{"m11","m12","m21","m22","offset-x","offset-y"}.Select(pose.Get<double>).ToArray();
        Point2 Point(RecordValue value)
        {
            var x = value.Get<double>("x");var y = value.Get<double>("y");
            return matrix is null ? new(x,y) : new(x*matrix[0]+y*matrix[2]+matrix[4],x*matrix[1]+y*matrix[3]+matrix[5]);
        }
        return new SequenceValue(Polygons,rows.Where(row=>row.Get<bool>("enabled"))
            .Select(row=>row.Get<RecordValue>("shape")).Where(shape=>shape.Get<string>("kind")=="polygon")
            .Select(shape=>(object?)new PolygonGeometry(BlobSchema.ImageFrame,shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(Point))));
    }
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class BlobNonFieldAttribute : Attribute;

/// <summary>Versioned tool metadata. It initializes new settings, never patches saved settings.</summary>

public static class BlobRoiInheritance
{
    public const string ModeId="input.roi.inheritance-mode";
    public const string Local="Local",Rectangle="Rectangle",RectangleParameters="RectangleParameters",Annulus="Annulus",AnnulusParameters="AnnulusParameters";
    public const string RectangleSource="input.roi.rectangle",AnnulusSource="input.roi.annulus";
    public static IReadOnlyList<string> RectangleMembers {get;}=Array.AsReadOnly(new[]{"center-x","center-y","width","height","angle-degrees"});
    public static IReadOnlyList<string> AnnulusMembers {get;}=Array.AsReadOnly(new[]{"center-x","center-y","inner-radius","outer-radius","start-degrees","sweep-degrees"});
    public static TypeRef RectangleType {get;}=ShapeType("blob.roi.rectangle",RectangleMembers);
    public static TypeRef AnnulusType {get;}=ShapeType("blob.roi.annulus",AnnulusMembers);
    private static TypeRef ShapeType(string name,IReadOnlyList<string> members)
        => TypeRef.RecordOf(name,new[] {new Port("center",FixtureDeclarations.Point)}
            .Concat(members.Skip(2).Select(id=>new Port(id,DataType.Number,Storage:NumericStorage.Float32))));

    private static DeclaredValueReader ShapeMember(DeclaredValueReader shape,string member) => member switch
    {
        "center-x" => shape.GetField("center").GetField("x"),
        "center-y" => shape.GetField("center").GetField("y"),
        _ => shape.GetField(member)
    };
    public static string Leaf(string mode,string member)=>"input.roi."+(mode==RectangleParameters?"rectangle.":mode==AnnulusParameters?"annulus.":throw new ArgumentException("Unknown inheritance parameter mode."))+member;
    public static IReadOnlyList<Port> Ports {get;}=Array.AsReadOnly(new[]{
        new Port(ModeId,DataType.Text,Required:false,EditModes:FieldEditModes.Constant),
        new Port(RectangleSource,TypeRef.SequenceOf(RectangleType),Required:false,EditModes:FieldEditModes.Reference),
        new Port(AnnulusSource,TypeRef.SequenceOf(AnnulusType),Required:false,EditModes:FieldEditModes.Reference)}
        .Concat(RectangleMembers.Select(id=>new Port(Leaf(RectangleParameters,id),DataType.NumberArray,Required:false,EditModes:FieldEditModes.Reference)))
        .Concat(AnnulusMembers.Select(id=>new Port(Leaf(AnnulusParameters,id),DataType.NumberArray,Required:false,EditModes:FieldEditModes.Reference))).ToArray());
    public static string ReadMode(FieldSettings fields)=>ReadMode(fields.Read(ModeId));
    internal static string ReadMode(ParameterSetting setting)=>setting switch{
        UnsetSetting=>Local,ConstantSetting {Value:string mode}=>ValidateMode(mode),_=>throw new ArgumentException("ROI继承方式必须是固定模式。")};
    public static string ValidateMode(string mode)=>mode is Local or Rectangle or RectangleParameters or Annulus or AnnulusParameters?mode:throw new ArgumentException("ROI继承方式无效。");

    internal static IEnumerable<string> SourcesToClear(string mode)
    {
        // InheritWayGroup2 clears the opposite shape family and keeps sources
        // when switching between area and parameters within the same family.
        if (mode is Local or Annulus or AnnulusParameters)
        {
            yield return RectangleSource;
            foreach (var member in RectangleMembers) yield return Leaf(RectangleParameters, member);
        }
        if (mode is Local or Rectangle or RectangleParameters)
        {
            yield return AnnulusSource;
            foreach (var member in AnnulusMembers) yield return Leaf(AnnulusParameters, member);
        }
    }
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
        for(var i=0;i<Math.Min(BlobLocalRoi.MaximumInheritedCount,count);i++)
        {
            var shapeSource=source?.GetElement(i);
            double Number(string member)
            {
                var value=shapeSource is not null?ShapeMember(shapeSource,member).GetScalar<double>():leaves![member].GetElement(i).GetScalar<double>();
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

