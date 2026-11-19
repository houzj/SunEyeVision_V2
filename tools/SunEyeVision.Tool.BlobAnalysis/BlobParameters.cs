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
