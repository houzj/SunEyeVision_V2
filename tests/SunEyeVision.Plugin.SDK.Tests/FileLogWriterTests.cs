using System;
using System.IO;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.SDK.Logging;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 验证 FileLogWriter 按 年/月/日 目录分层 + 时分秒文件名的落盘行为。
/// 每个测试使用独立临时目录，避免污染真实日志目录。
/// </summary>
public class FileLogWriterTests
{
    private static string CreateTempLogRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SunEyeVision_FileLogWriterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 写入单条日志后，文件应落在 logs/年/月/日/ 子目录，文件名带时分秒，扩展名为 .txt。
    /// </summary>
    [Fact]
    public void Write_ProducesNestedYearMonthDayFile()
    {
        var root = CreateTempLogRoot();
        try
        {
            var writer = new FileLogWriter(root);
            var now = DateTime.Now;

            writer.Write(new LogEntry
            {
                Message = "双轨制文件落盘测试",
                Level = LogLevel.Info,
                Source = "FileLogWriterTests",
                Timestamp = DateTime.Now,
                Target = LogTarget.File,
            });
            writer.Flush();
            writer.Dispose(); // 关闭文件流以便后续读取内容

            var todayDir = Path.Combine(root, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
            Assert.True(Directory.Exists(todayDir), $"应创建 年/月/日 子目录: {todayDir}");

            var file = Assert.Single(Directory.GetFiles(todayDir, "SunEyeVision_*.txt"));
            Assert.Equal(".txt", Path.GetExtension(file));
            Assert.Matches(@"SunEyeVision_\d{6}\.txt", Path.GetFileName(file));

            var content = File.ReadAllText(file);
            Assert.Contains("双轨制文件落盘测试", content);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 同一进程内连续写入（同一天）应复用同一文件，不产生多个文件。
    /// </summary>
    [Fact]
    public void Write_SameDay_ReusesSameFile()
    {
        var root = CreateTempLogRoot();
        try
        {
            var writer = new FileLogWriter(root);

            writer.Write(new LogEntry
            {
                Message = "第一条",
                Level = LogLevel.Info,
                Source = "T",
                Timestamp = DateTime.Now,
                Target = LogTarget.File,
            });
            writer.Flush();
            var first = writer.CurrentLogFile;

            writer.Write(new LogEntry
            {
                Message = "第二条",
                Level = LogLevel.Info,
                Source = "T",
                Timestamp = DateTime.Now,
                Target = LogTarget.File,
            });
            writer.Flush();
            var second = writer.CurrentLogFile;

            Assert.NotNull(first);
            Assert.Equal(first, second);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch
        {
            // 清理失败不影响测试结论
        }
    }
}
