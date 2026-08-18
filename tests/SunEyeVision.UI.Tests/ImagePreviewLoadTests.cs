using System.IO;
using System.Threading;
using Xunit;
using SunEyeVision.UI.Shell.Controls.Panels;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// ImagePreviewControl.LoadThumbnail 行为测试。
/// 覆盖：正常解码 / 取消 / 缓存命中 / 文件不存在。
/// 注：降级(System.Drawing)路径因真实样本均为标准 BMP、WIC 可直接解，且构造的 RLE4 样本 GDI+ 亦解不了，
/// 故"降级成功"无真实样本可测，由 RealSampleConcurrencyTests 的并发诊断 + 日志追溯补充。
/// </summary>
public class ImagePreviewLoadTests
{
    // 真实样本目录（本机验证用；其他机器无此目录时相关测试自动跳过，不失败）
    private const string SampleDir = @"D:\公司\项目\N101 焊锡外观检测\焊点外观已训练\1";

    [Fact]
    public void LoadThumbnail_decodes_real_sample()
    {
        if (!Directory.Exists(SampleDir)) return;   // 无真实样本，跳过
        var file = Directory.EnumerateFiles(SampleDir, "*.bmp").First();
        var bmp = ImagePreviewControl.LoadThumbnail(file, CancellationToken.None);

        Assert.NotNull(bmp);
        Assert.True(bmp.PixelWidth > 0);
        Assert.True(bmp.IsFrozen);
    }

    [Fact]
    public void LoadThumbnail_cancelled_token_returns_null_immediately()
    {
        if (!Directory.Exists(SampleDir)) return;   // 无真实样本，跳过
        // 取第二个样本，避免命中第一个样本被前面测试写入的共享缓存（s_thumbnailCache 是 static 跨测试共享）
        var file = Directory.EnumerateFiles(SampleDir, "*.bmp").Skip(1).First();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var bmp = ImagePreviewControl.LoadThumbnail(file, cts.Token);

        Assert.Null(bmp);
    }

    [Fact]
    public void LoadThumbnail_nonexistent_file_returns_null()
    {
        var bmp = ImagePreviewControl.LoadThumbnail(@"D:\nonexistent\file.bmp", CancellationToken.None);
        Assert.Null(bmp);
    }

    [Fact]
    public void LoadThumbnail_returns_cached_result_on_second_call()
    {
        if (!Directory.Exists(SampleDir)) return;   // 无真实样本，跳过
        var file = Directory.EnumerateFiles(SampleDir, "*.bmp").First();

        var first = ImagePreviewControl.LoadThumbnail(file, CancellationToken.None);
        var second = ImagePreviewControl.LoadThumbnail(file, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);   // 缓存命中，同一实例
    }
}
