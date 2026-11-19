using System.IO;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Stable source setting identities shared through declarations, never CLR reflection.</summary>
public static class ImageSourceFields
{
    public const string SourceKind="source.kind", Paths="local.paths", Start="local.start", Selected="local.selected",
        RunMode="local.run-mode", AutoSwitch="local.auto-switch", StopAtLast="local.stop-at-last",
        CameraDevice="camera.device", CameraTimeout="camera.timeout-ms", OutputFormat="output.format",
        OutputMono8="output.mono8", InitialSequence="sequence.initial", PathCache="local.path-cache";
    public const string ExposureTime="camera.exposure-time", Gain="camera.gain";
    public const string SaveImages="local.save-images";
    public const string SubscribeFolder="local.subscribe-folder", FolderPath="local.folder-path", RefreshFolder="local.refresh-folder";
    public const string TriggerFolderRun="local.trigger-folder-run";
    public const string StitchEnabled="stitch.enabled", StitchStart="stitch.start", StitchHeight="stitch.height",
        ClearStitch="stitch.clear", ClearStitchValue="stitch.clear-value";
    public const string TriggerFilterEnabled="trigger.filter-enabled", TriggerText="trigger.text", TriggerFilterText="trigger.filter-text";
    private static readonly TypeRef Text=TypeRef.ScalarOf(ScalarKind.Text);
    private static readonly TypeRef Integer=TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Boolean=TypeRef.ScalarOf(ScalarKind.Boolean);
    public const string ShowImageName="display.image-name";
    internal const long MaximumInitialSequence=100_000;
    public static ToolSchema DisplaySchema {get;}=new(Array.Empty<Port>(),Array.Empty<Port>(),new ToolPresentation(Array.Empty<FieldPresentation>()));
    public static TypeRef References {get;}=TypeRef.SequenceOf(Text,CountConstraint.Any);
    /// <summary>输入页字段：不进设置页，值随 Schema.Inputs 持久化，由输入编辑器提供。</summary>
    private static readonly Port[] InputPorts=
    {
        new Port(Paths,References,EditModes:FieldEditModes.Constant),
        new Port(Start,Integer,Required:false,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),
        new Port(Selected,References,Required:false,EditModes:FieldEditModes.Constant),
        new Port(RunMode,Text,Required:false,EditModes:FieldEditModes.Constant,Choices:new(new[]{"all","selected"}))
    };

    private static readonly Lazy<DeclaredParameterSchema> Declared=new(()=>
    {
        using var stream=typeof(ImageSourceSettings).Assembly
            .GetManifestResourceStream("ImageSourceParameters.xml")
            ??throw new InvalidDataException("ImageSourceParameters.xml is missing from the Runtime component.");
        return DeclaredParameterSchema.Load(stream,typeof(ImageSourceSettings));
    });

    /// <summary>设置页参数声明，来自 XML。</summary>
    public static ToolSchema SettingsSchema=>Declared.Value.Schema;

    /// <summary>完整输入契约：设置参数 + 输入页字段。</summary>
    public static ToolSchema Schema {get;}=new(SettingsSchema.Inputs.Concat(InputPorts).ToArray(),
        Array.Empty<Port>(),SettingsSchema.Presentation!);

    private static FieldPresentation Choice(string id,string label,string section,int order,string[] keys,string[] labels,string? sourceKind=null)
        =>new(id,label,section,order,FieldEditorKind.Choice,keys,keys.Zip(labels).ToDictionary(pair=>pair.First,pair=>pair.Second,StringComparer.Ordinal)) {
            Layout=new("基本参数",section,VisibleWhen:sourceKind is null?null:new(SourceKind,sourceKind),AllowReference:false)};

    public static InputValues Read(ImageSourceSettings settings)
    {
        var values=new Dictionary<string,object?> {
        [SourceKind]=settings.SourceKind,[SaveImages]=settings.SaveImages,[ShowImageName]=settings.ShowImageName,
        // 输入项在新建节点时给出初始值；之后的真实值由输入编辑器写入 FieldSettings。
        [Paths]=new SequenceValue(References,Array.Empty<string>()),
        [Selected]=new SequenceValue(References,Array.Empty<string>()),
        [Start]=-1L,[RunMode]="all",
        [AutoSwitch]=settings.AutoSwitch,[StopAtLast]=settings.StopAtLast,
        [CameraDevice]=settings.CameraDeviceId,[CameraTimeout]=(long)settings.CameraTimeoutMs,
        [OutputFormat]=settings.OutputFormat,[OutputMono8]=settings.OutputMono8,[InitialSequence]=settings.InitialSequence,
        [PathCache]=(long)settings.PathCache,[SubscribeFolder]=settings.SubscribeFolder,
        [FolderPath]=settings.FolderPath,[RefreshFolder]=settings.RefreshFolder,[TriggerFolderRun]=settings.TriggerFolderRun,
        [StitchEnabled]=settings.StitchEnabled,[StitchStart]=(long)settings.StitchStart,[StitchHeight]=(long)settings.StitchHeight,
        [ClearStitch]=settings.ClearStitch,[TriggerFilterEnabled]=settings.TriggerFilterEnabled
        };
        if(settings.ExposureTime is {} exposure)values[ExposureTime]=exposure;
        if(settings.Gain is {} gain)values[Gain]=gain;
        if(settings.ClearStitchValue is {} clear)values[ClearStitchValue]=clear;
        if(settings.TriggerText is {} text)values[TriggerText]=text;
        if(settings.TriggerFilterText is {} filter)values[TriggerFilterText]=filter;
        return new(Schema.Inputs,values);
    }
    public static ImageSourceSettings Apply(ImageSourceSettings original,IReadOnlyDictionary<string,object?> changes)
    {
        // 只保留设置项：输入项的值由输入编辑器维护，不参与设置往返。
        var values=Read(original).CopyValues()
            .Where(pair=>SettingsSchema.Inputs.Any(port=>port.Id==pair.Key))
            .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in changes)
        {
            if(!SettingsSchema.Inputs.Any(port=>port.Id==pair.Key))throw new ArgumentException("未声明的图像源字段："+pair.Key);
            values[pair.Key]=pair.Value;
        }
        return Materialize(new InputValues(SettingsSchema.Inputs,values));
    }
    public static ImageSourceSettings Materialize(InputValues values)
    {
        var candidate=new InputValues(SettingsSchema.Inputs,values.CopyValues()
            .Where(pair=>SettingsSchema.Inputs.Any(port=>port.Id==pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal));
        var result=new ImageSourceSettings {
            SourceKind=candidate.Get<string>(SourceKind),SaveImages=candidate.Get<bool>(SaveImages),ShowImageName=candidate.Contains(ShowImageName)?candidate.Get<bool>(ShowImageName):true,
            AutoSwitch=candidate.Get<bool>(AutoSwitch),StopAtLast=candidate.Get<bool>(StopAtLast),
            CameraDeviceId=candidate.Get<string>(CameraDevice),CameraTimeoutMs=checked((int)candidate.Get<long>(CameraTimeout)),
            ExposureTime=ReadCameraValue(candidate,ExposureTime),Gain=ReadCameraValue(candidate,Gain),
            OutputFormat=candidate.Get<string>(OutputFormat),OutputMono8=candidate.Get<bool>(OutputMono8),
            InitialSequence=candidate.Get<long>(InitialSequence),PathCache=checked((int)candidate.Get<long>(PathCache)),
            SubscribeFolder=candidate.Get<bool>(SubscribeFolder),FolderPath=candidate.Get<string>(FolderPath),
            RefreshFolder=candidate.Get<bool>(RefreshFolder),TriggerFolderRun=candidate.Get<bool>(TriggerFolderRun),
            StitchEnabled=candidate.Get<bool>(StitchEnabled),StitchStart=checked((int)candidate.Get<long>(StitchStart)),
            StitchHeight=checked((int)candidate.Get<long>(StitchHeight)),ClearStitch=candidate.Get<bool>(ClearStitch),
            ClearStitchValue=candidate.Contains(ClearStitchValue)?(long?)candidate.Get<object?>(ClearStitchValue):null,
            TriggerFilterEnabled=candidate.Get<bool>(TriggerFilterEnabled),
            TriggerText=candidate.Contains(TriggerText)?(string?)candidate.Get<object?>(TriggerText):null,
            TriggerFilterText=candidate.Contains(TriggerFilterText)?(string?)candidate.Get<object?>(TriggerFilterText):null
        };
        var errors=result.Validate();
        if(errors.Count!=0)throw new ArgumentException(string.Join("；",errors.Select(error=>error.Message)));
        return result.Freeze();
    }
    private static IReadOnlyList<string> ReadReferences(InputValues values,string field)
        =>Array.AsReadOnly(values.Get<SequenceValue>(field).Select(item=>item as string??throw new ArgumentException("文件引用必须是文本。")).ToArray());
    private static double? ReadCameraValue(InputValues values,string field)
        =>values.Contains(field)?(double?)values.Get<object?>(field):null;
}
