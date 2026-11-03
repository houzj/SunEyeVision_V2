using System.Collections.ObjectModel;
using System.Diagnostics;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public enum ContinuousPhase { Created, Running, Waiting, Paused, Stopping, Completed }
public enum ContinuousStopReason { Requested, Cancelled, LastImage, BusinessNg, Failed }
public sealed record ContinuousRunState(Guid EditSessionId, Guid RootRunId, ContinuousPhase Phase, long Round, string? ActiveWorkflowId);
public sealed record ContinuousNodeStatus(Guid RootRunId, long Round, string WorkflowId, string NodeId, ExecutionStatus Status);
/// <summary>Resource-free facts captured from this exact node invocation, never a retained output lease.</summary>
public sealed record ContinuousNodeSummary(string NodeId, ExecutionStatus Status, TimeSpan? Duration,
    double? AlgorithmExecutionTimeMs, RunIdentity? Identity, IReadOnlyList<string> Errors);
public sealed record WorkflowRoundSummary(string WorkflowId, ExecutionStatus Status, bool HasBusinessNg)
{
    public TimeSpan ExecutionTime { get; init; }
    public IReadOnlyList<ContinuousNodeSummary> Nodes { get; init; } = Array.Empty<ContinuousNodeSummary>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
public sealed record ContinuousRoundSummary(Guid RootRunId, long Round, IReadOnlyList<WorkflowRoundSummary> Workflows, ContinuousStopReason? StopReason);
public sealed record ContinuousRunCompletion(ContinuousStopReason Reason, long CompletedRounds, Exception? Error);

/// <summary>One root reservation; each round is freshly captured on the actual document owner.</summary>
public sealed class DocumentContinuousRun : IDisposable
{
    private readonly DocumentSession _session;
    private readonly ExecutionRunBatch _reservation;
    private readonly IReadOnlyList<string> _order;
    private readonly IReadOnlyDictionary<string, RootWorkspaceSettings> _settings;
    private readonly IHostInputFactory? _factory;
    private readonly Action<Action> _post;
    private readonly int _ownerThread;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, long> _due = new(StringComparer.Ordinal);
    private TaskCompletionSource _pulse = NewSignal();
    private DocumentRunBatch? _current;
    private ContinuousRunState _state;
    private bool _paused, _disposed;
    private long _pauseStarted, _pausedTicks;
    private int _started, _running;
    private bool _requestedStop;
    internal DocumentContinuousRun(DocumentSession session, ExecutionRunBatch reservation, DocumentRunBatch first,
        IReadOnlyDictionary<string, RootWorkspaceSettings> settings, IHostInputFactory? factory, Action<Action> post)
    {
        _session=session; _reservation=reservation; _current=first; _order=first.WorkflowOrder;
        _settings=new ReadOnlyDictionary<string, RootWorkspaceSettings>(new Dictionary<string, RootWorkspaceSettings>(settings, StringComparer.Ordinal));
        _factory=factory; _post=post; _ownerThread=Environment.CurrentManagedThreadId;
        _state=new(session.EditSessionId,reservation.RootId,ContinuousPhase.Created,0,null);
    }
    public ContinuousRunState State { get { lock(_gate) return _state; } }
    public Exception? LastObserverError { get; private set; }
    public event Action<ContinuousRunState>? StateChanged;
    public event Action<ContinuousRoundSummary>? RoundCompleted;
    public event Action<ContinuousNodeStatus>? NodeStatusChanged;
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void EnsureOwner() { if (Environment.CurrentManagedThreadId!=_ownerThread) throw new InvalidOperationException("连续运行必须由方案所属线程启动或释放。"); }
    private void SignalLocked() { var old=_pulse; _pulse=NewSignal(); old.TrySetResult(); }
    private long NowLocked()
    {
        var now=Stopwatch.GetTimestamp();
        return now-_pausedTicks-(_paused ? now-_pauseStarted : 0);
    }
    private void PostObserver(Action action)
    {
        try { _post(() => { if (Environment.CurrentManagedThreadId!=_ownerThread) { AddObserverError(new InvalidOperationException("运行通知未投递到方案所属线程。")); return; } lock(_gate) if(_disposed)return; try { action(); } catch(Exception error) { AddObserverError(error); } }); }
        catch(Exception error) { AddObserverError(error); }
    }
    private void AddObserverError(Exception error)
    { lock(_gate) LastObserverError=LastObserverError is null ? new AggregateException("连续运行观察者失败。",error) : new AggregateException("连续运行观察者失败。",LastObserverError,error); }
    private void Notify<T>(Action<T>? handlers,T value)
    {
        if(handlers is null)return;
        PostObserver(() => { foreach(Action<T> handler in handlers.GetInvocationList()) try { handler(value); } catch(Exception error) { AddObserverError(error); } });
    }
    private void SetState(ContinuousPhase phase,long round,string? flow)
    {
        ContinuousRunState state;
        lock(_gate) { if(_disposed)return; if(phase is ContinuousPhase.Running or ContinuousPhase.Waiting) phase=_stop.IsCancellationRequested?ContinuousPhase.Stopping:_paused?ContinuousPhase.Paused:phase; _state=state=new(_session.EditSessionId,_reservation.RootId,phase,round,flow); }
        Notify(StateChanged,state);
    }
    private Task<T> OnOwner<T>(Func<T> action)
    {
        var result=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try { _post(() => { try { EnsureOwner(); result.TrySetResult(action()); } catch(Exception error) { result.TrySetException(error); } }); }
        catch(Exception error) { result.TrySetException(error); }
        return result.Task;
    }
    public bool Pause()
    {
        lock(_gate) { if(_disposed || Volatile.Read(ref _running)==0 || _paused || _stop.IsCancellationRequested)return false; _paused=true; _pauseStarted=Stopwatch.GetTimestamp(); _current?.Pause(); SignalLocked(); }
        SetState(ContinuousPhase.Paused,State.Round,State.ActiveWorkflowId); return true;
    }
    public bool Resume()
    {
        lock(_gate) { if(_disposed || Volatile.Read(ref _running)==0 || !_paused || _stop.IsCancellationRequested)return false; _pausedTicks+=Stopwatch.GetTimestamp()-_pauseStarted; _paused=false; _current?.Resume(); SignalLocked(); }
        SetState(ContinuousPhase.Running,State.Round,State.ActiveWorkflowId); return true;
    }
    public void Stop()
    {
        DocumentRunBatch? current;
        lock(_gate) { if(_disposed || _requestedStop || _state.Phase==ContinuousPhase.Completed)return; _requestedStop=true; current=_current; SignalLocked(); }
        var errors=new List<Exception>();
        try { _stop.Cancel(); } catch(Exception error) { errors.Add(error); }
        try { current?.Stop(); } catch(Exception error) { errors.Add(error); }
        SetState(ContinuousPhase.Stopping,State.Round,State.ActiveWorkflowId);
        if(errors.Count!=0)throw new AggregateException("连续运行停止失败。",errors);
    }
    private async ValueTask WaitUntilReady(string flow,CancellationToken cancellation)
    {
        while(true)
        {
            Task pulse; long remaining; bool paused;
            lock(_gate) { paused=_paused; pulse=_pulse.Task; remaining=_due.GetValueOrDefault(flow)-NowLocked(); }
            cancellation.ThrowIfCancellationRequested();
            if(paused) { await pulse.WaitAsync(cancellation).ConfigureAwait(false); continue; }
            if(remaining<=0)return;
            SetState(ContinuousPhase.Waiting,State.Round,flow);
            var delay=TimeSpan.FromSeconds((double)remaining/Stopwatch.Frequency);
            await Task.WhenAny(Task.Delay(delay,cancellation),pulse).ConfigureAwait(false);
        }
    }
    public Task<ContinuousRunCompletion> RunAsync(Action<string> log,CancellationToken cancellation=default)
    {
        EnsureOwner(); ArgumentNullException.ThrowIfNull(log);
        if(Interlocked.CompareExchange(ref _started,1,0)!=0)throw new InvalidOperationException("连续运行只能启动一次。");
        lock(_gate) { ObjectDisposedException.ThrowIf(_disposed,this); Volatile.Write(ref _running,1); }
        return RunCoreAsync(log,cancellation);
    }
    private async Task<ContinuousRunCompletion> RunCoreAsync(Action<string> log,CancellationToken cancellation)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,_stop.Token);
        long rounds=0; ContinuousStopReason? reason=null; Exception? failure=null;
        try
        {
            while(reason is null)
            {
                linked.Token.ThrowIfCancellationRequested();
                var batch=_current ?? throw new InvalidOperationException("连续轮次缺少已捕获批次。");
                var summaries=new List<WorkflowRoundSummary>();
                void Status(string flow,string node,ExecutionStatus status)
                {
                    var handlers=NodeStatusChanged;
                    if(handlers is null)return;
                    var receipt=new ContinuousNodeStatus(_reservation.RootId,rounds+1,flow,node,status);
                    Notify(handlers,receipt);
                }
                batch.NodeStatusChanged+=Status;
                Exception? roundFailure=null;
                try
                {
                    SetState(ContinuousPhase.Running,rounds+1,null);
                    using var result=await batch.RunRoundAsync(log,linked.Token,(flow,token) => ValueTask.CompletedTask,
                    (flow,result,request) =>
                    {
                        var ng=result.NodeResults.Values.Any(n=>n.Result is {IsSuccess:true,Judgment:BusinessJudgment.Fail});
                        summaries.Add(new(flow,result.Status,ng) {
                            ExecutionTime=result.ExecutionTime,
                            Nodes=Array.AsReadOnly(result.NodeResults.Select(pair=>new ContinuousNodeSummary(pair.Key,
                                pair.Value.Status,pair.Value.Duration,pair.Value.Status==ExecutionStatus.NotExecuted ? null : pair.Value.Result?.ExecutionTimeMs,
                                pair.Value.Result?.Identity,Array.AsReadOnly(pair.Value.ErrorMessages.ToArray()))).ToArray()),
                            Errors=Array.AsReadOnly(result.Errors.ToArray()) });
                        lock(_gate) _due[flow]=checked(NowLocked()+(long)(_settings[flow].RunIntervalMs*(double)Stopwatch.Frequency/1000));
                        var last=false;
                        foreach(var (nodeId,preparer) in request.HostInputs)
                        {
                            if(preparer is not IHostInputRoundObserver observer)continue;
                            var key=new ScopedNodeIdentity(flow,nodeId);
                            foreach(var receipt in observer.CaptureCompletedReads())
                            {
                                if(receipt.EditSessionId!=_session.EditSessionId || receipt.Node!=key ||
                                    !batch.HostGenerations.TryGetValue(key,out var generation) || generation!=receipt.NodeGeneration ||
                                    !result.NodeResults.TryGetValue(nodeId,out var nodeResult) || nodeResult.Result?.Identity!=receipt.Run)
                                    throw new InvalidOperationException("宿主读取回执不属于本轮实际节点或运行身份。");
                                last |= receipt.StopAfterCurrent;
                            }
                        }
                        if(result.Status==ExecutionStatus.Cancelled)reason=Volatile.Read(ref _requestedStop)?ContinuousStopReason.Requested:ContinuousStopReason.Cancelled;
                        else if(!result.Success) { reason=ContinuousStopReason.Failed; failure=new InvalidOperationException("流程执行失败："+string.Join("；",result.Errors)); }
                        else if(ng && _settings[flow].StopOnNg)reason=ContinuousStopReason.BusinessNg;
                        else if(last)reason=ContinuousStopReason.LastImage;
                        return reason is null;
                    }, async (flow,start) =>
                    {
                        while(true)
                        {
                            await WaitUntilReady(flow,linked.Token).ConfigureAwait(false);
                            Task<ExecutionResult>? started=null;
                            lock(_gate)
                            {
                                linked.Token.ThrowIfCancellationRequested();
                                if(!_paused) started=start();
                            }
                            if(started is null)continue;
                            SetState(ContinuousPhase.Running,rounds+1,flow);
                            return await started.ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                    rounds++;
                    Notify(RoundCompleted,new(_reservation.RootId,rounds,Array.AsReadOnly(summaries.ToArray()),reason));
                }
                catch(Exception error) { roundFailure=error; throw; }
                finally
                {
                    batch.NodeStatusChanged-=Status;
                    var drained=false;
                    try { await OnOwner(() => { batch.Dispose(); return true; }).ConfigureAwait(false); drained=true; }
                    catch(Exception cleanup)
                    {
                        if(roundFailure is not null)throw new AggregateException("连续轮次运行及释放均失败。",roundFailure,cleanup);
                        throw;
                    }
                    finally { if(drained) lock(_gate) if(ReferenceEquals(_current,batch))_current=null; }
                }
                if(reason is not null)break;
                linked.Token.ThrowIfCancellationRequested();
                var next=await OnOwner(() => { linked.Token.ThrowIfCancellationRequested(); var captured=_session.CaptureContinuousRound(_order,_factory,_reservation); if(linked.IsCancellationRequested) { captured.Dispose(); linked.Token.ThrowIfCancellationRequested(); } return captured; }).ConfigureAwait(false);
                lock(_gate) _current=next;
            }
        }
        catch(OperationCanceledException) when(linked.IsCancellationRequested) { reason=_requestedStop?ContinuousStopReason.Requested:ContinuousStopReason.Cancelled; }
        catch(Exception error) { reason=ContinuousStopReason.Failed; failure=error; }
        finally { Volatile.Write(ref _running,0); SetState(ContinuousPhase.Completed,rounds,null); }
        return new(reason ?? ContinuousStopReason.Failed,rounds,failure);
    }
    public void Dispose()
    {
        EnsureOwner();
        lock(_gate) { if(_disposed)return; if(Volatile.Read(ref _running)!=0)throw new InvalidOperationException("连续运行尚未排空。"); _disposed=true; }
        var errors=new List<Exception>();
        try { _current?.Dispose(); } catch(Exception error) { errors.Add(error); }
        try { _reservation.Dispose(); } catch(Exception error) { errors.Add(error); }
        try { _stop.Dispose(); } catch(Exception error) { errors.Add(error); }
        if(errors.Count!=0)throw new AggregateException("连续运行释放失败。",errors);
    }
}
