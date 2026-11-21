using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>Immutable declared values; the image child resource transfers with this output.</summary>
public sealed class FrameMeanOutput : IToolOutput,IDisposable
{
    public const string Image="output.image",ImageCount="output.image-count",
        ModuleRunTime="output.module-time-ms",ModuleStatus="output.module-status";
    public const string ImageData="data",ImageWidth="width",ImageHeight="height",ImageFormat="pixel-format";
    public static TypeRef ImageType {get;}=TypeRef.RecordOf("frame-mean.image",new[] {
        new Port(ImageData,TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(ImageWidth,DataType.Integer,Storage:NumericStorage.Int32),
        new Port(ImageHeight,DataType.Integer,Storage:NumericStorage.Int32),
        new Port(ImageFormat,DataType.Integer,Nullable:true,Storage:NumericStorage.Int32)
    });
    internal static RecordValue FreezeImage(IReadOnlyImage image)=>new(ImageType,new Dictionary<string,object?> {
        [ImageData]=image,[ImageWidth]=(long)image.Width,[ImageHeight]=(long)image.Height,
        [ImageFormat]=image.Layout.Format switch { PixelLayout.Gray8 => 17301505L, PixelLayout.Rgb24 => 35127316L, _ => (long?)null }
    });
    public static IReadOnlyList<Port> Ports {get;}=Array.AsReadOnly(new[] {
        new Port(Image,ImageType,Nullable:true),
        new Port(ImageCount,DataType.Integer,Storage:NumericStorage.Int32),
        new Port(ModuleRunTime,DataType.Number,Storage:NumericStorage.Float32),
        new Port(ModuleStatus,DataType.Integer,Storage:NumericStorage.Int32)
    });
    public static IReadOnlyList<OutputDisplayNode> Display {get;}=OutputDisplayNode.Freeze(new[] {
        new OutputDisplayNode(Image,"均值图像",new[] {
            new OutputDisplayNode(ImageData,"图像数据"),new OutputDisplayNode(ImageWidth,"图像宽度"),
            new OutputDisplayNode(ImageHeight,"图像高度"),new OutputDisplayNode(ImageFormat,"图像像素格式")}),
        new OutputDisplayNode(ImageCount,"已统计图像数量"),
        new OutputDisplayNode(ModuleRunTime,"模块耗时 (ms)"),
        new OutputDisplayNode(ModuleStatus,"模块状态",displayInResultPanel:false)
    });
    private readonly InputValues _values;
    private IOutputResourceScope? _owned;
    internal FrameMeanOutput(IReadOnlyDictionary<string,object?> values,IOutputResourceScope owned)
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
