using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.ColorExtract.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>色彩提取执行语义：正常输出、边界/空输入、参数校验失败与面积筛选。</summary>
public sealed class ColorExtractExecutionTests
{
    private const int Size = 4;
    /// <summary>C2(G)=0、C3(B)=0 的纯红区间（原厂字段顺序：C1High,C1Low,C2High,C2Low,C3High,C3Low,...）。</summary>
    private const string RedOnly = "255,0,0,0,0,0,1,0,1";
    private const string GreenOnly = "0,0,255,0,0,0,1,0,1";

    private static readonly (byte R, byte G, byte B) Red = (255, 0, 0);
    private static readonly (byte R, byte G, byte B) Blue = (0, 0, 255);

    private static InputValues ImageInput(IReadOnlyImage image) => new(ColorExtractTool.Schema.Inputs,
        new Dictionary<string, object?> { [ColorExtractTool.ImageInput] = image });

    private static IToolExecutionContext Context(FrozenOutputResources resources, string nodeId = "color-extract-node")
    {
        var context = Substitute.For<IToolExecutionContext>();
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), nodeId, Guid.NewGuid()));
        context.Cancellation.Returns(CancellationToken.None);
        context.Resources.Returns(resources);
        return context;
    }

    private static byte[] Pixels(int width, int height, PixelLayout layout, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        var bytes = new byte[checked(width * height * 3)];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixel(x, y);
                var offset = (y * width + x) * 3;
                var leading = layout == PixelLayout.Rgb24 ? r : b;
                var trailing = layout == PixelLayout.Rgb24 ? b : r;
                bytes[offset] = leading; bytes[offset + 1] = g; bytes[offset + 2] = trailing;
            }
        return bytes;
    }

    private static async Task<ToolOutcome<ColorExtractOutput>> ExecuteAsync(IToolSession<ColorExtractSettings, ColorExtractOutput> session,
        ColorExtractSettings settings, IReadOnlyImage image, FrozenOutputResources resources)
        => await session.ExecuteAsync(new(settings, ImageInput(image), Context(resources)));

    private static IReadOnlyImage OutputImage(ColorExtractOutput output)
    {
        var record = (RecordValue)output.ToValues()[ColorExtractOutput.Image]!;
        return record.Get<IReadOnlyImage>(ColorExtractOutput.ImageData);
    }

    private sealed class TestImages : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        internal IReadOnlyImage Create(int width, int height, PixelLayout layout, byte[] pixels)
        {
            var frame = new ImageFrameAddress(Guid.NewGuid(), "color-extract-test");
            var coordinates = new CoordinateFrame("image:test", CoordinateDomain.Image, CoordinateUnit.Pixel);
            var runs = Enumerable.Range(0, height).Select(row => new RegionRun(row, 0, width - 1)).ToArray();
            var image = new FrozenImageResource(new ImageLayout(width, height, checked(width * 3), layout),
                frame, coordinates, pixels, runs);
            _owned.Add(image);
            return image;
        }

        public void Dispose()
        {
            foreach (var item in _owned) item.Dispose();
        }
    }

    [Fact]
    public async Task Rgb_range_extracts_matching_pixels_with_area_and_bounding_box()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        // 左上 2x2 纯红，其余纯蓝。
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (x, y) => x < 2 && y < 2 ? Red : Blue));
        await using var session = new ColorExtractTool().CreateSession();
        var outcome = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = RedOnly }, image, resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        Assert.Equal(4d, Assert.IsType<double>(values[ColorExtractOutput.TotalArea]));
        Assert.Equal(1L, Assert.IsType<long>(values[ColorExtractOutput.ModuleStatus]));
        var elapsed = Assert.IsType<double>(values[ColorExtractOutput.ModuleRunTime]);
        Assert.True(double.IsFinite(elapsed) && elapsed >= 0);
        Assert.Equal((double)(float)elapsed, elapsed);
        // 提取图像保留原彩色像素，域限定在提取区域（2 行各一段行程）。
        var extracted = OutputImage(output);
        Assert.Equal(Size, extracted.Width);
        Assert.Equal(Size, extracted.Height);
        Assert.Equal(PixelLayout.Rgb24, extracted.Layout.Format);
        using (var domain = extracted.EffectiveDomain.AcquireReader())
        {
            Assert.Equal(2, domain.RunCount);
            var runs = domain.ReadRuns(0, 2).ToArray();
            Assert.All(runs, run => Assert.Equal(0, run.FirstColumn));
            Assert.All(runs, run => Assert.Equal(1, run.LastColumnInclusive));
        }
        var identity = (RecordValue)values[ColorExtractOutput.Image]!;
        Assert.Equal(35127316L, identity.Get<long>(ColorExtractOutput.ImageFormat));
        Assert.Equal(ColorExtractOutput.Image, extracted.Frame.SourcePortId);
        Assert.NotNull(extracted.Frame.ProducerRun);
        var roi = (RecordValue)values[ColorExtractOutput.Roi]!;
        var center = roi.Get<PointGeometry>(ColorExtractOutput.RoiCenter);
        Assert.Equal(0.5, center.Point.X, 6);
        Assert.Equal(0.5, center.Point.Y, 6);
        // 平台既有外接矩形约定（Blob 检测矩形）：宽高 = HALCON smallest_rectangle2 半长 ×2，
        // 以像素中心为度量，故 2x2 像素块宽度为 1 而不是 2。
        Assert.Equal(1d, roi.Get<double>(ColorExtractOutput.RoiWidth));
        Assert.Equal(1d, roi.Get<double>(ColorExtractOutput.RoiHeight));
        Assert.True(Math.Abs(roi.Get<double>(ColorExtractOutput.RoiAngle)) <= 90);
    }

    [Fact]
    public async Task Hsv_range_uses_the_converted_channels()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (x, y) => x < 2 && y < 2 ? Red : Blue));
        await using var session = new ColorExtractTool().CreateSession();
        // 纯红在 HSV 中为 H=0,S=255,V=255；纯蓝 H=170 被排除。
        var outcome = await ExecuteAsync(session,
            new ColorExtractSettings { ColorRange = "10,0,255,200,255,200,2,0,1" }, image, resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(4d, Assert.IsType<double>(output.ToValues()[ColorExtractOutput.TotalArea]));
    }

    [Fact]
    public async Task Bgr24_input_keeps_the_input_channel_order()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        var image = images.Create(Size, Size, PixelLayout.Bgr24,
            Pixels(Size, Size, PixelLayout.Bgr24, (x, y) => x < 2 && y < 2 ? Red : Blue));
        await using var session = new ColorExtractTool().CreateSession();
        var outcome = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = RedOnly }, image, resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        Assert.Equal(4d, Assert.IsType<double>(values[ColorExtractOutput.TotalArea]));
        Assert.Equal(PixelLayout.Bgr24, OutputImage(output).Layout.Format);
        // BGR24 没有对应的原厂像素格式码：按平台约定报"不可用"而不是补 0。
        Assert.Null(((RecordValue)values[ColorExtractOutput.Image]!).CopyFields()[ColorExtractOutput.ImageFormat]);
    }

    [Fact]
    public async Task No_matching_pixel_reports_zero_area_and_no_rectangle()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (_, _) => Blue));
        await using var session = new ColorExtractTool().CreateSession();
        var outcome = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = GreenOnly }, image, resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        var values = output.ToValues();
        Assert.Equal(0d, Assert.IsType<double>(values[ColorExtractOutput.TotalArea]));
        Assert.Null(values[ColorExtractOutput.Roi]);
        var extracted = OutputImage(output);
        using var domain = extracted.EffectiveDomain.AcquireReader();
        Assert.True(domain.IsEmpty);
        Assert.Equal(0, domain.RunCount);
    }

    [Fact]
    public async Task Area_limit_keeps_only_components_inside_the_declared_range()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        // 左上 2x2 红块 + 右下单像素红点：不限面积 5，限 2..100 只保留红块，限 10..100 全部剔除。
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (x, y) => (x < 2 && y < 2) || (x == 3 && y == 3) ? Red : Blue));
        await using var session = new ColorExtractTool().CreateSession();
        var unlimited = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = RedOnly }, image, resources);
        Assert.True(unlimited.Success, string.Join(";", unlimited.Diagnostics.Select(item => item.Message)));
        using (var output = unlimited.Output!)
            Assert.Equal(5d, Assert.IsType<double>(output.ToValues()[ColorExtractOutput.TotalArea]));
        var limited = await ExecuteAsync(session,
            new ColorExtractSettings { ColorRange = RedOnly, AreaLimitEnabled = true, AreaLimitLow = 2, AreaLimitHigh = 100 },
            image, resources);
        Assert.True(limited.Success, string.Join(";", limited.Diagnostics.Select(item => item.Message)));
        using (var output = limited.Output!)
            Assert.Equal(4d, Assert.IsType<double>(output.ToValues()[ColorExtractOutput.TotalArea]));
        var rejected = await ExecuteAsync(session,
            new ColorExtractSettings { ColorRange = RedOnly, AreaLimitEnabled = true, AreaLimitLow = 10, AreaLimitHigh = 100 },
            image, resources);
        Assert.True(rejected.Success, string.Join(";", rejected.Diagnostics.Select(item => item.Message)));
        using (var output = rejected.Output!)
        {
            var values = output.ToValues();
            Assert.Equal(0d, Assert.IsType<double>(values[ColorExtractOutput.TotalArea]));
            Assert.Null(values[ColorExtractOutput.Roi]);
        }
    }

    [Fact]
    public async Task Inverted_range_selects_pixels_outside_the_band()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (x, y) => x < 2 && y < 2 ? Red : Blue));
        await using var session = new ColorExtractTool().CreateSession();
        // 反色标志（第 8 项）=1：取红区间在图像域内的补集，即其余 12 个蓝色像素。
        var outcome = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = "255,0,0,0,0,0,1,1,1" }, image, resources);
        Assert.True(outcome.Success, string.Join(";", outcome.Diagnostics.Select(item => item.Message)));
        using var output = outcome.Output!;
        Assert.Equal(12d, Assert.IsType<double>(output.ToValues()[ColorExtractOutput.TotalArea]));
    }

    [Fact]
    public async Task Unchecked_ranges_fail_explicitly_instead_of_emitting_an_empty_result()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (_, _) => Red));
        await using var session = new ColorExtractTool().CreateSession();
        var outcome = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = "255,0,255,0,255,0,1,0,0" }, image, resources);
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        Assert.Equal("COLOR_EXTRACT_RANGE_DISABLED", Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public async Task Non_color_image_is_rejected_with_the_original_message()
    {
        using var resources = new FrozenOutputResources();
        await using var session = new ColorExtractTool().CreateSession();
        var gray = resources.CreateGray8(Size, Size, new byte[Size * Size]);
        var outcome = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = RedOnly }, gray, resources);
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        var diagnostic = Assert.Single(outcome.Diagnostics);
        Assert.Equal("COLOR_EXTRACT_PIXEL_FORMAT", diagnostic.Code);
        Assert.Contains("图像格式不正确或者不支持", diagnostic.Message);
    }

    [Fact]
    public async Task Invalid_settings_are_explicit_failures()
    {
        using var resources = new FrozenOutputResources();
        using var images = new TestImages();
        var image = images.Create(Size, Size, PixelLayout.Rgb24,
            Pixels(Size, Size, PixelLayout.Rgb24, (_, _) => Red));
        await using var session = new ColorExtractTool().CreateSession();
        var invalid = await ExecuteAsync(session, new ColorExtractSettings { ColorRange = "255,0,255" }, image, resources);
        Assert.False(invalid.Success);
        Assert.Null(invalid.Output);
        Assert.Equal("COLOR_EXTRACT_RANGE_INVALID", Assert.Single(invalid.Diagnostics).Code);
    }
}
