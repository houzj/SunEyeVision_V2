using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>
/// 不可变声明值。本工具不产出图像/区域资源，因此输出不持有任何需要释放的子资源。
/// </summary>
public sealed class DivideImageOutput : IToolOutput, IDisposable
{
    /// <summary>原件输出 <c>ROI</c>（ROIBOX：DetectCenterX/Y、DetectWidth/Height/Angle）。</summary>
    public const string Roi = "output.roi";
    /// <summary>原件输出 <c>DivideRect</c>（ROIBOX 数组：DivideRectCenterX/Y、Width/Height/Angle）。</summary>
    public const string Rects = "output.divide-rects";
    /// <summary>原件输出 <c>DivideCount</c>（划片个数）。</summary>
    public const string Count = "output.divide-count";
    public const string ModuleRunTime = "output.module-time-ms";
    public const string ModuleStatus = "output.module-status";

    public const string RectCenter = "center", RectWidth = "width", RectHeight = "height", RectAngle = "angle-degrees";

    public static TypeRef CenterType { get; } = TypeRef.GeometryOf(GeometryKind.Point, RoiDeclarations.ImageFrame);

    public static TypeRef RectType { get; } = TypeRef.RecordOf("divide-image.rect", new[]
    {
        new Port(RectCenter, CenterType),
        new Port(RectWidth, DataType.Number, Storage: NumericStorage.Float32),
        new Port(RectHeight, DataType.Number, Storage: NumericStorage.Float32),
        new Port(RectAngle, DataType.Number, Storage: NumericStorage.Float32)
    });

    /// <summary>分片数量由运行期参数决定，因此使用不限数量的声明。</summary>
    public static TypeRef RectsType { get; } = TypeRef.SequenceOf(RectType, CountConstraint.Any);

    /// <summary>
    /// 平台既有矩形约定：宽高按像素跨度（整幅图像即 center=(W/2,H/2)、width=W、height=H，见 Blob 整图 ROI），
    /// 角度以度为单位。原件是否采用同一度量未取证（见交接单）。
    /// </summary>
    internal static RecordValue FreezeRect(double centerX, double centerY, double width, double height)
        => new(RectType, new Dictionary<string, object?>
        {
            [RectCenter] = new PointGeometry(RoiDeclarations.ImageFrame, new(centerX, centerY)),
            [RectWidth] = (double)(float)width,
            [RectHeight] = (double)(float)height,
            [RectAngle] = 0d
        });

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Roi, RectType),
        new Port(Rects, RectsType),
        new Port(Count, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ModuleRunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(ModuleStatus, DataType.Integer, Storage: NumericStorage.Int32)
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Roi, "检测框", new[]
        {
            new OutputDisplayNode(RectCenter, "矩形中心"), new OutputDisplayNode(RectWidth, "矩形宽度"),
            new OutputDisplayNode(RectHeight, "矩形高度"), new OutputDisplayNode(RectAngle, "矩形角度")
        }),
        new OutputDisplayNode(Rects, "分片矩形", new[]
        {
            new OutputDisplayNode(RectCenter, "矩形中心"), new OutputDisplayNode(RectWidth, "矩形宽度"),
            new OutputDisplayNode(RectHeight, "矩形高度"), new OutputDisplayNode(RectAngle, "矩形角度")
        }),
        new OutputDisplayNode(Count, "分片个数"),
        new OutputDisplayNode(ModuleRunTime, "模块耗时 (ms)"),
        new OutputDisplayNode(ModuleStatus, "模块状态", displayInResultPanel: false)
    });

    private readonly InputValues _values;
    private int _disposed;

    internal DivideImageOutput(IReadOnlyDictionary<string, object?> values)
        => _values = new(Ports, values);

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _values.CopyValues();
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
