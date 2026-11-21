using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>一次同步 HALCON 调用；本工具没有跨调用累计状态。</summary>
internal sealed class ColorExtractSession : IToolSession<ColorExtractSettings, ColorExtractOutput>, IReusableToolSession
{
    private int _executing, _disposed;
    private readonly object _lifetimeGate = new();

    public ValueTask<ToolOutcome<ColorExtractOutput>> ExecuteAsync(ToolInvocation<ColorExtractSettings> invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _executing, 1) != 0) throw new InvalidOperationException("同一色彩提取会话不能并发执行。");
        try
        {
            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                return ValueTask.FromResult(Execute(invocation));
            }
        }
        finally { Volatile.Write(ref _executing, 0); }
    }

    private ToolOutcome<ColorExtractOutput> Execute(ToolInvocation<ColorExtractSettings> invocation)
    {
        var cancellation = invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();
        var settings = invocation.Settings;
        var errors = settings.Validate();
        if (errors.Count != 0) return new(false, null, BusinessJudgment.NotEvaluated, errors);
        if (!invocation.Inputs.Contains(ColorExtractTool.ImageInput))
            return ToolOutcome<ColorExtractOutput>.Failed("COLOR_EXTRACT_INPUT", "缺少本轮冻结的输入图像。");
        var inputImage = invocation.Inputs.Get<IReadOnlyImage>(ColorExtractTool.ImageInput);
        ResourceContractRules.ValidateImage(inputImage);
        var layout = inputImage.Layout.Format;
        // 原厂 ROI 取色更新对 Gray8 直接弹「图像格式不正确或者不支持」；颜色抽取本身需要三个颜色通道。
        if (layout is not (PixelLayout.Rgb24 or PixelLayout.Bgr24))
            return ToolOutcome<ColorExtractOutput>.Failed("COLOR_EXTRACT_PIXEL_FORMAT",
                "图像格式不正确或者不支持：颜色抽取要求 RGB24 或 BGR24 三通道彩色图像。");
        var ranges = settings.Ranges();
        var enabled = ranges.Count(range => range.IsChecked);
        if (enabled == 0)
            return ToolOutcome<ColorExtractOutput>.Failed("COLOR_EXTRACT_RANGE_DISABLED",
                "没有勾选任何颜色区间，无法执行颜色抽取。");
        var moduleStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        IOutputResourceScope? resources = null;
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            using var frame = HalconResourceBridge.CopyImage(inputImage);
            cancellation.ThrowIfCancellationRequested();
            resources = invocation.Context.Resources.CreateScope();
            var frameAddress = new ImageFrameAddress(Guid.NewGuid(), "color-extract:" + invocation.Context.Identity.NodeId,
                invocation.Context.Identity, ColorExtractOutput.Image);
            var result = ColorExtraction.Run(frame, inputImage, frameAddress, resources, ranges,
                settings.AreaLimitEnabled, (int)settings.AreaLimitLow, (int)settings.AreaLimitHigh, cancellation);
            cancellation.ThrowIfCancellationRequested();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(moduleStarted).TotalMilliseconds;
            var values = new Dictionary<string, object?>
            {
                [ColorExtractOutput.Image] = ColorExtractOutput.FreezeImage(result.Image),
                [ColorExtractOutput.TotalArea] = (double)(float)result.Measurement.TotalArea,
                [ColorExtractOutput.Roi] = null,
                [ColorExtractOutput.ModuleStatus] = 1L,
                [ColorExtractOutput.ModuleRunTime] = (double)(float)elapsed
            };
            if (result.Measurement.HasRegion)
                values[ColorExtractOutput.Roi] = ColorExtractOutput.FreezeRoi(result.Measurement.CenterX,
                    result.Measurement.CenterY, result.Measurement.Width, result.Measurement.Height,
                    result.Measurement.AngleDegrees);
            invocation.Context.Log($"颜色抽取：图像 {inputImage.Width}x{inputImage.Height}（{layout}），" +
                $"启用区间 {enabled}/{ranges.Count} 个，面积上下限 {(settings.AreaLimitEnabled ? $"{settings.AreaLimitLow}..{settings.AreaLimitHigh}" : "未启用")}，" +
                $"提取总面积 {result.Measurement.TotalArea}，耗时 {elapsed:F3} ms");
            var output = new ColorExtractOutput(values, resources);
            resources = null;
            return ToolOutcome<ColorExtractOutput>.Completed(output);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return ToolOutcome<ColorExtractOutput>.Failed("COLOR_EXTRACT_EXECUTION", error.Message); }
        finally { resources?.Dispose(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate) Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }
}
