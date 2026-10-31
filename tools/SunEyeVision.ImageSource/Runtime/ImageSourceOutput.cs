using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Immutable declared values and one transferred child resource owner.</summary>
public sealed class ImageSourceOutput : IToolOutput,IDisposable
{
    public const string Image="output.image",GrayImage="output.gray-image",
        SourceKind="output.source-kind",DeviceId="output.device",FileReference="output.file",FileName="output.file-name",
        Sequence="output.sequence",FrameId="output.device-frame-id",Timestamp="output.device-timestamp";
    public const string ImageData="data",ImageWidth="width",ImageHeight="height",ImageFormat="pixel-format";
    public const string GrayData="data",GrayWidth="width",GrayHeight="height",GrayFormat="pixel-format";
    public static TypeRef ImageType {get;}=TypeRef.RecordOf("image-source.image",new[] {
        new Port(ImageData,TypeRef.ResourceOf(ResourceKind.Image)),new Port(ImageWidth,TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port(ImageHeight,TypeRef.ScalarOf(ScalarKind.Int64)),new Port(ImageFormat,TypeRef.ScalarOf(ScalarKind.Text))
    });
    public static TypeRef GrayImageType {get;}=TypeRef.RecordOf("image-source.gray-image",new[] {
        new Port(GrayData,TypeRef.ResourceOf(ResourceKind.Image)),new Port(GrayWidth,TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port(GrayHeight,TypeRef.ScalarOf(ScalarKind.Int64)),new Port(GrayFormat,TypeRef.ScalarOf(ScalarKind.Text))
    });
    public static IReadOnlyList<Port> Ports {get;}=Array.AsReadOnly(new[] {
        new Port(Image,ImageType),new Port(GrayImage,GrayImageType),
        new Port(FileReference,TypeRef.ScalarOf(ScalarKind.Text)),new Port(FileName,TypeRef.ScalarOf(ScalarKind.Text)),
        new Port(FrameId,TypeRef.ScalarOf(ScalarKind.Text)),new Port(Sequence,TypeRef.ScalarOf(ScalarKind.Int64)),
        new Port(SourceKind,TypeRef.ScalarOf(ScalarKind.Text)),new Port(DeviceId,TypeRef.ScalarOf(ScalarKind.Text)),new Port(Timestamp,TypeRef.ScalarOf(ScalarKind.Text))
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
        new OutputDisplayNode(Sequence,"SN码"),new OutputDisplayNode(Timestamp,"设备时间")
    });
    private readonly InputValues _values;
    private IOutputResourceScope? _owned;
    internal ImageSourceOutput(IReadOnlyDictionary<string,object?> values,IOutputResourceScope owned)
    {
        // Caller retains responsibility until construction succeeds.
        _values=new(Ports,values);_owned=owned;
    }
    public IReadOnlyDictionary<string,object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null,this);
        return _values.CopyValues();
    }
    public void Dispose()=>Interlocked.Exchange(ref _owned,null)?.Dispose();
}
