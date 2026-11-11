using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Immutable declared values and one transferred child resource owner.</summary>
public sealed class ImageSourceOutput : IToolOutput,IDisposable,IToolResultRecordOutput
{
    public const string Image="output.image",GrayImage="output.gray-image",
        SourceKind="output.source-kind",DeviceId="output.device",FileReference="output.file",FileName="output.file-name",
        Sequence="output.sequence",FrameId="output.device-frame-id",Timestamp="output.device-timestamp";
    public const string ImageData="data",ImageWidth="width",ImageHeight="height",ImageFormat="pixel-format";
    public const string GrayData="data",GrayWidth="width",GrayHeight="height",GrayFormat="pixel-format";
    public const string ModuleRunTime="output.module-time-ms";
    public const string CameraFailureCount="output.camera-failure-count";
    public const string ModuleStatus="output.module-status";
    public static TypeRef ImageType {get;}=ImageRecord("image-source.image",formatNullable:true);
    public static TypeRef GrayImageType {get;}=ImageRecord("image-source.gray-image",formatNullable:false);
    private static TypeRef ImageRecord(string name,bool formatNullable)=>TypeRef.RecordOf(name,new[] {
        new Port(ImageData,TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(ImageWidth,DataType.Integer,Storage:NumericStorage.Int32),
        new Port(ImageHeight,DataType.Integer,Storage:NumericStorage.Int32),
        new Port(ImageFormat,DataType.Integer,Nullable:formatNullable,Storage:NumericStorage.Int32)
    });
    internal static RecordValue FreezeImage(IReadOnlyImage image,TypeRef declaration)=>new(declaration,new Dictionary<string,object?> {
        [ImageData]=image,[ImageWidth]=(long)image.Width,[ImageHeight]=(long)image.Height,
        [ImageFormat]=image.Layout.Format switch { PixelLayout.Gray8 => 17301505L, PixelLayout.Rgb24 => 35127316L, _ => (long?)null }
    });
    public static IReadOnlyList<Port> Ports {get;}=Array.AsReadOnly(new[] {
        new Port(Image,ImageType),new Port(GrayImage,GrayImageType,Nullable:true),
        new Port(FileReference,TypeRef.ScalarOf(ScalarKind.Text)),new Port(FileName,TypeRef.ScalarOf(ScalarKind.Text)),
        new Port(FrameId,TypeRef.ScalarOf(ScalarKind.Text)),new Port(Sequence,TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port(SourceKind,TypeRef.ScalarOf(ScalarKind.Text)),new Port(DeviceId,TypeRef.ScalarOf(ScalarKind.Text)),new Port(Timestamp,TypeRef.ScalarOf(ScalarKind.Text)),
        new Port(ModuleRunTime,DataType.Number,Storage:NumericStorage.Float32),
        new Port(CameraFailureCount,DataType.Integer,Required:false,Storage:NumericStorage.Int32),
        new Port(ModuleStatus,DataType.Integer,Storage:NumericStorage.Int32)
    });
    public static IReadOnlyList<OutputDisplayNode> Display {get;}=OutputDisplayNode.Freeze(new[] {
        new OutputDisplayNode(Image,"图像",new[] {
            new OutputDisplayNode(ImageData,"图像数据"),new OutputDisplayNode(ImageWidth,"图像宽度"),
            new OutputDisplayNode(ImageHeight,"图像高度"),new OutputDisplayNode(ImageFormat,"图像像素格式")}),
        new OutputDisplayNode(GrayImage,"灰度图像",new[] {
            new OutputDisplayNode(GrayData,"灰度图像数据"),new OutputDisplayNode(GrayWidth,"灰度图像宽度"),
            new OutputDisplayNode(GrayHeight,"灰度图像高度"),new OutputDisplayNode(GrayFormat,"灰度图像像素格式")}),
        new OutputDisplayNode(SourceKind,"图像来源"),new OutputDisplayNode(DeviceId,"设备"),
        new OutputDisplayNode(FileReference,"当前图像路径"),new OutputDisplayNode(FileName,"当前图像名称"),new OutputDisplayNode(FrameId,"帧号"),
        new OutputDisplayNode(Sequence,"SN码"),new OutputDisplayNode(Timestamp,"设备时间"),
        new OutputDisplayNode(ModuleRunTime,"模块耗时 (ms)"),
        new OutputDisplayNode(CameraFailureCount,"相机采集失败次数"),
        new OutputDisplayNode(ModuleStatus,"模块状态",displayInResultPanel:false)
    });
    private readonly InputValues _values;
    private IOutputResourceScope? _owned;
    internal ImageSourceOutput(IReadOnlyDictionary<string,object?> values,IOutputResourceScope owned,bool availableOnly=false)
    {
        // Caller retains responsibility until construction succeeds.
        _values=availableOnly?InputValues.CaptureAvailableOutputs(Ports,values):new(Ports,values);_owned=owned;
    }
    public IReadOnlyDictionary<string,object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null,this);
        return _values.CopyValues();
    }
    public IEnumerable<string> DescribeResultRecords()
    {
        if(_values.Contains(Image))
        {
            var image = _values.Get<RecordValue>(Image);
            yield return $"Image:Width:{image.Get<long>(ImageWidth)} Height:{image.Get<long>(ImageHeight)}";
        }
        if (_values.Contains(SourceKind)&&_values.Get<string>(SourceKind) == "camera"&&_values.Contains(FrameId) && _values.Get<string>(FrameId) is { Length: > 0 } frameId)
            yield return $"FrameNum:{frameId}";
    }
    public void Dispose()=>Interlocked.Exchange(ref _owned,null)?.Dispose();
}
