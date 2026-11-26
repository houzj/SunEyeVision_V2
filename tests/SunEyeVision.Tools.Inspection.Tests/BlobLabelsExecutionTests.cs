using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobLabels.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>按原厂链路验收：图像 → 阈值区域 → 连通域 → 筛选排序 → 输出 / 判定。</summary>
public sealed class BlobLabelsExecutionTests
{
    private sealed class StubContext(IOutputResources resources) : IToolExecutionContext
    {
        private readonly List<string> _logs = new();
        public RunIdentity Identity { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "blob-labels", Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = resources;
        public void Log(string message) => _logs.Add(message);
        public IReadOnlyList<string> Logs => _logs;
    }

    private static async Task<ToolOutcome<BlobLabelsOutput>> RunAsync(BlobLabelsSettings settings, byte[] pixels, int size,
        FrozenOutputResources owner, IReadOnlyDictionary<string, object?>? additionalInputs = null)
    {
        var tool = new BlobLabelsTool();
        tool.InitializeRuntime();
        var image = owner.CreateGray8(size, size, pixels);
        var values = new Dictionary<string, object?>
        {
            [BlobLabelsSchema.ImageInput] = image
        };
        if (additionalInputs is not null)
            foreach (var pair in additionalInputs) values[pair.Key] = pair.Value;
        var inputs = new InputValues(tool.Describe().Inputs, values);
        await using var session = tool.CreateSession();
        return await session.ExecuteAsync(new ToolInvocation<BlobLabelsSettings>(settings, inputs, new StubContext(owner)));
    }

    private static byte[] Square(int size, int x, int y, int width, int height, byte value = 255)
    {
        var pixels = new byte[size * size];
        for (var row = 0; row < height; row++)
        for (var column = 0; column < width; column++)
            pixels[(y + row) * size + x + column] = value;
        return pixels;
    }

    [Fact]
    public async Task Single_label_publishes_count_area_rows_and_display_geometry()
    {
        using var owner = new FrozenOutputResources();
        var settings = new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0" };
        var outcome = await RunAsync(settings, Square(64, 2, 2, 4, 4), 64, owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(BusinessJudgment.Pass, outcome.Judgment);
        Assert.Equal(1, output.Projection.Count);
        Assert.Equal(16f, output.Projection.Areas[0]);
        Assert.Equal(1, output.Projection.ModuleStatus);

        var values = output.ToValues();
        var rows = Assert.IsType<SequenceValue>(values[BlobLabelsSchema.Rows]);
        Assert.Equal(1, rows.Count);
        var row = (RecordValue)rows[0]!;
        Assert.Equal(16d, row.Get<double>("area"));
        Assert.True(row.Contains("region"));
        Assert.True(row.Contains("contour"));
        Assert.True(row.Get<bool>("judgment-passed"));
        Assert.Equal(1L, values[BlobLabelsSchema.Count]);
        Assert.Equal(16d, values[BlobLabelsSchema.TotalArea]);

        Assert.True(output.ResultGeometry.Passed);
        Assert.Contains(output.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
        Assert.NotEmpty(output.DescribeResultRecords());
    }

    [Fact]
    public async Task Local_roi_document_limits_the_search_domain()
    {
        using var owner = new FrozenOutputResources();
        var pixels = new byte[64 * 64];
        void Paint(int x, int y)
        {
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                pixels[(y + row) * 64 + x + column] = 255;
        }
        Paint(2, 2);
        Paint(50, 50);
        var settings = new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0" };

        var withoutRoi = await RunAsync(settings, pixels, 64, owner);
        Assert.True(withoutRoi.Success, string.Join(";", withoutRoi.Diagnostics.Select(item => item.Message)));
        using (var output = withoutRoi.Output!) Assert.Equal(2, output.Projection.Count);

        // 本地 ROI 只框左上目标：原件参数页绘制后直接参与搜索域，不依赖重新执行。
        var document = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit, new[]
            {
                RoiDocumentValue.Item(Guid.NewGuid(), "左上", new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
                {
                    ["kind"] = "rectangle", ["center"] = RoiDocumentValue.Point(4, 4),
                    ["width"] = 16d, ["height"] = 16d, ["angle-degrees"] = 0d
                }))
            }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));
        var withRoi = await RunAsync(settings, pixels, 64, owner, new Dictionary<string, object?>
        {
            [BlobLabelsRoi.ModeId] = BlobLabelsRoi.Local,
            [BlobLabelsRoi.LocalDocumentId] = document
        });
        Assert.True(withRoi.Success, string.Join(";", withRoi.Diagnostics.Select(item => item.Message)));
        using var limited = withRoi.Output!;
        Assert.Equal(1, limited.Projection.Count);
        Assert.Contains(limited.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
    }

    [Theory]
    [InlineData(BlobLabelsRoi.RectangleParameters)]
    [InlineData(BlobLabelsRoi.AnnulusParameters)]
    public async Task Inherited_parameter_roi_is_applied_to_execution(string mode)
    {
        using var owner = new FrozenOutputResources();
        var settings = new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0" };
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BlobLabelsRoi.ModeId] = mode,
            [BlobLabelsRoi.RectangleSource + ".center-x"] = 4d,
            [BlobLabelsRoi.RectangleSource + ".center-y"] = 4d,
            [BlobLabelsRoi.RectangleSource + ".width"] = 16d,
            [BlobLabelsRoi.RectangleSource + ".height"] = 16d,
            [BlobLabelsRoi.RectangleSource + ".angle-degrees"] = 0d,
            [BlobLabelsRoi.AnnulusSource + ".center-x"] = 4d,
            [BlobLabelsRoi.AnnulusSource + ".center-y"] = 4d,
            [BlobLabelsRoi.AnnulusSource + ".inner-radius"] = 0d,
            [BlobLabelsRoi.AnnulusSource + ".outer-radius"] = 12d,
            [BlobLabelsRoi.AnnulusSource + ".start-degrees"] = 0d,
            [BlobLabelsRoi.AnnulusSource + ".sweep-degrees"] = 360d
        };
        var outcome = await RunAsync(settings, Square(64, 2, 2, 4, 4), 64, owner, values);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        // ROI 显隐由平台编辑会话统一控制；工具只把继承参数物化为执行用 ROI 文档，
        // 能否执行由结果几何（实际搜索域）证明，不再从工具输出读取 ROI 快照。
        Assert.Contains(output.ResultGeometry.Groups, group => group.Role == ResultGeometryRole.Inspection);
    }

    [Fact]
    public async Task Descending_area_sort_and_find_count_keep_the_largest_labels()
    {
        using var owner = new FrozenOutputResources();
        var pixels = new byte[64 * 64];
        void Paint(int x, int y, int w, int h) { for (var row = 0; row < h; row++) for (var column = 0; column < w; column++) pixels[(y + row) * 64 + x + column] = 255; }
        Paint(2, 2, 2, 2);      // 4 像素
        Paint(20, 2, 3, 3);     // 9 像素
        Paint(40, 2, 4, 4);     // 16 像素
        var settings = new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0", FindCount = 2 };
        var outcome = await RunAsync(settings, pixels, 64, owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(new[] { 16f, 9f }, output.Projection.Areas.ToArray());
    }

    [Fact]
    public async Task Area_filter_removes_labels_outside_the_declared_range()
    {
        using var owner = new FrozenOutputResources();
        var pixels = new byte[64 * 64];
        for (var row = 0; row < 4; row++) for (var column = 0; column < 4; column++) pixels[(2 + row) * 64 + 2 + column] = 255;
        for (var row = 0; row < 2; row++) for (var column = 0; column < 2; column++) pixels[(30 + row) * 64 + 30 + column] = 255;
        var settings = new BlobLabelsSettings
        {
            ThresholdArea = "1#1#255#1#999999999#0",
            AreaFilterEnabled = true,
            AreaFilterMin = 10,
            AreaFilterMax = 100
        };
        var outcome = await RunAsync(settings, pixels, 64, owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(1, output.Projection.Count);
        Assert.Equal(16f, output.Projection.Areas[0]);
    }

    [Fact]
    public async Task Empty_image_reports_zero_labels_as_failed_judgement()
    {
        using var owner = new FrozenOutputResources();
        var settings = new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0" };
        var outcome = await RunAsync(settings, new byte[32 * 32], 32, owner);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(0, output.Projection.Count);
        Assert.Equal(BusinessJudgment.Fail, outcome.Judgment);
        Assert.Equal(0, output.Projection.ModuleStatus);
    }

    [Fact]
    public async Task Missing_threshold_areas_fail_with_an_explicit_diagnostic()
    {
        using var owner = new FrozenOutputResources();
        var outcome = await RunAsync(new BlobLabelsSettings(), Square(32, 2, 2, 2, 2), 32, owner);
        Assert.False(outcome.Success);
        Assert.Contains(outcome.Diagnostics, item => item.Code == "BLOBLABEL_THRESHOLD_REQUIRED");
    }

    [Fact]
    public async Task Undeclared_classification_and_overlap_settings_are_rejected_explicitly()
    {
        using var owner = new FrozenOutputResources();
        var classified = await RunAsync(new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0", ClassName = "A" },
            Square(32, 2, 2, 2, 2), 32, owner);
        Assert.False(classified.Success);
        Assert.Contains(classified.Diagnostics, item => item.Message.Contains("类别信息", StringComparison.Ordinal));

        var overlap = await RunAsync(new BlobLabelsSettings { ThresholdArea = "1#1#255#1#999999999#0", MinOverlapRatio = 5 },
            Square(32, 2, 2, 2, 2), 32, owner);
        Assert.False(overlap.Success);
        Assert.Contains(overlap.Diagnostics, item => item.Message.Contains("重叠率", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_validation_reports_inverted_ranges()
    {
        var errors = new BlobLabelsSettings { AreaFilterEnabled = true, AreaFilterMin = 100, AreaFilterMax = 10 }.Validate();
        Assert.Contains(errors, item => item.Message.Contains("面积筛选", StringComparison.Ordinal));
        Assert.DoesNotContain(new BlobLabelsSettings().Validate(), item => item.Code == "BLOBLABEL_SETTINGS_INVALID");
    }

    [Fact]
    public void Threshold_area_settings_round_trip_through_the_declared_field()
    {
        var tool = new BlobLabelsTool();
        var settings = new BlobLabelsSettings { ThresholdArea = "1#1#255#10#999999999#0\n2#0#100#1#50#1" };
        var values = tool.ReadSettings(settings);
        var materialized = (BlobLabelsSettings)tool.MaterializeSettings(values);
        Assert.Equal(settings.ThresholdArea, materialized.ThresholdArea);
        Assert.Equal(2, materialized.ReadThresholdAreas().Count);
    }
}
