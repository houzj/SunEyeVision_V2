using System.Collections.Concurrent;
using SunEyeVision.Plugin.SDK.Logging;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 验证 VisionLogger 多通道日志路由的正确性。
/// 核心测试：默认 target=All 时，所有通道的 writer 都能收到消息。
/// </summary>
public class VisionLoggerRoutingTests
{
    /// <summary>
    /// 内存日志写入器 - 替代 FileLogWriter/UILogWriter 等，用于验证路由。
    /// </summary>
    private sealed class InMemoryLogWriter : ILogWriter
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public string Name { get; }
        public string Description => $"In-memory writer for {Name}";
        public LogLevel MinLevel { get; set; } = LogLevel.Debug;
        public bool IsEnabled { get; set; } = true;
        public LogTarget Channel { get; }

        public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

        public InMemoryLogWriter(string name, LogTarget channel)
        {
            Name = name;
            Channel = channel;
        }

        public void Write(LogEntry entry) => _entries.Enqueue(entry);

        public void WriteBatch(IReadOnlyList<LogEntry> entries)
        {
            foreach (var entry in entries) _entries.Enqueue(entry);
        }
    }

    /// <summary>
    /// 清理 VisionLogger 上所有 writer，确保测试间互不干扰。
    /// </summary>
    private static void ClearWriters(VisionLogger logger)
    {
        foreach (var name in logger.GetWriterNames())
            logger.RemoveWriter(name);
    }

    /// <summary>
    /// 核心测试：PluginLogger.Info() 默认 target=All 时，File 通道的 writer 能收到消息。
    /// </summary>
    [Fact]
    public async Task DefaultTarget_All_ReachesFileWriter()
    {
        var logger = VisionLogger.Instance;
        ClearWriters(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        logger.AddWriter(fileWriter);

        // 使用默认 target（现在为 LogTarget.All）
        logger.Info("Hello from default target");

        // 等待异步队列处理
        await FlushAsync(logger);

        Assert.Single(fileWriter.Entries);
        Assert.Equal("Hello from default target", fileWriter.Entries[0].Message);
    }

    /// <summary>
    /// 验证 Logger.Info 使用默认 target 时，File/UI/Debug 多个通道的 writer 都能收到。
    /// </summary>
    [Fact]
    public async Task DefaultTarget_All_ReachesAllChannels()
    {
        var logger = VisionLogger.Instance;
        ClearWriters(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        var debugWriter = new InMemoryLogWriter("DebugWriter", LogTarget.Debug);
        var consoleWriter = new InMemoryLogWriter("ConsoleWriter", LogTarget.Console);
        logger.AddWriter(fileWriter);
        logger.AddWriter(debugWriter);
        logger.AddWriter(consoleWriter);

        logger.Info("Broadcast message");

        await FlushAsync(logger);

        Assert.Single(fileWriter.Entries);
        Assert.Equal("Broadcast message", fileWriter.Entries[0].Message);
        Assert.Single(debugWriter.Entries);
        Assert.Equal("Broadcast message", debugWriter.Entries[0].Message);
        Assert.Single(consoleWriter.Entries);
        Assert.Equal("Broadcast message", consoleWriter.Entries[0].Message);
    }

    /// <summary>
    /// 回归验证：显式 LogTarget.UI 时，File 通道的 writer 不应收到。
    /// </summary>
    [Fact]
    public async Task UiOnlyTarget_DoesNotReachFileWriter()
    {
        var logger = VisionLogger.Instance;
        ClearWriters(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        logger.AddWriter(fileWriter);

        // 显式指定 UI target（旧行为）
        logger.Info("UI only message", target: LogTarget.UI);

        await FlushAsync(logger);

        Assert.Empty(fileWriter.Entries);
    }

    /// <summary>
    /// 验证 PluginLogger 静态入口的默认 target 也正确路由到所有通道。
    /// </summary>
    [Fact]
    public async Task PluginLogger_DefaultTarget_ReachesAllChannels()
    {
        var logger = VisionLogger.Instance;
        ClearWriters(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        var debugWriter = new InMemoryLogWriter("DebugWriter", LogTarget.Debug);
        logger.AddWriter(fileWriter);
        logger.AddWriter(debugWriter);

        // 通过 PluginLogger 静态方法调用，走默认 target
        PluginLogger.SetProvider(logger);
        PluginLogger.Info("PluginLogger broadcast");

        await FlushAsync(logger);

        // VisionLogger.Instance 是进程级单例，并行测试类（如 SdkTypeVocabularyContractTests）
        // 反序列化全局变量时也会写日志到该单例，可能在本测试 300ms flush 窗口内泄漏进来。
        // 本测试意图是验证路由（广播消息到达 File + Debug），因此断言"目标消息已到达"而非精确条数。
        Assert.Contains(fileWriter.Entries, e => e.Message == "PluginLogger broadcast");
        Assert.Contains(debugWriter.Entries, e => e.Message == "PluginLogger broadcast");
    }

    /// <summary>
    /// 等待 VisionLogger 异步队列处理完成。
    /// </summary>
    private static async Task FlushAsync(VisionLogger logger)
    {
        await logger.FlushAsync();
        // 给后台队列处理线程额外时间完成批量写入
        await Task.Delay(300);
    }
}
