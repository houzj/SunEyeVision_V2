using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Platform.Sdk;

/// <summary>
/// VM Platform SDK 对应的无 UI 外部宿主。方案会话、执行和结果回调统一由一个专属
/// owner 线程承载，外部调用方不需要引用 UI 或内部工作流服务。
/// </summary>
public sealed class PlatformRuntimeHost : IPlatformRuntime
{
    private readonly string _payloadRoot;
    private readonly BlockingCollection<Action> _work = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _owner;
    private readonly object _gate = new();
    private DocumentSessionManager? _manager;
    private RuntimeDirectorySnapshot? _directory;
    private PlatformSolution? _solution;
    private Exception? _initializationFailure;
    private int _ownerThread;
    private bool _disposed;
    private bool _disposing;

    public PlatformRuntimeHost(string payloadRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadRoot);
        _payloadRoot = Path.GetFullPath(payloadRoot);
        _owner = new Thread(OwnerLoop)
        {
            IsBackground = true,
            Name = "SunEyeVision.PlatformSdk.Owner"
        };
        _owner.Start();
        _ready.Task.GetAwaiter().GetResult();
        if (_initializationFailure is not null) throw new InvalidOperationException("Platform SDK 宿主初始化失败。", _initializationFailure);
    }

    public IPlatformSolution Load(string solutionPath, string? password = null)
    {
        if (!string.IsNullOrWhiteSpace(password))
            throw new NotSupportedException("当前方案格式没有密码解码合同。");
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        var fullPath = Path.GetFullPath(solutionPath);
        return Invoke(() => LoadCore(fullPath));
    }

    private PlatformSolution LoadCore(string path)
    {
        var manager = _manager ?? throw new ObjectDisposedException(nameof(PlatformRuntimeHost));
        if (!File.Exists(path)) throw new FileNotFoundException("方案文件不存在。", path);
        _solution?.InvalidateFromHostReplacement();
        var session = manager.Open(path,
            data => VariableVocabularyCapture.FromCandidate(data.Span),
            scope => new ComplexVariableValueCodec(scope),
            DocumentOptions,
            discardPendingEdits: true);
        var solution = new PlatformSolution(this, session);
        _solution = solution;
        return solution;
    }

    internal Task<PlatformRunResult> RunAsync(PlatformSolution solution, string workflowId, CancellationToken cancellationToken)
        => Task.Run(() => RunCore(solution, workflowId, cancellationToken), cancellationToken);

    private PlatformRunResult RunCore(PlatformSolution solution, string workflowId, CancellationToken cancellationToken)
    {
        DocumentRunBatch? batch = null;
        DocumentRunResult? result = null;
        try
        {
            batch = Invoke(() =>
            {
                EnsureCurrent(solution);
                return (_manager ?? throw new ObjectDisposedException(nameof(PlatformRuntimeHost)))
                    .CaptureCurrentRunBatch(new[] { workflowId });
            });
            solution.SetActiveBatch(batch);
            result = batch.RunAsync(_ => { }, cancellationToken).GetAwaiter().GetResult();
            return Invoke(() =>
            {
                EnsureCurrent(solution);
                solution.Session.DrainRuntimeChanges();
                return solution.MapRunResult(workflowId, result);
            });
        }
        finally
        {
            solution.SetActiveBatch(null);
            if (result is not null) result.Dispose();
            if (batch is not null) Invoke(() => batch.Dispose());
        }
    }

    internal void Stop(PlatformSolution solution)
    {
        solution.ActiveBatch?.Stop();
    }

    internal PlatformContinuousExecution CreateContinuous(PlatformSolution solution, IReadOnlyList<string> workflowIds)
        => Invoke(() =>
        {
            EnsureCurrent(solution);
            var manager = _manager ?? throw new ObjectDisposedException(nameof(PlatformRuntimeHost));
            var run = manager.CaptureCurrentContinuousRun(workflowIds, ownerPost: PostOwner);
            return new PlatformContinuousExecution(this, solution, workflowIds, run);
        });

    internal async Task<PlatformContinuousCompletion> RunContinuousAsync(PlatformContinuousExecution execution, CancellationToken cancellationToken)
    {
        var task = Invoke(() => execution.Start(cancellationToken));
        return await task.ConfigureAwait(false);
    }

    internal bool PauseContinuous(PlatformContinuousExecution execution)
        => Invoke(execution.PauseOnOwner);

    internal bool ResumeContinuous(PlatformContinuousExecution execution)
        => Invoke(execution.ResumeOnOwner);

    internal void StopContinuous(PlatformContinuousExecution execution)
        => Invoke(execution.StopOnOwner);

    internal void DisposeContinuous(PlatformContinuousExecution execution)
        => Invoke(execution.DisposeOnOwner);

    private void PostOwner(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { _work.Add(action); }
        catch (InvalidOperationException) { throw new ObjectDisposedException(nameof(PlatformRuntimeHost)); }
    }

    internal void Close(PlatformSolution solution)
    {
        Invoke(() =>
        {
            if (!ReferenceEquals(_solution, solution))
            {
                solution.InvalidateFromHostReplacement();
                return;
            }
            (_manager ?? throw new ObjectDisposedException(nameof(PlatformRuntimeHost))).CloseCurrent(discardPendingEdits: true);
            solution.InvalidateFromHostReplacement();
            _solution = null;
        });
    }

    internal void Save(PlatformSolution solution)
    {
        Invoke(() =>
        {
            EnsureCurrent(solution);
            var path = solution.Session.LastWrittenSnapshot?.TargetPath
                ?? throw new InvalidOperationException("当前方案尚未有可保存的目标路径。");
            var recovery = Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".recovery");
            solution.Session.Save(path, recovery, CaptureVariableTypes(solution.Session), DocumentOptions,
                scope => new ComplexVariableValueCodec(scope));
        });
    }

    internal void Subscribe(PlatformSolution solution, EventHandler<PublishedNodeResult> handler)
        => Invoke(() => { EnsureCurrent(solution); solution.Session.RuntimeResultAvailable += handler; });

    internal void Unsubscribe(PlatformSolution solution, EventHandler<PublishedNodeResult> handler)
        => Invoke(() =>
        {
            if (ReferenceEquals(_solution, solution)) solution.Session.RuntimeResultAvailable -= handler;
        });

    internal void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Invoke(() => { action(); return true; });
    }
    internal T Invoke<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Environment.CurrentManagedThreadId == _ownerThread) return action();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _work.Add(() =>
                {
                    try { completion.SetResult(action()); }
                    catch (Exception error) { completion.SetException(error); }
                });
            }
            catch (InvalidOperationException) { throw new ObjectDisposedException(nameof(PlatformRuntimeHost)); }
            return completion.Task.GetAwaiter().GetResult();
        }
    }

    private void OwnerLoop()
    {
        _ownerThread = Environment.CurrentManagedThreadId;
        try
        {
            var authority = new ReleasePayloadAuthority(_payloadRoot);
            _directory = authority.PrepareRuntimeDirectory();
            _manager = new DocumentSessionManager(_directory);
            _ready.TrySetResult();
            foreach (var work in _work.GetConsumingEnumerable()) work();
        }
        catch (Exception error)
        {
            _initializationFailure = error;
            _ready.TrySetResult();
        }
        finally
        {
            try { _manager?.Dispose(); } catch { }
            _work.Dispose();
        }
    }

    private void EnsureCurrent(PlatformSolution solution)
    {
        if (!ReferenceEquals(_solution, solution) || !ReferenceEquals(_manager?.CurrentSession, solution.Session))
            throw new InvalidOperationException("Platform 方案会话已被替换或关闭。");
    }

    private static readonly JsonSerializerOptions DocumentOptions = new() { WriteIndented = true };

    private static FrozenVariableTypeCatalog CaptureVariableTypes(DocumentSession session)
    {
        var declarations = JsonSerializer.SerializeToUtf8Bytes(new
        {
            GlobalVariables = session.Document.GlobalVariables.Select(variable => new { variable.Type }).ToArray()
        });
        return VariableVocabularyCapture.FromCandidate(declarations);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed || _disposing) return;
            _disposing = true;
        }
        if (Environment.CurrentManagedThreadId == _ownerThread)
        {
            _solution?.InvalidateFromHostReplacement();
            _solution = null;
            _manager?.Dispose();
            _work.CompleteAdding();
            Volatile.Write(ref _disposed, true);
            return;
        }
        try { Invoke(() => { _solution?.InvalidateFromHostReplacement(); _solution = null; _manager?.Dispose(); return true; }); }
        finally
        {
            Volatile.Write(ref _disposed, true);
            _work.CompleteAdding();
            if (_owner.IsAlive) _owner.Join();
        }
    }
}

internal sealed class PlatformSolution : IPlatformSolution
{
    private readonly PlatformRuntimeHost _host;
    private int _disposed;
    private DocumentRunBatch? _activeBatch;
    private readonly List<(Action<PlatformResultEvent> User, EventHandler<PublishedNodeResult> Handler)> _subscriptions = new();

    internal PlatformSolution(PlatformRuntimeHost host, DocumentSession session) => (_host, Session) = (host, session);
    internal DocumentSession Session { get; }
    internal DocumentRunBatch? ActiveBatch => Volatile.Read(ref _activeBatch);
    internal void SetActiveBatch(DocumentRunBatch? batch) => Volatile.Write(ref _activeBatch, batch);
    internal void InvalidateFromHostReplacement()
    {
        Interlocked.Exchange(ref _disposed, 1);
        foreach (var subscription in _subscriptions.ToArray())
            try { _host.Unsubscribe(this, subscription.Handler); } catch { }
        _subscriptions.Clear();
    }

    public string SolutionPath => _host.Invoke(() => Session.LastWrittenSnapshot?.TargetPath ?? string.Empty);
    public IReadOnlyList<PlatformWorkflowInfo> Workflows => _host.Invoke(() => SnapshotWorkflows());
    public IReadOnlyList<PlatformDeviceInfo> Devices => _host.Invoke(() => SnapshotDevices());
    public PlatformWorkflowInfo GetWorkflow(string workflowId)
        => Workflows.Single(workflow => workflow.Id == workflowId);
    public void Save() { EnsureUsable(); _host.Save(this); }
    public IPlatformExecution CreateExecution(string workflowId)
    {
        EnsureUsable();
        _ = GetWorkflow(workflowId);
        return new PlatformExecution(_host, this, workflowId);
    }
    public IPlatformContinuousExecution CreateContinuousExecution(IReadOnlyList<string> workflowIds)
    {
        EnsureUsable();
        ArgumentNullException.ThrowIfNull(workflowIds);
        var selected = workflowIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0) throw new ArgumentException("至少需要一个流程。", nameof(workflowIds));
        foreach (var workflowId in selected) _ = GetWorkflow(workflowId);
        return _host.CreateContinuous(this, selected);
    }
    public IPlatformResultSubscription SubscribeResults(Action<PlatformResultEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureUsable();
        EventHandler<PublishedNodeResult> wrapped = (_, receipt) =>
        {
            var node = Session.FindNode(receipt.Node.WorkflowId, receipt.Node.NodeId)
                ?? throw new InvalidOperationException("结果节点已不属于当前方案。");
            using var reader = Session.AcquirePublishedResult(receipt.Node.WorkflowId, receipt.Node.NodeId)
                ?? throw new InvalidOperationException("结果 reader 已退役。");
            var values = reader.Values is { } output
                ? output.CopyValues().ToDictionary(pair => pair.Key, pair => PlatformValueMapper.Copy(pair.Value), StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            var images = PlatformValueMapper.Images(reader.Values?.CopyValues().Values ?? Array.Empty<object?>());
            handler(new PlatformResultEvent(receipt.Node.WorkflowId, receipt.Node.NodeId, node.ToolType,
                reader.Snapshot.IsSuccess, new ReadOnlyDictionary<string, object?>(values), DateTimeOffset.UtcNow)
            {
                Images = images
            });
        };
        _host.Subscribe(this, wrapped);
        _subscriptions.Add((handler, wrapped));
        return new PlatformResultSubscription(this, handler, wrapped);
    }
    public void Close() { if (Interlocked.Exchange(ref _disposed, 1) == 0) _host.Close(this); }
    public void Dispose() => Close();

    internal PlatformRunResult MapRunResult(string workflowId, DocumentRunResult result)
    {
        var execution = result.Workflows[workflowId];
        var nodes = new List<PlatformNodeResult>();
        foreach (var item in execution.NodeResults.Values)
        {
            var node = Session.FindNode(workflowId, item.NodeId);
            var values = new Dictionary<string, object?>();
            var images = Array.Empty<PlatformImageFrame>();
            if (item.Result is { } snapshot)
            {
                using var reader = snapshot.AcquireReader();
                if (reader.Values is { } output)
                {
                    var copied = output.CopyValues();
                    foreach (var pair in copied) values[pair.Key] = PlatformValueMapper.Copy(pair.Value);
                    images = PlatformValueMapper.Images(copied.Values).ToArray();
                }
            }
            nodes.Add(new PlatformNodeResult(item.NodeId, node?.ToolType ?? string.Empty, item.Success, item.Duration,
                new ReadOnlyDictionary<string, object?>(values), new ReadOnlyCollection<string>(item.ErrorMessages.ToArray()))
            {
                Images = images
            });
        }
        return new PlatformRunResult(workflowId, execution.Success, execution.Status == ExecutionStatus.Cancelled,
            execution.ExecutionTime, new ReadOnlyCollection<PlatformNodeResult>(nodes),
            new ReadOnlyCollection<string>(execution.Errors.ToArray()));
    }

    private IReadOnlyList<PlatformWorkflowInfo> SnapshotWorkflows()
        => new ReadOnlyCollection<PlatformWorkflowInfo>(Session.GetWorkflows().Select(flow =>
            new PlatformWorkflowInfo(flow.Id, flow.Name, new ReadOnlyCollection<PlatformNodeInfo>(flow.Nodes.Select(node =>
                new PlatformNodeInfo(node.Id, node.ToolType, string.IsNullOrWhiteSpace(node.DispName) ? node.Name : node.DispName)).ToArray()))).ToArray());

    private IReadOnlyList<PlatformDeviceInfo> SnapshotDevices()
        => new ReadOnlyCollection<PlatformDeviceInfo>(Session.Document.Devices.Select(device =>
            new PlatformDeviceInfo(device.Id, device.Name, device.Type.ToString(), device.Manufacturer, device.Model,
                device.SerialNumber, device.WorkflowRef, device.IsEnabled, device.Status.ToString())).ToArray());

    private void EnsureUsable() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    internal void RemoveSubscription(EventHandler<PublishedNodeResult> handler, Action<PlatformResultEvent> user)
    {
        if (_subscriptions.RemoveAll(item => ReferenceEquals(item.Handler, handler)) == 0) return;
        _host.Unsubscribe(this, handler);
    }
}

/// <summary>
/// 把内部资源值转换为 Platform SDK 自有快照。该转换在 owner 线程完成，之后外部回调
/// 只持有独立 byte[]/区域数组，不会在方案关闭后继续访问内部对象。
/// </summary>
internal static class PlatformValueMapper
{
    public static object? Copy(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case IReadOnlyImage image:
                return CopyImage(image);
            case IReadOnlyRegion region:
                return CopyRegion(region);
            case byte[] bytes:
                return bytes.ToArray();
            case IReadOnlyDictionary<string, object?> dictionary:
                return new ReadOnlyDictionary<string, object?>(
                    dictionary.ToDictionary(pair => pair.Key, pair => Copy(pair.Value), StringComparer.Ordinal));
            case System.Collections.IDictionary dictionary:
                var copiedDictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                    copiedDictionary[entry.Key?.ToString() ?? string.Empty] = Copy(entry.Value);
                return copiedDictionary;
            case System.Collections.IEnumerable sequence when value is not string:
                return sequence.Cast<object?>().Select(Copy).ToArray();
            default:
                return value;
        }
    }

    public static IReadOnlyList<PlatformImageFrame> Images(IEnumerable<object?> values)
    {
        var result = new List<PlatformImageFrame>();
        var seen = new HashSet<Guid>();
        foreach (var value in values)
            CollectImages(value, result, seen);
        return new ReadOnlyCollection<PlatformImageFrame>(result);
    }

    private static void CollectImages(object? value, List<PlatformImageFrame> result, HashSet<Guid> seen)
    {
        switch (value)
        {
            case IReadOnlyImage image when seen.Add(image.Frame.FrameId):
                result.Add(CopyImage(image));
                return;
            case IReadOnlyDictionary<string, object?> dictionary:
                foreach (var item in dictionary.Values) CollectImages(item, result, seen);
                return;
            case System.Collections.IDictionary dictionary:
                foreach (var item in dictionary.Values) CollectImages(item, result, seen);
                return;
            case System.Collections.IEnumerable sequence when value is not string && value is not byte[]:
                foreach (var item in sequence) CollectImages(item, result, seen);
                return;
        }
    }

    private static PlatformImageFrame CopyImage(IReadOnlyImage image)
        => new(image.Width, image.Height, image.Layout.RowStrideBytes, image.Layout.Format.ToString(),
            image.CopyPixels(), image.Frame.FrameId, image.Frame.SourceId);

    private static PlatformRegionFrame CopyRegion(IReadOnlyRegion region)
    {
        using var reader = region.AcquireReader();
        var runs = new List<PlatformRegionRun>(reader.RunCount);
        for (var offset = 0; offset < reader.RunCount;)
        {
            var batch = reader.ReadRuns(offset, Math.Min(ResourceContractRules.MaximumRegionBatch, reader.RunCount - offset));
            foreach (var run in batch.Span)
                runs.Add(new PlatformRegionRun(run.Row, run.FirstColumn, run.LastColumnInclusive));
            if (batch.Length == 0) break;
            offset += batch.Length;
        }
        return new PlatformRegionFrame(region.SourceFrame.FrameId, region.SourceFrame.SourceId,
            new ReadOnlyCollection<PlatformRegionRun>(runs));
    }
}

internal sealed class PlatformResultSubscription(PlatformSolution solution, Action<PlatformResultEvent> user, EventHandler<PublishedNodeResult> handler)
    : IPlatformResultSubscription
{
    private PlatformSolution? _solution = solution;
    private readonly Action<PlatformResultEvent> _user = user;
    private readonly EventHandler<PublishedNodeResult> _handler = handler;
    public bool IsDisposed => Volatile.Read(ref _solution) is null;
    public void Dispose() => Interlocked.Exchange(ref _solution, null)?.RemoveSubscription(_handler, _user);
}

internal sealed class PlatformExecution(PlatformRuntimeHost host, PlatformSolution solution, string workflowId) : IPlatformExecution
{
    private readonly PlatformRuntimeHost _host = host;
    private readonly PlatformSolution _solution = solution;
    private int _disposed;
    private Task<PlatformRunResult>? _run;
    public string WorkflowId => workflowId;
    public Task<PlatformRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var run = new Lazy<Task<PlatformRunResult>>(() => _host.RunAsync(_solution, workflowId, cancellationToken));
        var existing = Interlocked.CompareExchange(ref _run, run.Value, null);
        return existing ?? run.Value;
    }
    public void Stop() => _host.Stop(_solution);
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (Volatile.Read(ref _run) is { } run && !run.IsCompleted)
        {
            Stop();
            try { run.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
    }
}

internal sealed class PlatformContinuousExecution : IPlatformContinuousExecution
{
    private readonly PlatformRuntimeHost _host;
    private readonly PlatformSolution _solution;
    private readonly DocumentContinuousRun _run;
    private readonly ReadOnlyCollection<string> _workflowIds;
    private int _disposed;
    private Task<PlatformContinuousCompletion>? _task;

    internal PlatformContinuousExecution(PlatformRuntimeHost host, PlatformSolution solution,
        IReadOnlyList<string> workflowIds, DocumentContinuousRun run)
    {
        _host = host;
        _solution = solution;
        _workflowIds = new ReadOnlyCollection<string>(workflowIds.ToArray());
        _run = run;
        _run.StateChanged += OnStateChanged;
        _run.RoundCompleted += OnRoundCompleted;
        _run.NodeStatusChanged += OnNodeStatusChanged;
    }

    public IReadOnlyList<string> WorkflowIds => _workflowIds;
    public PlatformContinuousState State => _host.Invoke(() => MapState(_run.State));
    public event Action<PlatformContinuousState>? StateChanged;
    public event Action<PlatformContinuousRound>? RoundCompleted;
    public event Action<PlatformContinuousNodeStatus>? NodeStatusChanged;

    public Task<PlatformContinuousCompletion> RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _host.RunContinuousAsync(this, cancellationToken);
    }

    internal Task<PlatformContinuousCompletion> Start(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var task = _run.RunAsync(_ => { }, cancellationToken);
        var mapped = CompleteAsync(task);
        var existing = Interlocked.CompareExchange(ref _task, mapped, null);
        if (existing is not null) throw new InvalidOperationException("连续运行只能启动一次。");
        return mapped;
    }

    private static async Task<PlatformContinuousCompletion> CompleteAsync(Task<ContinuousRunCompletion> task)
    {
        var completion = await task.ConfigureAwait(false);
        return new PlatformContinuousCompletion(completion.Reason.ToString(), completion.CompletedRounds,
            completion.Error?.ToString());
    }

    public bool Pause() { ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this); return _host.PauseContinuous(this); }
    public bool Resume() { ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this); return _host.ResumeContinuous(this); }
    public void Stop() { ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this); _host.StopContinuous(this); }

    internal bool PauseOnOwner() => _run.Pause();
    internal bool ResumeOnOwner() => _run.Resume();
    internal void StopOnOwner() => _run.Stop();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var running = Volatile.Read(ref _task);
        if (running is not null && !running.IsCompleted)
        {
            _host.StopContinuous(this);
            try { running.GetAwaiter().GetResult(); } catch { }
        }
        _host.DisposeContinuous(this);
    }

    internal void DisposeOnOwner()
    {
        _run.StateChanged -= OnStateChanged;
        _run.RoundCompleted -= OnRoundCompleted;
        _run.NodeStatusChanged -= OnNodeStatusChanged;
        _run.Dispose();
    }

    private void OnStateChanged(ContinuousRunState state) => StateChanged?.Invoke(MapState(state));
    private void OnNodeStatusChanged(ContinuousNodeStatus status)
        => NodeStatusChanged?.Invoke(new PlatformContinuousNodeStatus(status.RootRunId, status.Round,
            status.WorkflowId, status.NodeId, status.Status.ToString()));
    private void OnRoundCompleted(ContinuousRoundSummary summary)
        => RoundCompleted?.Invoke(new PlatformContinuousRound(summary.RootRunId, summary.Round,
            new ReadOnlyCollection<PlatformWorkflowRound>(summary.Workflows.Select(MapWorkflowRound).ToArray()),
            summary.StopReason?.ToString()));

    private static PlatformContinuousState MapState(ContinuousRunState state)
        => new(state.EditSessionId, state.RootRunId, state.Phase.ToString(), state.Round, state.ActiveWorkflowId);

    private static PlatformWorkflowRound MapWorkflowRound(WorkflowRoundSummary summary)
        => new(summary.WorkflowId, summary.Status.ToString(), summary.HasBusinessNg, summary.ExecutionTime,
            new ReadOnlyCollection<PlatformContinuousNode>(summary.Nodes.Select(node =>
                new PlatformContinuousNode(node.NodeId, node.Status.ToString(), node.Duration,
                    node.AlgorithmExecutionTimeMs, new ReadOnlyCollection<string>(node.Errors.ToArray()))).ToArray()),
            new ReadOnlyCollection<string>(summary.Errors.ToArray()));
}
