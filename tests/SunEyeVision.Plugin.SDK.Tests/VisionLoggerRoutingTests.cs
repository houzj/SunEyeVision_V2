using System.Collections.Concurrent;
using SunEyeVision.Plugin.SDK.Logging;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 验证 VisionLogger 多通道日志路由的正确性。
/// 核心测试：默认 target=All 时，所有通道的 writer 都能收到消息。
///
/// 隔离说明：VisionLogger.Instance 是进程级单例 + 异步队列，xUnit 默认不同测试类并行执行，
/// 其他测试类（如 SdkTypeVocabularyContractTests 反序列化全局变量）也会写日志到该单例，
/// 可能在本测试 flush 窗口内泄漏进来。因此本类所有断言都基于"本测试发出的唯一标记消息"，
/// 用 Contains / DoesNotContain 验证路由到达情况，而不依赖精确条数或队列为空。
/// 本类置于独占集合 LoggerRouting，保证内部 4 个测试串行、互不重置 writer。
/// </summary>
[Collection("LoggerRouting")]
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
    /// 每个测试前置：移除所有 writer 并排空历史队列，避免残留消息干扰路由验证。
    /// </summary>
    private static async Task ResetLoggerAsync(VisionLogger logger)
    {
        ClearWriters(logger);
        await FlushAsync(logger);
    }

    /// <summary>
    /// 核心测试：PluginLogger.Info() 默认 target=All 时，File 通道的 writer 能收到消息。
    /// </summary>
    [Fact]
    public async Task DefaultTarget_All_ReachesFileWriter()
    {
        var logger = VisionLogger.Instance;
        await ResetLoggerAsync(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        logger.AddWriter(fileWriter);

        // 使用默认 target（现在为 LogTarget.All）
        logger.Info("Hello from default target");

        // 等待异步队列处理
        await FlushAsync(logger);

        Assert.Contains(fileWriter.Entries, e => e.Message == "Hello from default target");
    }

    /// <summary>
    /// 验证 Logger.Info 使用默认 target 时，File/UI 双轨通道的 writer 都能收到。
    /// </summary>
    [Fact]
    public async Task DefaultTarget_All_ReachesAllChannels()
    {
        var logger = VisionLogger.Instance;
        await ResetLoggerAsync(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        var uiWriter = new InMemoryLogWriter("UIWriter", LogTarget.UI);
        logger.AddWriter(fileWriter);
        logger.AddWriter(uiWriter);

        logger.Info("Broadcast message");

        await FlushAsync(logger);

        Assert.Contains(fileWriter.Entries, e => e.Message == "Broadcast message");
        Assert.Contains(uiWriter.Entries, e => e.Message == "Broadcast message");
    }

    /// <summary>
    /// 回归验证：显式 LogTarget.UI 时，File 通道的 writer 不应收到。
    /// </summary>
    [Fact]
    public async Task UiOnlyTarget_DoesNotReachFileWriter()
    {
        var logger = VisionLogger.Instance;
        await ResetLoggerAsync(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        logger.AddWriter(fileWriter);

        // 显式指定 UI target（旧行为）
        logger.Info("UI only message", target: LogTarget.UI);

        await FlushAsync(logger);

        // 验证本测试发出的 UI-only 消息未路由到 File 通道（不受其他类并行写日志影响）
        Assert.DoesNotContain(fileWriter.Entries, e => e.Message == "UI only message");
    }

    /// <summary>
    /// 验证 PluginLogger 静态入口的默认 target 也正确路由到所有通道。
    /// </summary>
    [Fact]
    public async Task PluginLogger_DefaultTarget_ReachesAllChannels()
    {
        var logger = VisionLogger.Instance;
        await ResetLoggerAsync(logger);

        var fileWriter = new InMemoryLogWriter("FileWriter", LogTarget.File);
        var uiWriter = new InMemoryLogWriter("UIWriter", LogTarget.UI);
        logger.AddWriter(fileWriter);
        logger.AddWriter(uiWriter);

        // 通过 PluginLogger 静态方法调用，走默认 target
        PluginLogger.SetProvider(logger);
        PluginLogger.Info("PluginLogger broadcast");

        await FlushAsync(logger);

        Assert.Contains(fileWriter.Entries, e => e.Message == "PluginLogger broadcast");
        Assert.Contains(uiWriter.Entries, e => e.Message == "PluginLogger broadcast");
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

/// <summary>
/// LoggerRouting 独占集合定义，使 VisionLoggerRoutingTests 内部测试串行执行，互不重置 writer。
/// </summary>
[CollectionDefinition("LoggerRouting")]
public sealed class LoggerRoutingCollectionDefinition
{
}
