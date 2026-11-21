using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.DivideImage.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>划片拆分执行语义：正常输出、边界、参数校验失败与缺失输入。</summary>
public sealed class DivideImageExecutionTests
{
    private static InputValues ImageInput(IReadOnlyImage image) => new(DivideImageTool.Schema.Inputs,
        new Dictionary<string, object?> { [DivideImageTool.ImageInput] = image });

    private static IToolExecutionContext Context(FrozenOutputResources resources, string nodeId = "divide-image-node")
    {
        var context = Substitute.For<IToolExecutionContext>();
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nodeId, Guid.NewGuid()));
        context.Cancellation.Returns(CancellationToken.None);
        context.Resources.Returns(resources);
        return context;
    }

    private static async Task<ToolOutcome<DivideImageOutput>> ExecuteAsync(
        IToolSession<DivideImageSettings, DivideImageOutput> session, DivideImageSettings settings,
        InputValues inputs, FrozenOutputResources resources)
        => await session.ExecuteAsync(new(settings, inputs, Context(resources)));

    private static (double X, double Y, double Width, double Height) ReadRect(RecordValue rect)
    {
        var center = rect.Get<PointGeometry>(DivideImageOutput.RectCenter);
        return (center.Point.X, center.Point.Y, rect.Get<double>(DivideImageOutput.RectWidth),
            rect.Get<double>(DivideImageOutput.RectHeight));
    }

    [Fact]
    public async Task Defaults_output_one_tile_covering_the_whole_image()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(8, 6, new byte[48]);
        await using var session = new DivideImageTool().CreateSession();
        var outcome = await ExecuteAsync(session, new DivideImageSettings(), ImageInput(image), resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        Assert.Equal(1L, Assert.IsType<long>(values[DivideImageOutput.Count]));
        Assert.Equal(1L, Assert.IsType<long>(values[DivideImageOutput.ModuleStatus]));
        var elapsed = Assert.IsType<double>(values[DivideImageOutput.ModuleRunTime]);
        Assert.True(double.IsFinite(elapsed) && elapsed >= 0);
        Assert.Equal((double)(float)elapsed, elapsed);
        // 单片、无重叠：分片就是整幅图像（平台既有矩形约定：center=(W/2,H/2)、width=W、height=H）。
        var roi = Assert.IsType<RecordValue>(values[DivideImageOutput.Roi]);
        Assert.Equal((4d, 3d, 8d, 6d), ReadRect(roi));
        var rects = Assert.IsType<SequenceValue>(values[DivideImageOutput.Rects]);
        Assert.Equal(1, rects.Count);
        Assert.Equal((4d, 3d, 8d, 6d), ReadRect(Assert.IsType<RecordValue>(rects[0])));
        Assert.Equal(0d, roi.Get<double>(DivideImageOutput.RectAngle));
    }

    [Fact]
    public async Task Two_by_three_splits_into_six_rects_in_row_major_order()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(8, 6, new byte[48]);
        await using var session = new DivideImageTool().CreateSession();
        var outcome = await ExecuteAsync(session,
            new DivideImageSettings { Columns = 2, Rows = 3 }, ImageInput(image), resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        Assert.Equal(6L, Assert.IsType<long>(values[DivideImageOutput.Count]));
        var rects = Assert.IsType<SequenceValue>(values[DivideImageOutput.Rects]);
        Assert.Equal(6, rects.Count);
        var boxes = Enumerable.Range(0, rects.Count)
            .Select(index => ReadRect(Assert.IsType<RecordValue>(rects[index]))).ToArray();
        // 8×6 切成 2×3：每片 4×2，先横后竖（行优先）。
        Assert.Equal(new[] { (2d, 1d), (6d, 1d), (2d, 3d), (6d, 3d), (2d, 5d), (6d, 5d) },
            boxes.Select(box => (box.X, box.Y)).ToArray());
        Assert.All(boxes, box => { Assert.Equal(4d, box.Width); Assert.Equal(2d, box.Height); });
        // 分片首尾贴合图像边界，中间不遗漏。
        Assert.Equal(0d, boxes[0].X - boxes[0].Width / 2);
        Assert.Equal(8d, boxes[1].X + boxes[1].Width / 2);
        Assert.Equal(6d, boxes[^1].Y + boxes[^1].Height / 2);
        // 原件 ROI 输出与分片无关，仍是整幅输入。
        Assert.Equal((4d, 3d, 8d, 6d), ReadRect(Assert.IsType<RecordValue>(values[DivideImageOutput.Roi])));
    }

    [Fact]
    public async Task Overlap_rate_makes_neighbour_tiles_share_pixels()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(8, 4, new byte[32]);
        await using var session = new DivideImageTool().CreateSession();
        // 原生 ProcessAlg：X 方向 2 片、50% 重叠，片宽 6，步进 2。
        var outcome = await ExecuteAsync(session,
            new DivideImageSettings { Columns = 2, OverlapX = 50 }, ImageInput(image), resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var rects = Assert.IsType<SequenceValue>(output.ToValues()[DivideImageOutput.Rects]);
        var first = ReadRect(Assert.IsType<RecordValue>(rects[0]));
        var second = ReadRect(Assert.IsType<RecordValue>(rects[1]));
        Assert.Equal(6d, first.Width, 5);
        Assert.Equal(6d, second.Width, 5);
        Assert.Equal(3d, first.X, 5);
        Assert.Equal(5d, second.X, 5);
        Assert.Equal(4d, first.X + first.Width / 2 - (second.X - second.Width / 2), 5);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected_before_reading_the_input()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(4, 4, new byte[16]);
        await using var session = new DivideImageTool().CreateSession();
        var outcome = await ExecuteAsync(session, new DivideImageSettings { Columns = 0 }, ImageInput(image), resources);
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        var diagnostic = Assert.Single(outcome.Diagnostics);
        Assert.Equal("DIVIDE_IMAGE_COLUMNS", diagnostic.Code);
        Assert.Equal(DivideImageFields.Columns, diagnostic.FieldId);
    }

    [Fact]
    public async Task Overlap_of_one_hundred_percent_is_accepted_and_above_is_rejected()
    {
        // 原厂 IMVSDivideImageModuAlgorithmTab.xml 声明 OverlaprateX/Y 范围 0~100，
        // 因此 100% 必须被接受（各方向分片位置重合），101% 才越界。
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(4, 4, new byte[16]);
        await using var session = new DivideImageTool().CreateSession();
        var accepted = await ExecuteAsync(session,
            new DivideImageSettings { Columns = 2, OverlapX = 100 }, ImageInput(image), resources);
        Assert.True(accepted.Success);

        await using var rejecting = new DivideImageTool().CreateSession();
        var rejected = await ExecuteAsync(rejecting,
            new DivideImageSettings { Columns = 2, OverlapX = 101 }, ImageInput(image), resources);
        Assert.False(rejected.Success);
        Assert.Equal("DIVIDE_IMAGE_OVERLAP_X", Assert.Single(rejected.Diagnostics).Code);
    }

    [Fact]
    public async Task Many_columns_on_a_narrow_image_still_cover_every_column()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(4, 1, new byte[4]);
        await using var session = new DivideImageTool().CreateSession();
        var outcome = await ExecuteAsync(session,
            new DivideImageSettings { Columns = 5 }, ImageInput(image), resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var rects = Assert.IsType<SequenceValue>(output.ToValues()[DivideImageOutput.Rects]);
        Assert.Equal(5, rects.Count);
        // 原生输出保持浮点几何，首列为 0，末片仍覆盖到右边界。
        var first = ReadRect(Assert.IsType<RecordValue>(rects[0]));
        var last = ReadRect(Assert.IsType<RecordValue>(rects[^1]));
        Assert.Equal(0d, first.X - first.Width / 2, 5);
        Assert.Equal(4d, last.X + last.Width / 2, 5);
        Assert.All(Enumerable.Range(0, rects.Count), index =>
        {
            var box = ReadRect(Assert.IsType<RecordValue>(rects[index]));
            Assert.True(box.Width > 0d);
        });
    }

    [Fact]
    public async Task Disposed_session_and_output_refuse_further_use()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(4, 4, new byte[16]);
        var session = new DivideImageTool().CreateSession();
        var outcome = await ExecuteAsync(session, new DivideImageSettings(), ImageInput(image), resources);
        Assert.True(outcome.Success);
        var output = outcome.Output!;
        output.Dispose();
        Assert.Throws<ObjectDisposedException>(() => output.ToValues());
        await session.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await ExecuteAsync(session, new DivideImageSettings(), ImageInput(image), resources));
    }
}
