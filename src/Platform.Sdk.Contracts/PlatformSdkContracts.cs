namespace SunEyeVision.Platform.Sdk;

/// <summary>公开 Platform SDK 合同版本；不携带 UI、Application 或 Persistence 类型。</summary>
public static class PlatformSdkContract
{
    public const int Major = 1;
    public const int Minor = 0;
    public const string Version = "1.0";
}

/// <summary>对应 VM Platform SDK 的外部应用入口：加载一个方案并拥有其关闭生命周期。</summary>
public interface IPlatformRuntime : IDisposable
{
    IPlatformSolution Load(string solutionPath, string? password = null);
}

/// <summary>一个已加载方案。调用方只能通过公开身份、流程、运行和结果合同访问它。</summary>
public interface IPlatformSolution : IDisposable
{
    string SolutionPath { get; }
    IReadOnlyList<PlatformWorkflowInfo> Workflows { get; }
    IReadOnlyList<PlatformDeviceInfo> Devices { get; }
    PlatformWorkflowInfo GetWorkflow(string workflowId);
    void Save();
    IPlatformExecution CreateExecution(string workflowId);
    IPlatformContinuousExecution CreateContinuousExecution(IReadOnlyList<string> workflowIds);
    IPlatformResultSubscription SubscribeResults(Action<PlatformResultEvent> handler);
    void Close();
}

/// <summary>对应 VM 的 VmProcedure 身份快照。</summary>
public sealed record PlatformWorkflowInfo(string Id, string Name, IReadOnlyList<PlatformNodeInfo> Nodes);

/// <summary>对应 VM 模块路径/名称的无 UI 身份快照。</summary>
public sealed record PlatformNodeInfo(string Id, string ToolId, string Name);

/// <summary>设备身份快照；连接和驱动控制仍属于宿主扩展，不把内部设备对象暴露给外部工程。</summary>
public sealed record PlatformDeviceInfo(
    string Id,
    string Name,
    string Type,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string? WorkflowId,
    bool IsEnabled,
    string Status);

/// <summary>单次或连续运行的最小公开合同；输入和参数在创建执行时固定。</summary>
public interface IPlatformExecution : IDisposable
{
    string WorkflowId { get; }
    Task<PlatformRunResult> RunAsync(CancellationToken cancellationToken = default);
    void Stop();
}

/// <summary>对应 VM 连续运行入口；每轮仍从当前方案捕获独立快照。</summary>
public interface IPlatformContinuousExecution : IDisposable
{
    IReadOnlyList<string> WorkflowIds { get; }
    PlatformContinuousState State { get; }
    event Action<PlatformContinuousState>? StateChanged;
    event Action<PlatformContinuousRound>? RoundCompleted;
    event Action<PlatformContinuousNodeStatus>? NodeStatusChanged;
    Task<PlatformContinuousCompletion> RunAsync(CancellationToken cancellationToken = default);
    bool Pause();
    bool Resume();
    void Stop();
}

public sealed record PlatformContinuousState(
    Guid EditSessionId,
    Guid RootRunId,
    string Phase,
    long Round,
    string? ActiveWorkflowId);

public sealed record PlatformContinuousNodeStatus(
    Guid RootRunId,
    long Round,
    string WorkflowId,
    string NodeId,
    string Status);

public sealed record PlatformContinuousRound(
    Guid RootRunId,
    long Round,
    IReadOnlyList<PlatformWorkflowRound> Workflows,
    string? StopReason);

public sealed record PlatformWorkflowRound(
    string WorkflowId,
    string Status,
    bool HasBusinessNg,
    TimeSpan ExecutionTime,
    IReadOnlyList<PlatformContinuousNode> Nodes,
    IReadOnlyList<string> Errors);

public sealed record PlatformContinuousNode(
    string NodeId,
    string Status,
    TimeSpan? Duration,
    double? AlgorithmExecutionTimeMs,
    IReadOnlyList<string> Errors);

public sealed record PlatformContinuousCompletion(
    string Reason,
    long CompletedRounds,
    string? Error);

/// <summary>对应 VM 模块/流程回调的可退订句柄。</summary>
public interface IPlatformResultSubscription : IDisposable
{
    bool IsDisposed { get; }
}

/// <summary>结果事件不保留内部 reader、WPF 控件或方案对象。</summary>
public sealed record PlatformResultEvent(
    string WorkflowId,
    string NodeId,
    string ToolId,
    bool Success,
    IReadOnlyDictionary<string, object?> Values,
    DateTimeOffset Timestamp)
{
    /// <summary>结果中的图像独立快照；不保留宿主图像或 reader 引用。</summary>
    public IReadOnlyList<PlatformImageFrame> Images { get; init; } = Array.Empty<PlatformImageFrame>();
}

/// <summary>运行完成后的资源无关结果快照；资源所有权由实现内部在发布前完成收口。</summary>
public sealed record PlatformRunResult(
    string WorkflowId,
    bool Success,
    bool Cancelled,
    TimeSpan Duration,
    IReadOnlyList<PlatformNodeResult> Nodes,
    IReadOnlyList<string> Errors);

public sealed record PlatformNodeResult(
    string NodeId,
    string ToolId,
    bool Success,
    TimeSpan? Duration,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<string> Errors)
{
    /// <summary>节点结果中的图像独立快照；调用方拥有该数组副本。</summary>
    public IReadOnlyList<PlatformImageFrame> Images { get; init; } = Array.Empty<PlatformImageFrame>();
}

/// <summary>跨进程/跨程序集传递的图像快照；像素包含行填充，格式由 PixelFormat 指定。</summary>
public sealed record PlatformImageFrame(
    int Width,
    int Height,
    int RowStrideBytes,
    string PixelFormat,
    byte[] Pixels,
    Guid FrameId,
    string SourceId);

/// <summary>跨程序集传递的区域快照，不暴露内部区域句柄或 reader。</summary>
public sealed record PlatformRegionFrame(
    Guid FrameId,
    string SourceId,
    IReadOnlyList<PlatformRegionRun> Runs);

public readonly record struct PlatformRegionRun(int Row, int FirstColumn, int LastColumnInclusive);
