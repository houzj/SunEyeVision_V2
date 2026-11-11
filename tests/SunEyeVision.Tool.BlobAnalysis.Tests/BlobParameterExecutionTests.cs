using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>填充面积、直方图与内接矩形的生产算法和结果声明。</summary>
public sealed class BlobParameterExecutionTests
{
    private const int Size = 9;

    /// <summary>9×9 图中央 5×5 白色方块（25 像素），holes 指定被挖空的位置。</summary>
    private static byte[] Block(params (int X, int Y)[] holes)
    {
        var pixels = new byte[Size * Size];
        for (int y = 2; y <= 6; y++)
            for (int x = 2; x <= 6; x++)
                pixels[y * Size + x] = 255;
        foreach (var (x, y) in holes) pixels[y * Size + x] = 0;
        return pixels;
    }

    private static BlobSettings WithHoleMinArea(int value)
    {
        var settings = new BlobSettings();
        return settings with { FindParameters = settings.FindParameters with { HoleMinArea = value } };
    }

    private static BlobSettings WithHistInfo(bool enabled)
    {
        var settings = new BlobSettings();
        return settings with { FindParameters = settings.FindParameters with { SelectByHistInfo = enabled } };
    }

    private static string CodeOf(InvalidOperationException exception)
        => (string)exception.GetType()
            .GetProperty("Code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(exception)!;

    private sealed class Run : IDisposable
    {
        private readonly FrozenOutputResources _resources;
        private readonly IReadOnlyImage _image;
        internal readonly BlobOutput Output;

        internal Run(BlobSettings settings, byte[] pixels)
        {
            _resources = new FrozenOutputResources();
            try
            {
                _image = _resources.CreateGray8(Size, Size, pixels);
                Output = new BlobSession().Execute(settings, _image, _resources);
            }
            catch
            {
                _resources.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Output.Dispose();
            _resources.Dispose();
        }
    }

    [Fact]
    public void HoleMinArea_zero_keeps_inner_hole()
    {
        using var run = new Run(new BlobSettings(), Block((4, 4)));
        var blob = Assert.Single(run.Output.Measurements);
        Assert.Equal(24d, blob.Area);
        Assert.DoesNotContain(run.Output.ExecutionDiagnostics, x => x.Code == "BLOB_HOLE_FILL_LOCAL");
    }

    [Fact]
    public void HoleMinArea_fills_inner_hole_within_threshold()
    {
        using var run = new Run(WithHoleMinArea(1), Block((4, 4)));
        var blob = Assert.Single(run.Output.Measurements);
        Assert.Equal(25d, blob.Area);
        Assert.Contains(run.Output.ExecutionDiagnostics, x => x.Code == "BLOB_HOLE_FILL_LOCAL");
    }

    [Fact]
    public void HoleMinArea_does_not_fill_gap_connected_to_border()
    {
        // (4,2) 起向左连到图像背景，属开口而非孔洞，填充面积阈值不应对其生效。
        using var run = new Run(WithHoleMinArea(100), Block((4, 2), (4, 3), (4, 4)));
        var blob = Assert.Single(run.Output.Measurements);
        Assert.Equal(22d, blob.Area);
    }

    [Fact]
    public async Task Hole_fill_keeps_excluded_mask_pixels_and_openings_out_of_the_retained_blob()
    {
        using var resources=new FrozenOutputResources();
        await using var session=new BlobSession();
        var pixels=Block((3,3),(5,5));
        var maskPixels=Enumerable.Repeat((byte)255,pixels.Length).ToArray();
        var excluded=4*Size+3;
        maskPixels[excluded]=0;
        var image=resources.CreateGray8(Size,Size,pixels);
        var mask=resources.CreateGray8(Size,Size,maskPixels);
        using var result=session.Execute(WithHoleMinArea(2) with { OutputBinaryImage=true, OutputBlobImage=true },
            new BlobInput(image,null,null,null,ImageMask:mask),resources);
        var expectedBinary=pixels.ToArray();
        expectedBinary[excluded]=0;
        var expectedBlob=expectedBinary.ToArray();
        expectedBlob[5*Size+5]=255;
        Assert.Equal(expectedBinary,result.BinaryImage!.CopyPixels());
        Assert.Equal(expectedBlob,result.BlobImage!.CopyPixels());
        Assert.Equal(23d,Assert.Single(result.Measurements).Area);
    }

    [Fact]
    public void SelectByHistInfo_gates_histogram_without_changing_white_ratio()
    {
        using var off = new Run(WithHistInfo(false), Block());
        using var on = new Run(WithHistInfo(true), Block());
        Assert.Empty(off.Output.Histogram);
        Assert.Empty(off.Output.ChannelMeans);
        Assert.Equal(256, on.Output.Histogram.Count);
        Assert.Single(on.Output.ChannelMeans);
        Assert.Equal(off.Output.WhitePixelRatio, on.Output.WhitePixelRatio);
    }

    [Fact]
    public void Inner_rectangle_is_optional_and_matches_the_region_and_display_row()
    {
        var pixels = Block((5,4),(6,4),(5,5),(6,5),(5,6),(6,6));
        var settings = new BlobSettings();
        using var off = new Run(settings, pixels);
        Assert.Empty(off.Output.ResultProjection.MaxAreaInnerRect);
        using var on = new Run(settings with { FindParameters = settings.FindParameters with { MaxAreaInnerRectNum = 1 } }, pixels);
        Assert.Equal(new BlobOrientedBounds(3,4,3,5,0), Assert.Single(on.Output.ResultProjection.MaxAreaInnerRect));
        var values = new InputValues(BlobSchema.Outputs,on.Output.ToValues());
        var rectangle = values.Read(BlobSchema.InnerRectanglesPort).GetElement(0);
        Assert.Equal(3d,rectangle.GetField("center").GetField("x").GetScalar<double>());
        Assert.Equal(5d,rectangle.GetField("height").GetScalar<double>());
        var geometry = Assert.IsType<OrientedRectangleGeometry>(
            values.Read(BlobSchema.BlobsPort).GetElement(0).GetField("inner-rectangle").GetGeometry());
        Assert.Equal(new Point2(3,4),geometry.Center);
        Assert.Equal(3d,geometry.Width);
        Assert.Equal(5d,geometry.Height);
        BlobResultDisplay.Descriptor.Validate(new BlobTool().ResultSchema);
        var styleId = BlobResultDisplay.Field(BlobResultDisplay.InnerRectangle, "visible");
        var display = new FieldSettings(BlobResultDisplay.Schema.Inputs,
            BlobResultDisplay.Defaults().CopyValues().ToDictionary(item => item.Key, item => (ParameterSetting)new ConstantSetting(item.Value)));
        var appearance = BlobResultDisplay.Capture(display);
        Assert.True(appearance.InnerRectangle!.Visible);
        Assert.Equal(2d,appearance.InnerRectangle.Width);
        var hidden = BlobResultDisplay.Capture(display.Apply(new Dictionary<string, ParameterSetting> { [styleId] = new ConstantSetting(false) }));
        Assert.False(hidden.InnerRectangle!.Visible);
        Assert.True(hidden.Graphic.Visible);
    }

    [Fact]
    public void Inner_rectangle_limit_follows_ordered_retained_blobs()
    {
        var pixels = Block();
        for (var y = 2; y <= 6; y++) pixels[y * Size + 8] = 255;
        var settings = new BlobSettings
        {
            IsAreaFilterEnabled = false,
            SortMode = BlobSortMode.SortModeAscend,
            FindParameters = new BlobFindParameters { MaxAreaInnerRectNum = 1 }
        };
        using var run = new Run(settings, pixels);
        Assert.Equal(2,run.Output.Measurements.Count);
        Assert.Equal(new BlobOrientedBounds(8,4,1,5,0),Assert.Single(run.Output.ResultProjection.MaxAreaInnerRect));
        Assert.Null(run.Output.Measurements[1].InnerRectangle);
    }

    [Theory]
    [InlineData(12d,12d)]
    [InlineData(12.25d,10d)]
    public void Text_font_size_keeps_two_digit_editing_and_uses_native_integer_rendering(double configured,double rendered)
    {
        var id = BlobResultDisplay.Field(BlobResultDisplay.Text, "size");
        var fields = new FieldSettings(BlobResultDisplay.Schema.Inputs,
            BlobResultDisplay.Defaults().CopyValues().ToDictionary(item => item.Key, item => (ParameterSetting)new ConstantSetting(item.Value)));
        fields = fields.Apply(new Dictionary<string, ParameterSetting> { [id] = new ConstantSetting(configured) });
        Assert.Equal(2,BlobResultDisplay.Schema.Presentation!.Fields.Single(field=>field.FieldId==id).Layout!.DecimalPlaces);
        Assert.Equal(configured,fields.Get<double>(id));
        var display = BlobResultDisplay.Capture(fields);
        Assert.Equal(rendered,display.Text.FontSize);
        Assert.Equal("Blob面积:{面积}",display.Text.Content);
        Assert.Equal(1d,display.Text.Opacity);
        Assert.Equal(1d,display.ReferencePoint!.Width);
        Assert.Equal(1d,display.RunPoint!.Width);
    }

    [Fact]
    public void Roi_display_styles_declare_original_fill_judgment_and_measurement_tips()
    {
        var fields = new FieldSettings(BlobResultDisplay.Schema.Inputs,
            BlobResultDisplay.Defaults().CopyValues().ToDictionary(item => item.Key, item => (ParameterSetting)new ConstantSetting(item.Value)));
        var appearance = BlobResultDisplay.Capture(fields);
        Assert.Equal(new ResultDisplayColor(255, 84, 0), appearance.Mask!.Fill);
        Assert.Null(appearance.Mask.FixedJudgment);
        Assert.False(appearance.Mask.ShowMeasurements);
        Assert.True(appearance.Inspection!.ShowMeasurements);
        Assert.True(appearance.Annulus!.ShowMeasurements);
        Assert.True(appearance.ReferencePoint!.FixedJudgment);
        Assert.Equal("Fixture BasePoint", appearance.ReferencePoint.PointLabel);
        Assert.True(appearance.RunPoint!.FixedJudgment);
        Assert.Equal("Fixture RunPoint", appearance.RunPoint.PointLabel);
    }

    [Fact]
    public void Full_axis_lengths_are_shared_by_filter_judgment_and_result_row()
    {
        var settings = new BlobSettings();
        settings = settings with { FindParameters = settings.FindParameters with
        {
            SelectByLongAxis = true, MinLongAxis = 5, MaxLongAxis = 5,
            SelectByShortAxis = true, MinShortAxis = 3, MaxShortAxis = 3,
            LongAxisLimitEnable = true, LongAxisLimitLow = 5, LongAxisLimitHigh = 5,
            ShortAxisLimitEnable = true, ShortAxisLimitLow = 3, ShortAxisLimitHigh = 3
        } };
        var pixels = new byte[Size * Size];
        for (int y = 3; y < 6; y++)
        for (int x = 2; x < 7; x++)
            pixels[y * Size + x] = 255;
        using var run = new Run(settings, pixels);
        var measured = Assert.Single(run.Output.Measurements);
        Assert.True(measured.JudgmentPassed);
        Assert.Equal(5f, Assert.Single(run.Output.ResultProjection.LongAxis));
        Assert.Equal(3f, Assert.Single(run.Output.ResultProjection.ShortAxis));
        var row = BlobSchema.Freeze(measured);
        Assert.Equal(5d, row.Get<double>("long-axis"));
        Assert.Equal(3d, row.Get<double>("short-axis"));
    }

    [Theory]
    [InlineData("image")]
    [InlineData("roi")]
    [InlineData("mask")]
    public async Task Outside_pixel_limit_filters_boundary_contacts_before_blob_image_publication(string boundary)
    {
        const int width = 12, height = 9;
        using var resources = new FrozenOutputResources();
        await using var session = new BlobSession();
        var pixels = new byte[width * height];
        var left = boundary == "image" ? 0 : 2;
        var touching = new[] { 3 * width + left, 4 * width + left, 3 * width + left + 1 };
        var interior = new[] { 4 * width + 8, 5 * width + 8, 4 * width + 9 };
        foreach (var index in touching.Concat(interior)) pixels[index] = 255;
        var image = resources.CreateGray8(width, height, pixels);
        var inspection = boundary == "roi" ? resources.CreateRegion(BlobSchema.ImageFrame, image.Frame,
            Enumerable.Range(1, height - 2).Select(row => new RegionRun(row, left, width - 2)).ToArray()) : null;
        var mask = boundary == "mask" ? resources.CreateGray8(width, height,
            Enumerable.Range(0, pixels.Length).Select(index => index % width >= left ? (byte)255 : (byte)0).ToArray()) : null;
        var input = new BlobInput(image, inspection, null, null, ImageMask: mask);
        var settings = new BlobSettings { IsAreaFilterEnabled = false, OutputBlobImage = true, OutputBinaryImage = true };
        foreach (var (enabled, limit, remove) in new[] { (false, 0, false), (true, 0, true), (true, 1, true), (true, 2, false) })
        {
            using var output = session.Execute(settings with
            {
                FindParameters = settings.FindParameters with { MaxOutPixelNumEnable = enabled, MaxOutPixelNum = limit }
            }, input, resources);
            Assert.Equal(remove ? 1 : 2, output.Count);
            Assert.Equal(pixels, output.BinaryImage!.CopyPixels());
            var expected = pixels.ToArray();
            if (remove)
                foreach (var index in touching) expected[index] = 0;
            Assert.Equal(expected, output.BlobImage!.CopyPixels());
        }
    }

    [Fact]
    public void Original_overlap_field_roundtrips_without_altering_candidate_selection()
    {
        var definition = BlobParameterDefinition.Instance;
        var settings = new BlobSettings();
        var configured = settings with { FindParameters = settings.FindParameters with { OverlapRatio = 100 } };
        var materialized = definition.MaterializeSettings(definition.ReadSettings(configured));
        Assert.Equal(100, materialized.ToFindParameters().OverlapRatio);

        var pixels = Block();
        using var baseline = new Run(settings, pixels);
        using var actual = new Run(materialized, pixels);
        Assert.Equal(Assert.Single(baseline.Output.Measurements).Area, Assert.Single(actual.Output.Measurements).Area);
        Assert.Equal(baseline.Output.BlobImage!.CopyPixels(), actual.Output.BlobImage!.CopyPixels());
        Assert.Equal(baseline.Output.BinaryImage!.CopyPixels(), actual.Output.BinaryImage!.CopyPixels());
    }

    [Fact]
    public void Out_of_range_closure_values_are_settings_invalid()
    {
        foreach (var settings in new[]
                 {
                     new BlobSettings { FindParameters = new BlobFindParameters { HoleMinArea = -1 } },
                     new BlobSettings { FindParameters = new BlobFindParameters { MaxAreaInnerRectNum = 11 } },
                     new BlobSettings { FindParameters = new BlobFindParameters { OverlapRatio = 101 } },
                     new BlobSettings { FindParameters = new BlobFindParameters { MaxOutPixelNum = 9000001 } },
                 })
        {
            Assert.Contains(settings.Validate(), x => x.Code == "BLOB_SETTINGS_INVALID");
            var exception = Assert.ThrowsAny<InvalidOperationException>(() => new Run(settings, Block()));
            Assert.Equal("BLOB_SETTINGS_INVALID", CodeOf(exception));
        }
    }
}
