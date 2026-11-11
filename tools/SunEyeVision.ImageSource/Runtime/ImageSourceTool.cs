using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>One frozen file/frame invocation. Sequence and camera connection ownership stay in the host.</summary>
public sealed class ImageSourceTool : IToolDefinition<ImageSourceSettings,ImageSourceOutput>,IToolSettingsFields,IToolCatalogProvider,IToolResultPresentationProvider,IToolDisplaySettingsProvider,IToolManualReferenceSources,IToolHostInputDeclaration,IToolResourceFieldDeclarations,IToolParameterBindingNames,IToolDormantParameterDefaults,IToolExecutionFilter
{
    public const string Id="ImageLoad";
    public const string LocalAddress="input.local-address", CameraImage="input.camera-image", Sequence="input.sequence",
        DeviceId="input.device-id",DeviceFrameId="input.device-frame-id",DeviceTimestamp="input.device-timestamp";
    public const string SdkAddress="input.sdk-address", SdkImage="input.sdk-image";
    public const string CameraFailure="input.camera-failure";
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources {get;} =
        Array.AsReadOnly(new[] {ImageSourceOutput.Image,ImageSourceOutput.GrayImage}
            .SelectMany(port=>new[] {ImageSourceOutput.ImageWidth,ImageSourceOutput.ImageHeight,ImageSourceOutput.ImageFormat}
                .Select(member=>new ManualReferenceSource(port,new[]{member})))
            .Append(new ManualReferenceSource(ImageSourceOutput.ModuleRunTime))
            .Append(new ManualReferenceSource(ImageSourceOutput.Sequence))
            .Append(new ManualReferenceSource(ImageSourceOutput.CameraFailureCount)).ToArray());
    public string ToolId=>Id;
    public HostInputDeclaration HostInputs => new("image-source", 1,
        Schema.Inputs.Select(port => new HostInputPort(port.Id, port.Type)), new[] { "local-image-read", "camera-frame", "sdk-image" });
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.AsReadOnly(new[] {
        new ResourceFieldDeclaration("local.paths", ResourcePurpose.FileRead, ResourceReferenceCardinality.Sequence,
            new FileEmbeddingDeclaration(ImageSourceFields.SaveImages,120,ImageSourceFields.SubscribeFolder)),
        new ResourceFieldDeclaration("local.selected", ResourcePurpose.FileRead, ResourceReferenceCardinality.Sequence),
        new ResourceFieldDeclaration(ImageSourceFields.FolderPath, ResourcePurpose.DirectoryRead, ResourceReferenceCardinality.Scalar)
    });
    public ToolCatalogDescriptor Catalog {get;}=new("图像源","本地图像或方案相机的标准图像输出","image","采集",10,"IMAGE");
    public ToolSchema SettingsSchema=>ImageSourceFields.Schema;
    public IReadOnlyDictionary<string,string> ParameterBindingNames {get;}=
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>
        {
            [ImageSourceFields.ExposureTime]="ExposureTimeInput", [ImageSourceFields.Gain]="GainInput",
            [ImageSourceFields.ClearStitchValue]="ClearTriggerValue",
            [ImageSourceFields.TriggerText]="TriggerString", [ImageSourceFields.TriggerFilterText]="TriggerFilter"
        });
    public IReadOnlyDictionary<string,ConstantSetting> SelectDormantParameterDefaults(FieldSettings configuration)
    {
        var dormant = new List<string>();
        if (configuration.Read(ImageSourceFields.SourceKind) is not ConstantSetting {Value:"camera"})
            dormant.AddRange(new[] { ImageSourceFields.ExposureTime, ImageSourceFields.Gain });
        if (configuration.Read(ImageSourceFields.StitchEnabled) is not ConstantSetting {Value:true} ||
            configuration.Read(ImageSourceFields.ClearStitch) is not ConstantSetting {Value:true})
            dormant.Add(ImageSourceFields.ClearStitchValue);
        if (configuration.Read(ImageSourceFields.TriggerFilterEnabled) is not ConstantSetting {Value:true})
            dormant.AddRange(new[] { ImageSourceFields.TriggerText, ImageSourceFields.TriggerFilterText });
        return dormant.Where(id=>configuration.Read(id) is ReferenceSetting or VariableSetting)
            .ToDictionary(id=>id,_=>new ConstantSetting(null),StringComparer.Ordinal);
    }
    public bool ShouldExecute(object settings)
        => AllowsTrigger(RequireSettings(settings));

    internal static bool AllowsTrigger(ImageSourceSettings source)
    {
        if (!source.TriggerFilterEnabled) return true;
        static string Text(string? value)
        {
            value ??= "";
            var end = value.IndexOf('\0');
            return end < 0 ? value : value[..end];
        }
        var text = Text(source.TriggerText);
        var filter = Text(source.TriggerFilterText);
        return text.Length == 0 || filter.Length == 0 || text == filter
            || text == "##RunProcedureSilently##" || filter == "##RunProcedureSilently##";
    }
    public ToolSchema DisplaySettingsSchema=>ImageSourceFields.DisplaySchema;
    public InputValues CreateDisplaySettings()=>new(DisplaySettingsSchema.Inputs,
        new Dictionary<string,object?>{[ImageSourceFields.ShowImageName]=true});
    public ToolSchema ResultSchema {get;}=new(Array.Empty<Port>(),ImageSourceOutput.Ports,outputDisplay:ImageSourceOutput.Display);
    public ResultProjectionDescriptor ResultProjection {get;}=new(ResultProjectionKind.PropertyList,ResultCardinality.One,new[] {
        new ResultColumnDescriptor("image","图像",new(ImageSourceOutput.Image)),
        new ResultColumnDescriptor("gray-image","灰度图像",new(ImageSourceOutput.GrayImage)),

        new ResultColumnDescriptor("file","当前图像路径",new(ImageSourceOutput.FileReference)),
        new ResultColumnDescriptor("file-name","当前图像名称",new(ImageSourceOutput.FileName)),
        new ResultColumnDescriptor("frame","帧号",new(ImageSourceOutput.FrameId)),
        new ResultColumnDescriptor("sequence","SN码",new(ImageSourceOutput.Sequence))
    });
    public InputValues ReadSettings(object settings)=>ImageSourceFields.Read(RequireSettings(settings));
    public object MaterializeSettings(InputValues values)=>ImageSourceFields.Materialize(values);
    private static ImageSourceSettings RequireSettings(object settings)=>settings as ImageSourceSettings??throw new ArgumentException("图像源设置类型不正确。");
    public ToolSchema Describe()=>Schema;
    // No WorkflowImageInputId: an unrelated displayed/upstream image must never become a camera frame.
    public static ToolSchema Schema {get;}=new(new[] {
        new Port(LocalAddress,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(SdkAddress,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(SdkImage,TypeRef.ResourceOf(ResourceKind.Image),Required:false),
        new Port(CameraImage,TypeRef.ResourceOf(ResourceKind.Image),Required:false),
        new Port(CameraFailure,DataType.Text,Required:false),
        new Port(Sequence,TypeRef.ScalarOf(ScalarKind.Int64),Required:false,Storage:NumericStorage.Int64),
        new Port(DeviceId,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(DeviceFrameId,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(DeviceTimestamp,TypeRef.ScalarOf(ScalarKind.Text),Required:false)
    },ImageSourceOutput.Ports,new ToolPresentation(Array.Empty<FieldPresentation>()),outputDisplay:ImageSourceOutput.Display);
    public IReadOnlyList<Diagnostic> Validate(ImageSourceSettings settings)=>settings.Validate();
    public ToolCapabilities Capabilities()=>new(new[]{"halcon"});
    public IToolSession<ImageSourceSettings,ImageSourceOutput> CreateSession()=>new ImageSourceSession();
}
