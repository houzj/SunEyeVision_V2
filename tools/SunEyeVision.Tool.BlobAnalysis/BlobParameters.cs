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
public enum BlobThresholdType { BinaryImage = 1, SingleThreshold, DoubleThreshold, AutoThreshold, FixedSoftThreshold, RelativeSoftThreshold }
public enum BlobPolarity { DarkOnBright, BrightOnDark }
public enum BlobContourType { NoneContour, NoneSequentialContour, SequentialContour }
public sealed record BlobFindParameters
{
    public BlobThresholdType ThresholdType { get; init; } = BlobThresholdType.SingleThreshold;
    public BlobPolarity Polarity { get; init; } = BlobPolarity.DarkOnBright;
    public int LowThreshold { get; init; } = 0;
    public int HightThreshold { get; init; } = 0;
    public int LowSoftThreshold { get; init; } = 0;
    public int HightSoftThreshold { get; init; } = 0;
    public int Softness { get; init; } = 0;
    public int SoftLeftRatio { get; init; } = 0;
    public int SoftRightRatio { get; init; } = 0;
    public int SoftLowRatio { get; init; } = 0;
    public int SoftHighRatio { get; init; } = 0;
    public int FindNum { get; init; } = 0;
    public int HoleMinArea { get; init; } = 0;
    public bool SelectByArea { get; init; } = false;
    public int MinArea { get; init; } = 0;
    public int MaxArea { get; init; } = 0;
    public BlobContourType BlobContourType { get; init; } = BlobContourType.NoneContour;
    public bool BolbImageEnable { get; init; } = false;
    public bool BinaryImageEnable { get; init; } = false;
    public bool SelectByHistInfo { get; init; } = false;
    public bool SelectByBoxAngle { get; init; } = false;
    public int MinBoxAngle { get; init; } = -9000;
    public int MaxBoxAngle { get; init; } = 9000;
    public bool SelectByRectWidth { get; init; } = false;
    public int MinRectWidth { get; init; } = 100;
    public int MaxRectWidth { get; init; } = 200;
    public bool SelectByRectHeight { get; init; } = false;
    public int MinRectHeight { get; init; } = 100;
    public int MaxRectHeight { get; init; } = 200;
    public bool SelectByPerimeter { get; init; } = false;
    public int MinPerimeter { get; init; } = 10;
    public int MaxPerimeter { get; init; } = 999999999;
    public bool SelectByShortAxis { get; init; } = false;
    public int MinShortAxis { get; init; } = 1;
    public int MaxShortAxis { get; init; } = 999999999;
    public bool SelectByLongAxis { get; init; } = false;
    public int MinLongAxis { get; init; } = 10;
    public int MaxLongAxis { get; init; } = 999999999;
    public bool SelectByCircularuty { get; init; } = false;
    public double MinCircularity { get; init; } = 0d;
    public double MaxCircularity { get; init; } = 0d;
    public bool SelectByRectangularity { get; init; } = false;
    public double MinRectangularity { get; init; } = 0d;
    public double MaxRectangularity { get; init; } = 0d;
    public bool SelectByCentraBias { get; init; } = false;
    public double MinCenterBias { get; init; } = 0d;
    public double MaxCenterBias { get; init; } = 9999999.99d;
    public bool AxisRatioEnable { get; init; } = false;
    public double MinAxisRatio { get; init; } = 0.01d;
    public double MaxAxisRatio { get; init; } = 1d;
    public BlobSortFeature SortFeature { get; init; } = BlobSortFeature.SortFeatureArea;
    public BlobSortMode SortMode { get; init; } = BlobSortMode.SortModeDecend;
    public BlobConnectivity Connectivity { get; init; } = BlobConnectivity.Connected_8;
    public int MaxAreaInnerRectNum { get; init; } = 0;
    public int OverlapRatio { get; init; } = 0;
    public bool MaxOutPixelNumEnable { get; init; } = false;
    public int MaxOutPixelNum { get; init; } = 0;
    public bool OKWhenNumIsZero { get; init; } = false;
    public bool BlobNumLimitEnable { get; init; } = false;
    public int BlobNumLimitLow { get; init; } = 1;
    public int BlobNumLimitHigh { get; init; } = 99999;
    public bool BlobAreaLimitEnable { get; init; } = false;
    public double BlobAreaLimitLow { get; init; } = 1d;
    public double BlobAreaLimitHigh { get; init; } = 999999999d;
    public bool BlobTotalAreaLimitEnable { get; init; } = false;
    public double BlobTotalAreaLimitLow { get; init; } = 1d;
    public double BlobTotalAreaLimitHigh { get; init; } = 999999999d;
    public bool CentroidXLimitEnable { get; init; } = false;
    public double CentroidXLimitLow { get; init; } = -99999;
    public double CentroidXLimitHigh { get; init; } = 99999d;
    public bool CentroidYLimitEnable { get; init; } = false;
    public double CentroidYLimitLow { get; init; } = -99999;
    public double CentroidYLimitHigh { get; init; } = 99999d;
    public bool AngleLimitEnable { get; init; } = false;
    public double AngleLimitLow { get; init; } = -180;
    public double AngleLimitHigh { get; init; } = 180d;
    public bool CenterXLimitEnable { get; init; } = false;
    public double CenterXLimitLow { get; init; } = -99999;
    public double CenterXLimitHigh { get; init; } = 99999d;
    public bool CenterYLimitEnable { get; init; } = false;
    public double CenterYLimitLow { get; init; } = -99999;
    public double CenterYLimitHigh { get; init; } = 99999d;
    public bool BoxHeightLimitEnable { get; init; } = false;
    public double BoxHeightLimitLow { get; init; } = 1d;
    public double BoxHeightLimitHigh { get; init; } = 99999d;
    public bool BoxWidthLimitEnable { get; init; } = false;
    public double BoxWidthLimitLow { get; init; } = 1d;
    public double BoxWidthLimitHigh { get; init; } = 99999d;
    public bool PerimeterLimitEnable { get; init; } = false;
    public double PerimeterLimitLow { get; init; } = 1d;
    public double PerimeterLimitHigh { get; init; } = 999999999d;
    public bool LongAxisLimitEnable { get; init; } = false;
    public double LongAxisLimitLow { get; init; } = 1d;
    public double LongAxisLimitHigh { get; init; } = 999999999d;
    public bool ShortAxisLimitEnable { get; init; } = false;
    public double ShortAxisLimitLow { get; init; } = 1d;
    public double ShortAxisLimitHigh { get; init; } = 999999999d;
    public bool CircularityLimitEnable { get; init; } = false;
    public double CircularityLimitLow { get; init; } = 0d;
    public double CircularityLimitHigh { get; init; } = 1d;
    public bool RectangularityLimitEnable { get; init; } = false;
    public double RectangularityLimitLow { get; init; } = 0d;
    public double RectangularityLimitHigh { get; init; } = 1d;

    public static BlobFindParameters FromSettings(BlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var vm = settings.FindParameters ?? new BlobFindParameters();
        // The active original window declares only the three-valued contour mode.
        // Keep that mode unchanged through host projection and Freeze.
        return vm with
        {
            ThresholdType = settings.SegmentationMode switch { BlobSegmentationMode.None => BlobThresholdType.BinaryImage, BlobSegmentationMode.DoubleThreshold => BlobThresholdType.DoubleThreshold, BlobSegmentationMode.AutoThreshold => BlobThresholdType.AutoThreshold, BlobSegmentationMode.FixedSoftThreshold => BlobThresholdType.FixedSoftThreshold, BlobSegmentationMode.RelativeSoftThreshold => BlobThresholdType.RelativeSoftThreshold, _ => BlobThresholdType.SingleThreshold },
            Polarity = settings.Type == BlobThresholdMode.BinaryInv ? BlobPolarity.BrightOnDark : BlobPolarity.DarkOnBright,
            LowThreshold = settings.Threshold, HightThreshold = settings.UpperThreshold,
            LowSoftThreshold = settings.FixedSoftLowerThreshold, HightSoftThreshold = settings.FixedSoftUpperThreshold, Softness = settings.ThresholdSoftness,
            SoftLeftRatio = settings.RelativeLeftRatio, SoftRightRatio = settings.RelativeRightRatio, SoftLowRatio = settings.RelativeLowerRatio, SoftHighRatio = settings.RelativeUpperRatio,
            FindNum = settings.BlobCountLimit, SelectByArea = settings.IsAreaFilterEnabled, MinArea = settings.FilterAreaMin, MaxArea = settings.FilterAreaMax,
            BolbImageEnable = settings.OutputBlobImage, BinaryImageEnable = settings.OutputBinaryImage,
            SelectByCircularuty = settings.IsCircularityFilterEnabled, MinCircularity = settings.FilterCircularityMin, MaxCircularity = settings.FilterCircularityMax,
            SelectByRectangularity = settings.IsRectangularityFilterEnabled, MinRectangularity = settings.FilterRectangularityMin, MaxRectangularity = settings.FilterRectangularityMax,
            SortFeature = (BlobSortFeature)(int)settings.SortFeature, SortMode = (BlobSortMode)(int)settings.SortMode, Connectivity = (BlobConnectivity)(int)settings.Connectivity
        };
    }

    public BlobSettings ApplyTo(BlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = settings with
        {
            SegmentationMode = ThresholdType switch { BlobThresholdType.BinaryImage => BlobSegmentationMode.None, BlobThresholdType.DoubleThreshold => BlobSegmentationMode.DoubleThreshold, BlobThresholdType.AutoThreshold => BlobSegmentationMode.AutoThreshold, BlobThresholdType.FixedSoftThreshold => BlobSegmentationMode.FixedSoftThreshold, BlobThresholdType.RelativeSoftThreshold => BlobSegmentationMode.RelativeSoftThreshold, _ => BlobSegmentationMode.SingleThreshold },
            Type = Polarity == BlobPolarity.BrightOnDark ? BlobThresholdMode.BinaryInv : BlobThresholdMode.Binary,
            Threshold = LowThreshold, UpperThreshold = HightThreshold, FixedSoftLowerThreshold = LowSoftThreshold, FixedSoftUpperThreshold = HightSoftThreshold, ThresholdSoftness = Softness,
            RelativeLeftRatio = SoftLeftRatio, RelativeRightRatio = SoftRightRatio, RelativeLowerRatio = SoftLowRatio, RelativeUpperRatio = SoftHighRatio, BlobCountLimit = FindNum,
            IsAreaFilterEnabled = SelectByArea, FilterAreaMin = MinArea, FilterAreaMax = MaxArea, OutputBlobImage = BolbImageEnable, OutputBinaryImage = BinaryImageEnable,
            IsCircularityFilterEnabled = SelectByCircularuty, FilterCircularityMin = MinCircularity, FilterCircularityMax = MaxCircularity, IsRectangularityFilterEnabled = SelectByRectangularity, FilterRectangularityMin = MinRectangularity, FilterRectangularityMax = MaxRectangularity,
            SortFeature = (BlobSortFeature)(int)SortFeature, SortMode = (BlobSortMode)(int)SortMode, Connectivity = (BlobConnectivity)(int)Connectivity, FindParameters = this
        };
        return result;
    }

    internal static bool InRange(double value, double low, double high) => value >= low && value <= high;
}

/// <summary>The ten canonical input leaves. Fixture and point editor ports are projections of these leaves.</summary>
public static class BlobFixtureInputs
{
    public const string PositionCorrectionModeId = "input.pose.mode";
    public const string ReferencePoint = "input.pose.reference.point";
    public const string RunPoint = "input.pose.run.point";
    private const FieldEditModes SourceModes = FieldEditModes.Constant | FieldEditModes.Reference;
    private static readonly string[] PoseNames = ["reference", "run"];
    private static readonly string[] Coordinates = ["x", "y"];
    private static readonly string[] Values = ["angle", "scale-x", "scale-y"];
    private static readonly (string Id, string[] Path)[] Leaves = PoseNames.SelectMany(pose =>
        Coordinates.Select(axis => Leaf(pose, "point", axis)).Concat(Values.Select(value => Leaf(pose, value)))).ToArray();

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(PositionCorrectionModeId, DataType.Integer, false, EditModes:FieldEditModes.Constant),
        new Port(ReferencePoint, FixtureDeclarations.Point, false, EditModes:SourceModes),
        new Port(RunPoint, FixtureDeclarations.Point, false, EditModes:SourceModes)
    }.Concat(Leaves.Select(leaf => new Port(leaf.Id, DataType.Number, false, Storage:NumericStorage.Float32, EditModes:SourceModes))).ToArray());
    public static IReadOnlyList<string> LeafIds { get; } = Array.AsReadOnly(Leaves.Select(leaf => leaf.Id).ToArray());
    public static IReadOnlyList<string> LeavesFor(string projection) => Array.AsReadOnly(Members(projection).Select(member => member.Id).ToArray());
    internal static bool IsProjection(string id) => id is BlobTool.PoseInput or ReferencePoint or RunPoint;

    private static (string Id, string[] Path) Leaf(params string[] path) => (BlobTool.PoseInput + "." + string.Join(".", path), path);
    private static string[] Prefix(string id) => id switch
    {
        BlobTool.PoseInput => [],
        ReferencePoint => ["reference", "point"],
        RunPoint => ["run", "point"],
        _ => throw new ArgumentException("未知位置修正组合。", nameof(id))
    };
    private static TypeRef ProjectionType(string id) => id == BlobTool.PoseInput ? FixtureDeclarations.Info : FixtureDeclarations.Point;
    private static (string Id, string[] Path)[] Members(string id)
    {
        var prefix = Prefix(id);
        return Leaves.Where(leaf => leaf.Path.Take(prefix.Length).SequenceEqual(prefix))
            .Select(leaf => (leaf.Id, leaf.Path.Skip(prefix.Length).ToArray())).ToArray();
    }

    internal static void Expand(string id, ParameterSetting setting, IDictionary<string, ParameterSetting> changes)
    {
        var type = ProjectionType(id);
        foreach (var (leaf, path) in Members(id))
        {
            ParameterSetting value = setting switch
            {
                UnsetSetting => new UnsetSetting(),
                ConstantSetting { Value:RecordValue record } when record.Declaration == type => new ConstantSetting(ReadNumber(record, path)),
                ReferenceSetting source when Matches(source.Reference, type)
                    => source with { Reference = Reference(source.Reference, source.Reference.MemberFieldPath.Concat(path)) },
                _ => throw new ArgumentException("位置修正组合要求对应类型的单个结构化来源。")
            };
            if (changes.ContainsKey(leaf)) throw new ArgumentException("同一编辑不能重复设置位置修正叶。");
            changes.Add(leaf, value);
        }
    }

    internal static ParameterSetting Project(FieldSettings fields, string id)
    {
        var members = Members(id);
        var settings = members.Select(member => fields.Read(member.Id)).ToArray();
        if (settings.All(setting => setting is ConstantSetting))
        {
            var numbers = members.Select((member, index) => (member.Path, ((ConstantSetting)settings[index]).Value))
                .ToDictionary(item => string.Join(".", item.Path), item => (double)item.Value!, StringComparer.Ordinal);
            return new ConstantSetting(Build(ProjectionType(id), [], path => numbers[string.Join(".", path)]));
        }
        ReferenceSetting? first = null;
        string? identity = null;
        for (var i = 0; i < members.Length; i++)
        {
            var path = members[i].Path;
            if (settings[i] is not ReferenceSetting source || source.Reference.MemberFieldPath.Count < path.Length ||
                !source.Reference.MemberFieldPath.TakeLast(path.Length).SequenceEqual(path))
                return new UnsetSetting();
            var reference = Reference(source.Reference, source.Reference.MemberFieldPath.SkipLast(path.Length));
            var key = ParameterSettingCanonical.Serialize(reference);
            if (first is null)
            {
                first = source with { Reference = reference };
                identity = key;
            }
            else if (key != identity || first.Conversion != source.Conversion || first.OriginId != source.OriginId ||
                first.RebindingReason != source.RebindingReason)
                return new UnsetSetting();
        }
        return first is not null && Matches(first.Reference, ProjectionType(id))
            ? first : new UnsetSetting();
    }

    internal static RecordValue? Read(InputValues values)
    {
        if (!LeafIds.Any(values.Contains)) return null;
        return Build(FixtureDeclarations.Info, [], path => values.Get<double>(Leaf(path).Id));
    }

    private static RecordValue Build(TypeRef type, string[] prefix, Func<string[], double> read)
    {
        var fields = new Dictionary<string, object?>();
        foreach (var port in type.Fields)
        {
            string[] path = [..prefix, port.Id];
            fields.Add(port.Id, port.Type.Shape == ValueShape.Record ? Build(port.Type, path, read) : read(path));
        }
        return new(type, fields);
    }

    private static double ReadNumber(RecordValue record, IReadOnlyList<string> path)
    {
        foreach (var member in path.Take(path.Count - 1)) record = record.Get<RecordValue>(member);
        return record.Get<double>(path[^1]);
    }

    private static OutputReference Reference(OutputReference reference, IEnumerable<string> path)
        => new(reference.NodeId, reference.PortId, reference.Type, path, reference.Selection, reference.ScopeId, reference.Storage);

    private static bool Matches(OutputReference reference, TypeRef type)
    {
        var selected = ParameterReferenceRules.SelectedType(reference);
        return selected == type || selected.Shape == ValueShape.Sequence && selected.Element == type;
    }
}

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
public sealed record BlobInput(IReadOnlyImage Image, IReadOnlyRegion? Inspection,
    IReadOnlyRegion? Mask, RecordValue? Pose,RecordValue? LocalRoi=null,bool IsInheritedRoi=false,IReadOnlyImage? ImageMask=null,
    bool OutputMask=false,RecordValue? Fixture=null,bool IsInheritedMask=false)
{
    public static BlobInput Read(InputValues values,Action<string>? timingLog=null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var outputMask = values.Contains(BlobImageRoi.OutputEnabledId) && values.Get<bool>(BlobImageRoi.OutputEnabledId);
        if (values.Contains(BlobImageRoi.ModeId) && values.Get<long>(BlobImageRoi.ModeId) == 1)
        {
            var result = new BlobInput(values.Get<IReadOnlyImage>(BlobTool.ImageInput), null, null, null,
                ImageMask: values.Get<IReadOnlyImage>(BlobImageRoi.ImageId), OutputMask:outputMask);
            BlobInputTimingProbe.Log(timingLog,"image-mask-input");
            return result;
        }
        var fixture = BlobFixtureInputs.Read(values);
        var localRoi = values.Contains(BlobLocalRoi.InputId) ? values.Get<RecordValue>(BlobLocalRoi.InputId) : BlobLocalRoi.Empty;
        BlobLocalRoi.Validate(localRoi);
        var roi = BlobRoiInheritance.Materialize(values, localRoi);
        var output = new BlobInput(values.Get<IReadOnlyImage>(BlobTool.ImageInput), null, null,
            fixture is null ? null : FixtureDeclarations.ToAffine2D(fixture,BlobSchema.ImageFrame),
            BlobMaskInheritance.Materialize(values,roi),
            values.Contains(BlobRoiInheritance.ModeId)&&values.Get<string>(BlobRoiInheritance.ModeId)!=BlobRoiInheritance.Local,
            OutputMask:outputMask,Fixture:fixture,
            IsInheritedMask:values.Contains(BlobMaskInheritance.ModeId)&&values.Get<bool>(BlobMaskInheritance.ModeId));
        BlobInputTimingProbe.Log(timingLog,"normal-input");
        return output;
    }
}

file static class BlobInputTimingProbe
{
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("SUNEYEVISION_TIMING_PROBE"), "1", StringComparison.Ordinal);

    public static void Log(Action<string>? log,string branch)
    {
        if(!Enabled || log is null)return;
        log($"[ExecTimingProbe] tool=blob-entry stage=input-branch branch={branch}");
    }
}

/// <summary>Blob's sole persistent local ROI value. Scalars/records/sequences only; no image, editor or native handle.</summary>
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
        if(!_inputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{BlobTool.ImageInput,BlobTool.PoseInput}))
            throw new InvalidDataException("Blob input editor declarations differ from the input model.");
        foreach(var group in root.Elements("Group"))
        {
            Shape(group,["page","section","advanced","vmCategory"],["Parameter"]);
            Required(group,"page");Required(group,"section");
            Required(group,"vmCategory");
            if(group.Attribute("advanced") is not null)Boolean(group,"advanced");
            if(!group.HasElements)throw new InvalidDataException("Empty Blob parameter group.");
            foreach(var field in group.Elements())
            {
                Shape(field,["id","fieldId","allowReference","bindingName","type","default","label","minimum","maximum","decimals","increment","visibleWhen","visibleValue","visibleNegate","pairWith","pairMinimum","pairMaximum","pairLabel"],["Choice"]);
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
        foreach(var property in typeof(BlobFindParameters).GetProperties(BindingFlags.Public|BindingFlags.Instance))
            settings.TryAdd(property.Name,property.PropertyType);
        return settings;
    }
    internal static PropertyInfo? SettingsProperty(string id)=>typeof(BlobSettings).GetProperty(id);
    internal static PropertyInfo? FindProperty(string id)=>typeof(BlobFindParameters).GetProperty(id);
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
    public string VmCategory(string id)=>Required(_fields[id].Parent!,"vmCategory");
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

public sealed partial class BlobParameterDefinition
{
    private readonly IReadOnlyList<SettingBinding> _bindings;
    private sealed record SettingBinding(Port Port,FieldPresentation Presentation,
        Func<BlobSettings,object?> Read,Func<BlobSettings,object?,BlobSettings> Write);
    public ToolSchema SettingsSchema { get; }
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(
        _fields.Values.Where(item=>Boolean(item,"allowReference")).ToDictionary(item=>FieldId(item,"fieldId"),
            item=>Required(item,"bindingName"),StringComparer.Ordinal));

    public string SettingsFieldId(string property)
        => _fields.TryGetValue(property,out var item)?FieldId(item,"fieldId")
            :throw new ArgumentException("Unknown Blob setting: "+property,nameof(property));
    private static string FieldId(XElement item,string attribute)
    {
        var id=Required(item,attribute);
        if(!id.StartsWith("field:",StringComparison.Ordinal)||!Guid.TryParseExact(id[6..],"N",out _))
            throw new InvalidDataException("Blob editor requires an explicit stable field ID: "+id);
        return id;
    }
    private IReadOnlyList<SettingBinding> BuildSettingsBindings()
    {
        var bindings=new List<SettingBinding>();
        foreach(var item in _fields.Values)
        {
            var name=Required(item,"id");var type=ParameterType(name);
            var settingsProperty=SettingsProperty(name);var findProperty=FindProperty(name);
            if(settingsProperty is null&&findProperty is null)throw new InvalidDataException("Unknown Blob parameter: "+name);
            var choices=item.Elements("Choice").ToDictionary(x=>Required(x,"key"),x=>Required(x,"label"),StringComparer.Ordinal);
            var port=new Port(FieldId(item,"fieldId"),type==typeof(bool)?DataType.Boolean:type==typeof(int)?DataType.Integer:type==typeof(double)?DataType.Number:DataType.Text,
                Storage:type==typeof(int)?NumericStorage.Int32:type==typeof(double)?NumericStorage.Float64:null,
                EditModes:FieldEditModes.Constant|(Boolean(item,"allowReference")?FieldEditModes.Reference:FieldEditModes.None),
                Choices:type.IsEnum?new TextChoiceConstraint(choices.Keys):null);
            FieldCondition? condition=null;
            if(item.Attribute("visibleWhen") is { } visible)
            {
                if(!_fields.TryGetValue(visible.Value,out var controller))throw new InvalidDataException("Unknown Blob editor visibility source.");
                var controllerType=ParameterType(visible.Value);
                var raw=Required(item,"visibleValue");
                object value=controllerType.IsEnum?raw:controllerType==typeof(bool)?Boolean(item,"visibleValue")
                    :throw new InvalidDataException("Blob visibility requires a Boolean or enum controller.");
                condition=new(FieldId(controller,"fieldId"),value,item.Attribute("visibleNegate") is not null&&Boolean(item,"visibleNegate"));
            }
            else if(item.Attribute("visibleValue") is not null||item.Attribute("visibleNegate") is not null)
                throw new InvalidDataException("Incomplete Blob visibility declaration.");
            string? pair=null;
            if(item.Attribute("pairWith") is { } partner)
            {if(!_fields.TryGetValue(partner.Value,out var endpoint))throw new InvalidDataException("Unknown Blob range endpoint.");pair=FieldId(endpoint,"fieldId");}
            var layout=new FieldLayout(Page(name),Section(name),condition,pair,Advanced:Advanced(name),Minimum:Minimum(name),Maximum:Maximum(name),
                DecimalPlaces:DecimalPlaces(name),AllowReference:Boolean(item,"allowReference"),Increment:Increment(name),
                PairMinimum:Number(name,"pairMinimum"),PairMaximum:Number(name,"pairMaximum"),PairDisplayName:(string?)item.Attribute("pairLabel"),
                VmCategory:VmCategory(name));
            var presentation=new FieldPresentation(port.Id,Label(name),Section(name),bindings.Count,type.IsEnum?FieldEditorKind.Choice:FieldEditorKind.Scalar,
                type.IsEnum?choices.Keys:null,type.IsEnum?choices:null){Layout=layout};
            bindings.Add(new(port,presentation,settings=>
            {
                var value=settingsProperty is not null?settingsProperty.GetValue(settings):findProperty!.GetValue(settings.ToFindParameters());
                return type.IsEnum?value!.ToString():type==typeof(int)?(object)(long)(int)value!:value;
            },(settings,value)=>
            {
                var copy=settings.Freeze();
                object? converted=type.IsEnum?Enum.Parse(type,(string)value!,false):type==typeof(int)?checked((int)(long)value!):value;
                if(settingsProperty is not null)settingsProperty.SetValue(copy,converted);
                else
                {
                    var vm=copy.ToFindParameters();findProperty!.SetValue(vm,converted);copy=copy with {FindParameters=vm};
                }
                return copy;
            }));
        }
        if(bindings.Select(b=>b.Port.Id).Distinct(StringComparer.Ordinal).Count()!=bindings.Count)throw new InvalidDataException("Duplicate Blob editor field IDs.");
        return bindings.AsReadOnly();
    }
    public InputValues ReadSettings(BlobSettings settings)
    {
        var diagnostics=settings.Validate();
        if(diagnostics.Count!=0)throw new ArgumentException(string.Join("; ",diagnostics.Select(item=>item.Message)));
        return new(SettingsSchema.Inputs,_bindings.ToDictionary(b=>b.Port.Id,b=>PrepareField(b,b.Read(settings)),StringComparer.Ordinal));
    }
    private static object? PrepareField(SettingBinding field,object? value)
    {
        value=ValueRules.Freeze(field.Port,value);
        if(value is long or double)
        {
            var number=Convert.ToDouble(value,CultureInfo.InvariantCulture);var layout=field.Presentation.Layout!;
            if(layout.Minimum is {} min&&number<min||layout.Maximum is {} max&&number>max)
                throw new ArgumentException("Blob settings field is outside XML editor bounds: "+field.Port.Id);
        }
        return value;
    }
    public BlobSettings MaterializeSettings(InputValues resolved)
    {
        var values = new InputValues(SettingsSchema.Inputs, resolved.CopyValues());
        var result = new BlobSettings(false);
        foreach (var field in _bindings) result = field.Write(result, PrepareField(field,values.Get<object?>(field.Port.Id)));
        var diagnostics = result.Validate();
        if (diagnostics.Count != 0) throw new ArgumentException(string.Join("; ", diagnostics.Select(item => item.Message)));
        return result.Freeze();
    }
    public BlobSettings ApplySettings(BlobSettings settings,IReadOnlyDictionary<string,object?> changes)
    {
        var copy=settings.Freeze();
        foreach(var change in changes)
        {
            var field=_bindings.SingleOrDefault(b=>b.Port.Id==change.Key)??throw new ArgumentException("Unknown Blob settings field: "+change.Key);
            var value=PrepareField(field,change.Value);
            copy=field.Write(copy,value);
        }
        return copy.Freeze();
    }
}

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

public enum BlobConnectivity { Connected_8 = 8, Connected_4 = 4 }
public enum BlobThresholdMode { Binary, BinaryInv }
public enum BlobSortFeature { SortFeatureArea = 1, SortFeaturePerimeter = 2, SortFeatureCircularity = 3, SortFeatureRect = 4, SortFeatureCentroidX = 5, SortFeatureCentroidY = 6, SortFeatureBoxAngle = 7, SortFeatureBoxWidth = 8, SortFeatureBoxHeight = 9, SortFeatureRectX = 10, SortFeatureRectY = 11, SortFeatureAxisAngle = 12, SortFeatureAxisRatio = 13 }
public enum BlobSortMode { SortModeAscend = 1, SortModeDecend = 2, SortModeNotSort = 3 }
public enum BlobSegmentationMode { SingleThreshold, DoubleThreshold, None, AutoThreshold, FixedSoftThreshold, RelativeSoftThreshold }

/// <summary>Business settings only: no bindings, brushes, view state, backend handles or writable globals.</summary>
public sealed record BlobSettings : IToolSettings<BlobSettings>
{
    public BlobSettings() : this(true) { }
    internal BlobSettings(bool initializeDefaults)
    {
        if (!initializeDefaults) return;
        var definition=BlobParameterDefinition.Instance;
        FixedSoftLowerThreshold=definition.Default<int>(nameof(FixedSoftLowerThreshold));
        FixedSoftUpperThreshold=definition.Default<int>(nameof(FixedSoftUpperThreshold));
        ThresholdSoftness=definition.Default<int>(nameof(ThresholdSoftness));
        RelativeLeftRatio=definition.Default<int>(nameof(RelativeLeftRatio));
        RelativeRightRatio=definition.Default<int>(nameof(RelativeRightRatio));
        RelativeLowerRatio=definition.Default<int>(nameof(RelativeLowerRatio));
        RelativeUpperRatio=definition.Default<int>(nameof(RelativeUpperRatio));
        Threshold=definition.Default<int>(nameof(Threshold));
        UpperThreshold=definition.Default<int>(nameof(UpperThreshold));
        SegmentationMode=definition.Default<BlobSegmentationMode>(nameof(SegmentationMode));
        Type=definition.Default<BlobThresholdMode>(nameof(Type));
        IsPositionCorrectionEnabled=definition.Default<bool>(nameof(IsPositionCorrectionEnabled));
        IsAreaFilterEnabled=definition.Default<bool>(nameof(IsAreaFilterEnabled));
        FilterAreaMin=definition.Default<int>(nameof(FilterAreaMin));
        FilterAreaMax=definition.Default<int>(nameof(FilterAreaMax));
        IsCircularityFilterEnabled=definition.Default<bool>(nameof(IsCircularityFilterEnabled));
        FilterCircularityMin=definition.Default<double>(nameof(FilterCircularityMin));
        FilterCircularityMax=definition.Default<double>(nameof(FilterCircularityMax));
        IsRectangularityFilterEnabled=definition.Default<bool>(nameof(IsRectangularityFilterEnabled));
        FilterRectangularityMin=definition.Default<double>(nameof(FilterRectangularityMin));
        FilterRectangularityMax=definition.Default<double>(nameof(FilterRectangularityMax));
        BlobCountLimit=definition.Default<int>(nameof(BlobCountLimit));
        OutputBlobImage=definition.Default<bool>(nameof(OutputBlobImage));
        OutputBinaryImage=definition.Default<bool>(nameof(OutputBinaryImage));
        SortMode=definition.Default<BlobSortMode>(nameof(SortMode));
        SortFeature=definition.Default<BlobSortFeature>(nameof(SortFeature));
        Connectivity=definition.Default<BlobConnectivity>(nameof(Connectivity));
    }

    [JsonRequired]
    public int Threshold { get; init; }
    [JsonRequired]
    public int FixedSoftLowerThreshold { get; init; }
    [JsonRequired]
    public int FixedSoftUpperThreshold { get; init; }
    [JsonRequired]
    public int ThresholdSoftness { get; init; }
    [JsonRequired]
    public int RelativeLeftRatio { get; init; }
    [JsonRequired]
    public int RelativeRightRatio { get; init; }
    [JsonRequired]
    public int RelativeLowerRatio { get; init; }
    [JsonRequired]
    public int RelativeUpperRatio { get; init; }
    [JsonRequired]
    public int UpperThreshold { get; init; }
    [JsonRequired]
    public BlobSegmentationMode SegmentationMode { get; init; }
    [JsonRequired]
    public BlobThresholdMode Type { get; init; }
    [JsonRequired]
    public bool IsPositionCorrectionEnabled { get; init; }
    [JsonRequired]
    public bool IsAreaFilterEnabled { get; init; }
    [JsonRequired]
    public int FilterAreaMin { get; init; }
    [JsonRequired]
    public int FilterAreaMax { get; init; }
    [JsonRequired]
    public bool IsCircularityFilterEnabled { get; init; }
    [JsonRequired]
    public double FilterCircularityMin { get; init; }
    [JsonRequired]
    public double FilterCircularityMax { get; init; }
    [JsonRequired]
    public bool IsRectangularityFilterEnabled { get; init; }
    [JsonRequired]
    public double FilterRectangularityMin { get; init; }
    [JsonRequired]
    public double FilterRectangularityMax { get; init; }

    [JsonRequired]
    public int BlobCountLimit { get; init; }
    [JsonRequired]
    public bool OutputBlobImage { get; init; }
    [JsonRequired]
    public bool OutputBinaryImage { get; init; }
    [JsonRequired]
    public BlobSortMode SortMode { get; init; }
    [JsonRequired]
    public BlobSortFeature SortFeature { get; init; }
    [JsonRequired]
    public BlobConnectivity Connectivity { get; init; }

    /// <summary>
    /// 完整 VM 参数闭包。现有 XML 字段是宿主的稳定字段合同，闭包保存 VM 已确认但尚未全部开放到公共字段树的成员。
    /// Freeze 会用当前宿主字段覆盖对应成员，避免两个模型产生漂移。
    /// </summary>
    [BlobNonField]
    public BlobFindParameters FindParameters { get; init; } = new();

    public BlobFindParameters ToFindParameters() => BlobFindParameters.FromSettings(this);
    public BlobSettings Freeze() => this with { FindParameters = ToFindParameters() };

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Check(bool condition, string message) { if (!condition) errors.Add(new("BLOB_SETTINGS_INVALID", message)); }
        var definition = BlobParameterDefinition.Instance;
        bool Number(string field, double value)
        {
            var valid = double.IsFinite(value) && value >= definition.Minimum(field) && value <= definition.Maximum(field);
            if (!valid) errors.Add(new("BLOB_SETTINGS_INVALID", $"{definition.Label(field)}必须在{definition.Minimum(field)}至{definition.Maximum(field)}之间。", field));
            return valid;
        }
        Check(Enum.IsDefined(SegmentationMode) && Enum.IsDefined(Type) && Enum.IsDefined(SortMode) && Enum.IsDefined(SortFeature) && Enum.IsDefined(Connectivity), "不支持的Blob分割、极性或排序模式。");
        if (SegmentationMode != BlobSegmentationMode.None) Number(nameof(Threshold), Threshold);
        if (SegmentationMode == BlobSegmentationMode.DoubleThreshold)
        {
            Number(nameof(UpperThreshold), UpperThreshold);
        }
        Number(nameof(FixedSoftLowerThreshold),FixedSoftLowerThreshold);
        Number(nameof(FixedSoftUpperThreshold),FixedSoftUpperThreshold);
        Number(nameof(ThresholdSoftness),ThresholdSoftness);
        Number(nameof(RelativeLeftRatio),RelativeLeftRatio);
        Number(nameof(RelativeRightRatio),RelativeRightRatio);
        Number(nameof(RelativeLowerRatio),RelativeLowerRatio);
        Number(nameof(RelativeUpperRatio),RelativeUpperRatio);
        Number(nameof(BlobCountLimit), BlobCountLimit);
        // 仅存在于VM闭包的四项参数（填充面积阈值、最大内接矩形数量、最小重叠率、最大超界像素）同样受XML上下限约束。
        var closure = FindParameters ?? new BlobFindParameters();
        Number("HoleMinArea", closure.HoleMinArea);
        Number("MaxAreaInnerRectNum", closure.MaxAreaInnerRectNum);
        Number("OverlapRatio", closure.OverlapRatio);
        Number("MaxOutPixelNum", closure.MaxOutPixelNum);
        void Range(bool enabled, string minField, double min, string maxField, double max)
        {
            Number(minField, min); Number(maxField, max);
            if(enabled&&minField!=nameof(FilterAreaMin))Check(min <= max, "筛选下限不能超过上限。");
        }
        Range(IsAreaFilterEnabled, nameof(FilterAreaMin), FilterAreaMin, nameof(FilterAreaMax), FilterAreaMax);
        Range(IsCircularityFilterEnabled, nameof(FilterCircularityMin), FilterCircularityMin, nameof(FilterCircularityMax), FilterCircularityMax);
        Range(IsRectangularityFilterEnabled, nameof(FilterRectangularityMin), FilterRectangularityMin, nameof(FilterRectangularityMax), FilterRectangularityMax);
        return errors;
    }
}

/// <summary>One capability decision shared by formal editing and both execution entry points.</summary>
public static class BlobThresholdCapabilities
{
    public static IReadOnlyList<string> ControlledFields { get; } = Array.AsReadOnly(new[]
    {
        nameof(BlobSettings.Threshold), nameof(BlobSettings.UpperThreshold), nameof(BlobSettings.FixedSoftLowerThreshold),
        nameof(BlobSettings.FixedSoftUpperThreshold), nameof(BlobSettings.ThresholdSoftness), nameof(BlobSettings.RelativeLeftRatio),
        nameof(BlobSettings.RelativeRightRatio), nameof(BlobSettings.RelativeLowerRatio), nameof(BlobSettings.RelativeUpperRatio)
    });

    public static bool IsSupported(BlobSegmentationMode mode)=>mode is BlobSegmentationMode.SingleThreshold or BlobSegmentationMode.DoubleThreshold
        or BlobSegmentationMode.AutoThreshold or BlobSegmentationMode.FixedSoftThreshold or BlobSegmentationMode.RelativeSoftThreshold or BlobSegmentationMode.None;
    public static bool IsSupported(string? mode)=>mode=="BinaryImage"||Enum.TryParse<BlobSegmentationMode>(mode,out var parsed)&&IsSupported(parsed);
    // Native mode 3 overwrites its local polarity from endpoint order; the saved Type is retained but ignored.
    internal static bool IsInverse(BlobSettings settings)=>settings.SegmentationMode==BlobSegmentationMode.DoubleThreshold
        ?settings.Threshold>settings.UpperThreshold:settings.Type==BlobThresholdMode.BinaryInv;
    public static bool IsThresholdFieldActive(BlobSegmentationMode mode,string property)=>property switch
    {
        nameof(BlobSettings.Threshold)=>mode is BlobSegmentationMode.SingleThreshold or BlobSegmentationMode.DoubleThreshold,
        nameof(BlobSettings.UpperThreshold)=>mode==BlobSegmentationMode.DoubleThreshold,
        nameof(BlobSettings.FixedSoftLowerThreshold) or nameof(BlobSettings.FixedSoftUpperThreshold)=>mode==BlobSegmentationMode.FixedSoftThreshold,
        nameof(BlobSettings.ThresholdSoftness)=>mode is BlobSegmentationMode.FixedSoftThreshold or BlobSegmentationMode.RelativeSoftThreshold,
        nameof(BlobSettings.RelativeLeftRatio) or nameof(BlobSettings.RelativeRightRatio) or nameof(BlobSettings.RelativeLowerRatio) or nameof(BlobSettings.RelativeUpperRatio)=>mode==BlobSegmentationMode.RelativeSoftThreshold,
        _=>false
    };
}
