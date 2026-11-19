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

internal sealed partial class BlobLocalRoiOwner : IDisposable
{
    private readonly DeclaredInputFieldsView _inputs;
    private readonly NativeRoiUiDraftControls _controls;
    private readonly NativeRoiFrameSource? _source;
    private readonly SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow _imageField;
    private readonly Dictionary<string,NativeRoiEditor> _editors=new();
    private readonly Dictionary<string,NativeRoiSession> _sessions=new();
    private readonly Dictionary<string,Guid> _selectedRoiIds=new();
    private readonly Dictionary<NativeRoiSession,Action> _retirementHandlers=new();
    private bool _recordingRetirement;
    private readonly Dictionary<string,Guid> _scopes=new(){{_primaryGroup,Guid.NewGuid()},{_secondaryGroup,Guid.NewGuid()}};
    private readonly BlobRoiDocument _document;
    private const string _primaryGroup=BlobLocalRoi.Inspection,_secondaryGroup=BlobLocalRoi.Mask;
    private readonly Dictionary<string,NativeRoiPendingInput> _pending=new();
    private string? _unrestoredInput;
    private NativeRoiFrameLease? _frame;
    private INativeRoiEditLease? _interaction;
    private string? _active;
    private string? _pendingActivate;
    private RecordValue _observed;
    private bool _disposed,_accepting,_refreshing,_activating;
    private bool _synchronizingSelection;
    private readonly Func<string?>? _drawBlockReason;
    private bool _localInspection,_localMask;
    private string _observedPreviewMode=BlobRoiInheritance.Local;
    private long _generation,_projectionRevision;
    public long Revision {get;private set;}
    public string Error {get;private set;}="";
    private bool UnacceptedGesture=>_sessions.Values.Any(session=>session.ActiveDraw is not null||session.Items.Any(item=>item.HasPending))||_interaction?.HasPendingGesture==true;
    public bool HasPending=>_unrestoredInput is not null||_pending.Count>0||_editors.Values.Any(editor=>editor.HasPendingEdits)||UnacceptedGesture;
    public event Action? Changed;
    internal BlobLocalRoiOwner(DeclaredInputFieldsView inputs,NativeRoiUiDraftControls controls,NativeRoiFrameSource? source,Func<string?>? drawBlockReason=null)
    {
        _inputs=inputs;_controls=controls;_source=source;_drawBlockReason=drawBlockReason;_document=new(inputs);_observed=Read();
        _imageField=inputs.Fields.Single(field=>field.FieldId==BlobTool.ImageInput);
        try
        {
            foreach(var group in new[]{_primaryGroup,_secondaryGroup})
            {
                var editor=new NativeRoiEditor{Title=group==_primaryGroup?"形状":"屏蔽区",PolygonOnly=group==_secondaryGroup,
                    ToolTip="先运行图像来源，再编辑区域。"};
                editor.PrepareInteraction=()=>Activate(group);editor.InteractionFailureText=()=>_drawBlockReason?.Invoke()??(string.IsNullOrWhiteSpace(Error)?null:Error);editor.AllowUnboundFullDomain=group==_primaryGroup;editor.UnboundFullDomain=BlobLocalRoi.Coverage(_observed,group)==BlobLocalRoi.FullDomain;editor.PendingChanged+=Notify;_editors.Add(group,editor);editor.Bind(null);
            }
            controls.BindLocalEditors(_editors[_primaryGroup],_editors[_secondaryGroup]);
            controls.Changed+=OnModesChanged;
            _imageField.Changed+=RefreshFrame;
            if(source is not null){source.Changed+=RefreshFrame;source.FrameReplaced+=RebindCurrentFrame;}
            if(source is not null)source.InputViewportClicked+=OnInputViewportClicked;
            _localInspection=_controls.IsLocalDrawing(_primaryGroup);_localMask=_controls.IsLocalDrawing(_secondaryGroup);
            _observedPreviewMode=PreviewMode();InitializePreview();
        }
        catch(Exception failure)
        {
            try{Dispose();}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }

    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private RecordValue Read()=>_document.Read();
    private RoiEditSnapshot Snapshot(string editableGroup)
    {
        var root=Read();
        if(!_document.Same(root,_observed))throw new InvalidOperationException("本地区域已被其他编辑替换，请重新打开窗口。");
        return Project(root,editableGroup,_projectionRevision,_frame!.Image.Frame);
    }
    private RoiEditSnapshot Project(RecordValue root,string editableGroup,long revision,ImageFrameAddress frame)
        => RoiDocumentProjection.Project(new BlobRoiDocumentView(root),editableGroup,_scopes[editableGroup],revision,frame,
            _controls.IsLocalDrawing,_document.ToNative);
    private bool FrameCurrent(long generation)
    {
        // VM keeps the ROI objects attached to the module result view while
        // parameter sub-tabs are switched. The basic-page control may be
        // unloaded or hidden during that navigation, but the physical frame
        // and ROI session remain valid until the input frame or editor scope
        // actually changes.
        var frame=_frame;
        return !_disposed&&generation==_generation&&frame is not null&&frame.IsCurrent&&ReferenceEquals(frame,_frame);
    }
    private void AcquireFrame(bool interactive)
    {
        if(_disposed||_refreshing||_accepting||_activating||!_controls.IsLoaded||!_controls.IsVisible)return;
        _refreshing=true;
        try
        {
            if(FrameCurrent(_generation))return;
            RetireFrame();if(_disposed)return;var generation=_generation;
            var frame=_source?.Borrow(BlobTool.ImageInput,interactive);
            if(frame is null){Error=_drawBlockReason?.Invoke()??"请先运行并选择图像来源。";Notify();return;}
            if(_disposed||generation!=_generation||!frame.IsCurrent){frame.Dispose();return;}_frame=frame;
            foreach(var group in new[]{_primaryGroup,_secondaryGroup})
            {
                NativeRoiSession? session=null;
                session=new NativeRoiSession(frame.Image,()=>Snapshot(group),(stamp,proposal)=>Accept(ProposalGroup(proposal,session!.DrawingGroup),stamp,proposal),()=>FrameCurrent(generation),
                    ()=>new(_document.CanUndo,_document.CanRedo),group,isAvoid:group==_secondaryGroup,
                    canDraw:target=>target==group&&_controls.IsLocalDrawing(group),canSetCoverage:target=>target==group&&_controls.IsLocalDrawing(group),
                    maximumDrawItems:group==_primaryGroup?BlobLocalRoi.MaximumLocalInspectionCount:BlobLocalRoi.MaximumLocalMaskCount);
                _sessions.Add(group,session);
                var observedSession=session;
                observedSession.Changed+=()=>SyncSelection(observedSession);
                Action retiring=()=>RecordInteractionRetirement(group,session,generation);
                session.InteractionRetiring+=retiring;_retirementHandlers.Add(session,retiring);
                if(_pending.TryGetValue(group,out var pending)&&_controls.IsLocalDrawing(group))session.Select(group,pending.Id);
                _editors[group].Bind(session);
                if(pending is not null&&_controls.IsLocalDrawing(group))
                {
                    if(_editors[group].RestorePendingInput(pending))_pending.Remove(group);
                    else _unrestoredInput="区域对象已改变，未提交原文不能应用；请取消窗口后重新编辑。";
                }
                if(_selectedRoiIds.TryGetValue(group,out var selectedId)&&_controls.IsLocalDrawing(group))
                    session.Select(group,selectedId);
                if(!_selectedRoiIds.ContainsKey(group)&&_controls.IsLocalDrawing(group))
                {
                    var initial=session.Items.FirstOrDefault(item=>item.Enabled&&item.Visible);
                    if(initial is not null&&session.Select(group,initial.Id))
                    {
                        _selectedRoiIds[group]=initial.Id;
                        _pendingActivate ??= group;
                    }
                }
            }
            Error="";Notify();
            CompletePendingActivation();
        }
        catch(Exception error)
        {
            Error=error.Message;
            try{RetireFrame();}catch(Exception cleanup){throw new AggregateException(error,cleanup);}
            Notify();
        }
        finally{_refreshing=false;}
    }
    private static string ProposalGroup(RoiEditProposal proposal,string fallback)
        =>proposal switch
        {
            RoiEditProposal.Create create=>create.Group,
            RoiEditProposal.Replace replace=>replace.Target.Group,
            RoiEditProposal.SetCoverage coverage=>coverage.Group,
            RoiEditProposal.Delete delete=>delete.Targets[0].Group,
            _=>fallback
        };
    private void SyncSelection(NativeRoiSession source)
    {
        if(_disposed||_refreshing||_synchronizingSelection||source.CurROIData is not { } selected)return;
        _selectedRoiIds[selected.Group]=selected.Id;
        _synchronizingSelection=true;
        try
        {
            foreach(var pair in _sessions)
                if(!ReferenceEquals(pair.Value,source)&&pair.Value.IsCurrent&&
                    (pair.Value.CurROIData?.Group!=selected.Group||pair.Value.CurROIData?.Id!=selected.Id))
                    pair.Value.Select(selected.Group,selected.Id);
        }
        finally{_synchronizingSelection=false;}
    }
    internal void RefreshFrame()
    {
        if(_disposed||_refreshing||_accepting||_activating)return;
        // 原件语义（SetRoiVisible + UpdateROI，ModuleResultView.xaml.cs.pending:2252/:604-608）：
        // 形状=参数对象，页签/窗口可见性变化只隐藏不销毁；仅图像帧身份变化才退役。
        // 退役后（如切换可编辑图像）立即按当前来源重建会话，既有形状随参数值恢复显示，
        // 不依赖用户再点绘制按钮。
        if(_frame is not {} frame||!frame.IsCurrent)
        {
            if(_active is {} active)_pendingActivate=active;
            _refreshing=true;
            try{RetireFrame();Notify();}
            finally{_refreshing=false;}
        }
        if(_controls.IsVisible&&_controls.IsLoaded&&_sessions.Count==0
            &&(_controls.IsLocalDrawing(_primaryGroup)||_controls.IsLocalDrawing(_secondaryGroup)))
            AcquireFrame(interactive:false);
    }
    private void RebindCurrentFrame()
    {
        if(_disposed||_refreshing||_accepting||_activating)return;
        if(!_controls.IsLoaded||!_controls.IsVisible)return;
        var reacquire=_controls.IsLocalDrawing(_primaryGroup)||_controls.IsLocalDrawing(_secondaryGroup);
        if(_active is {} active)_pendingActivate=active;
        _refreshing=true;
        try
        {
            RetireFrame();
        }
        finally{_refreshing=false;}
        if(!_disposed&&reacquire)AcquireFrame(interactive:false);
    }
    private bool ActivateCore(string group)
    {
        if(!FrameCurrent(_generation)||!_sessions.TryGetValue(group,out var session))return false;
        if(_active==group&&_interaction?.IsCurrent==true)
        {
            // A free hit may have changed the selected object's group while
            // keeping the same viewport lease. Selecting a new-draw button
            // must restore that session's creation group before BeginDraw.
            if(session.DrawingGroup!=group)session.SetDrawingGroup(group);
            return true;
        }
        if(_interaction?.HasPendingGesture==true){Error="请先完成或取消当前绘制。";return false;}
        _activating=true;var generation=_generation;
        try
        {
            if(_active is {} previous&&!_editors[previous].TryCommitPending()){Error=_editors[previous].Error;return false;}
            var old=_interaction;_interaction=null;_active=null;old?.Dispose();
            if(!FrameCurrent(generation))return false;
            INativeRoiEditLease candidate;
            try
            {
                candidate=_frame!.BeginEditing(session);
            }
            catch(InvalidOperationException) when(!FrameCurrent(generation)||!session.IsCurrent)
            {
                // The result viewport can complete a frame replacement between the
                // focus event and BeginEditing. VM reprojects after that replacement.
                // Retire this stale lease and let Activate reacquire the new receipt.
                RetireFrame();
                return false;
            }
            if(!FrameCurrent(generation)){candidate.Dispose();return false;}
            _interaction=candidate;_active=group;_pendingActivate=null;_editors[group].DeferInteractionFeedback=false;Error="";return true;
        }
        finally{_activating=false;}
    }
    private bool Activate(string group)
    {
        if(_disposed||_activating||_accepting||!_controls.IsLoaded||!_controls.IsVisible||!_controls.IsLocalDrawing(group))return false;
        if(!FrameCurrent(_generation))AcquireFrame(interactive:true);
        if(ActivateCore(group))return true;
        // A frame may have changed after the first borrow but before the
        // viewport accepted the edit lease. Reborrow once on the new receipt.
        if(!FrameCurrent(_generation))
        {
            AcquireFrame(interactive:true);
            if(ActivateCore(group))return true;
        }
        // 原厂两段式（用户实机截图）：显示图不是输入图时点形状按钮只选中不弹框，
        // 提示延迟到图像落笔（OnInputViewportClicked 重试仍阻断时弹出）。
        // 待激活记录兜底：用户切回输入图后自动重建即武装。
        _editors[group].DeferInteractionFeedback=_drawBlockReason?.Invoke() is not null;
        _pendingActivate=group;
        return false;
    }
    private void OnInputViewportClicked()
    {
        if(_disposed||_refreshing||_accepting||_activating)return;
        if(_active is not null)return;
        var group=_controls.IsLocalDrawing(_primaryGroup)?_primaryGroup
            :_controls.IsLocalDrawing(_secondaryGroup)?_secondaryGroup:null;
        if(group is null)return;
        if(!Activate(group)&&_drawBlockReason?.Invoke() is not null)
            _editors[group].ShowInteractionFeedback();
    }
    private void CompletePendingActivation()
    {
        if(_disposed||_pendingActivate is not {} group)return;
        if(!_controls.IsLocalDrawing(group)){_pendingActivate=null;return;}
        ActivateCore(group);
    }
    private RoiEditAcceptance Accept(string group,RoiEditStamp expected,RoiEditProposal proposal)
    {
        var before=Snapshot(group);
        if(_disposed||_accepting||expected!=before.Stamp||_frame?.IsCurrent!=true)return RoiEditAcceptance.Reject(before,"区域编辑范围或物理帧已失效。");
        if(!_controls.IsLocalDrawing(group))return RoiEditAcceptance.Reject(before,"当前区域模式尚未接入执行。");
        if(_editors.Where(pair=>pair.Key!=group).Any(pair=>pair.Value.HasPendingEdits))return RoiEditAcceptance.Reject(before,"请先提交另一组区域数值。");
        var old=Read();
        if(!_document.TryApply(old,group,proposal,out var next,out var selection,out var documentError))
        {Error=documentError;return RoiEditAcceptance.Reject(before,documentError);}
        _accepting=true;
        try
        {
            _document.Write(next);
            _observed=next;++Revision;++_projectionRevision;var after=Project(next,group,_projectionRevision,expected.Frame);
            var generation=_generation;
            var actions=_sessions.Where(pair=>pair.Key!=group).Select(pair=>(Action)(()=>
            {
                if(FrameCurrent(generation)&&_sessions.TryGetValue(pair.Key,out var current)&&ReferenceEquals(current,pair.Value))current.Refresh();
            })).Append(()=>{if(!_disposed&&generation==_generation)Notify();}).ToArray();
            return new(true,after,Affected(before,after),selection,Publish:()=>Run(actions));
        }
        finally{_accepting=false;}
    }
    private IReadOnlyList<RoiEditTarget> Affected(RoiEditSnapshot before,RoiEditSnapshot after)
    {
        var targets=before.Items.Concat(after.Items).Select(row=>new RoiEditTarget(row.Group,row.Id)).Distinct();
        return targets.Where(target=>
        {
            var a=before.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);var b=after.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);
            return a is null||b is null||a.Name!=b.Name||a.Enabled!=b.Enabled||a.Visible!=b.Visible
                ||!_document.Same(_document.FromNative(a.Geometry,null),_document.FromNative(b.Geometry,null));
        }).ToArray();
    }
    internal bool TryCapture(out string error)
    {
        if(_disposed){error="区域编辑会话已关闭。";return false;}
        RefreshFrame();
        if(_unrestoredInput is not null||_pending.Count>0){error=_unrestoredInput??"图像来源暂不可用，区域原文已保留；请恢复图像后修正或取消窗口。";return false;}
        if(UnacceptedGesture){error="请先完成或取消当前绘制。";return false;}
        foreach(var editor in _editors.Values.Where(editor=>editor.HasPendingEdits))if(!editor.TryCommitPending()){error=editor.Error;return false;}
        try{_=Read();error="";return true;}catch(Exception failure){error=failure.Message;return false;}
    }
    private void OnModesChanged()
    {
        if(_disposed)return;
        // The ROI control creates the inherited parameter rows when their mode first becomes visible.
        TrackPreviewSources();
        if(_accepting||_activating||_refreshing)return;
        var inspection=_controls.IsLocalDrawing(_primaryGroup);var mask=_controls.IsLocalDrawing(_secondaryGroup);
        var mode=PreviewMode();var previewChanged=inspection!=_localInspection||mask!=_localMask||mode!=_observedPreviewMode;_observedPreviewMode=mode;
        if(inspection==_localInspection&&mask==_localMask){if(previewChanged)QueuePreview();Notify();return;}
        _localInspection=inspection;_localMask=mask;
        _pendingActivate=null;
        CapturePendingEditors();
        if(_active is {} group&&!_controls.IsLocalDrawing(group))
        {var old=_interaction;_interaction=null;_active=null;old?.Dispose();}
        ++_projectionRevision;
        var generation=_generation;
        Run(_sessions.Values.Select(session=>(Action)(()=>{if(FrameCurrent(generation))session.Refresh();})).ToArray());
        RestorePendingEditors();

        if(previewChanged)QueuePreview();Notify();
    }
    private void RecordInteractionRetirement(string group,NativeRoiSession session,long generation)
    {
        if(_disposed||_recordingRetirement||generation!=_generation||!_sessions.TryGetValue(group,out var current)||!ReferenceEquals(current,session))return;
        _recordingRetirement=true;
        try{CapturePendingEditors(group);}
        finally{_recordingRetirement=false;}
    }
    private void CapturePendingEditors(string? group=null)
    {
        foreach(var pair in _editors.Where(pair=>(group is null||pair.Key==group)&&pair.Value.HasPendingEdits).ToArray())
        {
            var input=pair.Value.CapturePendingInput();
            if(input is not null)_pending[pair.Key]=input;
            else if(!_pending.ContainsKey(pair.Key))_unrestoredInput="区域原文的对象已失效；请取消窗口后重新编辑。";
        }
    }
    private void RestorePendingEditors()
    {
        foreach(var pair in _pending.ToArray())
        {
            if(!_controls.IsLocalDrawing(pair.Key)||!_sessions.TryGetValue(pair.Key,out var session)||!session.IsCurrent)continue;
            if(session.Select(pair.Key,pair.Value.Id)&&_editors[pair.Key].RestorePendingInput(pair.Value))_pending.Remove(pair.Key);
            else _unrestoredInput="区域对象已改变，未提交原文不能应用；请取消窗口后重新编辑。";
        }
    }
    private void RetireFrame()
    {
        if(!_disposed)CapturePendingEditors();
        foreach(var pair in _sessions)
        {
            if(pair.Value.CurROIData is { } selected)_selectedRoiIds[pair.Key]=selected.Id;
            else _selectedRoiIds.Remove(pair.Key);
        }
        ClearPreview();
        ++_generation;var interaction=_interaction;_interaction=null;_active=null;var frame=_frame;_frame=null;
        var sessions=_sessions.Values.ToArray();_sessions.Clear();
        var handlers=sessions.Where(session=>_retirementHandlers.ContainsKey(session)).Select(session=>(Session:session,Handler:_retirementHandlers[session])).ToArray();
        foreach(var item in handlers)_retirementHandlers.Remove(item.Session);
        var coverage=_observed;
        Run(handlers.Select(item=>(Action)(()=>item.Session.InteractionRetiring-=item.Handler)).Concat(_editors.Select(pair=>(Action)(()=>{pair.Value.UnboundFullDomain=BlobLocalRoi.Coverage(coverage,pair.Key)==BlobLocalRoi.FullDomain;pair.Value.Bind(null);})).ToArray()).Concat(new Action[]{()=>interaction?.Dispose()})
            .Concat(sessions.Select(session=>(Action)session.Dispose)).Append(()=>frame?.Dispose()));
    }
    private static void Run(IEnumerable<Action> actions)
    {
        var errors=new List<Exception>();foreach(var action in actions.ToArray())try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_pendingActivate=null;_controls.Changed-=OnModesChanged;
        _imageField.Changed-=RefreshFrame;if(_source is not null){_source.Changed-=RefreshFrame;_source.FrameReplaced-=RebindCurrentFrame;_source.InputViewportClicked-=OnInputViewportClicked;}Changed=null;
        Run(new Action[]{DisposePreview,RetireFrame}.Concat(_editors.Values.Select(editor=>(Action)(()=>{editor.PendingChanged-=Notify;editor.Dispose();}))));
    }
}



internal sealed partial class BlobLocalRoiOwner
{
    private NativeRoiInputLease? _previewInputs;
    private NativeResultViewport.ReadOnlyRoiLayer? _previewLayer;
    private DispatcherOperation? _previewRefresh;
    private DispatcherOperation? _frameRefresh;
    private long _previewRequest,_previewRevision;
    private bool _previewing,_previewAgain;
    private readonly HashSet<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow> _previewFields=new();
    internal string PreviewError {get;private set;}="";
    private void InitializePreview()
    {
        _controls.Loaded+=PreviewLoaded;_controls.Unloaded+=PreviewUnloaded;_controls.IsVisibleChanged+=PreviewVisibilityChanged;
        TrackPreviewSources();
        _controls.Changed+=QueuePreview;
        if(_source is not null)_source.Changed+=QueuePreview;
    }
    private void TrackPreviewSources()
    {
        foreach(var field in _inputs.Fields)
        {
            var id=field.FieldId;
            if((id==BlobTool.ImageInput||id==BlobMaskInheritance.SourceId||BlobRoiInheritance.Ports.Any(port=>port.Id==id))
                &&_previewFields.Add(field))
                field.Changed+=QueuePreview;
        }
    }
    private void PreviewLoaded(object sender,RoutedEventArgs args)
    {
        QueuePreview();
        QueueFrameRefresh();
    }
    private void PreviewUnloaded(object sender,RoutedEventArgs args)
    {
        _frameRefresh?.Abort();_frameRefresh=null;
    }
    private void PreviewVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)
    {
        if(_controls.IsVisible){QueuePreview();QueueFrameRefresh();}
        else{_frameRefresh?.Abort();_frameRefresh=null;}
    }
    private void QueueFrameRefresh()
    {
        if(_disposed||!_controls.IsLoaded||!_controls.IsVisible)return;
        _frameRefresh?.Abort();
        _frameRefresh=_controls.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(() =>
        {
            _frameRefresh=null;
            if(!_disposed&&_controls.IsLoaded&&_controls.IsVisible)RefreshFrame();
        }));
    }
    private string PreviewMode()=>_controls.IsImageArea?BlobRoiInheritance.Local:_inputs.ReadUnpresentedInput(BlobRoiInheritance.ModeId) switch
    {UnsetSetting=>BlobRoiInheritance.Local,ConstantSetting{Value:string mode}=>BlobRoiInheritance.ValidateMode(mode),_=>throw new InvalidOperationException("ROI继承方式无效。")};
    private void QueuePreview()
    {
        if(_disposed)return;
        // Retire only this read-only layer. Mask session revision, raw inputs and gestures are untouched.
        ClearPreview();
        if(_previewing){_previewAgain=true;return;}
        var request=_previewRequest;
        _previewRefresh=_controls.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
        {
            if(_disposed||request!=_previewRequest)return;_previewRefresh=null;RefreshPreview(request);
        }));
    }
    private void RefreshPreview(long request)
    {
        if(_disposed||_previewing||!_controls.IsLoaded||!_controls.IsVisible)return;
        _previewing=true;NativeRoiInputLease? inputs=null;NativeResultViewport.ReadOnlyRoiLayer? layer=null;
        try
        {
            var mode=PreviewMode();var maskInherited=_controls.IsInheritedMask;
            if(mode==BlobRoiInheritance.Local&&!maskInherited)return;
            if(!FrameCurrent(_generation)){AcquireFrame(interactive:false);if(request!=_previewRequest){if(FrameCurrent(_generation))QueuePreview();return;}}
            var frame=_frame;var generation=_generation;
            bool OwnerCurrent(){var valid=!_disposed&&request==_previewRequest&&generation==_generation&&ReferenceEquals(frame,_frame)&&frame is not null&&FrameCurrent(generation)&&_controls.IsLoaded&&_controls.IsVisible;return valid&&!_disposed&&request==_previewRequest&&generation==_generation&&ReferenceEquals(frame,_frame);}
            if(!OwnerCurrent())throw new InvalidOperationException("请选择与输入一致的已发布图像后预览。");
            var ids=(mode switch
            {
                BlobRoiInheritance.Rectangle=>new[]{BlobRoiInheritance.RectangleSource},
                BlobRoiInheritance.Annulus=>new[]{BlobRoiInheritance.AnnulusSource},
                BlobRoiInheritance.Local=>Array.Empty<string>(),
                BlobRoiInheritance.RectangleParameters=>BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(mode,member)).ToArray(),
                _=>BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(mode,member)).ToArray()
            }).Concat(new[]{BlobTool.ImageInput,BlobRoiInheritance.ModeId})
                .Concat(maskInherited?new[]{BlobMaskInheritance.ModeId,BlobMaskInheritance.SourceId}:Array.Empty<string>()).ToArray();
            inputs=_source?.CaptureInputs(ids)??throw new InvalidOperationException("继承来源预览尚未就绪。");
            if(!OwnerCurrent()||!inputs.IsCurrent||!OwnerCurrent())return;
            var image=inputs.Inputs.Get<IReadOnlyImage>(BlobTool.ImageInput);
            if(image.Frame!=frame!.Image.Frame||image.EffectiveDomain.Frame!=frame.Image.EffectiveDomain.Frame)
                throw new InvalidOperationException("继承输入与可见图像不是同一物理帧和坐标域。");
            var root=BlobMaskInheritance.Materialize(inputs.Inputs,BlobRoiInheritance.Materialize(inputs.Inputs,Read()))!;
            var rows=(mode==BlobRoiInheritance.Local?Enumerable.Empty<RoiEditItem>():Items(_primaryGroup))
                .Concat(maskInherited?Items(_secondaryGroup):Enumerable.Empty<RoiEditItem>()).ToArray();
            IEnumerable<RoiEditItem> Items(string group)=>BlobLocalRoi.Items(root,group)
                .Select(item=>new RoiEditItem(group,Guid.ParseExact(item.Get<string>("id"),"N"),item.Get<string>("name"),
                    _document.ToNative(item.Get<RecordValue>("shape")),Enabled:false,Visible:true));
            if(!OwnerCurrent()||!inputs.IsCurrent||!OwnerCurrent())return;
            var captured=inputs;layer=frame.CreateReadOnlyLayer();
            bool Current()=>OwnerCurrent()&&captured.IsCurrent&&OwnerCurrent();
            if(!layer.Present(frame.Receipt,++_previewRevision,rows,Current)||!Current())return;
            _previewInputs=inputs;inputs=null;_previewLayer=layer;layer=null;
            if(_interaction is null&&_controls.IsLocalDrawing(_secondaryGroup)&&BlobLocalRoi.Items(Read(),_secondaryGroup).Any(item=>item.Get<bool>("enabled")&&item.Get<bool>("visible")))Activate(_secondaryGroup);
        }
        catch(Exception failure)
        {
            if(!_disposed&&request==_previewRequest){PreviewError=failure.Message;_controls.ToolTip=failure.Message;}
        }
        finally
        {
            _previewing=false;
            try{Run(new Action[]{()=>layer?.Dispose(),()=>inputs?.Dispose()});}
            finally{if(_previewAgain&&!_disposed){_previewAgain=false;QueuePreview();}}
        }
    }
    private void ClearPreview()
    {
        ++_previewRequest;var pending=_previewRefresh;_previewRefresh=null;
        var layer=_previewLayer;_previewLayer=null;var inputs=_previewInputs;_previewInputs=null;
        if (PreviewError.Length != 0)
        {
            PreviewError = "";
            _controls.ClearValue(FrameworkElement.ToolTipProperty);
        }
        Run(new Action[]{()=>pending?.Abort(),()=>layer?.Dispose(),()=>inputs?.Dispose()});
    }
    private void DisposePreview()
    {
        _controls.Loaded-=PreviewLoaded;_controls.Unloaded-=PreviewUnloaded;_controls.IsVisibleChanged-=PreviewVisibilityChanged;
        _frameRefresh?.Abort();_frameRefresh=null;
        foreach(var field in _previewFields)field.Changed-=QueuePreview;_previewFields.Clear();
        _controls.Changed-=QueuePreview;
        if(_source is not null)_source.Changed-=QueuePreview;ClearPreview();
    }
}



/// <summary>Stateless UI boundary; the private scalar record remains the sole stored value.</summary>
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
