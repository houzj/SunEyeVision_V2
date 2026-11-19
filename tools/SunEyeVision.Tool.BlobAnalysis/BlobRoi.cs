using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using System.Windows;
using System.Xml.Linq;
using System.Xml;
using System;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime
{

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
    public const string Inspection=RoiDocumentValue.Inspection,Mask=RoiDocumentValue.Mask;
    public const string None=RoiDocumentValue.None,FullDomain=RoiDocumentValue.FullDomain,Explicit=RoiDocumentValue.Explicit;
    public static TypeRef PointType=>RoiDocumentValue.PointType;
    public static TypeRef ShapeType=>RoiDocumentValue.ShapeType;
    public static TypeRef ItemType=>RoiDocumentValue.ItemType;
    public static TypeRef ItemsType=>RoiDocumentValue.ItemsType;
    public static TypeRef GroupType=>RoiDocumentValue.GroupType;
    public static TypeRef Declaration=>RoiDocumentValue.Declaration;
    public static Port Port {get;}=new(InputId,RoiDocumentValue.Declaration,Required:false,EditModes:FieldEditModes.Constant);
    public static RecordValue Empty=>RoiDocumentValue.Empty;
    public static RecordValue Point(double x,double y)=>RoiDocumentValue.Point(x,y);
    public static RecordValue Item(Guid id,string name,RecordValue shape,bool enabled=true,bool visible=true)
        =>RoiDocumentValue.Item(id,name,shape,enabled,visible);
    public static RecordValue Group(string coverage,IEnumerable<RecordValue> items)
        =>RoiDocumentValue.Group(coverage,items);
    public static RecordValue Root(RecordValue inspection,RecordValue mask)
        =>RoiDocumentValue.Root(inspection,mask);
    public static IReadOnlyList<RecordValue> Items(RecordValue root,string group)
        =>RoiDocumentValue.Items(root,group);
    public static string Coverage(RecordValue root,string group)=>RoiDocumentValue.Coverage(root,group);
    public static bool Same(object? left,object? right)=>RoiDocumentValue.Same(left,right);
    public static double NormalizeDegrees(double angle)=>RoiDocumentValue.NormalizeDegrees(angle);
    public static void ValidateShape(RecordValue shape)=>RoiDocumentValue.ValidateShape(shape);
    public const int MaximumLocalInspectionCount=16;
    public const int MaximumLocalMaskCount=8;
    internal const int MaximumInheritedCount=64;
    /// <summary>通用结构校验之后追加 Blob 的容量规则。</summary>
    public static void Validate(RecordValue root,int maximumInspectionCount=MaximumLocalInspectionCount,bool inheritedMask=false)
    {
        RoiDocumentValue.Validate(root);
        if(maximumInspectionCount is not (MaximumLocalInspectionCount or MaximumInheritedCount))
            throw new ArgumentException("区域数量合同无效。");
        foreach(var group in new[]{Inspection,Mask})
        {
            var count=Items(root,group).Count;
            if(group==Inspection&&count>maximumInspectionCount)
                throw new ArgumentException("检测区域数量超出允许范围。");
            if(group==Mask&&count>(inheritedMask?MaximumInheritedCount:MaximumLocalMaskCount))
                throw new ArgumentException("屏蔽区数量超出允许范围。");
        }
    }
}


/// <summary>Blind-region subscription uses one comma-delimited coordinate string per polygon.</summary>

public static class BlobMaskInheritance
{
    public const string ModeId = "input.roi.mask.inherit";
    public const string SourceId = "input.roi.mask.polygon-strings";
    internal const int MaximumVertices=(int)RoiDocumentValue.MaximumVertices;
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

}

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views
{

/// <summary>One private formal-input owner; the two sessions are read-only projections of that same value.</summary>
/// <summary>ROI 文档的操作与历史：只处理 Blob 数据结构，不碰编辑器、帧和会话。</summary>
internal sealed class BlobRoiDocument : INativeRoiEditDocument
{
    private readonly DeclaredInputFieldsView _inputs;
    private readonly Stack<RecordValue> _undo = new(), _redo = new();

    public BlobRoiDocument(DeclaredInputFieldsView inputs) => _inputs = inputs;

    public IReadOnlyList<NativeRoiEditGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new NativeRoiEditGroup(BlobLocalRoi.Inspection, "形状", false,
            BlobLocalRoi.MaximumLocalInspectionCount, BlobLocalRoi.FullDomain),
        new NativeRoiEditGroup(BlobLocalRoi.Mask, "屏蔽区", true,
            BlobLocalRoi.MaximumLocalMaskCount, BlobLocalRoi.None)
    });

    public RecordValue Empty => BlobLocalRoi.Empty;
    public void Write(RecordValue document) =>
        _inputs.WriteUnpresentedInput(BlobLocalRoi.InputId, new ConstantSetting(document));
    public IRoiDocument View(RecordValue document) => new BlobRoiDocumentView(document);
    public RecordValue ToNative(RecordValue geometry) => BlobLocalRoiProjection.ToNative(geometry);
    public RecordValue FromNative(RecordValue geometry, RecordValue? previous) =>
        BlobLocalRoiProjection.FromNative(geometry, previous);
    public bool Same(RecordValue left, RecordValue right) => BlobLocalRoi.Same(left, right);
    public string ImageInputFieldId => BlobTool.ImageInput;
    public string Coverage(RecordValue document, string group) => BlobLocalRoi.Coverage(document, group);
    public string ModeFieldId => BlobRoiInheritance.ModeId;
    public string LocalModeValue => BlobRoiInheritance.Local;
    public bool IsPreviewSource(string fieldId) => fieldId == BlobTool.ImageInput
        || fieldId == BlobMaskInheritance.SourceId
        || BlobRoiInheritance.Ports.Any(port => port.Id == fieldId);
    public IReadOnlyList<string> PreviewInputIds(string mode, bool maskInherited) => (mode switch
    {
        BlobRoiInheritance.Rectangle => new[] { BlobRoiInheritance.RectangleSource },
        BlobRoiInheritance.Annulus => new[] { BlobRoiInheritance.AnnulusSource },
        BlobRoiInheritance.Local => Array.Empty<string>(),
        BlobRoiInheritance.RectangleParameters => BlobRoiInheritance.RectangleMembers
            .Select(member => BlobRoiInheritance.Leaf(mode, member)).ToArray(),
        _ => BlobRoiInheritance.AnnulusMembers.Select(member => BlobRoiInheritance.Leaf(mode, member)).ToArray()
    }).Concat(maskInherited ? new[] { BlobMaskInheritance.ModeId, BlobMaskInheritance.SourceId } : Array.Empty<string>())
        .ToArray();
    public RecordValue MaterializePreview(InputValues inputs, RecordValue document)
        => BlobMaskInheritance.Materialize(inputs, BlobRoiInheritance.Materialize(inputs, document))!;
    public string PreviewMode() =>
        BlobRoiInheritance.ReadMode(_inputs.ReadUnpresentedInput(BlobRoiInheritance.ModeId));

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public RecordValue Read()
    {
        var setting = _inputs.ReadUnpresentedInput(BlobLocalRoi.InputId);
        var value = setting switch
        {
            UnsetSetting => BlobLocalRoi.Empty,
            ConstantSetting { Value: RecordValue record } => record,
            _ => throw new ArgumentException("本地区域必须为纯值定义。")
        };
        BlobLocalRoi.Validate(value);
        return value;
    }

    public bool TryApply(RecordValue before, string group, RoiEditProposal proposal,
        out RecordValue after, out RoiEditSelection selection, out string error)
    {
        after = before;
        selection = new RoiEditSelection.Keep();
        error = "";
        var rows = BlobLocalRoi.Items(before, group).ToList();
        var coverage = BlobLocalRoi.Coverage(before, group);
        var history = false;
        try
        {
            switch (proposal)
            {
                case RoiEditProposal.Create create when create.Group == group:
                    var id = Guid.NewGuid();
                    rows.Add(BlobLocalRoi.Item(id, group == BlobLocalRoi.Inspection ? "检测区域" : "屏蔽区域",
                        BlobLocalRoiProjection.FromNative(create.Geometry)));
                    coverage = BlobLocalRoi.Explicit;
                    selection = new RoiEditSelection.Select(new(group, id));
                    break;
                case RoiEditProposal.Replace replace when replace.Target.Group == group:
                    var index = rows.FindIndex(row => row.Get<string>("id") == replace.Target.Id.ToString("N"));
                    if (index < 0) throw new ArgumentException("区域已不存在。");
                    var item = rows[index];
                    rows[index] = BlobLocalRoi.Item(replace.Target.Id, item.Get<string>("name"),
                        BlobLocalRoiProjection.FromNative(replace.Geometry, item.Get<RecordValue>("shape")),
                        item.Get<bool>("enabled"), item.Get<bool>("visible"));
                    break;
                case RoiEditProposal.Delete delete when delete.Targets.All(target => target.Group == group):
                    foreach (var target in delete.Targets)
                        if (rows.RemoveAll(row => row.Get<string>("id") == target.Id.ToString("N")) != 1)
                            throw new ArgumentException("区域已不存在。");
                    if (rows.Count == 0)
                        coverage = group == BlobLocalRoi.Inspection ? BlobLocalRoi.FullDomain : BlobLocalRoi.None;
                    selection = new RoiEditSelection.Clear();
                    break;
                case RoiEditProposal.SetCoverage set when set.Group == group
                    && set.Coverage == (group == BlobLocalRoi.Inspection ? RoiCoverage.FullDomain : RoiCoverage.None):
                    rows.Clear();
                    coverage = set.Coverage.ToString();
                    selection = new RoiEditSelection.Clear();
                    break;
                case RoiEditProposal.Undo when _undo.Count > 0:
                    after = _undo.Peek(); history = true; selection = new RoiEditSelection.Clear(); break;
                case RoiEditProposal.Redo when _redo.Count > 0:
                    after = _redo.Peek(); history = true; selection = new RoiEditSelection.Clear(); break;
                default: throw new ArgumentException("此区域操作尚未接通。");
            }
            if (!history)
                after = group == BlobLocalRoi.Inspection
                    ? BlobLocalRoi.Root(BlobLocalRoi.Group(coverage, rows), before.Get<RecordValue>(BlobLocalRoi.Mask))
                    : BlobLocalRoi.Root(before.Get<RecordValue>(BlobLocalRoi.Inspection), BlobLocalRoi.Group(coverage, rows));
            if (BlobLocalRoi.Items(after, BlobLocalRoi.Mask)
                .Any(row => row.Get<RecordValue>("shape").Get<string>("kind") != "polygon"))
                throw new ArgumentException("屏蔽区仅支持多边形。");
            BlobLocalRoi.Validate(after);
            if (BlobLocalRoi.Same(before, after)) throw new ArgumentException("区域内容没有变化。");
            if (proposal is RoiEditProposal.Undo) { _undo.Pop(); _redo.Push(before); }
            else if (proposal is RoiEditProposal.Redo) { _redo.Pop(); _undo.Push(before); }
            else { _undo.Push(before); _redo.Clear(); }
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException)
        {
            after = before;
            error = failure.Message;
            return false;
        }
    }
}

/// <summary>把一个 Blob 本地 ROI 文档投影成平台可读的 ROI 文档视图。</summary>
internal sealed class BlobRoiDocumentView : IRoiDocument
{
    private readonly RecordValue _root;

    public BlobRoiDocumentView(RecordValue root) => _root = root;

    public IReadOnlyList<string> Groups { get; } =
        Array.AsReadOnly(new[] { BlobLocalRoi.Inspection, BlobLocalRoi.Mask });

    public string Title(string group) => group == BlobLocalRoi.Inspection ? "形状" : "屏蔽区";
    public bool PolygonOnly(string group) => group == BlobLocalRoi.Mask;

    public int MaximumItems(string group) => group == BlobLocalRoi.Inspection
        ? BlobLocalRoi.MaximumLocalInspectionCount
        : BlobLocalRoi.MaximumLocalMaskCount;

    public string DefaultCoverage(string group) => group == BlobLocalRoi.Inspection
        ? BlobLocalRoi.FullDomain
        : BlobLocalRoi.None;

    public string Coverage(string group) => BlobLocalRoi.Coverage(_root, group);

    public IReadOnlyList<RoiDocumentItem> Items(string group) => BlobLocalRoi.Items(_root, group)
        .Select(item => new RoiDocumentItem(Guid.ParseExact(item.Get<string>("id"), "N"),
            item.Get<string>("name"), item.Get<RecordValue>("shape"),
            item.Get<bool>("enabled"), item.Get<bool>("visible")))
        .ToArray();

    public IRoiDocument WithItems(string group, IReadOnlyList<RoiDocumentItem> items, string coverage)
    {
        var rows = items.Select(item => BlobLocalRoi.Item(item.Id, item.Name, item.Geometry, item.Enabled, item.Visible));
        var next = group == BlobLocalRoi.Inspection
            ? BlobLocalRoi.Root(BlobLocalRoi.Group(coverage, rows), _root.Get<RecordValue>(BlobLocalRoi.Mask))
            : BlobLocalRoi.Root(_root.Get<RecordValue>(BlobLocalRoi.Inspection), BlobLocalRoi.Group(coverage, rows));
        return new BlobRoiDocumentView(next);
    }
}

internal static class BlobLocalRoiProjection
{
    internal static RecordValue ToNative(RecordValue shape)
    {
        var projected=BlobRoiDisplayProjection.Shape(shape);
        RoiGeometryEditing.Validate(projected);
        return projected;
    }
    internal static RecordValue FromNative(RecordValue shape,RecordValue? previous=null)
    {
        RoiGeometryEditing.Validate(shape);
        var fields=shape.CopyFields().Where(pair=>pair.Key!="closed").ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(shape.Contains("center")){var point=shape.Get<PointGeometry>("center").Point;fields["center"]=BlobLocalRoi.Point(point.X,point.Y);}
        if(shape.Contains("vertices"))fields["vertices"]=new SequenceValue(BlobLocalRoi.ShapeType.Fields.Single(p=>p.Id=="vertices").Type,
            shape.Get<SequenceValue>("vertices").Cast<PointGeometry>().Select(p=>(object?)BlobLocalRoi.Point(p.Point.X,p.Point.Y)));
        if(previous?.Get<string>("kind")=="rectangle"&&shape.Get<string>("kind")=="rectangle"
            &&BlobLocalRoi.NormalizeDegrees(previous.Get<double>("angle-degrees"))==BlobLocalRoi.NormalizeDegrees(shape.Get<double>("angle-degrees")))
            fields["angle-degrees"]=previous.Get<double>("angle-degrees");
        var result=new RecordValue(BlobLocalRoi.ShapeType,fields);BlobLocalRoi.ValidateShape(result);return result;
    }
}

internal static class BlobRoiUi
{
    internal static NativeAssistUiDraftControls? Add(DeclaredInputFieldsView inputs,Action<string,FrameworkElement,string?> attach,Action<NativeRoiUiDraftControls> observe)
    {
        var rectangleLabels=new[]{"roicenterx","roicentery","roiwidth","roiheight","roiangle"};
        var annulusLabels=new[]{"roiAnnulusCenterX","roiAnnulusCenterY","CircleInnerRadius","roiAnnulusOuterRadius","Angle Start","AngleRange"};
        IReadOnlyList<DeclaredFieldRow> Fields(string mode,IReadOnlyList<string> members,IReadOnlyList<string> labels)
            =>members.Select((member,index)=>inputs.ComposeInputEditor(BlobRoiInheritance.Leaf(mode,member),labels[index])).ToArray();
        // 控件行、继承绑定和插入位置都由平台按声明创建，工具只交字段名与模式取值。
        var controls=NativeRoiGroupComposer.Compose(inputs,new RoiGroupComposerSpec
        {
            ModeFieldId=BlobRoiInheritance.ModeId,
            LocalModeValue=BlobRoiInheritance.Local,
            InheritedModes=new[]{BlobRoiInheritance.Rectangle,BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.Annulus,BlobRoiInheritance.AnnulusParameters},
            RectangleSourceFieldId=BlobRoiInheritance.RectangleSource,
            AnnulusSourceFieldId=BlobRoiInheritance.AnnulusSource,
            RectangleParameterFields=()=>Fields(BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.RectangleMembers,rectangleLabels),
            AnnulusParameterFields=()=>Fields(BlobRoiInheritance.AnnulusParameters,BlobRoiInheritance.AnnulusMembers,annulusLabels),
            AreaTypeFieldId=BlobImageRoi.ModeId,
            AreaTypeLabel="ROI Area Type",
            AreaTypeChoices=new[]
            {
                new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("Type")!),
                new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_Image Type")!)
            },
            MaskImageFieldId=BlobImageRoi.ImageId,
            MaskImageLabel="Mask Image",
            OutputMaskFieldId=BlobImageRoi.OutputEnabledId,
            OutputMaskLabel="OutputMask",
            MaskSourceFieldId=BlobMaskInheritance.SourceId,
            MaskSourceLabel="屏蔽区",
            ReadMaskInherited=()=>BlobMaskInheritance.IsInherited(inputs.ReadUnpresentedInput(BlobMaskInheritance.ModeId)),
            AcceptMaskInherited=value=>inputs.WriteUnpresentedInput(BlobMaskInheritance.ModeId,new ConstantSetting(value)),
            AttachAnchorFieldId=BlobTool.ImageInput,
            AttachSection="ROI区域"
        },attach,observe);
        try
        {
            var pose=inputs.Fields.SingleOrDefault(field=>field.FieldId==BlobTool.PoseInput);
            if(pose is null)return null;
            var mode=inputs.ComposeInputEditor(BlobFixtureInputs.PositionCorrectionModeId,"Choose Mode",defaultWhenUnset:new ConstantSetting(0L),
                integerRadioOptions:new NativeIntegerRadioOptions(new[]
                {
                    new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("RunParam_By Info","按信息")),
                    new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_By Point","按点")),
                    new NativeIntegerRadioChoice(2,AppLocalizationService.GetLang("RunParam_By Coordinate","按坐标"))
                },useFiveSlotTemplate:true));
            return new NativeAssistUiDraftControls(mode,pose,
                Sources("reference","Fixtured Point","Init",BlobFixtureInputs.ReferencePoint)
                .Concat(Sources("run","Unfixtured Point","Run",BlobFixtureInputs.RunPoint)));
        }
        catch {controls.Dispose();throw;}
        IEnumerable<(int Mode, Func<DeclaredFieldRow> Create)> Sources(string pose,string pointLabel,string labelPrefix,string point)
        {
            (int, Func<DeclaredFieldRow>) Source(int mode,string id,string caption)
                => (mode,()=>inputs.ComposeInputEditor(id,caption,referenceOnly:true));
            var prefix=BlobTool.PoseInput+"."+pose;
            yield return Source(1,point,pointLabel);
            yield return Source(2,point+".x",labelPrefix+"PointX");
            yield return Source(2,point+".y",labelPrefix+"PointY");
            yield return Source(3,prefix+".angle",labelPrefix+"Angle");
            yield return Source(3,prefix+".scale-x",labelPrefix+"ScaleX");
            yield return Source(3,prefix+".scale-y",labelPrefix+"ScaleY");
        }
    }
}
}
