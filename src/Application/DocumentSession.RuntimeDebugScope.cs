using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    public DocumentDebugSession OpenDebugSession(string workflowId, NodeDefinition actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        EnsureOwnerCanEdit();
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            var node = RequireNode(scope, actual.Id);
            if (!ReferenceEquals(node.Node, actual)) throw new InvalidOperationException("调试节点不是实际方案成员。");
            var lifetime = CaptureNodeLifetime(scope, actual);
            var activity = AcquireActivityLease("工具编辑器调试");
            try { return new DocumentDebugSession(this, workflowId, actual, lifetime.Generation, lifetime.Retirement, activity); }
            catch { activity.Dispose(); throw; }
        }
    }
}

/// <summary>One actual editor lifetime. Cancellation and draining precede disposal.</summary>
public sealed class DocumentDebugSession : IDisposable
{
    private readonly DocumentSession _session;
    private readonly string _workflowId;
    private readonly NodeDefinition _node;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly CancellationTokenSource _retirement;
    private IDisposable? _activity;
    private int _active, _disposed;
    private CancellationTokenSource? _continuousCancellation;
    private Task? _continuousTask;
    public bool IsContinuous => _continuousTask is not null;
    public bool IsContinuousRequested => _continuousCancellation is { IsCancellationRequested: false };
    public Task ContinuousCompletion => _continuousTask ?? Task.CompletedTask;

    /// <summary>Serial editor self-execution. The owner-thread callbacks capture
    /// each invocation; execution remains in the existing runtime pipeline.</summary>
    public Task StartContinuousAsync(Func<Task<bool>> executeOnce, Func<bool> isCurrent, Func<bool> canExecute)
    {
        EnsureOwner();
        ArgumentNullException.ThrowIfNull(executeOnce);
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(canExecute);
        if (_continuousTask is not null || Volatile.Read(ref _active) != 0)
            throw new InvalidOperationException("上一调试尚未排空。");
        // Capture/accept callbacks have thread affinity, just like Capture.
        if (SynchronizationContext.Current is null)
            throw new InvalidOperationException("连续调试必须由编辑器所属同步上下文启动。");
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_retirement.Token);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _continuousCancellation = cancellation;
        _continuousTask = completion.Task;
        _ = RepeatAsync();
        return completion.Task;

        async Task RepeatAsync()
        {
            Exception? failure = null;
            try
            {
                while (!cancellation.IsCancellationRequested && isCurrent())
                {
                    var ready = canExecute();
                    // Host predicates may synchronously retire the editor.
                    // Never capture another invocation after that callback.
                    if (cancellation.IsCancellationRequested || !isCurrent()) break;
                    if (ready && !await executeOnce()) break;
                    // BottomCommand polls at 300 ms and does not overlap a
                    // previous callback. Await also keeps stop/close responsive.
                    await Task.Delay(300, cancellation.Token);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception error) { failure = error; }
            finally
            {
                _continuousCancellation = null;
                _continuousTask = null;
                cancellation.Dispose();
                if (failure is null) completion.TrySetResult();
                else completion.TrySetException(failure);
            }
        }
    }

    public void StopContinuous()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("连续调试必须由编辑器所属线程停止。");
        _continuousCancellation?.Cancel();
    }
    internal DocumentDebugSession(DocumentSession session, string workflowId, NodeDefinition node, Guid nodeGeneration,
        CancellationToken nodeRetirement, IDisposable activity)
    { _session = session; _workflowId = workflowId; _node = node; NodeGeneration = nodeGeneration;
      _retirement = CancellationTokenSource.CreateLinkedTokenSource(nodeRetirement); _activity = activity; }
    public Guid Id { get; } = Guid.NewGuid();
    public Guid NodeGeneration { get; }
    internal CancellationToken Retirement => _retirement.Token;
    public DocumentDebugInvocation Capture(NodeConfigurationEditDraft draft, IHostInputPreparer? hostInput,
        Action<string> log, CancellationToken cancellation = default, IReadOnlyCollection<string>? previewInputs = null,
        IHostInputFactory? hostFactory = null)
    {
        EnsureOwner(); ArgumentNullException.ThrowIfNull(draft);
        if (!ReferenceEquals(draft.Session, _session) || draft.WorkflowId != _workflowId || !ReferenceEquals(draft.Node, _node))
            throw new InvalidOperationException("调试草稿不属于编辑器的实际节点。");
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) throw new InvalidOperationException("上一调试尚未排空。");
        try { return _session.CaptureDebug(this, draft, hostInput, log, cancellation, previewInputs, hostFactory, () => Interlocked.Exchange(ref _active, 0)); }
        catch { Interlocked.Exchange(ref _active, 0); throw; }
    }
    public void Cancel()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("调试会话必须由编辑器所属线程操作。");
        if (Volatile.Read(ref _disposed) == 0) _retirement.Cancel();
    }
    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("调试会话必须由编辑器所属线程操作。");
        if (Volatile.Read(ref _disposed) != 0 || _retirement.IsCancellationRequested) throw new InvalidOperationException("调试会话已退役。");
    }
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("调试会话必须由编辑器所属线程释放。");
        if (Volatile.Read(ref _disposed) != 0) return;
        _retirement.Cancel();
        if (Volatile.Read(ref _active) != 0 || _continuousTask is not null) throw new InvalidOperationException("调试尚未排空，请先等待执行及预览结束。");
        Interlocked.Exchange(ref _disposed, 1);
        _retirement.Dispose(); Interlocked.Exchange(ref _activity, null)?.Dispose();
    }
}
