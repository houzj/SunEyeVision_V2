using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>B3-1：填充面积阈值与直方图开关的真实执行，以及最大内接矩形族的不可误用占位。</summary>
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
        return settings with { VmParameters = settings.VmParameters with { HoleMinArea = value } };
    }

    private static BlobSettings WithHistInfo(bool enabled)
    {
        var settings = new BlobSettings();
        return settings with { VmParameters = settings.VmParameters with { SelectByHistInfo = enabled } };
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
    public void Inner_rect_family_is_rejected_when_enabled()
    {
        var settings = new BlobSettings();
        var enabled = settings with { VmParameters = settings.VmParameters with { MaxAreaInnerRectNum = 1 } };
        var exception = Assert.ThrowsAny<InvalidOperationException>(() => new Run(enabled, Block()));
        Assert.Equal("BLOB_INNER_RECT_NOT_READY", CodeOf(exception));
        Assert.Contains("尚未取得原厂产出链证据", exception.Message);
    }

    [Fact]
    public void Out_of_range_closure_values_are_settings_invalid()
    {
        foreach (var settings in new[]
                 {
                     new BlobSettings { VmParameters = new BlobVmFindParameters { HoleMinArea = -1 } },
                     new BlobSettings { VmParameters = new BlobVmFindParameters { MaxAreaInnerRectNum = 11 } },
                     new BlobSettings { VmParameters = new BlobVmFindParameters { OverlapRatio = 101 } },
                     new BlobSettings { VmParameters = new BlobVmFindParameters { MaxOutPixelNum = 9000001 } },
                 })
        {
            Assert.Contains(settings.Validate(), x => x.Code == "BLOB_SETTINGS_INVALID");
            var exception = Assert.ThrowsAny<InvalidOperationException>(() => new Run(settings, Block()));
            Assert.Equal("BLOB_SETTINGS_INVALID", CodeOf(exception));
        }
    }
}
