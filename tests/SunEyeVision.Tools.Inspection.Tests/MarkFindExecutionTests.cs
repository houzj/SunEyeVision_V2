using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.MarkFind.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>按原厂链路验收：图像 → 搜索区域 → 模板匹配 → 结果行/几何 → 输出/判定。</summary>
public sealed class MarkFindExecutionTests
{
    private const int Size = 128;

    private sealed class StubContext(IOutputResources resources) : IToolExecutionContext
    {
        private readonly List<string> _logs = new();
        public RunIdentity Identity { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "mark-find", Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = resources;
        public void Log(string message) => _logs.Add(message);
        public IReadOnlyList<string> Logs => _logs;
    }

    /// <summary>非对称 L 形标记：水平臂 24×6 + 竖直臂 6×18，避免 90 度自相似。</summary>
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

    private static string CreateTemplate(FrozenOutputResources owner, byte[] pixels)
    {
        var image = owner.CreateGray8(Size, Size, pixels);
        var roi = FullRegion(owner, image);
        return MarkFindTemplateBuilder.Create(image, roi, new MarkFindSettings());
    }

    private static async Task<ToolOutcome<MarkFindOutput>> RunAsync(MarkFindSettings settings, byte[] pixels,
        FrozenOutputResources owner, bool withRoi = true)
    {
        var tool = new MarkFindTool();
        tool.InitializeRuntime();
        var image = owner.CreateGray8(Size, Size, pixels);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MarkFindSchema.ImageInput] = image };
        if (withRoi) values[MarkFindSchema.RoiInput] = FullRegion(owner, image);
        var inputs = new InputValues(tool.Describe().Inputs, values);
        await using var session = tool.CreateSession();
        return await session.ExecuteAsync(new ToolInvocation<MarkFindSettings>(settings, inputs, new StubContext(owner)));
    }

    [Fact]
    public void Template_payload_round_trips_and_describes_the_mark()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var template = MarkFindTemplate.Decode(payload);
        Assert.True(template.PointCount > 0);
        Assert.True(template.LevelCount >= 1);
        Assert.True(template.Box.Length1 > 0 && template.Box.Length2 > 0);
        Assert.Equal(payload, MarkFindTemplate.Decode(payload).Encode());
        Assert.True(MarkFindTemplate.TryDecode(payload, out _, out var error), error);
        Assert.False(MarkFindTemplate.TryDecode("not-a-template", out _, out var invalid));
        Assert.Contains("模板", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Single_mark_is_located_with_pose_score_rows_and_display_geometry()
    {
        using var owner = new FrozenOutputResources();
        const int markX = 20, markY = 20, offsetX = 17, offsetY = 11;
        var settings = new MarkFindSettings { ModelData = CreateTemplate(owner, Mark(Size, markX, markY)) };
        var outcome = await RunAsync(settings, Mark(Size, markX + offsetX, markY + offsetY), owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.Count);
        Assert.Equal(1, output.Projection.ModuleStatus);

        // L 形标记外接框 24×18；像素中心语义下框中心 = 左上角 + (11.5, 8.5)。
        var expectedX = markX + offsetX + 11.5;
        var expectedY = markY + offsetY + 8.5;
        Assert.InRange(output.Projection.MatchPoints[0].X, expectedX - 2, expectedX + 2);
        Assert.InRange(output.Projection.MatchPoints[0].Y, expectedY - 2, expectedY + 2);
        Assert.InRange(output.Projection.Boxes[0].Width, 23d, 25d);
        Assert.InRange(output.Projection.Boxes[0].Height, 17d, 19d);
        Assert.InRange(output.Projection.Scores[0], 0.5f, 1f);
        Assert.InRange(output.Projection.Angles[0], -0.5f, 0.5f);

        var values = output.ToValues();
        var rows = Assert.IsType<SequenceValue>(values[MarkFindSchema.Rows]);
        Assert.Equal(1, rows.Count);
        var row = (RecordValue)rows[0]!;
        Assert.True(row.Contains("region"));
        Assert.True(row.Contains("outline"));
        Assert.True(row.Get<bool>("judgment-passed"));
        Assert.Equal(1L, values[MarkFindSchema.Count]);
        Assert.Null(values[MarkFindSchema.Scales]);
        Assert.NotNull(values[MarkFindSchema.OutlinePoints]);
        Assert.Contains(nameof(MarkFindResultProjection.Scales), output.Projection.Unavailable);

        Assert.True(output.ResultGeometry.Passed);
        Assert.Contains(output.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
        Assert.NotEmpty(output.DescribeResultRecords());
    }

    [Fact]
    public async Task Empty_search_image_reports_no_match_as_failed_judgement()
    {
        using var owner = new FrozenOutputResources();
        var settings = new MarkFindSettings { ModelData = CreateTemplate(owner, Mark(Size, 20, 20)) };
        var outcome = await RunAsync(settings, new byte[Size * Size], owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(0, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Equal(0, output.Projection.ModuleStatus);
    }

    [Fact]
    public async Task Missing_roi_and_missing_template_fail_with_explicit_diagnostics()
    {
        using var owner = new FrozenOutputResources();
        var payload = CreateTemplate(owner, Mark(Size, 20, 20));
        var noRoi = await RunAsync(new MarkFindSettings { ModelData = payload }, Mark(Size, 20, 20), owner, withRoi: false);
        Assert.False(noRoi.Success);
        Assert.Contains(noRoi.Diagnostics, item => item.Code == "MARKFIND_ROI_REQUIRED");

        var noTemplate = await RunAsync(new MarkFindSettings(), Mark(Size, 20, 20), owner);
        Assert.False(noTemplate.Success);
        Assert.Contains(noTemplate.Diagnostics, item => item.Code == "MARKFIND_TEMPLATE_REQUIRED");
    }

    [Fact]
    public async Task Count_limit_judges_the_number_of_matches()
    {
        using var owner = new FrozenOutputResources();
        var settings = new MarkFindSettings
        {
            ModelData = CreateTemplate(owner, Mark(Size, 16, 16)),
            MaxMatchCount = 2,
            CountLimitEnabled = true,
            CountLimitLow = 1,
            CountLimitHigh = 1
        };
        var pixels = new byte[Size * Size];
        foreach (var (x, y) in new[] { (10, 10), (70, 70) })
        {
            var patch = Mark(Size, x, y);
            for (var index = 0; index < pixels.Length; index++) pixels[index] = Math.Max(pixels[index], patch[index]);
        }
        var outcome = await RunAsync(settings, pixels, owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(2, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Contains(output.LimitMessages, message => message.Contains("匹配个数判断", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsupported_settings_are_rejected_explicitly_instead_of_being_ignored()
    {
        Assert.Contains(new MarkFindSettings { Polarity = 2 }.Validate(), item => item.Code == "MARKFIND_POLARITY_NOT_READY");
        Assert.Contains(new MarkFindSettings { ScaleStart = 0.8, ScaleEnd = 1.2 }.Validate(), item => item.Code == "MARKFIND_SCALE_NOT_READY");
        Assert.Contains(new MarkFindSettings { ProjectionInterval = 5 }.Validate(), item => item.Code == "MARKFIND_PROJECTION_NOT_READY");
        Assert.Contains(new MarkFindSettings { MarkType = MarkFindMarkType.HollowCross }.Validate(), item => item.Code == "MARKFIND_MARK_TYPE_NOT_READY");
        Assert.Contains(new MarkFindSettings { TimeOut = 100 }.Validate(), item => item.Code == "MARKFIND_TIMEOUT_NOT_READY");
        Assert.Contains(new MarkFindSettings { MatchThresholdMode = 1 }.Validate(), item => item.Code == "MARKFIND_MATCH_THRESHOLD_NOT_READY");
        var inverted = new MarkFindSettings { PyramidScaleMode = MarkFindPyramidMode.Manual, RoughPyramidScale = 3, FinePyramidScale = 5 }.Validate();
        Assert.Contains(inverted, item => item.Message.Contains("特征尺度", StringComparison.Ordinal));
        Assert.Empty(new MarkFindSettings().Validate());
    }

    [Fact]
    public void Settings_round_trip_through_the_declared_fields()
    {
        var tool = new MarkFindTool();
        var settings = new MarkFindSettings
        {
            ModelData = "{\"format\":\"x\"}",
            MaxMatchCount = 4,
            MinScore = 0.7,
            AngleStart = -30,
            AngleEnd = 30,
            SortType = MarkFindSortType.Ascend
        };
        var materialized = (MarkFindSettings)tool.MaterializeSettings(tool.ReadSettings(settings));
        Assert.Equal(settings.MaxMatchCount, materialized.MaxMatchCount);
        Assert.Equal(settings.MinScore, materialized.MinScore);
        Assert.Equal(settings.AngleStart, materialized.AngleStart);
        Assert.Equal(settings.SortType, materialized.SortType);
        Assert.Equal(settings.ModelData, materialized.ModelData);
    }
}
