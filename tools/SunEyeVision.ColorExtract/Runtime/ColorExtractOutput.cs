using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>不变的声明值；图像子资源随本输出一起转移所有权。</summary>
public sealed class ColorExtractOutput : IToolOutput, IDisposable
{
    /// <summary>原厂输出 OutputImage（OutImage/OutImageWidth/OutImageHeight/OutImagePixelFormat）。</summary>
    public const string Image = "output.image";
    /// <summary>原厂输出 TotalArea（float）。</summary>
    public const string TotalArea = "output.total-area";
    /// <summary>原厂输出 ROI（ROIBOX：DetectCenterX/Y、DetectWidth/Height/Angle）。</summary>
    public const string Roi = "output.roi";
    public const string ModuleRunTime = "output.module-time-ms";
    public const string ModuleStatus = "output.module-status";

    public const string ImageData = "data", ImageWidth = "width", ImageHeight = "height", ImageFormat = "pixel-format";
    public const string RoiCenter = "center", RoiWidth = "width", RoiHeight = "height", RoiAngle = "angle-degrees";

    /// <summary>与原厂 DetectRectCenterPoint 一致：外接矩形中心使用图像像素坐标下的点。</summary>
    public static TypeRef CenterType { get; } =
        TypeRef.GeometryOf(GeometryKind.Point, RoiDeclarations.ImageFrame);

    public static TypeRef ImageType { get; } = TypeRef.RecordOf("color-extract.image", new[]
    {
        new Port(ImageData, TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(ImageWidth, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ImageHeight, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ImageFormat, DataType.Integer, Nullable: true, Storage: NumericStorage.Int32)
    });

    public static TypeRef RoiType { get; } = TypeRef.RecordOf("color-extract.roi", new[]
    {
        new Port(RoiCenter, CenterType),
        new Port(RoiWidth, DataType.Number, Storage: NumericStorage.Float32),
        new Port(RoiHeight, DataType.Number, Storage: NumericStorage.Float32),
        new Port(RoiAngle, DataType.Number, Storage: NumericStorage.Float32)
    });

    internal static RecordValue FreezeImage(IReadOnlyImage image) => new(ImageType, new Dictionary<string, object?>
    {
        [ImageData] = image,
        [ImageWidth] = (long)image.Width,
        [ImageHeight] = (long)image.Height,
        [ImageFormat] = image.Layout.Format switch
        {
            PixelLayout.Gray8 => 17301505L,
            PixelLayout.Rgb24 => 35127316L,
            _ => (long?)null
        }
    });

    internal static RecordValue FreezeRoi(double centerX, double centerY, double width, double height, double angleDegrees)
        => new(RoiType, new Dictionary<string, object?>
        {
            [RoiCenter] = new PointGeometry(RoiDeclarations.ImageFrame, new(centerX, centerY)),
            [RoiWidth] = (double)(float)width,
            [RoiHeight] = (double)(float)height,
            [RoiAngle] = (double)(float)angleDegrees
        });

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Image, ImageType),
        new Port(TotalArea, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Roi, RoiType, Nullable: true),
        new Port(ModuleRunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(ModuleStatus, DataType.Integer, Storage: NumericStorage.Int32)
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Image, "提取图像", new[]
        {
            new OutputDisplayNode(ImageData, "图像数据"), new OutputDisplayNode(ImageWidth, "图像宽度"),
            new OutputDisplayNode(ImageHeight, "图像高度"), new OutputDisplayNode(ImageFormat, "图像像素格式")
        }),
        new OutputDisplayNode(TotalArea, "提取总面积"),
        new OutputDisplayNode(Roi, "提取区域外接矩形", new[]
        {
            new OutputDisplayNode(RoiCenter, "矩形中心"), new OutputDisplayNode(RoiWidth, "矩形宽度"),
            new OutputDisplayNode(RoiHeight, "矩形高度"), new OutputDisplayNode(RoiAngle, "矩形角度")
        }),
        new OutputDisplayNode(ModuleRunTime, "模块耗时 (ms)"),
        new OutputDisplayNode(ModuleStatus, "模块状态", displayInResultPanel: false)
    });

    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal ColorExtractOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
    {
        // 构造成功前由调用方负责释放资源。
        _values = new(Ports, values);
        _owned = owned;
    }

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null, this);
        return _values.CopyValues();
    }

    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();
}
