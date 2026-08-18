using System.IO;
using System.Threading;
using Xunit;
using SunEyeVision.UI.Shell.Controls.Panels;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// 降级兜底有效性测试。
/// 验证：WIC 并发偶发 NotSupportedException 时，LoadThumbnail 的 System.Drawing 降级能否救回真实样本。
/// 这是对真实红叉根因（并发 NotSupportedException）的治本验证。
/// </summary>
public class FallbackEffectivenessTests
{
    private const string SampleDir = @"D:\公司\项目\N101 焊锡外观检测\焊点外观已训练\1";

    /// <summary>
    /// 用真实样本并发调用 LoadThumbnail（含降级），验证是否全部能解出（无红叉）。
    /// 若降级有效，即使 WIC 偶发失败，System.Drawing 也能兜底返回非 null。
    /// </summary>
    [Fact]
    public void LoadThumbnail_concurrent_all_recover_via_fallback()
    {
        if (!Directory.Exists(SampleDir)) return;   // 无真实样本，跳过
        var samples = Directory.EnumerateFiles(SampleDir, "*.bmp").Take(200).ToList();
        Assert.True(samples.Count >= 50);

        var results = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        var throttle = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount - 1, 4, 16));

        Parallel.ForEach(samples, new ParallelOptions { MaxDegreeOfParallelism = 16 }, path =>
        {
            throttle.Wait();
            try
            {
                var bmp = ImagePreviewControl.LoadThumbnail(path, CancellationToken.None);
                results[path] = bmp != null;
            }
            finally
            {
                throttle.Release();
            }
        });

        var failed = results.Where(kv => !kv.Value).ToList();
        Assert.True(failed.Count == 0,
            $"{failed.Count}/{results.Count} 个样本经降级后仍失败: " +
            string.Join(" | ", failed.Take(10).Select(kv => Path.GetFileName(kv.Key))));
    }
}
