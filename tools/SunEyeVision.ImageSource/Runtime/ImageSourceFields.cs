using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Stable source setting identities shared through declarations, never CLR reflection.</summary>
public static class ImageSourceFields
{
    public const string SourceKind="source.kind", Paths="local.paths", Start="local.start", Selected="local.selected",
        RunMode="local.run-mode", AutoSwitch="local.auto-switch", StopAtLast="local.stop-at-last",
        CameraDevice="camera.device", CameraTimeout="camera.timeout-ms", OutputFormat="output.format", InitialSequence="sequence.initial";
    private static readonly TypeRef Text=TypeRef.ScalarOf(ScalarKind.Text);
    private static readonly TypeRef Integer=TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Boolean=TypeRef.ScalarOf(ScalarKind.Boolean);
    public static TypeRef References {get;}=TypeRef.SequenceOf(Text,CountConstraint.Any);
    public static ToolSchema Schema {get;}=new(new[] {
        new Port(SourceKind,Text,EditModes:FieldEditModes.Constant,Choices:new(new[]{"local","camera"})),
        new Port(Paths,References,EditModes:FieldEditModes.Constant),
        new Port(Start,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),
        new Port(Selected,References,EditModes:FieldEditModes.Constant),
        new Port(RunMode,Text,EditModes:FieldEditModes.Constant,Choices:new(new[]{"all","selected"})),
        new Port(AutoSwitch,Boolean,EditModes:FieldEditModes.Constant),
        new Port(StopAtLast,Boolean,EditModes:FieldEditModes.Constant),
        new Port(CameraDevice,Text,EditModes:FieldEditModes.Constant),
        new Port(CameraTimeout,Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant),
        new Port(OutputFormat,Text,EditModes:FieldEditModes.Constant,Choices:new(new[]{"original","mono8","rgb24"})),
        new Port(InitialSequence,Integer,Storage:NumericStorage.Int64,EditModes:FieldEditModes.Constant)
    },Array.Empty<Port>(),new ToolPresentation(new[] {
        Choice(SourceKind,"图像来源","图像来源",0,new[]{"local","camera"},new[]{"本地图像","相机"}),
        Choice(OutputFormat,"输出格式","基本参数",10,new[]{"original","mono8","rgb24"},new[]{"保持原图","MONO8","RGB24"},"local"),
        new FieldPresentation(InitialSequence,"SN初始值","基本参数",20) {
            Layout=new("基本参数","基本参数",Minimum:0,DecimalPlaces:0,AllowReference:false)},
        new FieldPresentation(AutoSwitch,"自动切换","触发",30) {
            Layout=new("基本参数","触发",VisibleWhen:new(SourceKind,"local"),AllowReference:false)},
        new FieldPresentation(StopAtLast,"末张成功后停止","触发",40) {
            Layout=new("基本参数","触发",VisibleWhen:new(SourceKind,"local"),AllowReference:false)},
        new FieldPresentation(CameraTimeout,"取帧超时(ms)","相机",50) {
            Layout=new("基本参数","相机",VisibleWhen:new(SourceKind,"camera"),Minimum:1,Maximum:int.MaxValue,DecimalPlaces:0,AllowReference:false)}
    }));

    private static FieldPresentation Choice(string id,string label,string section,int order,string[] keys,string[] labels,string? sourceKind=null)
        =>new(id,label,section,order,FieldEditorKind.Choice,keys,keys.Zip(labels).ToDictionary(pair=>pair.First,pair=>pair.Second,StringComparer.Ordinal)) {
            Layout=new("基本参数",section,VisibleWhen:sourceKind is null?null:new(SourceKind,sourceKind),AllowReference:false)};

    public static InputValues Read(ImageSourceSettings settings)=>new(Schema.Inputs,new Dictionary<string,object?> {
        [SourceKind]=settings.SourceKind,[Paths]=new SequenceValue(References,settings.FileReferences),
        [Start]=(long)settings.StartIndex,[Selected]=new SequenceValue(References,settings.SelectedReferences),
        [RunMode]=settings.RunMode,[AutoSwitch]=settings.AutoSwitch,[StopAtLast]=settings.StopAtLast,
        [CameraDevice]=settings.CameraDeviceId,[CameraTimeout]=(long)settings.CameraTimeoutMs,
        [OutputFormat]=settings.OutputFormat,[InitialSequence]=settings.InitialSequence
    });
    public static ImageSourceSettings Apply(ImageSourceSettings original,IReadOnlyDictionary<string,object?> changes)
    {
        var values=Read(original).CopyValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in changes)
        {
            if(!values.ContainsKey(pair.Key))throw new ArgumentException("未声明的图像源字段："+pair.Key);
            values[pair.Key]=pair.Value;
        }
        var candidate=new InputValues(Schema.Inputs,values);
        var result=new ImageSourceSettings {
            SourceKind=candidate.Get<string>(SourceKind),
            FileReferences=ReadReferences(candidate,Paths),StartIndex=checked((int)candidate.Get<long>(Start)),
            SelectedReferences=ReadReferences(candidate,Selected),RunMode=candidate.Get<string>(RunMode),
            AutoSwitch=candidate.Get<bool>(AutoSwitch),StopAtLast=candidate.Get<bool>(StopAtLast),
            CameraDeviceId=candidate.Get<string>(CameraDevice),CameraTimeoutMs=checked((int)candidate.Get<long>(CameraTimeout)),
            OutputFormat=candidate.Get<string>(OutputFormat),InitialSequence=candidate.Get<long>(InitialSequence)
        };
        var errors=result.Validate();
        if(errors.Count!=0)throw new ArgumentException(string.Join("；",errors.Select(error=>error.Message)));
        return result.Freeze();
    }
    private static IReadOnlyList<string> ReadReferences(InputValues values,string field)
        =>Array.AsReadOnly(values.Get<SequenceValue>(field).Select(item=>item as string??throw new ArgumentException("文件引用必须是文本。")).ToArray());
}
