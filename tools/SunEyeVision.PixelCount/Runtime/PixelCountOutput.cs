using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 像素统计的输出声明。与原厂 <c>IMVSPixelCountModuVA.xml</c> 的 Output 段对照：
/// <c>ModuRunTime</c>/<c>ModuStatus</c>/<c>Count</c>(int 数组)/<c>Ratio</c>(float 数组)/<c>ROI</c>/<c>OutputMask</c>
/// 逐项保留；<c>ROIAnnulus</c>（扇环回显）与 <c>Output Position Correction Info</c>（位置修正）本批未实现，
/// 见交接单未实现项。另加仅供结果页使用的行集合 <see cref="Rows"/>：原厂数组输出没有逐元素身份，
/// 结果页需要同一行内的区域/矩形，故沿用 Blob 工具的既有做法声明记录行。
/// </summary>
public sealed class PixelCountOutput : IToolOutput, IDisposable
{
    public const string Status = "output.module-status";
    public const string RunTime = "output.module-time-ms";
    public const string Count = "output.count";
    public const string Ratio = "output.ratio";
    public const string Roi = "output.roi";
    public const string Mask = "output.mask";
    public const string Rows = "output.rows";

    public const string RangeIndex = "range-index", ThresholdMin = "threshold-min", ThresholdMax = "threshold-max",
        RangeCount = "count", RangeRatio = "ratio", Rectangle = "rectangle", Region = "region",
        JudgmentPassed = "judgment-passed";
    public const string ImageData = "data", ImageWidth = "width", ImageHeight = "height", ImageFormat = "pixel-format";

    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;

    /// <summary>检测区域外接旋转矩形：原厂 ROI 输出的中心/宽/高/角度四项（DetectCenterX/Y、DetectWidth/Height、DetectAngle）。</summary>
    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("pixelcount.rectangle", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    /// <summary>一个阈值区间的结果行：序号、区间上下限、像素数、比率、外接矩形、像素区域与逐区间判定。</summary>
    public static TypeRef RangeType { get; } = TypeRef.RecordOf("pixelcount.range-result", new[]
    {
        new Port(RangeIndex, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ThresholdMin, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ThresholdMax, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RangeCount, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RangeRatio, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Rectangle, RectangleType),
        new Port(Region, TypeRef.ResourceOf(ResourceKind.Region)),
        new Port(JudgmentPassed, DataType.Boolean)
    });

    public static TypeRef RowsType { get; } =
        TypeRef.SequenceOf(RangeType, new CountConstraint(1, PixelCountRanges.MaximumCount));

    public static TypeRef MaskType { get; } = TypeRef.RecordOf("pixelcount.image", new[]
    {
        new Port(ImageData, TypeRef.ResourceOf(ResourceKind.Image)),
        new Port(ImageWidth, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ImageHeight, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ImageFormat, DataType.Integer, Storage: NumericStorage.Int32)
    });

    private static TypeRef CountsType { get; } =
        TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64), new CountConstraint(1, PixelCountRanges.MaximumCount));

    private static TypeRef RatiosType { get; } =
        TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64), new CountConstraint(1, PixelCountRanges.MaximumCount));

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Count, CountsType, Storage: NumericStorage.Int32),
        new Port(Ratio, RatiosType, Storage: NumericStorage.Float32),
        new Port(Roi, RectangleType, Nullable: true),
        new Port(Mask, MaskType, Nullable: true),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时 (ms)", isModuleExecutionSummary: true),
        new OutputDisplayNode(Count, "像素数"),
        new OutputDisplayNode(Ratio, "比率"),
        new OutputDisplayNode(Roi, "检测区域"),
        new OutputDisplayNode(Mask, "输出掩膜", isAuxiliaryImage: true),
        new OutputDisplayNode(Rows, "区间结果行")
    });

    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal PixelCountOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
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

    internal static RecordValue FreezeRange(int index, PixelCountRange range, long count, double ratio,
        RecordValue rectangle, IReadOnlyRegion region, bool passed)
        => new(RangeType, new Dictionary<string, object?>
        {
            [RangeIndex] = (long)index,
            [ThresholdMin] = (long)range.Minimum,
            [ThresholdMax] = (long)range.Maximum,
            [RangeCount] = count,
            [RangeRatio] = (double)(float)ratio,
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
