using SunEyeVision.UI.Features.Camera;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CameraCategoryLocalizationTests
{
    [Theory]
    [InlineData("Analog Controls", "模拟控制")]
    [InlineData("Image Format Controls", "图像格式")]
    [InlineData("AOI Controls", "感兴趣区域 (AOI)")]
    [InlineData("Acquisition Controls", "采集控制")]
    [InlineData("Digital I／O Controls", "数字 I/O 控制")]
    [InlineData("Counter and Timer Controls", "计数器与定时器")]
    [InlineData("Basler Light Control", "巴斯勒光源控制")]
    [InlineData("Chunk Data Streams", "附加数据流")]
    [InlineData("Auto Functions", "自动功能")]
    [InlineData("Device Control", "设备控制")]
    public void NativeCategoryNamesUseChineseLabels(string nativeName, string expected)
    {
        Assert.Equal(expected, CameraDetailViewModel.LocalizeCategory(nativeName));
    }

    [Fact]
    public void UnmappedVendorCategoryIsPreserved()
    {
        Assert.Equal("Vendor Specific Features", CameraDetailViewModel.LocalizeCategory("Vendor Specific Features"));
    }
}
