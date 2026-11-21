using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量的输出声明。与原件 <c>Output</c> 段逐项对照
/// （docs/07-工具逆向/tools/imvsintensitymeasuremodu/parameters.json:735-1030）：
/// <c>ModuRunTime</c>(float)、<c>ModuStatus</c>(int)、<c>LumMin</c>(int)、<c>LumMax</c>(int)、
/// <c>LumMean</c>(float)、<c>LumStd</c>(float)、<c>LumHistogram</c>(int 数组)、<c>HistContrast</c>(float)、
/// <c>ROI</c>(ROIBOX)、<c>OutputMask</c>(IMAGE) 保留；<c>ROIAnnulus</c>（扇环回显）与
/// <c>Output Position Correction Info</c>（位置修正，原件 Visible=false）本批未实现，见交接单未实现项。
/// <c>ROI</c> 的成员名为 <c>DetectCenterX/Y</c>、<c>DetectWidth/Height</c>、<c>DetectAngle</c>
/// （parameters.json:848-892），本方按同一语义声明为矩形记录。
/// 另加仅供结果页使用的单行集合 <see cref="Rows"/>：原厂输出是扁平通道，没有逐行身份，
/// 而结果显示需要同一行内的矩形与区域，故沿用像素统计工具的既有做法。
/// </summary>
public sealed class IntensityMeasureOutput : IToolOutput, IDisposable
{
    public const string Status = "output.module-status";
    public const string RunTime = "output.module-time-ms";
    public const string LumMin = "output.lum-min";
    public const string LumMax = "output.lum-max";
    public const string LumMean = "output.lum-mean";
    public const string LumStd = "output.lum-std";
    public const string HistContrast = "output.hist-contrast";
    public const string Histogram = "output.lum-histogram";
    public const string Roi = "output.roi";
    public const string Mask = "output.mask";
    public const string Rows = "output.rows";

    public const string RowIndex = "row-index", Rectangle = "rectangle", Region = "region",
        JudgmentPassed = "judgment-passed";
    public const string ImageData = "data", ImageWidth = "width", ImageHeight = "height", ImageFormat = "pixel-format";

    /// <summary>原件灰度直方图固定 256 级（Gray8 量化），见 <c>LumHistogram</c> 的 int 数组声明。</summary>
    public const int HistogramBinCount = 256;

    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;

    /// <summary>检测区域外接旋转矩形：原件 ROI 输出的中心/宽/高/角度四项。</summary>
    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("intensity-measure.rectangle", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    /// <summary>单行结果：五项统计、外接矩形、亮度区域与判定。</summary>
    public static TypeRef RowType { get; } = TypeRef.RecordOf("intensity-measure.row", new[]
    {
        new Port(RowIndex, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(LumMin, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(LumMax, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(LumMean, DataType.Number),
        new Port(LumStd, DataType.Number),
        new Port(HistContrast, DataType.Number),
        new Port(Rectangle, RectangleType),
        new Port(Region, TypeRef.ResourceOf(ResourceKind.Region)),
        new Port(JudgmentPassed, DataType.Boolean)
    });

    public static TypeRef RowsType { get; } = TypeRef.SequenceOf(RowType, new CountConstraint(1, 1));

    public static TypeRef MaskType { get; } = TypeRef.RecordOf("intensity-measure.image", new[]
    {
        new Port(ImageData, TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(ImageWidth, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ImageHeight, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ImageFormat, DataType.Integer, Storage: NumericStorage.Int32)
    });

    public static TypeRef HistogramType { get; } = TypeRef.SequenceOf(
        TypeRef.ScalarOf(ScalarKind.Int64), new CountConstraint(HistogramBinCount, HistogramBinCount));

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(LumMin, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(LumMax, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(LumMean, DataType.Number),
        new Port(LumStd, DataType.Number),
        new Port(HistContrast, DataType.Number),
        new Port(Histogram, HistogramType),
        new Port(Roi, RectangleType, Nullable: true),
        new Port(Mask, MaskType, Nullable: true),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时 (ms)", isModuleExecutionSummary: true),
        new OutputDisplayNode(LumMin, "亮度最小值"),
        new OutputDisplayNode(LumMax, "亮度最大值"),
        new OutputDisplayNode(LumMean, "亮度均值"),
        new OutputDisplayNode(LumStd, "亮度标准差"),
        new OutputDisplayNode(HistContrast, "直方图对比度"),
        new OutputDisplayNode(Histogram, "灰度直方图"),
        new OutputDisplayNode(Roi, "检测区域"),
        new OutputDisplayNode(Mask, "输出掩膜", isAuxiliaryImage: true),
        new OutputDisplayNode(Rows, "亮度区域行")
    });

    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal IntensityMeasureOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
    {
        ArgumentNullException.ThrowIfNull(values);
        // 调用方在构造成功前保留责任：构造失败时不吞掉已建好的资源作用域。
        _values = new InputValues(Ports, values);
        _owned = owned ?? throw new ArgumentNullException(nameof(owned));
    }

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null, this);
        return _values.CopyValues();
    }

    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();

    internal static RecordValue FreezeRectangle(double centerX, double centerY, double width, double height, double angleDegrees)
        => new(RectangleType, new Dictionary<string, object?>
        {
            ["center"] = new RecordValue(FixtureDeclarations.Point,
                new Dictionary<string, object?> { ["x"] = (double)(float)centerX, ["y"] = (double)(float)centerY }),
            ["width"] = (double)(float)width,
            ["height"] = (double)(float)height,
            ["angle-degrees"] = (double)(float)angleDegrees
        });

    internal static RecordValue FreezeRow(long min, long max, double mean, double deviation, double contrast,
        RecordValue rectangle, IReadOnlyRegion region, bool passed)
        => new(RowType, new Dictionary<string, object?>
        {
            [RowIndex] = 1L,
            [LumMin] = min,
            [LumMax] = max,
            [LumMean] = mean,
            [LumStd] = deviation,
            [HistContrast] = contrast,
            [Rectangle] = rectangle,
            [Region] = region,
            [JudgmentPassed] = passed
        });

    internal static RecordValue FreezeImage(IReadOnlyImage image)
        => new(MaskType, new Dictionary<string, object?>
        {
            [ImageData] = image,
            [ImageWidth] = (long)image.Width,
            [ImageHeight] = (long)image.Height,
            // 输出掩膜只由 RegionToBin 产生，格式固定 Gray8；其它格式说明链路有误，直接报错而不是留空。
            [ImageFormat] = image.Layout.Format switch
            {
                PixelLayout.Gray8 => 17301505L,
                _ => throw new InvalidOperationException("输出掩膜必须是 Gray8 图像。")
            }
        });
}
