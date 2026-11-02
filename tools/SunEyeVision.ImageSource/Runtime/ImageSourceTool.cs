using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>One frozen file/frame invocation. Sequence and camera connection ownership stay in the host.</summary>
public sealed class ImageSourceTool : IToolDefinition<ImageSourceSettings,ImageSourceOutput>,IToolSettingsFields,IToolCatalogProvider,IToolResultPresentationProvider,IToolManualReferenceSources,IToolHostInputDeclaration,IToolResourceFieldDeclarations
{
    public const string Id="ImageLoad";
    public const string LocalAddress="input.local-address", CameraImage="input.camera-image", Sequence="input.sequence",
        DeviceId="input.device-id",DeviceFrameId="input.device-frame-id",DeviceTimestamp="input.device-timestamp";
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources {get;} =
        Array.AsReadOnly(new[] {new ManualReferenceSource(ImageSourceOutput.Image,new[]{ImageSourceOutput.ImageWidth}),new ManualReferenceSource(ImageSourceOutput.Image,new[]{ImageSourceOutput.ImageHeight})});
    public string ToolId=>Id;
    public HostInputDeclaration HostInputs => new("image-source", 1,
        Schema.Inputs.Select(port => new HostInputPort(port.Id, port.Type)), new[] { "local-image-read", "camera-frame" });
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.AsReadOnly(new[] {
        new ResourceFieldDeclaration("local.paths", ResourcePurpose.FileRead, ResourceReferenceCardinality.Sequence),
        new ResourceFieldDeclaration("local.selected", ResourcePurpose.FileRead, ResourceReferenceCardinality.Sequence)
    });
    public ToolCatalogDescriptor Catalog {get;}=new("图像源","本地图像或方案相机的标准图像输出","image","采集",10);
    public ToolSchema SettingsSchema=>ImageSourceFields.Schema;
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
        new Port(CameraImage,TypeRef.ResourceOf(ResourceKind.Image),Required:false),
        new Port(Sequence,TypeRef.ScalarOf(ScalarKind.Int64),Required:false,Storage:NumericStorage.Int64),
        new Port(DeviceId,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(DeviceFrameId,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(DeviceTimestamp,TypeRef.ScalarOf(ScalarKind.Text),Required:false)
    },ImageSourceOutput.Ports,new ToolPresentation(Array.Empty<FieldPresentation>()),outputDisplay:ImageSourceOutput.Display);
    public IReadOnlyList<Diagnostic> Validate(ImageSourceSettings settings)=>settings.Validate();
    public ToolCapabilities Capabilities()=>new(new[]{"halcon"});
    public IToolSession<ImageSourceSettings,ImageSourceOutput> CreateSession()=>new ImageSourceSession();
}
