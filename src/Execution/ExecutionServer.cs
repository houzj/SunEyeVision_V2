using System.Collections.Concurrent;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>Stable request boundary for the resident execution service.</summary>
public sealed record RunRequest(Guid RunId, IReadOnlyList<string> WorkflowIds, string Mode);

/// <summary>Execution service boundary. The current coordinator delegates to the existing engine
/// while keeping request admission, active-run tracking and retirement outside the UI.</summary>
public sealed class ExecutionServer : IAsyncDisposable
{
    private readonly object _admissionGate = new();
    private readonly ConcurrentDictionary<Guid, RunRequest> _active = new();
    private readonly IDisposable _lifecycle;
    private int _retired;
    private int _lifecycleReleased;
    private string? _residentPlanKey;
    private long? _residentPlanGeneration;

    public ExecutionCoordinator Coordinator { get; }

    public ExecutionServer()
    {
        _lifecycle = RuntimeLifecycleProbe.EnterExecutionServer();
        Coordinator = new ExecutionCoordinator(this);
        RuntimeLifecycleProbe.Log("execution-server-created");
    }

    public bool IsRetired => Volatile.Read(ref _retired) != 0;
    public int ActiveRunCount => _active.Count;

    /// <summary>Checks the plan identity retained by the resident solution runtime.</summary>
    public bool TryUseResidentPlan(string planKey, long generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planKey);
        lock (_admissionGate)
        {
            if (IsRetired) throw new ObjectDisposedException(nameof(ExecutionServer));
            var hit = string.Equals(_residentPlanKey, planKey, StringComparison.Ordinal) &&
                _residentPlanGeneration == generation;
            RuntimeLifecycleProbe.Log(hit
                ? $"resident-plan-hit key={planKey} generation={generation}"
                : $"resident-plan-miss key={planKey} generation={generation}");
            return hit;
        }
    }

    /// <summary>Publishes the immutable structure plan identity for subsequent runs.</summary>
    public void PublishResidentPlan(string planKey, long generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planKey);
        lock (_admissionGate)
        {
            if (IsRetired) throw new ObjectDisposedException(nameof(ExecutionServer));
            _residentPlanKey = planKey;
            _residentPlanGeneration = generation;
            RuntimeLifecycleProbe.Log($"resident-plan-published key={planKey} generation={generation}");
        }
    }

    /// <summary>Invalidates only the resident structure plan; active runs remain owned by their request.</summary>
    public void InvalidateResidentPlan()
    {
        lock (_admissionGate)
        {
            _residentPlanKey = null;
            _residentPlanGeneration = null;
            RuntimeLifecycleProbe.Log("resident-plan-invalidated");
        }
    }

    public async Task<T> RunAsync<T>(RunRequest request, Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(execute);
        lock (_admissionGate)
        {
            if (IsRetired) throw new ObjectDisposedException(nameof(ExecutionServer));
            if (!_active.TryAdd(request.RunId, request)) throw new InvalidOperationException("重复接纳执行请求。");
        }
        using var requestLifecycle = RuntimeLifecycleProbe.EnterExecutionRequest();
        RuntimeLifecycleProbe.Log($"run-request-accepted id={request.RunId} mode={request.Mode} workflows={request.WorkflowIds.Count} active={ActiveRunCount}");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        try
        {
            var result = await execute(linked.Token).ConfigureAwait(false);
            RuntimeLifecycleProbe.Log($"run-request-completed id={request.RunId} mode={request.Mode} active={ActiveRunCount}");
            return result;
        }
        catch (OperationCanceledException)
        {
            RuntimeLifecycleProbe.Log($"run-request-cancelled id={request.RunId} mode={request.Mode} active={ActiveRunCount}");
            throw;
        }
        catch (Exception)
        {
            RuntimeLifecycleProbe.Log($"run-request-faulted id={request.RunId} mode={request.Mode} active={ActiveRunCount}");
            throw;
        }
        finally
        {
            _active.TryRemove(request.RunId, out _);
            RuntimeLifecycleProbe.Log($"run-request-retired id={request.RunId} mode={request.Mode} active={ActiveRunCount}");
        }
    }

    public async ValueTask RetireAsync(CancellationToken cancellation = default)
    {
        lock (_admissionGate)
        {
            if (Interlocked.Exchange(ref _retired, 1) != 0) return;
            _residentPlanKey = null;
            _residentPlanGeneration = null;
        }
        while (_active.Count != 0)
        {
            cancellation.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellation).ConfigureAwait(false);
        }
        RuntimeLifecycleProbe.Log("execution-server-retired");
        if (Interlocked.Exchange(ref _lifecycleReleased, 1) == 0) _lifecycle.Dispose();
    }

    public ValueTask DisposeAsync() => RetireAsync();
}
