using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// 真实样本并发解码诊断测试。
/// 目标：用真实 BMP 样本（15MB 2448x2048，标准 24 位）模拟 LoadThumbnail 的并发解码路径，
/// 复现真实"红叉"并捕获真实异常类型，追溯根因。
/// </summary>
public class RealSampleConcurrencyTests
{
    private const string SampleDir = @"D:\公司\项目\N101 焊锡外观检测\焊点外观已训练\1";

    private static IEnumerable<string> GetSamples(int maxCount)
    {
        if (!Directory.Exists(SampleDir))
            yield break;
        var count = 0;
        foreach (var f in Directory.EnumerateFiles(SampleDir, "*.bmp"))
        {
            if (count++ >= maxCount) yield break;
            yield return f;
        }
    }

    /// <summary>
    /// 单张解码基线：确认真实样本单张 WIC 能解（排除格式变种问题）。
    /// </summary>
    [Fact]
    public void Single_sample_decodes_with_wic()
    {
        if (!Directory.Exists(SampleDir)) return;   // 无真实样本，跳过
        var first = GetSamples(1).FirstOrDefault();
        Assert.NotNull(first);

        var bmp = DecodeThumbnail(first!);
        Assert.NotNull(bmp);
        Assert.True(bmp.PixelWidth > 0);
        Assert.True(bmp.IsFrozen);
    }

    /// <summary>
    /// 并发解码压力：一批真实样本并发 WIC 解码，统计失败数并捕获异常类型。
    /// 这是复现真实红叉的核心用例。
    /// </summary>
    [Fact]
    public void Concurrent_decode_stress_reproduces_failures()
    {
        if (!Directory.Exists(SampleDir)) return;   // 无真实样本，跳过
        var samples = GetSamples(200).ToList();
        Assert.True(samples.Count >= 50, $"样本不足: {samples.Count}");

        // 收集并发解码结果
        var failures = new List<(string file, Exception ex)>();
        var okCount = 0;
        var throttle = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount - 1, 4, 16));

        Parallel.ForEach(samples, new ParallelOptions { MaxDegreeOfParallelism = 16 }, path =>
        {
            throttle.Wait();
            try
            {
                var bmp = DecodeThumbnail(path);
                if (bmp != null)
                    Interlocked.Increment(ref okCount);
                else
                    lock (failures)
                        failures.Add((path, new InvalidOperationException("DecodeThumbnail returned null")));
            }
            catch (Exception ex)
            {
                lock (failures)
                    failures.Add((path, ex));
            }
            finally
            {
                throttle.Release();
            }
        });

        // 报告失败文件名 + 异常（用于定位是否固定文件 vs 并发偶发）
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{samples.Count} 解码失败. 成功 {okCount}. " +
            string.Join(" || ", failures.Take(20).Select(f => $"[{Path.GetFileName(f.file)}] {f.ex.GetType().Name}: {f.ex.Message}")));
    }

    /// <summary>
    /// 模拟 LoadThumbnail 的 DecodeThumbnailCore：WIC UriSource + DecodePixelWidth + OnLoad + Freeze。
    /// 与真实代码路径一致。
    /// </summary>
    private static BitmapImage? DecodeThumbnail(string path)
    {
        var src = new BitmapImage();
        src.BeginInit();
        src.CacheOption = BitmapCacheOption.OnLoad;
        src.DecodePixelWidth = 100;
        src.UriSource = new Uri(path);
        src.EndInit();
        src.Freeze();
        return src;
    }
}
