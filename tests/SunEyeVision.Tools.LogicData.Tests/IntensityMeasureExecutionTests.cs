using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.IntensityMeasure.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>亮度测量执行语义：五项统计、直方图、ROI/掩膜裁切、空区域、参数校验失败与判定。</summary>
public sealed class IntensityMeasureExecutionTests
{
    private const int Size = 4;

    /// <summary>4×4 灰度图，像素值 0..15（行优先）。</summary>
    private static byte[] Gradient() => Enumerable.Range(0, Size * Size).Select(value => (byte)value).ToArray();

    private sealed class Context(IOutputResources resources, string nodeId = "intensity-measure-node") : IToolExecutionContext
    {
        public RunIdentity Identity { get; } =
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nodeId, Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = resources;
        public void Log(string message) { }
    }

    private static InputValues Inputs(IReadOnlyImage image, IReadOnlyRegion? roi = null, IReadOnlyImage? mask = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [IntensityMeasureTool.ImageInput] = image };
        if (roi is not null) values[IntensityMeasureTool.RoiInput] = roi;
        if (mask is not null) values[IntensityMeasureTool.MaskInput] = mask;
        return new(IntensityMeasureTool.Schema.Inputs, values);
    }

    private static async Task<ToolOutcome<IntensityMeasureOutput>> RunAsync(InputValues inputs,
        IntensityMeasureSettings? settings = null, FrozenOutputResources? resources = null)
    {
        resources ??= new FrozenOutputResources();
        await using var session = new IntensityMeasureTool().CreateSession();
        return await session.ExecuteAsync(new(settings ?? new IntensityMeasureSettings(), inputs, new Context(resources)));
    }

    private static IntensityMeasureOutput Succeed(ToolOutcome<IntensityMeasureOutput> outcome)
    {
        Assert.True(outcome.Success, string.Join("；", outcome.Diagnostics.Select(item => item.Message)));
        return outcome.Output!;
    }

    private static long[] Histogram(IReadOnlyDictionary<string, object?> values)
        => Assert.IsType<SequenceValue>(values[IntensityMeasureOutput.Histogram])
            .Select(item => Assert.IsType<long>(item)).ToArray();

    [Fact]
    public async Task Five_statistics_and_the_256_bin_histogram_describe_the_whole_image()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(Inputs(image), resources: resources);
        using var output = Succeed(outcome);
        Assert.Equal(BusinessJudgment.NotEvaluated, outcome.Judgment);
        var values = output.ToValues();
        Assert.Equal(1L, Assert.IsType<long>(values[IntensityMeasureOutput.Status]));
        Assert.Equal(0L, Assert.IsType<long>(values[IntensityMeasureOutput.LumMin]));
        Assert.Equal(15L, Assert.IsType<long>(values[IntensityMeasureOutput.LumMax]));
        Assert.Equal(7.5d, Assert.IsType<double>(values[IntensityMeasureOutput.LumMean]), 6);
        // 0..15 的总体标准差 = sqrt(21.25)。
        Assert.Equal(Math.Sqrt(21.25d), Assert.IsType<double>(values[IntensityMeasureOutput.LumStd]), 5);
        Assert.Equal(15d, Assert.IsType<double>(values[IntensityMeasureOutput.HistContrast]), 6);
        var histogram = Histogram(values);
        Assert.Equal(IntensityMeasureOutput.HistogramBinCount, histogram.Length);
        Assert.Equal(Size * Size, histogram.Sum());
        Assert.All(histogram.Take(16), count => Assert.Equal(1L, count));
        Assert.All(histogram.Skip(16), count => Assert.Equal(0L, count));
    }

    [Fact]
    public async Task Result_rows_carry_the_geometry_and_the_region_that_were_measured()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        using var output = Succeed(await RunAsync(Inputs(image), resources: resources));
        var values = output.ToValues();
        var rows = Assert.IsType<SequenceValue>(values[IntensityMeasureOutput.Rows]);
        var row = Assert.IsType<RecordValue>(Assert.Single(rows));
        Assert.Equal(1L, row.Get<long>(IntensityMeasureOutput.RowIndex));
        Assert.Equal(0L, row.Get<long>(IntensityMeasureOutput.LumMin));
        Assert.Equal(15L, row.Get<long>(IntensityMeasureOutput.LumMax));
        Assert.Equal(7.5d, row.Get<double>(IntensityMeasureOutput.LumMean), 6);
        Assert.True(row.Get<bool>(IntensityMeasureOutput.JudgmentPassed));
        Assert.NotNull(row.Get<IReadOnlyRegion>(IntensityMeasureOutput.Region));
        var bounds = row.Get<RecordValue>(IntensityMeasureOutput.Rectangle);
        var center = bounds.Get<RecordValue>("center");
        Assert.Equal(1.5d, center.Get<double>("x"));
        Assert.Equal(1.5d, center.Get<double>("y"));
        Assert.Equal(4d, bounds.Get<double>("width"));
        Assert.Equal(4d, bounds.Get<double>("height"));
        Assert.Equal(0d, bounds.Get<double>("angle-degrees"));
        // 原厂 ROI 输出与结果行矩形是同一份回显（同一矩形声明与取值）。
        var roiEcho = Assert.IsType<RecordValue>(values[IntensityMeasureOutput.Roi]);
        Assert.Equal(roiEcho.Get<RecordValue>("center").Get<double>("x"), center.Get<double>("x"));
        Assert.Equal(roiEcho.Get<double>("width"), bounds.Get<double>("width"));
    }

    [Fact]
    public async Task Output_mask_covers_the_measured_region_and_is_gray8()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        using var output = Succeed(await RunAsync(Inputs(image), resources: resources));
        var mask = Assert.IsType<RecordValue>(output.ToValues()[IntensityMeasureOutput.Mask]);
        var pixels = mask.Get<IReadOnlyImage>(IntensityMeasureOutput.ImageData);
        Assert.Equal(PixelLayout.Gray8, pixels.Layout.Format);
        Assert.Equal(Size, pixels.Width);
        Assert.Equal(Size, pixels.Height);
        Assert.All(pixels.CopyPixels(), value => Assert.Equal(255, value));
        Assert.Equal(17301505L, mask.Get<long>(IntensityMeasureOutput.ImageFormat));
    }

    [Fact]
    public async Task Roi_limits_the_statistical_area()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var roi = resources.CreateRegion(RoiDeclarations.ImageFrame, image.Frame, new[] { new RegionRun(0, 0, 3) });
        using var output = Succeed(await RunAsync(Inputs(image, roi), resources: resources));
        var values = output.ToValues();
        Assert.Equal(0L, Assert.IsType<long>(values[IntensityMeasureOutput.LumMin]));
        Assert.Equal(3L, Assert.IsType<long>(values[IntensityMeasureOutput.LumMax]));
        Assert.Equal(1.5d, Assert.IsType<double>(values[IntensityMeasureOutput.LumMean]), 6);
        Assert.Equal(3d, Assert.IsType<double>(values[IntensityMeasureOutput.HistContrast]), 6);
        Assert.Equal(Size, Histogram(values).Sum());
        // ROI 回显取检测区域本身：单行 4 像素 → 中心 (1.5,0)、宽 4、高 1。
        var roiBounds = Assert.IsType<RecordValue>(values[IntensityMeasureOutput.Roi]);
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
        using var output = Succeed(await RunAsync(Inputs(image, mask: maskImage), resources: resources));
        // 左半列像素值 0,1,4,5,8,9,12,13。
        var values = output.ToValues();
        Assert.Equal(0L, Assert.IsType<long>(values[IntensityMeasureOutput.LumMin]));
        Assert.Equal(13L, Assert.IsType<long>(values[IntensityMeasureOutput.LumMax]));
        Assert.Equal(13d, Assert.IsType<double>(values[IntensityMeasureOutput.HistContrast]), 6);
        Assert.Equal(Size * Size / 2, Histogram(values).Sum());
    }

    [Fact]
    public async Task Enabled_limits_judge_the_module_and_the_row()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var passing = await RunAsync(Inputs(image), new IntensityMeasureSettings
        {
            MeanLimitEnabled = true, MeanLimitLow = 7, MeanLimitHigh = 8,
            MaxValueLimitEnabled = true, MaxValueLimitLow = 15, MaxValueLimitHigh = 15
        }, resources);
        using var output = Succeed(passing);
        Assert.Equal(BusinessJudgment.Pass, passing.Judgment);
        Assert.True(Assert.IsType<RecordValue>(Assert.Single(
            Assert.IsType<SequenceValue>(output.ToValues()[IntensityMeasureOutput.Rows])))
            .Get<bool>(IntensityMeasureOutput.JudgmentPassed));

        var failing = await RunAsync(Inputs(image), new IntensityMeasureSettings
        {
            MinValueLimitEnabled = true, MinValueLimitLow = 5, MinValueLimitHigh = 10
        }, resources);
        using var failed = Succeed(failing);
        Assert.Equal(BusinessJudgment.Fail, failing.Judgment);
        Assert.False(Assert.IsType<RecordValue>(Assert.Single(
            Assert.IsType<SequenceValue>(failed.ToValues()[IntensityMeasureOutput.Rows])))
            .Get<bool>(IntensityMeasureOutput.JudgmentPassed));
    }

    [Fact]
    public async Task Contrast_limit_uses_the_histogram_dynamic_range()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(Inputs(image), new IntensityMeasureSettings
        {
            ContrastLimitEnabled = true, ContrastLimitLow = 16, ContrastLimitHigh = 255
        }, resources);
        using var output = Succeed(outcome);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Equal(15d, Assert.IsType<double>(output.ToValues()[IntensityMeasureOutput.HistContrast]), 6);
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
        Assert.Equal("INTENSITYMEASURE_ROI_EMPTY", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public async Task Mask_with_a_different_grid_fails_instead_of_being_resized()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var mask = resources.CreateGray8(Size + 2, Size, Enumerable.Repeat((byte)255, (Size + 2) * Size).ToArray());
        var outcome = await RunAsync(Inputs(image, mask: mask), resources: resources);
        Assert.False(outcome.Success);
        Assert.Equal("INTENSITYMEASURE_MASK_INVALID", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public void Missing_image_input_is_rejected_before_execution()
    {
        // 输入图像是必需端口：冻结输入时即拒绝，执行期不需要也不允许兜底成"空图像"。
        Assert.Throws<ArgumentException>(() =>
            new InputValues(IntensityMeasureTool.Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Invalid_limits_fail_before_touching_the_image()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var outcome = await RunAsync(Inputs(image), new IntensityMeasureSettings
        {
            StdLimitEnabled = true, StdLimitLow = 10, StdLimitHigh = 1
        }, resources);
        Assert.False(outcome.Success);
        Assert.Equal("INTENSITYMEASURE_SETTINGS_INVALID", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public async Task Disposed_output_rejects_further_reads()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(Size, Size, Gradient());
        var output = Succeed(await RunAsync(Inputs(image), resources: resources));
        output.Dispose();
        Assert.Throws<ObjectDisposedException>(() => output.ToValues());
        // 输出作用域释放后输入仍然可用，说明资源所有权没有越界。
        Assert.Equal(Size * Size, image.CopyPixels().Length);
    }
}
