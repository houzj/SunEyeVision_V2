using System.Diagnostics;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 一次同步的像素统计调用；会话本身不保存任何跨调用状态，可选地被宿主顺序复用。
/// 算法链：图像 → 灰度归一 → 有效统计域（全图 ∩ ROI ∩ 掩膜）→ 逐区间取灰度带 → 计数/比率。
/// </summary>
internal sealed class PixelCountSession : IToolSession<PixelCountSettings, PixelCountOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<PixelCountOutput>> ExecuteAsync(ToolInvocation<PixelCountSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(invocation);
        try
        {
            return ValueTask.FromResult(Execute(invocation));
        }
        catch (PixelCountCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<PixelCountOutput>.Failed(error.Code, error.Message));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<PixelCountOutput>.Failed("PIXELCOUNT_CANCELLED", "像素统计已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<PixelCountOutput>.Failed("PIXELCOUNT_EXECUTION", error.Message));
        }
    }

    private static ToolOutcome<PixelCountOutput> Execute(ToolInvocation<PixelCountSettings> invocation)
    {
        var started = Stopwatch.GetTimestamp();
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new PixelCountCapabilityException("PIXELCOUNT_SETTINGS_INVALID",
                string.Join("；", errors.Select(error => error.Message)));
        var ranges = settings.ReadRanges();
        var cancellation = invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();

        var inputs = invocation.Inputs;
        if (!inputs.Contains(PixelCountTool.ImageInput))
            throw new PixelCountCapabilityException("PIXELCOUNT_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
        var image = inputs.Get<IReadOnlyImage?>(PixelCountTool.ImageInput)
            ?? throw new PixelCountCapabilityException("PIXELCOUNT_IMAGE_REQUIRED", "缺少本轮冻结的输入图像。");
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
            throw new PixelCountCapabilityException("PIXELCOUNT_PIXEL_FORMAT", error.Message);
        }
        using (gray)
        using (var domain = gray.GetDomain())
        using (var roi = OptionalRegion(inputs, PixelCountTool.RoiInput, image.Frame, width, height))
        using (var mask = OptionalMask(inputs, PixelCountTool.MaskInput, image.Frame, width, height))
        using (var effective = EffectiveRegion(domain, roi, mask))
        {
            cancellation.ThrowIfCancellationRequested();
            var total = RegionArea(effective);
            if (total <= 0)
                throw new PixelCountCapabilityException("PIXELCOUNT_ROI_EMPTY", "统计区域为空：请检查检测区域与输入掩膜。");
            // 原厂 ROI 输出回显的是检测区域本身（掩膜只裁统计范围，不改变 ROI 回显）。
            var rectangle = OrientedBounds(roi ?? effective);
            using var reduced = gray.ReduceDomain(effective);

            IOutputResourceScope? resources = null;
            try
            {
                var scope = invocation.Context.Resources.CreateScope();
                resources = scope;
                var rows = new List<object?>(ranges.Count);
                var counts = new long[ranges.Count];
                var ratios = new double[ranges.Count];
                var allPassed = true;
                for (var index = 0; index < ranges.Count; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var range = ranges[index];
                    // 原厂区间上下限来自 Integer 参数 0..255；本方按闭区间取带，端点语义属未确认项（见交接单）。
                    HOperatorSet.Threshold(reduced, out var bandTuple, range.Minimum, range.Maximum);
                    using (bandTuple)
                    using (var band = new HRegion(bandTuple))
                    {
                        var count = RegionArea(band);
                        var ratio = (double)count / total;
                        counts[index] = count;
                        ratios[index] = (double)(float)ratio;
                        var passed = Judge(settings, count, ratio);
                        allPassed &= passed;
                        var captured = HalconResourceBridge.CaptureRegion(band, PixelCountOutput.ImageFrame, image.Frame, scope);
                        rows.Add(PixelCountOutput.FreezeRange(index + 1, range, count, ratio, rectangle, captured, passed));
                    }
                }
                cancellation.ThrowIfCancellationRequested();

                IReadOnlyImage maskImage;
                HOperatorSet.RegionToBin(effective, out var binaryTuple, 255, 0, width, height);
                using (binaryTuple)
                using (var binary = new HImage(binaryTuple))
                    maskImage = HalImageSnapshotSource.Capture(binary, image.Frame, PixelCountOutput.ImageFrame, scope);

                var values = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [PixelCountOutput.Status] = 1L,
                    [PixelCountOutput.RunTime] = (double)(float)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    [PixelCountOutput.Count] = counts,
                    [PixelCountOutput.Ratio] = ratios,
                    [PixelCountOutput.Roi] = rectangle,
                    [PixelCountOutput.Mask] = PixelCountOutput.FreezeImage(maskImage),
                    [PixelCountOutput.Rows] = new SequenceValue(PixelCountOutput.RowsType, rows)
                };
                var output = new PixelCountOutput(values, scope);
                resources = null;
                var evaluated = settings.CountLimitEnabled || settings.RatioLimitEnabled;
                var judgment = evaluated ? allPassed ? BusinessJudgment.Pass : BusinessJudgment.Fail : BusinessJudgment.NotEvaluated;
                invocation.Context.Log(
                    $"[Perf] tool=PixelCount ranges={ranges.Count} area={total} elapsed={Stopwatch.GetElapsedTime(started).TotalMilliseconds:0.###}ms");
                return new(true, output, judgment, Array.Empty<Diagnostic>());
            }
            finally
            {
                resources?.Dispose();
            }
        }
    }

    /// <summary>原厂判定：数量判断与比率判断各自开关，命中范围即通过；两者都关闭时不做产品判定。</summary>
    private static bool Judge(PixelCountSettings settings, long count, double ratio)
    {
        if (settings.CountLimitEnabled && (count < settings.CountLimitLow || count > settings.CountLimitHigh)) return false;
        if (settings.RatioLimitEnabled && (ratio < settings.RatioLimitLow || ratio > settings.RatioLimitHigh)) return false;
        return true;
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
            return PixelCountOutput.FreezeRectangle(
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

    /// <summary>原厂 InputMask（IMAGE）：非零像素参与统计；尺寸或像素格式不符直接报错，不做缩放兜底。</summary>
    private static HRegion? OptionalMask(InputValues values, string id, ImageFrameAddress frame, int width, int height)
    {
        if (!values.Contains(id)) return null;
        var mask = values.Get<IReadOnlyImage?>(id);
        if (mask is null) return null;
        ResourceContractRules.ValidateImage(mask);
        if (mask.Width != width || mask.Height != height || mask.Layout.Format != PixelLayout.Gray8)
            throw new PixelCountCapabilityException("PIXELCOUNT_MASK_INVALID", "掩膜图像必须与输入图像同尺寸且为 Gray8。");
        using var native = HalconResourceBridge.CopyImage(mask);
        HOperatorSet.Threshold(native, out var maskRegion, 1, 255);
        using (maskRegion)
            return new HRegion(maskRegion);
    }
}

/// <summary>携带明确诊断码的执行期能力错误；只有这一类错误会被转成失败结果。</summary>
internal sealed class PixelCountCapabilityException(string code, string message) : Exception(message)
{
    internal string Code { get; } = code;
}
