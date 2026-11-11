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
    public static ToolSchema DisplaySchema {get;}=new(new[] {
        new Port(ShowImageName,Boolean,EditModes:FieldEditModes.Constant)
    },Array.Empty<Port>(),new ToolPresentation(new[] {
        new FieldPresentation(ShowImageName,"显示图像名称","基本参数",20,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",AllowReference:false)}
    }));
    public static TypeRef References {get;}=TypeRef.SequenceOf(Text,CountConstraint.Any);
    public static ToolSchema Schema {get;}=new(new[] {
        new Port(SourceKind,Text,EditModes:FieldEditModes.Constant,Choices:new(new[]{"local","camera","sdk"})),
        new Port(SaveImages,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(Paths,References,EditModes:FieldEditModes.Constant),
        new Port(Start,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),
        new Port(Selected,References,EditModes:FieldEditModes.Constant),
        new Port(RunMode,Text,EditModes:FieldEditModes.Constant,Choices:new(new[]{"all","selected"})),
        new Port(AutoSwitch,Boolean,EditModes:FieldEditModes.Constant),
        new Port(StopAtLast,Boolean,EditModes:FieldEditModes.Constant),
        new Port(CameraDevice,Text,EditModes:FieldEditModes.Constant),
        new Port(CameraTimeout,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),
        new Port(ExposureTime,DataType.Number,Required:false,Nullable:true,Storage:NumericStorage.Float32,EditModes:FieldEditModes.Reference),
        new Port(Gain,DataType.Number,Required:false,Nullable:true,Storage:NumericStorage.Float32,EditModes:FieldEditModes.Reference),
        new Port(OutputFormat,Text,EditModes:FieldEditModes.Constant,Choices:new(new[]{"mono8","rgb24","original"})),
        new Port(OutputMono8,Boolean,EditModes:FieldEditModes.Constant),
        new Port(InitialSequence,Integer,Storage:NumericStorage.Int64,EditModes:FieldEditModes.Constant),
        new Port(PathCache,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant,
            DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(SubscribeFolder,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(FolderPath,Text,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(RefreshFolder,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(TriggerFolderRun,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(StitchEnabled,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(StitchStart,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(StitchHeight,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(ClearStitch,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(ClearStitchValue,Integer,Required:false,Nullable:true,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Reference),
        new Port(TriggerFilterEnabled,Boolean,EditModes:FieldEditModes.Constant,DefaultPresence:FieldDefaultPresence.UseDeclaredDefault),
        new Port(TriggerText,Text,Required:false,Nullable:true,EditModes:FieldEditModes.Reference),
        new Port(TriggerFilterText,Text,Required:false,Nullable:true,EditModes:FieldEditModes.Reference)
    },Array.Empty<Port>(),new ToolPresentation(new[] {
        Choice(SourceKind,"图像源","基本参数",0,new[]{"local","camera","sdk"},new[]{"本地图像","相机","SDK"}),
        Choice(OutputFormat,"像素格式","基本参数",10,new[]{"mono8","rgb24","original"},new[]{"MONO8","RGB24","保持原图"},"local"),
        new FieldPresentation(SaveImages,"保存图像","基本参数",15,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(SourceKind,"local"),AllowReference:false,BooleanTips:new("最大保存120张图片"))},
        new FieldPresentation(OutputMono8,"输出Mono8","基本参数",70,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(OutputFormat,"mono8",Negate:true),
                OrVisibleWhen:new(SourceKind,"local",Negate:true),AllowReference:false)},
        new FieldPresentation(InitialSequence,"SN初始值","基本参数",50,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",Minimum:0,Maximum:MaximumInitialSequence,DecimalPlaces:0,AllowReference:false)},
        new FieldPresentation(PathCache,"图片缓存","基本参数",60,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(SourceKind,"local"),Minimum:0,Maximum:5,DecimalPlaces:0,AllowReference:false)},
        new FieldPresentation(AutoSwitch,"自动切换","触发设置",100,FieldEditorKind.Scalar) {
            Layout=new("基本参数","触发设置",VisibleWhen:new(SourceKind,"local"),AllowReference:false)},
        new FieldPresentation(StopAtLast,"最后一张停止","触发设置",110,FieldEditorKind.Scalar) {
            Layout=new("基本参数","触发设置",VisibleWhen:new(SourceKind,"local"),AllowReference:false)},
        new FieldPresentation(SubscribeFolder,"订阅图片文件夹","订阅图片文件夹",200,FieldEditorKind.Scalar) {
            Layout=new("基本参数","订阅图片文件夹",VisibleWhen:new(SourceKind,"local"),AllowReference:false)},
        new FieldPresentation(FolderPath,"图像路径","订阅图片文件夹",201,FieldEditorKind.FolderPath) {
            Layout=new("基本参数","订阅图片文件夹",VisibleWhen:new(SubscribeFolder,true),AllowReference:false)},
        new FieldPresentation(RefreshFolder,"刷新文件夹图片","订阅图片文件夹",202,FieldEditorKind.Scalar) {
            Layout=new("基本参数","订阅图片文件夹",VisibleWhen:new(SubscribeFolder,true),AllowReference:false)},
        new FieldPresentation(TriggerFolderRun,"触发流程运行","订阅图片文件夹",203,FieldEditorKind.Scalar) {
            Layout=new("基本参数","订阅图片文件夹",VisibleWhen:new(SubscribeFolder,true),AllowReference:false)},
        new FieldPresentation(StitchEnabled,"拼接图像","基本参数",80,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",AllowReference:false)},
        new FieldPresentation(StitchStart,"拼接起始高度","基本参数",81,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(StitchEnabled,true),Minimum:0,Maximum:20000,DecimalPlaces:0,AllowReference:false)},
        new FieldPresentation(StitchHeight,"拼接高度","基本参数",82,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(StitchEnabled,true),Minimum:0,Maximum:20000,DecimalPlaces:0,AllowReference:false)},
        new FieldPresentation(ClearStitch,"清空触发","基本参数",83,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(StitchEnabled,true),AllowReference:false)},
        new FieldPresentation(ClearStitchValue,"清空条件","基本参数",84,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(ClearStitch,true))},
        new FieldPresentation(TriggerFilterEnabled,"字符触发过滤","触发设置",120,FieldEditorKind.Scalar) {
            Layout=new("基本参数","触发设置",AllowReference:false)},
        new FieldPresentation(TriggerText,"输入字符","触发设置",121,FieldEditorKind.Scalar) {
            Layout=new("基本参数","触发设置",VisibleWhen:new(TriggerFilterEnabled,true))},
        new FieldPresentation(TriggerFilterText,"触发字符","触发设置",122,FieldEditorKind.Scalar) {
            Layout=new("基本参数","触发设置",VisibleWhen:new(TriggerFilterEnabled,true))},
        new FieldPresentation(CameraTimeout,"取帧超时(ms)","基本参数",45,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(SourceKind,"camera"),Minimum:1,Maximum:int.MaxValue,DecimalPlaces:0,AllowReference:false)},
        new FieldPresentation(ExposureTime,"控制曝光","基本参数",30,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(SourceKind,"camera"))},
        new FieldPresentation(Gain,"控制增益","基本参数",40,FieldEditorKind.Scalar) {
            Layout=new("基本参数","基本参数",VisibleWhen:new(SourceKind,"camera"))}
    }));

    private static FieldPresentation Choice(string id,string label,string section,int order,string[] keys,string[] labels,string? sourceKind=null)
        =>new(id,label,section,order,FieldEditorKind.Choice,keys,keys.Zip(labels).ToDictionary(pair=>pair.First,pair=>pair.Second,StringComparer.Ordinal)) {
            Layout=new("基本参数",section,VisibleWhen:sourceKind is null?null:new(SourceKind,sourceKind),AllowReference:false)};

    public static InputValues Read(ImageSourceSettings settings)
    {
        var values=new Dictionary<string,object?> {
        [SourceKind]=settings.SourceKind,[SaveImages]=settings.SaveImages,[Paths]=new SequenceValue(References,settings.FileReferences),
        [Start]=(long)settings.StartIndex,[Selected]=new SequenceValue(References,settings.SelectedReferences),
        [RunMode]=settings.RunMode,[AutoSwitch]=settings.AutoSwitch,[StopAtLast]=settings.StopAtLast,
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
        var values=Read(original).CopyValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in changes)
        {
            if(!Schema.Inputs.Any(port=>port.Id==pair.Key))throw new ArgumentException("未声明的图像源字段："+pair.Key);
            values[pair.Key]=pair.Value;
        }
        return Materialize(new InputValues(Schema.Inputs,values));
    }
    public static ImageSourceSettings Materialize(InputValues values)
    {
        var candidate=new InputValues(Schema.Inputs,values.CopyValues());
        var result=new ImageSourceSettings {
            SourceKind=candidate.Get<string>(SourceKind),SaveImages=candidate.Get<bool>(SaveImages),
            FileReferences=ReadReferences(candidate,Paths),StartIndex=checked((int)candidate.Get<long>(Start)),
            SelectedReferences=ReadReferences(candidate,Selected),RunMode=candidate.Get<string>(RunMode),
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
