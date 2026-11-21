using System.Diagnostics;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 一次同步的亮度统计调用；会话本身不保存跨调用状态，可选地被宿主顺序复用。
/// 算法链：图像 → 灰度归一 → 有效统计域（全图 ∩ ROI ∩ 掩膜）→ 最小值/最大值/均值/标准差 →
/// 256 级直方图 → 直方图对比度 → 逐判据判定。
/// 原件算法体（<c>Process</c>/<c>ProcessAlg</c> 与 native 统计入口）未取到源码级证据，
/// 本实现按原件已证实的结果通道语义（LumMin/LumMax/LumMean/LumStd/LumHistogram/HistContrast，
/// parameters.json:757-818）以 HALCON 标准统计算子实现；逐算子对应关系见交接单证据表。
/// </summary>
internal sealed class IntensityMeasureSession : IToolSession<IntensityMeasureSettings, IntensityMeasureOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<IntensityMeasureOutput>> ExecuteAsync(ToolInvocation<IntensityMeasureSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(invocation);
        try
        {
            return ValueTask.FromResult(Execute(invocation));
        }
        catch (IntensityMeasureCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<IntensityMeasureOutput>.Failed(error.Code, error.Message));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<IntensityMeasureOutput>.Failed("INTENSITYMEASURE_CANCELLED", "亮度测量已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<IntensityMeasureOutput>.Failed("INTENSITYMEASURE_EXECUTION", error.Message));
        }
    }

    private static ToolOutcome<IntensityMeasureOutput> Execute(ToolInvocation<IntensityMeasureSettings> invocation)
    {
        var started = Stopwatch.GetTimestamp();
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_SETTINGS_INVALID",
                string.Join("；", errors.Select(error => error.Message)));
        var cancellation = invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();

        var inputs = invocation.Inputs;
        if (!inputs.Contains(IntensityMeasureTool.ImageInput))
            throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        var image = inputs.Get<IReadOnlyImage?>(IntensityMeasureTool.ImageInput)
            ?? throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        ResourceContractRules.ValidateImage(image);

        using var native = HalconResourceBridge.CopyImage(image);
        HOperatorSet.GetImageSize(native, out var widthTuple, out var heightTuple);
        int width, height;
        using (widthTuple) using (heightTuple) { width = widthTuple.I; height = heightTuple.I; }
        cancellation.ThrowIfCancellationRequested();

        HImage gray;
        try { gray = HalconImageAdmission.Gray(native); }
        catch (NotSupportedException error)
        {
            throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_PIXEL_FORMAT", error.Message);
        }
        using (gray)
        using (var domain = gray.GetDomain())
        using (var roi = OptionalRegion(inputs, IntensityMeasureTool.RoiInput, image.Frame, width, height))
        using (var mask = OptionalMask(inputs, IntensityMeasureTool.MaskInput, image.Frame, width, height))
        using (var effective = EffectiveRegion(domain, roi, mask))
        {
            cancellation.ThrowIfCancellationRequested();
            var area = RegionArea(effective);
            if (area <= 0)
                throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_ROI_EMPTY",
                    "统计区域为空：请检查检测区域与输入掩膜。");
            // 原件 LumMin/LumMax 为 int、LumHistogram 为 256 级 int 数组：灰度值必须落在 8 位量程内，
            // 非 byte 图像直接报错，不做缩放或取整兜底。
            if (gray.GetImageType() != "byte")
                throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_PIXEL_FORMAT",
                    "亮度测量只接受 8 位灰度图像（原件输出为整数灰度与 256 级直方图）。");

            using var reduced = gray.ReduceDomain(effective);
            HOperatorSet.MinMaxGray(effective, reduced, 0, out var minTuple, out var maxTuple, out var rangeTuple);
            int minimum, maximum;
            using (minTuple) using (maxTuple) using (rangeTuple)
            {
                if (minTuple.Length == 0 || maxTuple.Length == 0)
                    throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_ROI_EMPTY", "统计区域内没有可统计的灰度值。");
                minimum = (int)Math.Round(minTuple.D);
                maximum = (int)Math.Round(maxTuple.D);
            }
            cancellation.ThrowIfCancellationRequested();

            HOperatorSet.Intensity(effective, reduced, out var meanTuple, out var deviationTuple);
            double mean, deviation;
            using (meanTuple) using (deviationTuple)
            {
                if (meanTuple.Length == 0 || deviationTuple.Length == 0)
                    throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_ROI_EMPTY", "统计区域内没有可统计的灰度值。");
                mean = (double)(float)meanTuple.D;
                deviation = (double)(float)deviationTuple.D;
            }
            cancellation.ThrowIfCancellationRequested();

            HOperatorSet.GrayHisto(effective, reduced, out var absoluteTuple, out var relativeTuple);
            long[] histogram;
            using (absoluteTuple) using (relativeTuple)
            {
                if (absoluteTuple.Length != IntensityMeasureOutput.HistogramBinCount)
                    throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_HISTOGRAM",
                        "灰度直方图级数不是 256：" + absoluteTuple.Length);
                histogram = new long[IntensityMeasureOutput.HistogramBinCount];
                for (var bin = 0; bin < histogram.Length; bin++) histogram[bin] = absoluteTuple[bin].L;
            }
            // 原件 HistContrast 只有一个 float 通道，没有公式证据（未确认）。
            // 本方按直方图非空量程（最大灰度 − 最小灰度）实现，量程与原件 FloatBettween 的 0..255 一致。
            var contrast = (double)(float)(maximum - minimum);
            cancellation.ThrowIfCancellationRequested();

            var judgment = Judge(settings, minimum, maximum, mean, deviation, contrast);

            IOutputResourceScope? resources = null;
            try
            {
                var scope = invocation.Context.Resources.CreateScope();
                resources = scope;
                // 原厂 ROI 输出回显的是检测区域本身（掩膜只裁统计范围，不改变 ROI 回显）。
                var rectangle = OrientedBounds(roi ?? effective);
                var region = HalconResourceBridge.CaptureRegion(effective, IntensityMeasureOutput.ImageFrame,
                    image.Frame, scope);
                HOperatorSet.RegionToBin(effective, out var binaryTuple, 255, 0, width, height);
                IReadOnlyImage maskImage;
                using (binaryTuple)
                using (var binary = new HImage(binaryTuple))
                    maskImage = HalImageSnapshotSource.Capture(binary, image.Frame, IntensityMeasureOutput.ImageFrame, scope);

                var values = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [IntensityMeasureOutput.Status] = 1L,
                    [IntensityMeasureOutput.RunTime] = (double)(float)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    [IntensityMeasureOutput.LumMin] = (long)minimum,
                    [IntensityMeasureOutput.LumMax] = (long)maximum,
                    [IntensityMeasureOutput.LumMean] = mean,
                    [IntensityMeasureOutput.LumStd] = deviation,
                    [IntensityMeasureOutput.HistContrast] = contrast,
                    [IntensityMeasureOutput.Histogram] = new SequenceValue(IntensityMeasureOutput.HistogramType,
                        histogram.Select(value => (object?)value)),
                    [IntensityMeasureOutput.Roi] = rectangle,
                    [IntensityMeasureOutput.Mask] = IntensityMeasureOutput.FreezeImage(maskImage),
                    [IntensityMeasureOutput.Rows] = new SequenceValue(IntensityMeasureOutput.RowsType, new object?[]
                    {
                        IntensityMeasureOutput.FreezeRow(minimum, maximum, mean, deviation, contrast,
                            rectangle, region, judgment != BusinessJudgment.Fail)
                    })
                };
                var output = new IntensityMeasureOutput(values, scope);
                resources = null;
                invocation.Context.Log(
                    $"[Perf] tool=IntensityMeasure area={area} min={minimum} max={maximum} elapsed={Stopwatch.GetElapsedTime(started).TotalMilliseconds:0.###}ms");
                return new(true, output, judgment, Array.Empty<Diagnostic>());
            }
            finally
            {
                resources?.Dispose();
            }
        }
    }

    /// <summary>
    /// 原件判定：五类判据各自开关，命中范围即通过；全部关闭时不做产品判定。
    /// 逐判据的开关/上下限来自 <see cref="IntensityMeasureSettings"/>（parameters.json:12-278）。
    /// </summary>
    private static BusinessJudgment Judge(IntensityMeasureSettings settings, int minimum, int maximum,
        double mean, double deviation, double contrast)
    {
        var evaluated = false;
        var passed = true;
        void Integer(bool enabled, long value, long low, long high)
        {
            if (!enabled) return;
            evaluated = true;
            passed &= value >= low && value <= high;
        }
        void Number(bool enabled, double value, double low, double high)
        {
            if (!enabled) return;
            evaluated = true;
            passed &= value >= low && value <= high;
        }
        Integer(settings.MaxValueLimitEnabled, maximum, settings.MaxValueLimitLow, settings.MaxValueLimitHigh);
        Integer(settings.MinValueLimitEnabled, minimum, settings.MinValueLimitLow, settings.MinValueLimitHigh);
        Number(settings.MeanLimitEnabled, mean, settings.MeanLimitLow, settings.MeanLimitHigh);
        Number(settings.StdLimitEnabled, deviation, settings.StdLimitLow, settings.StdLimitHigh);
        Number(settings.ContrastLimitEnabled, contrast, settings.ContrastLimitLow, settings.ContrastLimitHigh);
        return evaluated ? passed ? BusinessJudgment.Pass : BusinessJudgment.Fail : BusinessJudgment.NotEvaluated;
    }

    private static long RegionArea(HRegion region)
    {
        HOperatorSet.AreaCenter(region, out var area, out var row, out var column);
        using (area) using (row) using (column)
            return area.Length == 0 ? 0 : (long)Math.Round(area.D);
    }

    /// <summary>检测区域外接旋转矩形（HALCON 行/列与屏幕 x/y 互换，角度换算为度）。</summary>
    private static RecordValue OrientedBounds(HRegion region)
    {
        HOperatorSet.SmallestRectangle2(region, out var row, out var column, out var phi, out var length1, out var length2);
        using (row) using (column) using (phi) using (length1) using (length2)
        {
            var angle = phi.Length == 0 ? 0d : phi.D * 180d / Math.PI;
            // HALCON 的半长是像素中心间距离：像素跨度要按 +1 换算，否则 4 像素宽的检测区域会报 3。
            return IntensityMeasureOutput.FreezeRectangle(
                column.Length == 0 ? 0d : column.D, row.Length == 0 ? 0d : row.D,
                length1.Length == 0 ? 0d : length1.D * 2d + 1d, length2.Length == 0 ? 0d : length2.D * 2d + 1d, angle);
        }
    }

    private static HRegion EffectiveRegion(HRegion domain, HRegion? roi, HRegion? mask)
    {
        HRegion current = roi is null ? domain.Clone() : domain.Intersection(roi);
        if (mask is null) return current;
        using (current)
            return current.Intersection(mask);
    }

    private static HRegion? OptionalRegion(InputValues values, string id, ImageFrameAddress frame, int width, int height)
    {
        if (!values.Contains(id)) return null;
        var region = values.Get<IReadOnlyRegion?>(id);
        return region is null ? null : HalconResourceBridge.CopyRegion(region, frame, width, height);
    }

    /// <summary>原件 InputMask（IMAGE）：非零像素参与统计；尺寸或像素格式不符直接报错，不做缩放兜底。</summary>
    private static HRegion? OptionalMask(InputValues values, string id, ImageFrameAddress frame, int width, int height)
    {
        if (!values.Contains(id)) return null;
        var mask = values.Get<IReadOnlyImage?>(id);
        if (mask is null) return null;
        ResourceContractRules.ValidateImage(mask);
        if (mask.Width != width || mask.Height != height || mask.Layout.Format != PixelLayout.Gray8)
            throw new IntensityMeasureCapabilityException("INTENSITYMEASURE_MASK_INVALID", "掩膜图像必须与输入图像同尺寸且为 Gray8。");
        using var native = HalconResourceBridge.CopyImage(mask);
        HOperatorSet.Threshold(native, out var maskRegion, 1, 255);
        using (maskRegion)
            return new HRegion(maskRegion);
    }
}

/// <summary>携带明确诊断码的执行期能力错误；只有这一类错误会被转成失败结果。</summary>
internal sealed class IntensityMeasureCapabilityException(string code, string message) : Exception(message)
{
    internal string Code { get; } = code;
}
