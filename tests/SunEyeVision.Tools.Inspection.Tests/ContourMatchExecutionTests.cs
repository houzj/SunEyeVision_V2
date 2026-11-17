using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.ContourMatch.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>按原厂链路验收：图像 → 搜索区域 → 模板学习 → 匹配 → 排序/判定 → 结果行/几何 → 输出/释放。</summary>
public sealed class ContourMatchExecutionTests
{
    private const int Size = 128;

    private sealed class StubContext(IOutputResources resources) : IToolExecutionContext
    {
        private readonly List<string> _logs = new();
        public RunIdentity Identity { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "contour-match", Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = resources;
        public void Log(string message) => _logs.Add(message);
        public IReadOnlyList<string> Logs => _logs;
    }

    /// <summary>非对称 L 形目标：水平臂 24×6 + 竖直臂 6×18，避免 90 度自相似。</summary>
    private static byte[] Mark(int size, int x, int y)
    {
        var pixels = new byte[size * size];
        for (var row = 0; row < 6; row++)
        for (var column = 0; column < 24; column++)
            pixels[(y + row) * size + x + column] = 255;
        for (var row = 0; row < 18; row++)
        for (var column = 0; column < 6; column++)
            pixels[(y + row) * size + x + column] = 255;
        return pixels;
    }

    private static IReadOnlyRegion FullRegion(FrozenOutputResources owner, IReadOnlyImage image)
    {
        var runs = Enumerable.Range(0, image.Height).Select(row => new RegionRun(row, 0, image.Width - 1)).ToArray();
        return owner.CreateRegion(image.EffectiveDomain.Frame, image.Frame, runs);
    }

    private static string CreateTemplate(FrozenOutputResources owner, byte[] pixels, ContourMatchSettings? settings = null)
    {
        var image = owner.CreateGray8(Size, Size, pixels);
        var roi = FullRegion(owner, image);
        return ContourMatchTemplateBuilder.Create(image, roi, settings ?? new ContourMatchSettings());
    }

    private static async Task<ToolOutcome<ContourMatchOutput>> RunAsync(ContourMatchSettings settings, byte[] pixels,
        FrozenOutputResources owner, bool withRoi = true)
    {
        var tool = new ContourMatchTool();
        tool.InitializeRuntime();
        var image = owner.CreateGray8(Size, Size, pixels);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ContourMatchSchema.ImageInput] = image };
        if (withRoi) values[ContourMatchSchema.RoiInput] = FullRegion(owner, image);
        var inputs = new InputValues(tool.Describe().Inputs, values);
        await using var session = tool.CreateSession();
        return await session.ExecuteAsync(new ToolInvocation<ContourMatchSettings>(settings, inputs, new StubContext(owner)));
    }

    private static byte[] TwoMarks(params (int X, int Y)[] positions)
    {
        var pixels = new byte[Size * Size];
        foreach (var (x, y) in positions)
        {
            var patch = Mark(Size, x, y);
            for (var index = 0; index < pixels.Length; index++) pixels[index] = Math.Max(pixels[index], patch[index]);
        }
        return pixels;
    }

    [Fact]
    public void Template_payload_round_trips_and_describes_the_contour()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var template = ContourMatchTemplate.Decode(payload);
        Assert.True(template.PointCount > 0);
        Assert.True(template.LevelCount >= 1);
        Assert.True(template.Box.Length1 > 0 && template.Box.Length2 > 0);
        Assert.Equal(payload, ContourMatchTemplate.Decode(payload).Encode());
        Assert.True(ContourMatchTemplate.TryDecode(payload, out _, out var error), error);
        Assert.False(ContourMatchTemplate.TryDecode("not-a-template", out _, out var invalid));
        Assert.Contains("模板", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Single_contour_is_located_with_pose_score_rows_and_display_geometry()
    {
        using var owner = new FrozenOutputResources();
        const int markX = 20, markY = 20, offsetX = 17, offsetY = 11;
        var settings = new ContourMatchSettings { ModelData = CreateTemplate(owner, Mark(Size, markX, markY)) };
        var outcome = await RunAsync(settings, Mark(Size, markX + offsetX, markY + offsetY), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.Count);
        Assert.Equal(1, output.Projection.ModuleStatus);

        // L 形目标外接框 24×18；像素中心语义下框中心 = 左上角 + (11.5, 8.5)。
        var expectedX = markX + offsetX + 11.5;
        var expectedY = markY + offsetY + 8.5;
        Assert.InRange(output.Projection.MatchPoints[0].X, expectedX - 2, expectedX + 2);
        Assert.InRange(output.Projection.MatchPoints[0].Y, expectedY - 2, expectedY + 2);
        Assert.InRange(output.Projection.Boxes[0].Width, 23d, 25d);
        Assert.InRange(output.Projection.Boxes[0].Height, 17d, 19d);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
        Assert.InRange(output.Projection.Angles[0], -0.5f, 0.5f);

        var values = output.ToValues();
        var rows = Assert.IsType<SequenceValue>(values[ContourMatchSchema.Rows]);
        Assert.Equal(1, rows.Count);
        var row = (RecordValue)rows[0]!;
        Assert.Equal(1L, row.Get<long>("element-id"));
        Assert.True(row.Contains("region"));
        Assert.True(row.Contains("outline"));
        Assert.True(row.Get<bool>("judgment-passed"));
        Assert.Null(row.Get<object?>("scale"));
        Assert.Equal(1L, values[ContourMatchSchema.Count]);
        Assert.Null(values[ContourMatchSchema.Scales]);
        Assert.NotNull(values[ContourMatchSchema.OutlinePoints]);
        Assert.Contains(nameof(ContourMatchResultProjection.Scales), output.Projection.Unavailable);

        Assert.True(output.ResultGeometry.Passed);
        Assert.Contains(output.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
        Assert.NotEmpty(output.DescribeResultRecords());
    }

    [Fact]
    public async Task Empty_search_image_reports_no_match_as_failed_judgement()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings { ModelData = CreateTemplate(owner, Mark(Size, 20, 20)) };
        var outcome = await RunAsync(settings, new byte[Size * Size], owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(0, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Equal(0, output.Projection.ModuleStatus);
    }

    [Fact]
    public async Task Zero_matches_pass_when_the_original_ignore_count_switch_is_on()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 20, 20)),
            OkWhenCountIsZero = true
        };
        var outcome = await RunAsync(settings, new byte[Size * Size], owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(0, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.ModuleStatus);
    }

    [Fact]
    public async Task Missing_roi_and_missing_template_fail_with_explicit_diagnostics()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var noRoi = await RunAsync(new ContourMatchSettings { ModelData = payload }, Mark(Size, 20, 20), owner, withRoi: false);
        Assert.False(noRoi.Success);
        Assert.Contains(noRoi.Diagnostics, item => item.Code == "CONTOURMATCH_ROI_REQUIRED");

        var noTemplate = await RunAsync(new ContourMatchSettings(), Mark(Size, 20, 20), owner);
        Assert.False(noTemplate.Success);
        Assert.Contains(noTemplate.Diagnostics, item => item.Code == "CONTOURMATCH_TEMPLATE_REQUIRED");
    }

    [Fact]
    public async Task Count_limit_judges_the_number_of_matches()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 16, 16)),
            MaxMatchCount = 2,
            CountLimitEnabled = true,
            CountLimitLow = 1,
            CountLimitHigh = 1
        };
        var outcome = await RunAsync(settings, TwoMarks((10, 10), (70, 70)), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(2, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Contains(output.LimitMessages, message => message.Contains("匹配个数判断", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sort_type_x_orders_rows_and_numbers_them_in_sorted_order()
    {
        using var owner = new FrozenOutputResources();
        var settings = new ContourMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 16, 16)),
            MaxMatchCount = 2,
            SortType = ContourMatchSortType.X
        };
        var outcome = await RunAsync(settings, TwoMarks((70, 10), (10, 70)), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(2, output.Projection.Count);
        Assert.True(output.Projection.MatchPoints[0].X < output.Projection.MatchPoints[1].X);
        var rows = Assert.IsType<SequenceValue>(output.ToValues()[ContourMatchSchema.Rows]);
        Assert.Equal(1L, ((RecordValue)rows[0]!).Get<long>("element-id"));
        Assert.Equal(2L, ((RecordValue)rows[1]!).Get<long>("element-id"));
    }

    [Fact]
    public void Unsupported_settings_are_rejected_explicitly_instead_of_being_ignored()
    {
        Assert.Contains(new ContourMatchSettings { Polarity = 0 }.Validate(), item => item.Code == "CONTOURMATCH_POLARITY_NOT_READY");
        Assert.Contains(new ContourMatchSettings { ScaleStart = 0.8, ScaleEnd = 1.2 }.Validate(), item => item.Code == "CONTOURMATCH_SCALE_NOT_READY");
        Assert.Contains(new ContourMatchSettings { ScaleXStart = 1.1 }.Validate(), item => item.Code == "CONTOURMATCH_SCALE_NOT_READY");
        Assert.Contains(new ContourMatchSettings { ScaleLimitEnabled = true }.Validate(), item => item.Code == "CONTOURMATCH_SCALE_LIMIT_NOT_READY");
        Assert.Contains(new ContourMatchSettings { TimeOut = 100 }.Validate(), item => item.Code == "CONTOURMATCH_TIMEOUT_NOT_READY");
        Assert.Contains(new ContourMatchSettings { MatchThresholdMode = 2 }.Validate(), item => item.Code == "CONTOURMATCH_MATCH_THRESHOLD_NOT_READY");
        Assert.Contains(new ContourMatchSettings { SpotterEnabled = true }.Validate(), item => item.Code == "CONTOURMATCH_SPOTTER_NOT_READY");
        Assert.Contains(new ContourMatchSettings { MatchExtentRate = 10 }.Validate(), item => item.Code == "CONTOURMATCH_MATCH_EXTENT_NOT_READY");
        Assert.Contains(new ContourMatchSettings { OutlinePointsEnabled = true }.Validate(), item => item.Code == "CONTOURMATCH_OUTLINE_POINTS_NOT_READY");
        Assert.Contains(new ContourMatchSettings { UseMatchAllMode = true }.Validate(), item => item.Code == "CONTOURMATCH_MATCH_ALL_MODE_NOT_READY");
        Assert.Contains(new ContourMatchSettings { RangeScaleType = 1 }.Validate(), item => item.Code == "CONTOURMATCH_RANGE_SCALE_TYPE_NOT_READY");
        Assert.Contains(new ContourMatchSettings { MultiModelMaxOverlap = 30 }.Validate(), item => item.Code == "CONTOURMATCH_MULTI_MODEL_NOT_READY");
        Assert.Contains(new ContourMatchSettings { SkewXStart = 5 }.Validate(), item => item.Code == "CONTOURMATCH_SKEW_NOT_READY");
        Assert.Contains(new ContourMatchSettings { MaskModel = ContourMatchMaskModel.OuterOutline }.Validate(), item => item.Code == "CONTOURMATCH_MASK_MODEL_NOT_READY");
        Assert.Contains(new ContourMatchSettings { ChainMode = ContourMatchChainMode.Manual }.Validate(), item => item.Code == "CONTOURMATCH_CHAIN_MODE_NOT_READY");
        var inverted = new ContourMatchSettings
        {
            PyramidMode = ContourMatchPyramidMode.Manual,
            PyramidScaleLevel = 3,
            PyramidScaleRLevel = 5
        }.Validate();
        Assert.Contains(inverted, item => item.Message.Contains("特征尺度", StringComparison.Ordinal));
        Assert.Empty(new ContourMatchSettings().Validate());
    }

    [Fact]
    public async Task Manual_pyramid_and_manual_edge_threshold_build_a_usable_template()
    {
        using var owner = new FrozenOutputResources();
        var templateSettings = new ContourMatchSettings
        {
            PyramidMode = ContourMatchPyramidMode.Manual,
            PyramidScaleLevel = 4,
            PyramidScaleRLevel = 2,
            EdgeThresholdMode = ContourMatchThresholdMode.Manual,
            EdgeThreshold = 20
        };
        var settings = templateSettings with { ModelData = CreateTemplate(owner, Mark(Size, 20, 20), templateSettings) };
        var outcome = await RunAsync(settings, Mark(Size, 33, 41), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(1, output.Projection.Count);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
    }

    [Fact]
    public void Settings_round_trip_through_the_declared_fields()
    {
        var tool = new ContourMatchTool();
        var settings = new ContourMatchSettings
        {
            ModelData = "{\"format\":\"x\"}",
            MaxMatchCount = 4,
            MinScore = 0.7,
            AngleStart = -30,
            AngleEnd = 30,
            SortType = ContourMatchSortType.YX,
            PyramidScaleLevel = 7,
            EdgeThreshold = 64
        };
        var materialized = (ContourMatchSettings)tool.MaterializeSettings(tool.ReadSettings(settings));
        Assert.Equal(settings.MaxMatchCount, materialized.MaxMatchCount);
        Assert.Equal(settings.MinScore, materialized.MinScore);
        Assert.Equal(settings.AngleStart, materialized.AngleStart);
        Assert.Equal(settings.SortType, materialized.SortType);
        Assert.Equal(settings.PyramidScaleLevel, materialized.PyramidScaleLevel);
        Assert.Equal(settings.EdgeThreshold, materialized.EdgeThreshold);
        Assert.Equal(settings.ModelData, materialized.ModelData);
    }
}
