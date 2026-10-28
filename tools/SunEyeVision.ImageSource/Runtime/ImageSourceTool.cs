using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>One frozen file/frame invocation. Sequence and camera connection ownership stay in the host.</summary>
public sealed class ImageSourceTool : IToolDefinition<ImageSourceSettings,ImageSourceOutput>,IToolSettingsFields,IToolCatalogProvider
{
    public const string Id="ImageLoad";
    public const string LocalAddress="input.local-address", CameraImage="input.camera-image", Sequence="input.sequence",
        DeviceId="input.device-id",DeviceFrameId="input.device-frame-id",DeviceTimestamp="input.device-timestamp";
    public string ToolId=>Id;
    public ToolCatalogDescriptor Catalog {get;}=new("图像源","本地图像或方案相机的标准图像输出","image","采集",10);
    public ToolSchema SettingsSchema=>ImageSourceFields.Schema;
    public InputValues ReadSettings(object settings)=>ImageSourceFields.Read(RequireSettings(settings));
    public object ApplySettings(object settings,IReadOnlyDictionary<string,object?> changes)=>ImageSourceFields.Apply(RequireSettings(settings),changes);
    private static ImageSourceSettings RequireSettings(object settings)=>settings as ImageSourceSettings??throw new ArgumentException("图像源设置类型不正确。");
    public ToolSchema Describe(ImageSourceSettings settings)=>Schema;
    // No WorkflowImageInputId: an unrelated displayed/upstream image must never become a camera frame.
    public static ToolSchema Schema {get;}=new(new[] {
        new Port(LocalAddress,TypeRef.ScalarOf(ScalarKind.Text),Required:false,EditModes:FieldEditModes.Constant),
        new Port(CameraImage,TypeRef.ResourceOf(ResourceKind.Image),Required:false),
        new Port(Sequence,TypeRef.ScalarOf(ScalarKind.Int64),Storage:NumericStorage.Int64),
        new Port(DeviceId,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(DeviceFrameId,TypeRef.ScalarOf(ScalarKind.Text),Required:false),
        new Port(DeviceTimestamp,TypeRef.ScalarOf(ScalarKind.Text),Required:false)
    },ImageSourceOutput.Ports);
    public IReadOnlyList<Diagnostic> Validate(ImageSourceSettings settings)=>settings.Validate();
    public ToolCapabilities Capabilities(ImageSourceSettings settings)=>new(new[]{"halcon"});
    public IToolSession<ImageSourceSettings,ImageSourceOutput> CreateSession()=>new ImageSourceSession();
}
