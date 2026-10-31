using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>One frozen file/frame invocation. Sequence and camera connection ownership stay in the host.</summary>
public sealed class ImageSourceTool : IToolDefinition<ImageSourceSettings,ImageSourceOutput>,IToolSettingsFields,IToolCatalogProvider,IToolResultPresentationProvider
{
    public const string Id="ImageLoad";
    public const string LocalAddress="input.local-address", CameraImage="input.camera-image", Sequence="input.sequence",
        DeviceId="input.device-id",DeviceFrameId="input.device-frame-id",DeviceTimestamp="input.device-timestamp";
    public string ToolId=>Id;
    public ToolCatalogDescriptor Catalog {get;}=new("图像源","本地图像或方案相机的标准图像输出","image","采集",10);
    public ToolSchema SettingsSchema=>ImageSourceFields.Schema;
    public ToolSchema ResultSchema {get;}=new(Array.Empty<Port>(),ImageSourceOutput.Ports,outputDisplay:ImageSourceOutput.OutputDisplay);
    public ResultProjectionDescriptor ResultProjection {get;}=new(ResultProjectionKind.PropertyList,ResultCardinality.One,new[] {
        new ResultColumnDescriptor("image","图像",new(ImageSourceOutput.Image)),
        new ResultColumnDescriptor("gray-image","灰度图像",new(ImageSourceOutput.GrayImage)),
        new ResultColumnDescriptor("width","图像宽度",new(ImageSourceOutput.Width)),
        new ResultColumnDescriptor("height","图像高度",new(ImageSourceOutput.Height)),
        new ResultColumnDescriptor("pixel-format","图像像素格式",new(ImageSourceOutput.PixelFormat)),
        new ResultColumnDescriptor("source","图像来源",new(ImageSourceOutput.SourceKind)),
        new ResultColumnDescriptor("device","设备",new(ImageSourceOutput.DeviceId)),
        new ResultColumnDescriptor("file","文件引用",new(ImageSourceOutput.FileReference)),
        new ResultColumnDescriptor("sequence","序列号",new(ImageSourceOutput.Sequence)),
        new ResultColumnDescriptor("frame","设备帧号",new(ImageSourceOutput.FrameId)),
        new ResultColumnDescriptor("timestamp","设备时间",new(ImageSourceOutput.Timestamp))
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
    },ImageSourceOutput.Ports,new ToolPresentation(Array.Empty<FieldPresentation>()),outputDisplay:ImageSourceOutput.OutputDisplay);
    public IReadOnlyList<Diagnostic> Validate(ImageSourceSettings settings)=>settings.Validate();
    public ToolCapabilities Capabilities()=>new(new[]{"halcon"});
    public IToolSession<ImageSourceSettings,ImageSourceOutput> CreateSession()=>new ImageSourceSession();
}
