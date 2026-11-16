using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.PixelCount.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>像素统计执行语义：正常输出、ROI/掩膜裁切、空区域、参数校验失败与判定。</summary>
public sealed class PixelCountExecutionTests
{
    private const int Size = 4;

    /// <summary>4×4 灰度图，像素值 0..15（行优先）。</summary>
    private static byte[] Gradient() => Enumerable.Range(0, Size * Size).Select(value => (byte)value).ToArray();

    private sealed class Context(IOutputResources resources, string nodeId = "pixel-count-node") : IToolExecutionContext
    {
        public RunIdentity Identity { get; } =
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nodeId, Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = resources;
        public void Log(string message) { }
    }

    private static InputValues Inputs(IReadOnlyImage image, IReadOnlyRegion? roi = null, IReadOnlyImage? mask = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [PixelCountTool.ImageInput] = image };
        if (roi is not null) values[PixelCountTool.RoiInput] = roi;
        if (mask is not null) values[PixelCountTool.MaskInput] = mask;
        return new(PixelCountTool.Schema.Inputs, values);
    }

    private static InputValues ImageOnly(IReadOnlyImage image) => Inputs(image);

    private static async Task<ToolOutcome<PixelCountOutput>> RunAsync(InputValues inputs,
        PixelCountSettings? settings = null, FrozenOutputResources? resources = null, Context? context = null)
    {
        resources ??= new FrozenOutputResources();
        await using var session = new PixelCountTool().CreateSession();
        return await session.ExecuteAsync(new(settings ?? new PixelCountSettings(), inputs, context ?? new Context(resources)));
    }

    private static PixelCountOutput Succeed(ToolOutcome<PixelCountOutput> outcome)
    {
        Assert.True(outcome.Success, string.Join("；", outcome.Diagnostics.Select(item => item.Message)));
        return outcome.Output!;
    }

    /// <summary>区间像素数以声明序列返回；逐元素读取，避免把声明序列当成数组。</summary>
    private static long[] Counts(IReadOnlyDictionary<string, object?> values)
        => Assert.IsType<SequenceValue>(values[PixelCountOutput.Count])
            .Select(item => Assert.IsType<long>(item)).ToArray();

    [Fact]
    public async Task Counts_and_ratios_are_reported_per_declared_range()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(ImageOnly(image),
            new PixelCountSettings { ThresholdRanges = "0-5;6-15" }, resources);
        using var output = Succeed(outcome);
        Assert.Equal(BusinessJudgment.NotEvaluated, outcome.Judgment);
        var values = output.ToValues();
        Assert.Equal(1L, Assert.IsType<long>(values[PixelCountOutput.Status]));
        Assert.Equal(new long[] { 6, 10 }, Counts(values));
        Assert.Equal(new double[] { 0.375d, 0.625d }, Assert.IsType<double[]>(values[PixelCountOutput.Ratio]));
        var rows = Assert.IsType<SequenceValue>(values[PixelCountOutput.Rows]);
        Assert.Equal(2, rows.Count);
        var first = Assert.IsType<RecordValue>(rows[0]);
        Assert.Equal(1L, first.Get<long>(PixelCountOutput.RangeIndex));
        Assert.Equal(0L, first.Get<long>(PixelCountOutput.ThresholdMin));
        Assert.Equal(5L, first.Get<long>(PixelCountOutput.ThresholdMax));
        Assert.Equal(6L, first.Get<long>(PixelCountOutput.RangeCount));
        Assert.Equal(0.375d, first.Get<double>(PixelCountOutput.RangeRatio));
        Assert.True(first.Get<bool>(PixelCountOutput.JudgmentPassed));
        Assert.NotNull(first.Get<IReadOnlyRegion>(PixelCountOutput.Region));
        var bounds = first.Get<RecordValue>(PixelCountOutput.Rectangle);
        var center = bounds.Get<RecordValue>("center");
        Assert.Equal(1.5d, center.Get<double>("x"));
        Assert.Equal(1.5d, center.Get<double>("y"));
        Assert.Equal(4d, bounds.Get<double>("width"));
        Assert.Equal(4d, bounds.Get<double>("height"));
        Assert.Equal(0d, bounds.Get<double>("angle-degrees"));
    }

    [Fact]
    public async Task Output_mask_covers_the_effective_region_and_is_gray8()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        using var output = Succeed(await RunAsync(ImageOnly(image), resources: resources));
        var mask = Assert.IsType<RecordValue>(output.ToValues()[PixelCountOutput.Mask]);
        var pixels = mask.Get<IReadOnlyImage>(PixelCountOutput.ImageData);
        Assert.Equal(PixelLayout.Gray8, pixels.Layout.Format);
        Assert.Equal(Size, pixels.Width);
        Assert.Equal(Size, pixels.Height);
        Assert.All(pixels.CopyPixels(), value => Assert.Equal(255, value));
        Assert.Equal(17301505L, mask.Get<long>(PixelCountOutput.ImageFormat));
    }

    [Fact]
    public async Task Roi_limits_the_statistical_area()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var roi = resources.CreateRegion(RoiDeclarations.ImageFrame, image.Frame, new[] { new RegionRun(0, 0, Size - 1) });
        using var output = Succeed(await RunAsync(Inputs(image, roi),
            new PixelCountSettings { ThresholdRanges = "0-5" }, resources));
        var values = output.ToValues();
        Assert.Equal(new long[] { 4 }, Counts(values));
        Assert.Equal(new double[] { 1d }, Assert.IsType<double[]>(values[PixelCountOutput.Ratio]));
        // ROI 回显取检测区域本身：单行 4 像素 → 中心 (1.5,0)、宽 4、高 1。
        var roiBounds = Assert.IsType<RecordValue>(values[PixelCountOutput.Roi]);
        var center = roiBounds.Get<RecordValue>("center");
        Assert.Equal(1.5d, center.Get<double>("x"));
        Assert.Equal(0d, center.Get<double>("y"));
        Assert.Equal(4d, roiBounds.Get<double>("width"));
        Assert.Equal(1d, roiBounds.Get<double>("height"));
    }

    [Fact]
    public async Task Mask_image_limits_the_statistical_area()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var mask = new byte[Size * Size];
        for (var row = 0; row < Size; row++)
            for (var column = 0; column < Size / 2; column++)
                mask[row * Size + column] = 255;
        var maskImage = resources.CreateGray8(Size, Size, mask);
        using var output = Succeed(await RunAsync(Inputs(image, mask: maskImage),
            new PixelCountSettings { ThresholdRanges = "0-15" }, resources));
        var values = output.ToValues();
        Assert.Equal(new long[] { 8 }, Counts(values));
        // 左半列像素值 0,1,4,5,8,9,12,13：区间 0-5 命中 4 个。
        using var second = Succeed(await RunAsync(Inputs(image, mask: maskImage),
            new PixelCountSettings { ThresholdRanges = "0-5" }, resources));
        Assert.Equal(new long[] { 4 }, Counts(second.ToValues()));
    }

    [Fact]
    public async Task Count_limit_judges_every_range_and_marks_the_module_failed()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(ImageOnly(image), new PixelCountSettings
        {
            ThresholdRanges = "0-5;6-15",
            CountLimitEnabled = true,
            CountLimitLow = 6,
            CountLimitHigh = 6
        }, resources);
        using var output = Succeed(outcome);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        var rows = Assert.IsType<SequenceValue>(output.ToValues()[PixelCountOutput.Rows]);
        Assert.True(Assert.IsType<RecordValue>(rows[0]).Get<bool>(PixelCountOutput.JudgmentPassed));
        Assert.False(Assert.IsType<RecordValue>(rows[1]).Get<bool>(PixelCountOutput.JudgmentPassed));
    }

    [Fact]
    public async Task Ratio_limit_passes_when_every_range_stays_inside_the_band()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(ImageOnly(image), new PixelCountSettings
        {
            ThresholdRanges = "0-5;6-15",
            RatioLimitEnabled = true,
            RatioLimitLow = 0.3,
            RatioLimitHigh = 0.7
        }, resources);
        using var output = Succeed(outcome);
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.All(Assert.IsType<SequenceValue>(output.ToValues()[PixelCountOutput.Rows]),
            row => Assert.True(Assert.IsType<RecordValue>(row).Get<bool>(PixelCountOutput.JudgmentPassed)));
    }

    [Fact]
    public async Task Empty_roi_fails_explicitly_instead_of_reporting_zero()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var roi = resources.CreateRegion(RoiDeclarations.ImageFrame, image.Frame, Array.Empty<RegionRun>());
        var outcome = await RunAsync(Inputs(image, roi), resources: resources);
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        Assert.Equal("PIXELCOUNT_ROI_EMPTY", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public async Task Mask_with_a_different_grid_fails_instead_of_being_resized()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var mask = resources.CreateGray8(Size + 2, Size, Enumerable.Repeat((byte)255, (Size + 2) * Size).ToArray());
        var outcome = await RunAsync(Inputs(image, mask: mask), resources: resources);
        Assert.False(outcome.Success);
        Assert.Equal("PIXELCOUNT_MASK_INVALID", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public void Missing_image_input_is_rejected_before_execution()
    {
        // 输入图像是必需端口：冻结输入时即拒绝，执行期不需要也不允许兜底成"空图像"。
        Assert.Throws<ArgumentException>(() =>
            new InputValues(PixelCountTool.Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Invalid_threshold_text_fails_before_touching_the_image()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(ImageOnly(image),
            new PixelCountSettings { ThresholdRanges = "300-400" }, resources);
        Assert.False(outcome.Success);
        Assert.Equal("PIXELCOUNT_SETTINGS_INVALID", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public async Task Disposed_output_rejects_further_reads()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var output = Succeed(await RunAsync(ImageOnly(image), resources: resources));
        output.Dispose();
        Assert.Throws<ObjectDisposedException>(() => output.ToValues());
        // 输出作用域释放后输入仍然可用，说明资源所有权没有越界。
        Assert.Equal(Size * Size, image.CopyPixels().Length);
    }
}
