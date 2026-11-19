using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Wpf.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows;
using System.Xml.Linq;
using System.Xml;

using static SunEyeVision.Plugin.Contracts.DeclaredParameterSchema;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime
{

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
        var localRoi = values.Contains(BlobLocalRoi.InputId) ? values.Get<RecordValue>(BlobLocalRoi.InputId) : RoiDocumentValue.Empty;
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
    private readonly DeclaredParameterSchema _schema;
    public static BlobParameterDefinition Instance { get; } = Load();
    private BlobParameterDefinition(DeclaredParameterSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema=schema;
        if(schema.WorkflowImageInputId!=BlobTool.ImageInput)
            throw new InvalidDataException("Blob workflow image default must name its declared image input.");
        if(!schema.InputIds.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{BlobTool.ImageInput,BlobTool.PoseInput}))
            throw new InvalidDataException("Blob input editor declarations differ from the input model.");
        if(typeof(BlobSettings).GetProperties(BindingFlags.Public|BindingFlags.Instance)
            .Where(property=>property.GetCustomAttribute<BlobNonFieldAttribute>() is null)
            .Any(property=>!schema.Contains(property.Name)))
            throw new InvalidDataException("Blob settings fields must all be declared in the parameter XML.");
        _bindings=BuildSettingsBindings();
        // 契约直接采用平台解析结果，与图像源一致：工具不再重复解析同一份声明。
        SettingsSchema=schema.Schema;
    }
    private static BlobParameterDefinition Load()
    {
        using var stream=typeof(BlobSettings).Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")
            ??throw new InvalidDataException("BlobParameters.xml is missing from the Runtime component.");
        return Read(stream);
    }
    public static BlobParameterDefinition Read(Stream stream)=>new(
        DeclaredParameterSchema.LoadWithTypes(stream??throw new ArgumentNullException(nameof(stream)),typeof(BlobSettings),
            property=>property.GetCustomAttribute<BlobNonFieldAttribute>() is null,
            // 矩形、圆形的记录序列由工具定义；ROI 继承字段的端口由执行契约声明，供 <Field> 引用呈现。
            BlobRoiInheritance.ExplicitType,
            BlobRoiInheritance.Ports,
            typeof(BlobFindParameters)));
    private IReadOnlyDictionary<string,XElement> _fields=>_schema.Fields;
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
    private double? Number(string id,string attribute)=>_fields[id].Attribute(attribute) is not {} item?null:Finite(_fields[id],attribute);
    public bool Contains(string id)=>_schema.Contains(id);
    public string WorkflowImageInputId=>_schema.WorkflowImageInputId;
    public FieldPresentation InputPresentation(Port port,int order)=>_schema.InputPresentation(port,order);
    public string Label(string id)=>_schema.Label(id);
    public string Page(string id)=>_schema.Page(id);
    public string Section(string id)=>_schema.Section(id);
    public string VmCategory(string id)=>_schema.VmCategory(id);
    public bool Advanced(string id)=>_schema.Advanced(id);
    public T Default<T>(string id)=>_schema.Default<T>(id);
    public double? Minimum(string id)=>_schema.Minimum(id);
    public double? Maximum(string id)=>_schema.Maximum(id);
    public int DecimalPlaces(string id)=>_schema.DecimalPlaces(id);
    public double Increment(string id)=>_schema.Increment(id);
}

public sealed partial class BlobParameterDefinition
{
    private readonly IReadOnlyList<SettingBinding> _bindings;
    /// <summary>设置模型属性与其字段身份、以及值读写映射。契约本身由平台解析结果提供。</summary>
    private sealed record SettingBinding(string FieldId,
        Func<BlobSettings,object?> Read,Func<BlobSettings,object?,BlobSettings> Write);
    public ToolSchema SettingsSchema { get; }
    public IReadOnlyDictionary<string,string> ParameterBindingNames=>_schema.BindingNames;

    public string SettingsFieldId(string property)=>_schema.SettingsFieldId(property);
    private IReadOnlyList<SettingBinding> BuildSettingsBindings()
    {
        var bindings=new List<SettingBinding>();
        foreach(var item in _fields.Values)
        {
            var name=Required(item,"id");var type=ParameterType(name);
            var settingsProperty=SettingsProperty(name);var findProperty=FindProperty(name);
            if(settingsProperty is null&&findProperty is null)throw new InvalidDataException("Unknown Blob parameter: "+name);
            // Port 与呈现由平台按同一份声明生成，这里只保留属性值到字段值的读写映射。
            var fieldId=_schema.SettingsFieldId(name);
            bindings.Add(new(fieldId,settings=>
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
        if(bindings.Select(b=>b.FieldId).Distinct(StringComparer.Ordinal).Count()!=bindings.Count)
            throw new InvalidDataException("Duplicate Blob settings field IDs.");
        return bindings.AsReadOnly();
    }
    public InputValues ReadSettings(BlobSettings settings)
    {
        var diagnostics=settings.Validate();
        if(diagnostics.Count!=0)throw new ArgumentException(string.Join("; ",diagnostics.Select(item=>item.Message)));
        return new(SettingsSchema.Inputs,_bindings.ToDictionary(b=>b.FieldId,b=>PrepareField(b,b.Read(settings)),StringComparer.Ordinal));
    }
    private object? PrepareField(SettingBinding field,object? value)
    {
        // Port 与范围校验取自平台解析结果，保持单一来源。
        var port=SettingsSchema.Inputs.Single(item=>item.Id==field.FieldId);
        value=ValueRules.Freeze(port,value);
        if(value is long or double)
        {
            var layout=SettingsSchema.Presentation!.Fields.Single(item=>item.FieldId==field.FieldId).Layout!;
            var number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            if(layout.Minimum is {} min&&number<min||layout.Maximum is {} max&&number>max)
                throw new ArgumentException("Blob settings field is outside XML editor bounds: "+field.FieldId);
        }
        return value;
    }
    public BlobSettings MaterializeSettings(InputValues resolved)
    {
        var values = new InputValues(SettingsSchema.Inputs, resolved.CopyValues());
        var result = new BlobSettings(false);
        foreach (var field in _bindings) result = field.Write(result, PrepareField(field,values.Get<object?>(field.FieldId)));
        var diagnostics = result.Validate();
        if (diagnostics.Count != 0) throw new ArgumentException(string.Join("; ", diagnostics.Select(item => item.Message)));
        return result.Freeze();
    }
    public BlobSettings ApplySettings(BlobSettings settings,IReadOnlyDictionary<string,object?> changes)
    {
        var copy=settings.Freeze();
        foreach(var change in changes)
        {
            var field=_bindings.SingleOrDefault(b=>b.FieldId==change.Key)??throw new ArgumentException("Unknown Blob settings field: "+change.Key);
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

/// <summary>Blob 结果页图形显示声明：只给组名与角色，控件由平台按声明生成。</summary>
public static class BlobDisplayDeclaration
{
    public static IReadOnlyList<string> GraphicGroups{get;}=Array.AsReadOnly(new[]
    {
        BlobResultDisplay.Graphic,
        BlobResultDisplay.InnerRectangle,
        BlobResultDisplay.Inspection,
        BlobResultDisplay.ReferencePoint,
        BlobResultDisplay.RunPoint,
        BlobResultDisplay.Mask,
        BlobResultDisplay.Annulus
    });

    public static IReadOnlyList<string> VisibilityGroups{get;}=Array.AsReadOnly(new[]
    {
        BlobResultDisplay.Text,
        BlobResultDisplay.Outline
    });

    public static string TextGroup=>BlobResultDisplay.Text;

    public static IReadOnlyList<string> GraphicRoles{get;}=Array.AsReadOnly(new[]
    {
        "visible","ok","ng","opacity","width"
    });

    public static IReadOnlyList<string> TextRoles{get;}=Array.AsReadOnly(new[]
    {
        "content","ok","ng","size","opacity","x","y"
    });

    public static string FieldId(string group,string role)=>BlobResultDisplay.Field(group,role);

    /// <summary>供平台按声明生成的实例包装；静态成员保持不变，测试可直接断言。</summary>
    public static SunEyeVision.Plugin.Wpf.Presenters.Display.IDisplayDeclaration Instance{get;}=new Declaration();

    private sealed class Declaration : SunEyeVision.Plugin.Wpf.Presenters.Display.IDisplayDeclaration
    {
        public IReadOnlyList<string> GraphicGroups=>BlobDisplayDeclaration.GraphicGroups;
        public IReadOnlyList<string> VisibilityGroups=>BlobDisplayDeclaration.VisibilityGroups;
        public string? TextGroup=>BlobDisplayDeclaration.TextGroup;
        public IReadOnlyList<string> GraphicRoles=>BlobDisplayDeclaration.GraphicRoles;
        public IReadOnlyList<string> TextRoles=>BlobDisplayDeclaration.TextRoles;
        public string FieldId(string group,string role)=>BlobDisplayDeclaration.FieldId(group,role);
    }
}

}

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Design
{

}

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views
{

/// <summary>The same real three-page composition for runtime and the XAML designer.</summary>
internal sealed class BlobUiComposition : IDisposable
{
    private const string PositionCorrectionField = "field:c95f3a26edf55097863a7ec47d17cce5";
    private readonly DeclaredSettingsView _editor;
    internal IToolSettingsDraft Draft {get;}
    private readonly ToolSettingsViewOptions _sourceOptions;
    private readonly ToolSettingsViewOptions _baseOptions;
    private NativeAssistUiDraftControls? _assistUi;
    private readonly FrameworkElement _assistPointVisibility = new() { Visibility = Visibility.Collapsed };
    private BlobSupportedWindowState? _supportedState;
    internal NativeRoiEditHost? _roiOwner;
    private NativeRoiUiDraftControls? _roiUi;
    private readonly ToolSettingsViewOptions _composed;
    private bool _disposed,_bound;
    private int _buildingPages;
    private DeclaredFieldsView? _pageHost;
    private string? _compositionError;
    private readonly Dictionary<string,long> _stageMs=new(StringComparer.Ordinal);

    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal IReadOnlyDictionary<string,long> StageMs=>_stageMs;
    internal Task ReadyTask=>_ready.Task;
    internal bool IsReady=>!_disposed&&_bound&&_buildingPages==0&&_compositionError is null;
    internal string? CompositionError=>_compositionError;
    internal event Action? Changed;
    internal bool HasPending=>_roiOwner?.HasPending==true||_roiUi?.HasPendingEdits==true||_assistUi is {HasPendingEdits:true,Visibility:Visibility.Visible};
    internal long RoiRevision=>_roiOwner?.Revision??0;
    internal BlobUiComposition(DeclaredSettingsView editor,IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        _editor=editor;Draft=draft;_sourceOptions=options;
        options=options with{HeaderlessSectionAnchors=new HashSet<string>{PositionCorrectionField}};
        // 13 个 ROI 订阅槽的端口由执行契约声明，参数页据此为 <Field> 呈现行补端口建行。
        _baseOptions=BlobRunUi.Configure(options with {PresentationPorts=BlobRoiInheritance.Ports});
        try
        {
            _composed=BlobDisplayUi.Configure(_baseOptions,_assistPointVisibility) with {ValidateUiState=ValidateUiState,PageBuilt=BuildPage};
            RefreshSupported();editor.Bind(draft,_composed);_bound=true;
            editor.Changed+=RefreshSupported;
            editor.SelectedPageChanged+=OnPageChanged;editor.IsVisibleChanged+=OnVisibility;editor.Unloaded+=OnUnloaded;
            _ready.TrySetResult(true);
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private void OnInputChanged(){RefreshSupported();Notify();}
    internal void RefreshAcceptedInputs()
    {
        if(_disposed||_roiUi is null||_supportedState is null)return;
        _supportedState.RefreshAcceptedInputs(_roiUi);
    }
    private void RefreshSupported()
    {
        if(_disposed)return;
        try
        {
            var mode = _baseOptions.ReadInputSetting?.Invoke(BlobImageRoi.ModeId)
                ?? (_baseOptions.InputContent as DeclaredInputFieldsView)?.ReadUnpresentedInput(BlobImageRoi.ModeId)
                ?? new UnsetSetting();
            var correction = BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsPositionCorrectionEnabled));
            var correctionEnabled = Draft.CaptureFields().Read(correction) is ConstantSetting { Value:true };
            _assistPointVisibility.Visibility = !BlobImageRoi.IsImageMode(mode) && correctionEnabled
                ? Visibility.Visible : Visibility.Collapsed;
            _roiUi?.SetPositionCorrectionEnabled(correctionEnabled);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            _assistPointVisibility.Visibility = Visibility.Collapsed;
        }
        if(_assistUi is {} assist)
            assist.Visibility=_assistPointVisibility.Visibility;
        if(_supportedState is {} state)state.Refresh();
        else BlobSupportedWindowState.ProjectSources(Draft,_baseOptions.InputContent as DeclaredInputFieldsView,()=>!_disposed);
    }
    private string? ValidateUiState()
    {
        if(_disposed||!ReferenceEquals(_editor.Draft,Draft))return "参数窗口已失效，请重新打开。";
        var hostError=_sourceOptions.ValidateUiState?.Invoke();
        if(!string.IsNullOrEmpty(hostError))return hostError;
        return BlobSupportedWindowState.ValidateSettings(Draft)??ValidateOrderedRanges()
            ??(_supportedState is not null?_supportedState.Validate():_baseOptions.InputContent is DeclaredInputFieldsView inputs?BlobSupportedWindowState.ValidateInputs(inputs,Draft)
                :_baseOptions.InputContentFactory is not null?"基本参数输入页尚未建立，无法核验原有输入保护；请先打开基本参数页再确认或执行。":null);
    }
    private string? ValidateOrderedRanges()
    {
        var fields=Draft.CaptureFields();
        string Id(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
        var pairs=new List<(string Low,string High,string Name)>();
        if(fields.Read(Id(nameof(BlobSettings.SegmentationMode))) is ConstantSetting{Value:"FixedSoftThreshold"})
            pairs.Add((Id(nameof(BlobSettings.FixedSoftLowerThreshold)),Id(nameof(BlobSettings.FixedSoftUpperThreshold)),"固定软阈值"));
        if(fields.Read(Id(nameof(BlobSettings.IsAreaFilterEnabled))) is ConstantSetting{Value:true})
            pairs.Add((Id(nameof(BlobSettings.FilterAreaMin)),Id(nameof(BlobSettings.FilterAreaMax)),"面积"));
        foreach(var pair in pairs)
        {
            // Subscription values are resolved by the invocation. Last accepted current values
            // do not describe a newly selected source or index. Native model/readback checks are distinct from binding proposals.
            if(fields.Read(pair.Low) is not ConstantSetting lower||fields.Read(pair.High) is not ConstantSetting upper)continue;
            if(lower.Value is not long low||upper.Value is not long high)return pair.Name+"范围常量类型无效。";
            if(low>high)return pair.Name+"使用原有序范围控件，当前下限超过上限；原设置保持不变。";
        }
        return null;
    }
    private void BuildPage(string page,DeclaredFieldsView fields,FrameworkElement? pageInput)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(ReferenceEquals(_pageHost,fields)&&_compositionError is not null)return;
        _buildingPages++;
        try
        {
            if(!ReferenceEquals(_pageHost,fields))ResetPageHost(fields);
            BuildPageCore(page,fields,pageInput);
        }
        catch(Exception error)
        {
            _compositionError=error.Message;
            SunEyeVision.Foundation.Logging.PluginLogger.Error(error.Message,nameof(BlobUiComposition),error);
            try{ReleaseInputComposition();}
            catch(Exception cleanup)
            {
                SunEyeVision.Foundation.Logging.PluginLogger.Error(cleanup.Message,nameof(BlobUiComposition),cleanup);
            }
        }
        finally
        {
            _buildingPages--;
        }
        if(_bound)Notify();
    }
    private void BuildPageCore(string page,DeclaredFieldsView fields,FrameworkElement? pageInput)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        void Measure(string stage,Action action)
        {
            var clock=Stopwatch.StartNew();
            try{action();}
            finally{_stageMs[$"{page}:{stage}"]=clock.ElapsedMilliseconds;}
        }
        var basicPage=Draft.Schema.Inputs.Count==0?null:_baseOptions.InputSection;
        // The input page is identified by the actual input view as well as its
        // metadata key.  A lazy settings host may report the page key before
        // the schema's section key is normalized; the concrete input view is
        // the authoritative signal and keeps the composition from disappearing
        // on a second editor opened with page zero selected.
        if((StringComparer.Ordinal.Equals(page,basicPage)||pageInput is DeclaredInputFieldsView)&&_roiUi is null)
        {
            var input=pageInput??_baseOptions.InputContent;
            if(input is DeclaredInputFieldsView inputs)
            {
                Measure("ROI组合",() =>
                {
                    // ROI 组按分组归位，不依赖锚点字段：输入项不在编辑器 schema 内，用锚点会抛未知字段。
                    var pose=BlobRoiUi.Add(inputs,
                        (field,content,group)=>
                        {
                            // VM 的 ROI 区域先完整呈现，位置修正行固定在该组末尾，
                            // 位置修正来源紧随开关之后按启用模式展开。
                            if(content is NativeRoiUiDraftControls)
                            {
                                fields.AddAfterField(PositionCorrectionField,content,
                                    followAnchorVisibility:false,insertBefore:true);
                                return;
                            }
                            fields.AddToGroup(group??"ROI区域",content);
                        },
                        observe:value=>_roiUi=value);
                    _assistUi=pose;
                    _roiOwner=new NativeRoiEditHost(inputs,_roiUi!,_baseOptions.RoiFrames,BlobRoiDeclaration.Create(inputs),_baseOptions.RoiDrawBlockReason);
                    _supportedState=new BlobSupportedWindowState(Draft,inputs,pose);
                    _roiUi!.SetPositionCorrectionEnabled(Draft.CaptureFields().Read(PositionCorrectionField) is ConstantSetting { Value:true });
                    fields.BindFieldVisibilityTo(PositionCorrectionField,_roiUi!.GraphicVisibilitySource);
                    // The row's visibility already follows the correction field
                    // through BindFieldVisibilityTo/followAnchorVisibility. Do
                    // not add a second conditional gate: that gate prevents the
                    // collapsed VM control from being materialized in the WPF
                    // visual tree when the editor opens with correction off.
                    if(pose is null)
                    {
                        _roiUi.Changed+=OnInputChanged;
                        _roiOwner.Changed+=Notify;
                        return;
                    }
                    // FlatNative may already have transferred input.pose into the
                    // VM settings root. Reuse that original row instead of
                    // attaching the same Visual a second time.
                    if(!fields.ContainsView(pose))
                        fields.AddAfterField(PositionCorrectionField,pose);
                    // Materialize the VM assist control once even when the
                    // correction switch starts off.  WPF defers a collapsed
                    // ContentPresenter, which otherwise makes the real control
                    // disappear from the page until the switch is toggled.
                    if(_assistPointVisibility.Visibility!=Visibility.Visible)
                    {
                        pose.Visibility=Visibility.Visible;
                        RoutedEventHandler? loaded=null;
                        loaded=(_,_)=>
                        {
                            pose.Loaded-=loaded;
                            if(!_disposed)pose.Visibility=_assistPointVisibility.Visibility;
                        };
                        pose.Loaded+=loaded;
                    }
                    pose.Changed+=OnInputChanged;
                    if(_roiUi is not null)_roiUi.Changed+=OnInputChanged;
                    _roiOwner.Changed+=Notify;
                });
            }
        }
        _baseOptions.PageBuilt?.Invoke(page,fields,pageInput);
    }

    private void ResetPageHost(DeclaredFieldsView next)
    {
        ReleaseInputComposition();
        _pageHost=next;_compositionError=null;RefreshSupported();
    }
    private void ReleaseInputComposition()
    {
        var oldOwner=_roiOwner;_roiOwner=null;
        var oldSupported=_supportedState;_supportedState=null;
        var oldAssist=_assistUi;_assistUi=null;
        var oldRoi=_roiUi;_roiUi=null;
        if(oldOwner is not null)oldOwner.Changed-=Notify;
        if(oldAssist is not null)oldAssist.Changed-=OnInputChanged;
        if(oldRoi is not null)oldRoi.Changed-=OnInputChanged;
        var errors=new List<Exception>();
        // 先清容器再销毁编辑器：编辑器销毁时若仍挂在画布上，WPF 会拒绝重新归属。
        foreach(var (name,action) in new (string,Action)[]
        {
            ("roi",()=>oldRoi?.Dispose()),("owner",()=>oldOwner?.Dispose()),
            ("supported",()=>oldSupported?.Dispose()),("assist",()=>oldAssist?.Dispose())
        })
            try{action();}catch(Exception error){errors.Add(new InvalidOperationException(name+" 清理失败",error));}
        if(errors.Count!=0)throw new AggregateException("基本参数交互清理失败。",errors);
    }
    internal bool TryCapture(out FieldSettings fields,out string error)
    {
        fields=null!;
        if(!IsReady){error="参数组合仍在组装，请稍候。";return false;}
        if(!ReferenceEquals(_editor.Draft,Draft)){error="参数窗口已失效，请重新打开。";return false;}
        if(_roiUi is not null&&!_roiUi.TryCaptureActiveSources(out error))return false;
        if(_roiOwner is not null&&!_roiOwner.TryCapture(out error))return false;
        return _editor.TryCapture(out fields,out error);
    }
    private void OnPageChanged(object? sender,EventArgs args)=>_assistUi?.RetireInteractions();
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args){if(!_editor.IsVisible)_assistUi?.RetireInteractions();}
    private void OnUnloaded(object sender,RoutedEventArgs args)=>_assistUi?.RetireInteractions();
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        if(!_ready.Task.IsCompleted)_ready.TrySetCanceled();
        _editor.Changed-=RefreshSupported;_editor.SelectedPageChanged-=OnPageChanged;_editor.IsVisibleChanged-=OnVisibility;_editor.Unloaded-=OnUnloaded;
        var errors=new List<Exception>();foreach(var action in new Action[]{ReleaseInputComposition,()=>{if(!_bound&&_baseOptions.InputContent is IDisposable input)input.Dispose();}})
            try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Blob界面作用域清理失败。",errors);
    }
}



/// <summary>Read-only capability projection over the real draft; never stores settings or runtime values.</summary>
internal sealed class BlobSupportedWindowState : IDisposable
{
    private readonly IToolSettingsDraft _draft;
    private readonly DeclaredInputFieldsView _inputs;
    private readonly NativeAssistUiDraftControls? _assist;
    private bool _disposed, _refreshing, _correctionEnabled;
    internal BlobSupportedWindowState(IToolSettingsDraft draft,DeclaredInputFieldsView inputs,NativeAssistUiDraftControls? assist)
    {
        _draft=draft;_inputs=inputs;_assist=assist;
        _correctionEnabled = draft.CaptureFields().Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is ConstantSetting { Value:true };
        foreach(var field in inputs.Fields)field.Changed+=Refresh;
        Refresh();
    }
    private static string Id(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
    internal string? Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if (_assist is { Visibility: Visibility.Visible } assist && assist.CommitActiveSources() is {} sourceError)
            return sourceError;
        return ValidateInputs(_inputs, _draft);
    }
    internal static string? ValidateInputs(DeclaredInputFieldsView inputs, IToolSettingsDraft draft)
    {
        if (BlobImageRoi.IsImageMode(inputs.ReadUnpresentedInput(BlobImageRoi.ModeId)))
        {
            var mask = inputs.Fields.Single(field => field.FieldId == BlobImageRoi.ImageId);
            if (mask.Setting is UnsetSetting) return "请选择屏蔽图像。";
            return mask.TryCommitPending() ? null : mask.Error;
        }
        var roiValues=new Dictionary<string,ParameterSetting>(StringComparer.Ordinal);
        foreach(var port in BlobRoiInheritance.Ports)
        {
            var setting=port.Id==BlobRoiInheritance.ModeId
                ?inputs.ReadUnpresentedInput(port.Id)
                :inputs.Fields.FirstOrDefault(field=>field.FieldId==port.Id)?.Setting??new UnsetSetting();
            if(setting is not UnsetSetting)roiValues.Add(port.Id,setting);
        }
        var roiDiagnostics=BlobRoiInheritance.ValidateConfiguration(new FieldSettings(BlobRoiInheritance.Ports,roiValues));
        if(roiDiagnostics.Count!=0)return roiDiagnostics[0].Message;
        if (BlobMaskInheritance.IsInherited(inputs.ReadUnpresentedInput(BlobMaskInheritance.ModeId)))
        {
            var mask=inputs.Fields.Single(field=>field.FieldId==BlobMaskInheritance.SourceId);
            if (mask.Setting is UnsetSetting) return "请选择继承屏蔽区来源。";
            if (!mask.TryCommitPending()) return mask.Error;
        }
        var correction = draft.CaptureFields().Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is ConstantSetting { Value:true };
        if (correction && BlobFixtureInputs.LeafIds.Any(id => inputs.ReadInputProjection(id) is UnsetSetting))
            return "请选择完整的位置修正来源。";
        return null;
    }
    // These guards must also run before the lazy Basic page has created its input-owned state.
    internal static string? ValidateSettings(IToolSettingsDraft draft)
    {
        var values=draft.CaptureFields();
        if(!BlobThresholdCapabilities.IsSupported((values.Read(Id(nameof(BlobSettings.SegmentationMode))) as ConstantSetting)?.Value as string))
            return "当前阈值模式尚未就绪；原设置保留，不能确认或执行。";
        // Result judgments and boundary output are formal settings consumed by
        // BlobAlgorithm. Their enabled state is not an unsupported UI capability.
        return null;
    }
    internal void Refresh()
    {
        if (_disposed || _refreshing) return;
        _refreshing = true;
        try
        {
            var enabled = _draft.CaptureFields().Read(Id(nameof(BlobSettings.IsPositionCorrectionEnabled))) is ConstantSetting { Value:true };
            if (_correctionEnabled && !enabled)
            {
                var pose = _assist?.Children.OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow>()
                    .SingleOrDefault(field => field.FieldId == BlobTool.PoseInput);
                if (pose is not null && !pose.TryClear())
                    throw new InvalidOperationException("位置修正来源清理未被参数草稿接纳。");
            }
            _correctionEnabled = enabled;
            ProjectSources(_draft,_inputs,()=>!_disposed);
        }
        finally { _refreshing = false; }
    }
    internal void RefreshAcceptedInputs(NativeRoiUiDraftControls roi)
    {
        if (_disposed) return;
        roi.RefreshAcceptedModes(()=>
        {
            _inputs.RefreshInputProjection(BlobTool.ImageInput);
            _inputs.RefreshInputProjection(BlobImageRoi.OutputEnabledId,new ConstantSetting(false));
            _inputs.RefreshInputProjection(BlobImageRoi.ImageId);
        });
        Refresh();
    }
    // Refresh the shared sources without forcing the lazy Basic input page into existence.
    internal static void ProjectSources(IToolSettingsDraft draft,DeclaredInputFieldsView? inputs,
        Func<bool> current)
    {
        if (inputs is null) return;
        try
        {
            var revision=draft.Revision;
            if(!current())return;
            if (inputs.Fields.Any(field => field.FieldId == BlobMaskInheritance.SourceId))
                inputs.RefreshInputProjection(BlobMaskInheritance.SourceId);
            foreach (var field in inputs.Fields.Where(field => BlobRoiInheritance.Ports.Any(port => port.Id == field.FieldId)).ToArray())
            {
                if (!current() || revision != draft.Revision) return;
                inputs.RefreshInputProjection(field.FieldId);
            }
            foreach (var field in inputs.Fields.Where(field => field.FieldId.StartsWith(BlobTool.PoseInput, StringComparison.Ordinal)).ToArray())
            {
                if (!current() || revision != draft.Revision) return;
                // VM's SelectWay is a real persisted radio with default value 0.
                // The projection may be unset while the optional fixture leaves are
                // dormant, but that must not clear the selected radio or make the
                // three choices appear inert after the enable switch is accepted.
                inputs.RefreshInputProjection(field.FieldId,
                    field.FieldId == BlobFixtureInputs.PositionCorrectionModeId ? new ConstantSetting(0L) : null);
                if (field.FieldId is not (BlobTool.PoseInput or BlobFixtureInputs.ReferencePoint or BlobFixtureInputs.RunPoint)) continue;
                var members = BlobFixtureInputs.LeavesFor(field.FieldId).Select(inputs.ReadInputProjection).ToArray();
                field.SetReferenceSummary(field.Setting is UnsetSetting && members.Any(member => member is not UnsetSetting)
                    ? "(" + string.Join(", ", members.Select(Describe)) + ")" : null);
            }
            string Describe(ParameterSetting value)
            {
                try { return inputs.DescribeInputSource(value); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return "来源不可用"; }
            }
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException) { }
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var field in _inputs.Fields)field.Changed-=Refresh;
    }
}



/// <summary>
/// VM 参数树中的运行参数页接缝。
///
/// 运行参数已经由 BlobParameters.xml、BlobParameterDefinition 和
/// DeclaredFieldsView 组成；这里仅挂接运行态能力，不再保留一套按代码
/// 逐行创建的备用参数界面。这样运行参数的字段身份、模板和释放路径只有
/// 一个来源，避免旧元数据分支重新引入旧控件组合。
/// </summary>
internal static class BlobRunUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        var definition=BlobParameterDefinition.Instance;
        var controller=definition.SettingsFieldId(nameof(BlobSettings.SegmentationMode));
        var conditions=options.ParentConditions?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)
            ??new Dictionary<string,IReadOnlyList<FieldCondition>>(StringComparer.Ordinal);
        // Original ThresholdType triggers select the active controls. Apply the
        // same activity before materialization, including both range endpoints.
        foreach(var property in BlobThresholdCapabilities.ControlledFields)
        {
            var id=definition.SettingsFieldId(property);
            var hidden=Enum.GetValues<BlobSegmentationMode>()
                .Where(mode=>!BlobThresholdCapabilities.IsThresholdFieldActive(mode,property))
                .Select(mode=>new FieldCondition(controller,mode.ToString(),true));
            conditions[id]=(conditions.GetValueOrDefault(id)??Array.Empty<FieldCondition>()).Concat(hidden).Distinct().ToArray();
        }
        // 原件 0x3 双阈值 Trigger（IMVSBlobFindModuAlgorithmTab.xml:160-173）只显示
        // LowThreshold/HightThreshold，隐藏 Polarity 行——极性由端点顺序决定
        // （BlobThresholdCapabilities.IsInverse 同源约定）。
        var polarityId=definition.SettingsFieldId(nameof(BlobSettings.Type));
        conditions[polarityId]=(conditions.GetValueOrDefault(polarityId)??Array.Empty<FieldCondition>())
            .Append(new FieldCondition(controller,BlobSegmentationMode.DoubleThreshold.ToString(),true)).Distinct().ToArray();
        // The original advanced expander never exposes OverlapRatio.
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? Enumerable.Empty<string>(), StringComparer.Ordinal)
        {
            definition.SettingsFieldId(nameof(BlobFindParameters.OverlapRatio))
        };
        return options with { ParentConditions = conditions, SuppressedFieldRows = suppressed };
    }

}



/// <summary>Real tool view; the document owner controls execution, capture and disposal.</summary>
public partial class BlobParameterEditor : UserControl,IToolSettingsView,IToolSettingsViewReady,IToolSettingsViewInputProjection
{
    private static readonly DependencyPropertyKey SelectedPageIndexPropertyKey=DependencyProperty.RegisterReadOnly(nameof(SelectedPageIndex),typeof(int),typeof(BlobParameterEditor),new PropertyMetadata(0));
    public static readonly DependencyProperty SelectedPageIndexProperty=SelectedPageIndexPropertyKey.DependencyProperty;
    public int SelectedPageIndex=>(int)GetValue(SelectedPageIndexProperty);
    private BlobUiComposition? _composition;
    private BlobUiComposition? Composition=>_composition;
    public Task ReadyTask=>Composition?.ReadyTask??Task.CompletedTask;
    public string? CompositionError=>Composition?.CompositionError;
    public IReadOnlyDictionary<string,long> CompositionStageMs=>Composition?.StageMs??new Dictionary<string,long>();
    private Action? _uiOnlyChanged;
    private bool _disposed;
    private void SelectedPageChanged(object? sender,EventArgs args)=>SetValue(SelectedPageIndexPropertyKey,Editor.SelectedPageIndex);
    public BlobParameterEditor()
    {InitializeComponent();Editor.SelectedPageChanged+=SelectedPageChanged;SelectedPageChanged(this,EventArgs.Empty);}
    public BlobParameterEditor(IToolSettingsDraft draft,ToolSettingsViewOptions options):this()
    {
        try
        {
            _composition=new(Editor,draft,options);_composition.Changed+=OnUiOnlyChanged;
            SelectedPageChanged(Editor,EventArgs.Empty);
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    public FrameworkElement View=>this;
    public void RefreshAcceptedInputs()=>Composition?.RefreshAcceptedInputs();
    public bool HasInvalidInput=>Editor.HasInvalidInput||Composition?.IsReady!=true||Composition?.CompositionError is not null;
    public bool HasPendingEdits=>Editor.HasPendingEdits||Composition?.HasPending==true;
    public bool HasPendingEditsOutside(IReadOnlySet<string> excludedFields)=>Editor.HasPendingEditsOutside(excludedFields)
        ||Composition is not {IsReady:true,HasPending:false};
    public long AcceptedRevision=>Editor.AcceptedRevision+(Composition?.RoiRevision??0);
    public IReadOnlyList<string> AcceptedFieldOrder=>Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope,string? fieldId=null)
        =>Editor.ReadNumericModels(scope,fieldId);
    public event Action? Changed {add{Editor.Changed+=value;_uiOnlyChanged+=value;}remove{Editor.Changed-=value;_uiOnlyChanged-=value;}}
    public bool TryCapture(out FieldSettings fields,out string error)
    {
        if(Composition is {} composition&&composition.CompositionError is {} failure){fields=null!;error=failure;return false;}
        if(Composition is {} ready&&!ready.IsReady){fields=null!;error="参数组合仍在组装，请稍候。";return false;}
        return Composition is {} current?current.TryCapture(out fields,out error):Editor.TryCapture(out fields,out error);
    }
    private void OnUiOnlyChanged(){if(!_disposed){_uiOnlyChanged?.Invoke();System.Windows.Input.CommandManager.InvalidateRequerySuggested();}}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Editor.SelectedPageChanged-=SelectedPageChanged;_uiOnlyChanged=null;
        var composition=Composition;if(composition is not null)composition.Changed-=OnUiOnlyChanged;
        try{composition?.Dispose();}finally{Editor.Dispose();}
    }
}
}
namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views
{
internal static class BlobDisplayUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options,FrameworkElement pointVisibility)
    {
        var text=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Text;
        string Id(string group,string role)=>SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(group,role);
        var expressions=options.TextExpressions?.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal)??new();
        var contentId=Id(text,"content");var sourceOptions=expressions.GetValueOrDefault(contentId);
        expressions[contentId]=new(sourceOptions?.ReadCandidates??(()=>new(Array.Empty<SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate>(),
            _=>throw new InvalidOperationException("文本来源尚未就绪。"),()=>true)),CreateSetting:proposal=>
        {
            var slots=proposal.Accepted is DisplayExpressionSetting existing?existing.Slots.ToList():new List<DisplayExpressionSlot>();
            if(proposal.Selected is {} selected)
            {
                if(proposal.Token is not {Length:>0} token)throw new ArgumentException("文本来源标记无效。");
                var reference=selected.Reference(BindingSelection.All);
                if(proposal.Text.Split("{"+token+"}",StringSplitOptions.None).Length>2&&slots.Any(slot=>slot.Token==token&&slot.Reference is {} old&&SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.SameReferenceIdentity(old,reference)==false))
                    throw new ArgumentException("文本来源名称不唯一，请使用不同的来源名称。");
                slots.RemoveAll(slot=>slot.Token==token);slots.Add(new(token,reference));
            }
            return SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.ParseContent(proposal.Text,slots);
        });
        var visibilitySources = options.FieldVisibilitySources?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)
            ?? new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
        visibilitySources.Add(Id(BlobResultDisplay.ReferencePoint,"visible"),pointVisibility);
        visibilitySources.Add(Id(BlobResultDisplay.RunPoint,"visible"),pointVisibility);
        return SunEyeVision.Plugin.Wpf.Presenters.Display.DisplayOptionsFactory.Apply(
            options with {TextExpressions=expressions, FieldVisibilitySources=visibilitySources},
            BlobDisplayDeclaration.Instance);
    }

}
}
