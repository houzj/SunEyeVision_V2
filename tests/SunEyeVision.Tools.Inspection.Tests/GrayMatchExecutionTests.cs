using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.GrayMatch.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>按原厂链路验收：图像 → 搜索区域 → 灰度模板学习 → 匹配 → 排序/判定 → 结果行/几何 → 输出/释放。</summary>
public sealed class GrayMatchExecutionTests
{
    private const int Size = 128;

    private sealed class StubContext(IOutputResources resources) : IToolExecutionContext
    {
        private readonly List<string> _logs = new();
        public RunIdentity Identity { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "gray-match", Guid.NewGuid());
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

    private static IReadOnlyRegion Region(FrozenOutputResources owner, IReadOnlyImage image,
        int row1, int column1, int row2, int column2)
        => owner.CreateRegion(image.EffectiveDomain.Frame, image.Frame,
            Enumerable.Range(row1, row2 - row1 + 1).Select(row => new RegionRun(row, column1, column2)).ToArray());

    private static IReadOnlyRegion FullRegion(FrozenOutputResources owner, IReadOnlyImage image)
        => Region(owner, image, 0, 0, image.Height - 1, image.Width - 1);

    /// <summary>
    /// 灰度模板必须是「目标本身」的区域（NCC 用区域内的灰度值建模），因此模板 ROI 取标记外接框，
    /// 搜索 ROI 取整幅图像。
    /// </summary>
    private static string CreateTemplate(FrozenOutputResources owner, byte[] pixels, GrayMatchSettings? settings = null,
        int markX = 20, int markY = 20)
    {
        var image = owner.CreateGray8(Size, Size, pixels);
        var roi = Region(owner, image, markY, markX, markY + 17, markX + 23);
        return GrayMatchTemplateBuilder.Create(image, roi, settings ?? new GrayMatchSettings());
    }

    private static async Task<ToolOutcome<GrayMatchOutput>> RunAsync(GrayMatchSettings settings, byte[] pixels,
        FrozenOutputResources owner, bool withRoi = true)
    {
        var tool = new GrayMatchTool();
        tool.InitializeRuntime();
        var image = owner.CreateGray8(Size, Size, pixels);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [GrayMatchSchema.ImageInput] = image };
        if (withRoi) values[GrayMatchSchema.RoiInput] = FullRegion(owner, image);
        var inputs = new InputValues(tool.Describe().Inputs, values);
        await using var session = tool.CreateSession();
        return await session.ExecuteAsync(new ToolInvocation<GrayMatchSettings>(settings, inputs, new StubContext(owner)));
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
    public void Template_payload_round_trips_and_describes_the_template()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var template = GrayMatchTemplate.Decode(payload);
        Assert.True(template.LevelCount >= 1);
        Assert.True(template.Box.Length1 > 0 && template.Box.Length2 > 0);
        Assert.InRange(template.Width, 20, 28);
        Assert.InRange(template.Height, 14, 22);
        Assert.Equal(payload, GrayMatchTemplate.Decode(payload).Encode());
        Assert.True(GrayMatchTemplate.TryDecode(payload, out _, out var error), error);
        Assert.False(GrayMatchTemplate.TryDecode("not-a-template", out _, out var invalid));
        Assert.Contains("模板", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Single_gray_target_is_located_with_pose_score_rows_and_display_geometry()
    {
        using var owner = new FrozenOutputResources();
        const int markX = 20, markY = 20, offsetX = 17, offsetY = 11;
        var settings = new GrayMatchSettings { ModelData = CreateTemplate(owner, Mark(Size, markX, markY)) };
        var outcome = await RunAsync(settings, Mark(Size, markX + offsetX, markY + offsetY), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.Count);
        Assert.Equal(1, output.Projection.ModuleStatus);

        // NCC 匹配位置＝模板原点（模板 ROI 中心）在图像中的位置：目标中心 (20..43, 20..37) 平移 (17,11)。
        var expectedX = markX + offsetX + 11.5;
        var expectedY = markY + offsetY + 8.5;
        Assert.InRange(output.Projection.MatchPoints[0].X, expectedX - 2, expectedX + 2);
        Assert.InRange(output.Projection.MatchPoints[0].Y, expectedY - 2, expectedY + 2);
        Assert.InRange(output.Projection.Boxes[0].Width, 22d, 26d);
        Assert.InRange(output.Projection.Boxes[0].Height, 16d, 20d);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
        Assert.InRange(output.Projection.Angles[0], -0.5f, 0.5f);

        var values = output.ToValues();
        var rows = Assert.IsType<SequenceValue>(values[GrayMatchSchema.Rows]);
        Assert.Equal(1, rows.Count);
        var row = (RecordValue)rows[0]!;
        Assert.Equal(1L, row.Get<long>("element-id"));
        Assert.True(row.Contains("region"));
        Assert.True(row.Get<bool>("judgment-passed"));
        Assert.Equal(1L, values[GrayMatchSchema.Count]);

        Assert.True(output.ResultGeometry.Passed);
        Assert.Contains(output.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
        Assert.NotEmpty(output.DescribeResultRecords());
    }

    [Fact]
    public async Task Empty_search_image_reports_no_match_as_failed_judgement()
    {
        using var owner = new FrozenOutputResources();
        var settings = new GrayMatchSettings { ModelData = CreateTemplate(owner, Mark(Size, 20, 20)) };
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
        var settings = new GrayMatchSettings
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
        var noRoi = await RunAsync(new GrayMatchSettings { ModelData = payload }, Mark(Size, 20, 20), owner, withRoi: false);
        Assert.False(noRoi.Success);
        Assert.Contains(noRoi.Diagnostics, item => item.Code == "GRAYMATCH_ROI_REQUIRED");

        var noTemplate = await RunAsync(new GrayMatchSettings(), Mark(Size, 20, 20), owner);
        Assert.False(noTemplate.Success);
        Assert.Contains(noTemplate.Diagnostics, item => item.Code == "GRAYMATCH_TEMPLATE_REQUIRED");
    }

    [Fact]
    public async Task Count_limit_judges_the_number_of_matches()
    {
        using var owner = new FrozenOutputResources();
        var settings = new GrayMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 20, 20)),
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
        var settings = new GrayMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 20, 20)),
            MaxMatchCount = 2,
            SortType = GrayMatchSortType.X
        };
        var outcome = await RunAsync(settings, TwoMarks((70, 10), (10, 70)), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(2, output.Projection.Count);
        Assert.True(output.Projection.MatchPoints[0].X < output.Projection.MatchPoints[1].X);
        var rows = Assert.IsType<SequenceValue>(output.ToValues()[GrayMatchSchema.Rows]);
        Assert.Equal(1L, ((RecordValue)rows[0]!).Get<long>("element-id"));
        Assert.Equal(2L, ((RecordValue)rows[1]!).Get<long>("element-id"));
    }

    [Fact]
    public async Task Manual_pyramid_level_builds_a_usable_template()
    {
        using var owner = new FrozenOutputResources();
        var templateSettings = new GrayMatchSettings { PyramidMode = GrayMatchPyramidMode.Manual, PyramidLevel = 3 };
        var settings = templateSettings with { ModelData = CreateTemplate(owner, Mark(Size, 20, 20), templateSettings) };
        var outcome = await RunAsync(settings, Mark(Size, 33, 41), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(1, output.Projection.Count);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
    }

    [Fact]
    public async Task Run_angle_range_outside_the_template_coverage_is_rejected()
    {
        using var owner = new FrozenOutputResources();
        var templateSettings = new GrayMatchSettings { ModelAngleStart = -10, ModelAngleEnd = 10 };
        var settings = templateSettings with { ModelData = CreateTemplate(owner, Mark(Size, 20, 20), templateSettings) };
        var outcome = await RunAsync(settings, Mark(Size, 20, 20), owner);
        Assert.False(outcome.Success);
        Assert.Contains(outcome.Diagnostics, item => item.Code == "GRAYMATCH_TEMPLATE_ANGLE_RANGE");
    }

    [Fact]
    public async Task End_level_beyond_the_template_levels_is_rejected()
    {
        using var owner = new FrozenOutputResources();
        var settings = new GrayMatchSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 20, 20)),
            EndPyramidLevel = 8
        };
        var outcome = await RunAsync(settings, Mark(Size, 20, 20), owner);
        if (GrayMatchTemplate.Decode(settings.ModelData).LevelCount < 8)
        {
            Assert.False(outcome.Success);
            Assert.Contains(outcome.Diagnostics, item => item.Code == "GRAYMATCH_END_LEVEL_INVALID");
        }
    }

    [Fact]
    public void Unsupported_settings_are_rejected_explicitly_instead_of_being_ignored()
    {
        Assert.Contains(new GrayMatchSettings { Polarity = 0 }.Validate(), item => item.Code == "GRAYMATCH_POLARITY_NOT_READY");
        Assert.Contains(new GrayMatchSettings { MatchingPolarity = 0 }.Validate(), item => item.Code == "GRAYMATCH_MATCHING_POLARITY_NOT_READY");
        Assert.Contains(new GrayMatchSettings { TimeOut = 100 }.Validate(), item => item.Code == "GRAYMATCH_TIMEOUT_NOT_READY");
        Assert.Contains(new GrayMatchSettings { UseMatchAllMode = true }.Validate(), item => item.Code == "GRAYMATCH_MATCH_ALL_MODE_NOT_READY");
        Assert.Contains(new GrayMatchSettings { EndPyramidLevel = 9 }.Validate(), item => item.Code == "GRAYMATCH_SETTINGS_INVALID");
        var inverted = new GrayMatchSettings { ModelAngleStart = 30, ModelAngleEnd = -30 }.Validate();
        Assert.Contains(inverted, item => item.Message.Contains("起始角度", StringComparison.Ordinal));
        Assert.Empty(new GrayMatchSettings().Validate());
    }

    [Fact]
    public void Settings_round_trip_through_the_declared_fields()
    {
        var tool = new GrayMatchTool();
        var settings = new GrayMatchSettings
        {
            ModelData = "{\"format\":\"x\"}",
            MaxMatchCount = 4,
            MinScore = 0.7,
            AngleStart = -30,
            AngleEnd = 30,
            SortType = GrayMatchSortType.YX,
            PyramidLevel = 7,
            ModelAngleStep = 5,
            EndPyramidLevel = 2
        };
        var materialized = (GrayMatchSettings)tool.MaterializeSettings(tool.ReadSettings(settings));
        Assert.Equal(settings.MaxMatchCount, materialized.MaxMatchCount);
        Assert.Equal(settings.MinScore, materialized.MinScore);
        Assert.Equal(settings.AngleStart, materialized.AngleStart);
        Assert.Equal(settings.SortType, materialized.SortType);
        Assert.Equal(settings.PyramidLevel, materialized.PyramidLevel);
        Assert.Equal(settings.ModelAngleStep, materialized.ModelAngleStep);
        Assert.Equal(settings.EndPyramidLevel, materialized.EndPyramidLevel);
        Assert.Equal(settings.ModelData, materialized.ModelData);
    }
}
