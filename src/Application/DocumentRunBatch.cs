using System.Collections.ObjectModel;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>One owner-reserved, single-use execution batch. Dispose on the document owner after the run drains.</summary>
public sealed class DocumentRunBatch : IDisposable
{
    private readonly DocumentSession _session;
    private readonly ExecutionRunBatch _reservation;
    private readonly bool _ownsReservation;
    private readonly IReadOnlyDictionary<SunEyeVision.Domain.Workflows.ScopedNodeIdentity, Guid> _hostGenerations;
    internal IReadOnlyList<string> WorkflowOrder => _order;
    internal IReadOnlyDictionary<SunEyeVision.Domain.Workflows.ScopedNodeIdentity, Guid> HostGenerations => _hostGenerations;
    private readonly ExecutionVariableValues _variables;
    private readonly IReadOnlyDictionary<string, ExecutionRunRequest> _requests;
    private readonly IReadOnlyList<string> _order;
    private readonly WorkflowExecutionEngine _engine = new();
    private CancellationTokenSource? _cancellation;
    private int _state;
    internal DocumentRunBatch(DocumentSession session, ExecutionRunBatch reservation, ExecutionVariableValues variables,
        IReadOnlyDictionary<string, ExecutionRunRequest> requests, IReadOnlyList<string> order, bool ownsReservation = true,
        IReadOnlyDictionary<SunEyeVision.Domain.Workflows.ScopedNodeIdentity, Guid>? hostGenerations = null)
    { _session = session; _reservation = reservation; _variables = variables; _requests = requests; _order = order; _ownsReservation = ownsReservation; _hostGenerations = hostGenerations ?? new Dictionary<SunEyeVision.Domain.Workflows.ScopedNodeIdentity, Guid>(); }

    public event Action<string, string, ExecutionStatus>? NodeStatusChanged;
    public bool Pause() => _engine.PauseExecution();
    public bool Resume() => _engine.ResumeExecution();
    public void Stop()
    {
        Exception? failure = null;
        try { Volatile.Read(ref _cancellation)?.Cancel(); } catch (ObjectDisposedException) { } catch (Exception error) { failure = error; }
        try { _engine.StopExecution(); } catch (Exception error) { if (failure is not null) throw new AggregateException(failure, error); throw; }
        if (failure is not null) throw failure;
    }

    public Task<DocumentRunResult> RunAsync(Action<string> log, CancellationToken cancellationToken = default)
        => RunCoreAsync(log, cancellationToken, null, null, null);

    internal Task<DocumentRunResult> RunRoundAsync(Action<string> log, CancellationToken cancellationToken,
        Func<string, CancellationToken, ValueTask> beforeWorkflow,
        Func<string, ExecutionResult, ExecutionRunRequest, bool> afterWorkflow,
        Func<string, Func<Task<ExecutionResult>>, Task<ExecutionResult>> startWorkflow)
        => RunCoreAsync(log, cancellationToken, beforeWorkflow, afterWorkflow, startWorkflow);

    private async Task<DocumentRunResult> RunCoreAsync(Action<string> log, CancellationToken cancellationToken,
        Func<string, CancellationToken, ValueTask>? beforeWorkflow,
        Func<string, ExecutionResult, ExecutionRunRequest, bool>? afterWorkflow,
        Func<string, Func<Task<ExecutionResult>>, Task<ExecutionResult>>? startWorkflow)
    {
        ArgumentNullException.ThrowIfNull(log);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        { cancellation.Dispose(); throw new InvalidOperationException("运行批次只能执行一次。 "); }
        var results = new Dictionary<string, ExecutionResult>(StringComparer.Ordinal);
        Volatile.Write(ref _cancellation, cancellation);
        try
        {
            foreach (var id in _order)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (beforeWorkflow is not null) await beforeWorkflow(id, cancellation.Token).ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();
                void OnStatus(string nodeId, ExecutionStatus status) => NodeStatusChanged?.Invoke(id, nodeId, status);
                _engine.NodeStatusChanged += OnStatus;
                try
                {
                    Task<ExecutionResult> Start() => _engine.ExecuteAsync(_reservation, _requests[id], log, cancellation.Token);
                    var result = await (startWorkflow is null ? Start() : startWorkflow(id, Start)).ConfigureAwait(false);
                    results.Add(id, result);
                    if (result.Status == ExecutionStatus.Cancelled || (afterWorkflow is not null && !afterWorkflow(id, result, _requests[id]))) break;
                }
                finally { _engine.NodeStatusChanged -= OnStatus; }
            }
            return new DocumentRunResult(results);
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var result in results.Values) try { result.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 1) throw new AggregateException("运行失败且结果释放失败。", errors);
            throw;
        }
        finally
        {
            Volatile.Write(ref _cancellation, null);
            cancellation.Dispose();
            Interlocked.Exchange(ref _state, 2);
        }
    }

    public void Dispose()
    {
        // This also enforces the document owner thread before releasing the root reservation.
        _session.DrainRuntimeChanges();
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state == 3) return;
            if (state == 1) throw new InvalidOperationException("运行尚未排空，请先停止并等待完成。 ");
            if (Interlocked.CompareExchange(ref _state, 3, state) == state) break;
        }
        var errors = new List<Exception>();
        foreach (var request in _requests.Values) try { request.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { _variables.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (_ownsReservation) try { _reservation.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("运行批次释放失败。", errors);
    }
}

public sealed class DocumentRunResult : IDisposable
{
    private IReadOnlyDictionary<string, ExecutionResult>? _results;
    internal DocumentRunResult(IDictionary<string, ExecutionResult> results)
        => _results = new ReadOnlyDictionary<string, ExecutionResult>(new Dictionary<string, ExecutionResult>(results, StringComparer.Ordinal));
    public IReadOnlyDictionary<string, ExecutionResult> Workflows => Volatile.Read(ref _results)
        ?? throw new ObjectDisposedException(nameof(DocumentRunResult));
    public void Dispose()
    {
        var results = Interlocked.Exchange(ref _results, null); if (results is null) return;
        var errors = new List<Exception>();
        foreach (var result in results.Values) try { result.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("运行结果释放失败。", errors);
    }
}
