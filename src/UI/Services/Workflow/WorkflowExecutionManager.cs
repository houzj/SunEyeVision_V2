using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流执行管理器
    /// 管理工作流的同步和异步执行
    /// </summary>
    public partial class WorkflowExecutionManager
    {
        private readonly IInputProvider _inputProvider;
        private readonly WorkflowEngine _workflowEngine;
        private readonly IRuntimeInputSnapshotService _runtimeInputSnapshots;
        private readonly Dictionary<string, CancellationTokenSource> _cancellationTokens;
        private readonly Dictionary<string, Task> _runningTasks;
        private readonly Dictionary<string, WorkflowExecutionSnapshot> _executionSnapshots = new();
        // 所有执行入口共用的工作流运行租约。不能只依赖某个视图的按钮禁用状态，
        // 否则快捷键、预览图点击或其他直接调用方仍可能并发启动同一个工作流。
        private readonly HashSet<string> _activeWorkflowIds = new();
        private readonly object _lock = new object();
        private bool _transitionBlocked;
        private int _historyLeaseDepth;
        private int _historyLeaseOwnerThread;
        private CancellationTokenSource? _solutionRunCts;
        private CancellationTokenSource? _communicationRunCts;

        public IDisposable BeginSolutionTransition()
        {
            lock (_lock)
            {
                if (_transitionBlocked) throw new InvalidOperationException("已有方案切换操作。");
                _transitionBlocked = true;
            }
            return new TransitionLease(() => { lock (_lock) _transitionBlocked = false; });
        }
        /// <summary>Hold the same admission gate used by manual, continuous, solution and
        /// communication execution through a complete document history transaction.</summary>
        internal IDisposable BeginHistoryMutation()
        {
            lock (_lock)
            {
                var thread = Environment.CurrentManagedThreadId;
                if (_historyLeaseDepth != 0)
                {
                    if (_historyLeaseOwnerThread != thread) throw new InvalidOperationException("另一线程正在修改编辑历史。");
                    _historyLeaseDepth = checked(_historyLeaseDepth + 1);
                    return new TransitionLease(ReleaseHistoryMutation);
                }
                if (_transitionBlocked || _solutionRunCts is not null || _communicationOperation || _activeWorkflowIds.Count != 0)
                    throw new InvalidOperationException("工作流或方案切换正在进行，不能修改编辑历史。");
                _transitionBlocked = true; _historyLeaseOwnerThread = thread; _historyLeaseDepth = 1;
            }
            return new TransitionLease(ReleaseHistoryMutation);
        }
        private void ReleaseHistoryMutation()
        {
            lock (_lock)
            {
                if (_historyLeaseDepth <= 0) throw new InvalidOperationException("历史租约已经释放。");
                if (--_historyLeaseDepth == 0) { _historyLeaseOwnerThread = 0; _transitionBlocked = false; }
            }
        }
        private sealed class TransitionLease(Action release) : IDisposable
        {
            private Action? _release = release;
            public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
        }
        public async Task StopAndWaitAsync(TimeSpan timeout)
        {
            CancellationTokenSource[] sources;
            lock (_lock)
            {
                sources = _cancellationTokens.Values
                    .Concat(new[] { _solutionRunCts, _communicationRunCts }.OfType<CancellationTokenSource>())
                    .Distinct().ToArray();
            }
            // Cancellation callbacks may acquire the execution lock; never invoke them while holding it.
            CancelSources(sources);
            var timer = Stopwatch.StartNew();
            while (IsAnyRunning())
            {
                if (timer.Elapsed > timeout) throw new TimeoutException("工作流尚未停止，已取消方案切换，原方案保持打开。");
                await Task.Delay(50);
            }
        }
        private bool _communicationOperation;
        private readonly AsyncLocal<bool> _communicationOwner = new();
        private readonly AsyncLocal<IReadOnlyList<CommunicationParameterAssignment>?> _communicationAssignments = new();
        private readonly AsyncLocal<CancellationToken> _communicationCancellation = new();
        private readonly AsyncLocal<SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService?> _communicationInputs = new();
        private SynchronizationContext? _synchronizationContext;
        private readonly ResultDispatchQueue _resultNotifications;
        private readonly System.Windows.Threading.Dispatcher? _resultDispatcher;

        /// <summary>
        /// 当前运行时参数（从 MainWindowViewModel 设置）
        /// </summary>
        private Dictionary<string, object> _runtimeParameters = new();

        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionStarted;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionCompleted;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionStopped;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionError;
        public event EventHandler<WorkflowExecutionProgressEventArgs>? WorkflowExecutionProgress;

        /// <summary>
        /// 连续执行的每一轮迭代开始前触发（在 ExecuteWorkflowInternal 之前）。
        /// 供宿主在自动切换开启时把输入图推进到下一张并刷新运行时参数。
        /// </summary>

        /// <summary>
        /// 节点执行完成事件
        /// </summary>
        public event EventHandler<NodeExecutionResultEventArgs>? NodeExecutionCompleted;
        public event EventHandler<WorkflowExecutionSnapshot>? ExecutionSnapshotUpdated;

        public WorkflowExecutionSnapshot? GetExecutionSnapshot(string workflowId)
        {
            lock (_lock)
                return _executionSnapshots.TryGetValue(workflowId, out var snapshot) ? snapshot : null;
        }

        /// <summary>
        /// 为工作流建立长期 UI 快照。它只保存展示状态；真正运行前仍会复制不可变的运行请求。
        /// </summary>
        public WorkflowExecutionSnapshot EnsureExecutionSnapshot(
            string workflowId,
            string workflowName,
            IEnumerable<SunEyeVision.Workflow.WorkflowNodeBase>? designNodes = null)
        {
            var snapshot = GetOrCreateExecutionSnapshot(workflowId, workflowName);
            if (designNodes != null)
                snapshot.SynchronizeDefinition(workflowName, designNodes);
            return snapshot;
        }

        /// <summary>根流程详情只读取根定义；Group画布的可见节点不构成根流程清单。</summary>
        internal WorkflowExecutionSnapshot? EnsureRootExecutionSnapshot(WorkflowTabViewModel? tab)
        {
            if(tab==null||string.IsNullOrWhiteSpace(tab.RootWorkflowId))return null;
            var root=_workflowEngine.GetWorkflowById(tab.RootWorkflowId);
            return root==null?null:EnsureExecutionSnapshot(root.Id,root.Name,root.Nodes);
        }

        public WorkflowExecutionManager(
            IInputProvider inputProvider,
            WorkflowEngine workflowEngine,
            IRuntimeInputSnapshotService runtimeInputSnapshots)
        {
            _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
            _runtimeInputSnapshots = runtimeInputSnapshots ?? throw new ArgumentNullException(nameof(runtimeInputSnapshots));
            _cancellationTokens = new Dictionary<string, CancellationTokenSource>();
            _runningTasks = new Dictionary<string, Task>();
            _synchronizationContext = SynchronizationContext.Current;
            _resultDispatcher=System.Windows.Application.Current?.Dispatcher;
            _resultNotifications=new ResultDispatchQueue(action=>
            {
                if(_resultDispatcher is not null)
                {
                    if(_resultDispatcher.HasShutdownStarted||_resultDispatcher.HasShutdownFinished)
                        throw new InvalidOperationException("界面调度器已关闭，不能继续投递结果。");
                    var operation=_resultDispatcher.BeginInvoke(action);
                    operation.Aborted+=ResultNotificationAborted;
                    if(operation.Status==System.Windows.Threading.DispatcherOperationStatus.Aborted)_resultNotifications.Dispose();
                }
                else if(_synchronizationContext is not null)_synchronizationContext.Post(_=>action(),null);
                else action();
            });
            if(_resultDispatcher is not null)_resultDispatcher.ShutdownStarted+=ResultDispatcherShutdown;
        }

        private void ResultNotificationAborted(object? sender,EventArgs e)=>_resultNotifications.Dispose();
        private void ResultDispatcherShutdown(object? sender,EventArgs e)=>_resultNotifications.Dispose();

        /// <summary>
        /// 设置运行时参数
        /// </summary>
        /// <param name="parameters">运行时参数字典</param>
        public void SetRuntimeParameters(Dictionary<string, object> parameters)
        {
            _runtimeParameters = parameters ?? new Dictionary<string, object>();
        }

        private sealed record PublishedRunInputs(WorkflowRunConfiguration Configuration, WorkflowGlobalRunValues ProducedGlobals, PreparedLocalImages LocalImages, IReadOnlyList<GlobalVariable> GlobalDefinitions, IReadOnlyDictionary<string, object> RuntimeParameters,
            SunEyeVision.Workflow.Persistence.SolutionResourceResolver ResourcePaths,ToolExecutionOwner ExecutionOwner,IReadOnlyList<WorkflowGlobalProducerOperation> GlobalProducers,string SolutionId,string SolutionName,RuntimeParameterValueAcceptance? ParameterValues);
        private readonly Dictionary<string, PublishedRunInputs> _preparedRunInputs = new(StringComparer.Ordinal);
        internal Func<WorkflowPublishedConfiguration>? CapturePublishedConfiguration { get; set; }

        /// <summary>
        /// 获取运行时参数
        /// </summary>
        public Dictionary<string, object> GetRuntimeParameters()
        {
            return new Dictionary<string, object>(_runtimeParameters);
        }

        /// <summary>
        /// 执行指定工作流
        /// </summary>
        public async Task RunSingleAsync(WorkflowTabViewModel workflow, CancellationToken cancellationToken = default,
            bool advanceLocalImage = true, string? explicitSourceId = null, int navigationDirection = 0)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));
            if(navigationDirection is < -1 or > 1)throw new ArgumentOutOfRangeException(nameof(navigationDirection));

            var workflowId = workflow.RootWorkflowId;
            if (!TryAcquireExecution(workflowId))
            {
                NotifyExecutionRejected(workflowId);
                return;
            }

            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_lock) _cancellationTokens[workflowId] = runCancellation;
            cancellationToken = runCancellation.Token;
            try
            {
                // Project the accepted single run to its root tab; the execution lease stays unchanged.
                await InvokeOnUIAsync(() => workflow.IsRunning = true);
                if (!_communicationOwner.Value)
                    await InvokeOnUIAsync(() => PreparePublishedRun(workflow.RootWorkflowId));
                using var localImages = await BeginLocalImagesAsync(workflow, cancellationToken);
                localImages.Advance=advanceLocalImage;localImages.ExplicitSourceId=explicitSourceId;localImages.NavigationDirection=navigationDirection;
                OnWorkflowExecutionStarted(workflowId);
                try
                {
                    using var execution = await ExecuteSingleTriggeredAsync(workflow, cancellationToken, localImages: localImages);
                    if(localImages.RoundStarted)await CompleteLocalImagesAsync(localImages,cancellationToken);
                    if(execution.Status==ExecutionStatus.Cancelled)OnWorkflowExecutionStopped(workflowId);
                    else if(execution.Success)OnWorkflowExecutionCompleted(workflowId);
                    else OnWorkflowExecutionError(workflowId,string.Join("; ",execution.Errors));
                }
                finally
                {
                    if(localImages.RoundStarted)await CompleteLocalImagesAsync(localImages,CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                OnWorkflowExecutionStopped(workflowId);
            }
            catch (Exception ex)
            {
                OnWorkflowExecutionError(workflowId, ex.Message);
            }
            finally
            {
                lock (_lock) _cancellationTokens.Remove(workflowId);
                try { ReleaseExecution(workflowId); }
                finally { await InvokeOnUIAsync(() => workflow.IsRunning = false); }
            }
        }

        /// <summary>
        /// 开始指定工作流的连续执行
        /// </summary>
        public bool StartContinuousRun(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));

            var workflowId = workflow.RootWorkflowId;

            if (!TryAcquireExecution(workflowId))
            {
                NotifyExecutionRejected(workflowId);
                return false;
            }

            try
            {
                lock (_lock)
                {
                    var cts = new CancellationTokenSource();
                    _cancellationTokens[workflowId] = cts;

                    var task = Task.Run(async () =>
                    {
                        var canceled = false;
                        var failed = false;
                        CameraWorkflowFrameFeed? cameraFeed = null;
                        LocalImageExecution? localImages = null;
                        CameraSourceBinding? cameraSource = null;
                        var nextCameraSummary = DateTime.UtcNow.AddSeconds(1);
                        var sequenceNumbers = new Dictionary<string, long>();

                        try
                        {
                            OnWorkflowExecutionStarted(workflowId);
                            // Task 本身不传入 cts.Token，保证即使 Stop 紧随 Start，
                            // 清理 finally 仍会执行并归还执行租约。
                            cts.Token.ThrowIfCancellationRequested();
                            if (!_communicationOwner.Value)
                                await InvokeOnUIAsync(() => PreparePublishedRun(workflow.RootWorkflowId));
                            localImages = await BeginLocalImagesAsync(workflow, cts.Token);
                            cameraSource = ResolveSingleCameraSource(workflow, localImages.BaseOverrides);
                            if (cameraSource is not null)
                            {
                                cameraFeed = await CameraWorkflowFrameFeed.StartAsync(
                                    SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime,
                                    new CameraDeviceRef(cameraSource.Value.CameraId),
                                    TimeSpan.FromMilliseconds(cameraSource.Value.TimeoutMs),
                                    cts.Token);
                            }
                            var firstIteration = true;
                            while (true)
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                if (!firstIteration && !_communicationOwner.Value)
                                    await InvokeOnUIAsync(() => PreparePublishedRun(workflow.RootWorkflowId));
                                if (!firstIteration)
                                {
                                    PreparedLocalImages nextLocal;
                                    lock (_lock) nextLocal = _preparedRunInputs[workflow.RootWorkflowId].LocalImages;
                                    if (nextLocal.DefinitionSignature != localImages.DefinitionSignature)
                                    {
                                        localImages.Dispose();
                                        localImages = await BeginLocalImagesAsync(workflow, cts.Token);
                                    }
                                    var nextCamera = ResolveSingleCameraSource(workflow, localImages.BaseOverrides);
                                    if (nextCamera != cameraSource)
                                    {
                                        if (cameraFeed is not null) await cameraFeed.DisposeAsync();
                                        cameraFeed = null; cameraSource = nextCamera;
                                        if (cameraSource is not null)
                                            cameraFeed = await CameraWorkflowFrameFeed.StartAsync(
                                                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime,
                                                new CameraDeviceRef(cameraSource.Value.CameraId),
                                                TimeSpan.FromMilliseconds(cameraSource.Value.TimeoutMs), cts.Token);
                                    }
                                }
                                firstIteration = false;
                                RootWorkspaceSettings runPolicy;
                                lock(_lock)runPolicy=_preparedRunInputs[workflowId].Configuration.GetRootSettings(workflowId);
                                ExecutionResult? execution = null;
                                try
                                {
                                var stopAfterCurrent = false;
                                if (cameraFeed is not null && cameraSource is not null)
                                {
                                    // 相机先产出本轮帧，节点只消费该帧。软件触发下一次命令
                                    // 仅在上一轮处理完成之后由 NextFrameAsync 发出。
                                    using var frame = await cameraFeed.NextFrameAsync(cts.Token, continuous: true);
                                    execution = await ExecuteWorkflowInternal(workflow, cts.Token,
                                        cameraFrame: frame, cameraNodeId: cameraSource.Value.NodeId,
                                        sequenceNumbers: sequenceNumbers, localImages: localImages);
                                    stopAfterCurrent = await CompleteLocalImagesAsync(localImages, cts.Token)
                                        == LocalImageCompletion.StopAtEnd;
                                }
                                else
                                {
                                    cts.Token.ThrowIfCancellationRequested();
                                    execution = await ExecuteWorkflowInternal(workflow, cts.Token,
                                        sequenceNumbers: sequenceNumbers, localImages: localImages);
                                    stopAfterCurrent = await CompleteLocalImagesAsync(localImages, cts.Token)
                                        == LocalImageCompletion.StopAtEnd;
                                }
                                // 完成通知表示本轮执行结束（也供停止交互复位），不表示成功。
                                // 本地图像位置由读取接纳决定；SN 仍按对应来源结果更新。
                                OnWorkflowExecutionCompleted(workflowId);
                                cts.Token.ThrowIfCancellationRequested();
                                if (!execution.Success)
                                {
                                    OnWorkflowExecutionError(workflowId, string.Join("; ", execution.Errors));

                                }
                                if (stopAfterCurrent || runPolicy.ShouldStopAfter(execution))
                                {
                                    if(!stopAfterCurrent)InvokeOnUI(()=>WorkflowExecutionProgress?.Invoke(this,new WorkflowExecutionProgressEventArgs(workflowId,"检测NG，按根流程策略停止连续运行。")));
                                    canceled = true;
                                    break;
                                }

                                if (cameraFeed is not null && DateTime.UtcNow >= nextCameraSummary)
                                {
                                    var summary = $"相机帧: 收到={cameraFeed.ReceivedFrames}, " +
                                        $"进入工作流={cameraFeed.DeliveredFrames}, 跳过={cameraFeed.ReplacedFrames}";
                                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,
                                        new WorkflowExecutionProgressEventArgs(workflowId, summary)));
                                    nextCameraSummary = DateTime.UtcNow.AddSeconds(1);
                                }

                                var interval=execution.Success?runPolicy.RunIntervalMs:Math.Max(250,runPolicy.RunIntervalMs);
                                if(interval>0)await Task.Delay(interval,cts.Token);
                                else await Task.Yield();
                                }
                                finally { execution?.Dispose(); }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            canceled = true;
                        }
                        catch (Exception ex)
                        {
                            failed = true;
                            OnWorkflowExecutionError(workflowId, ex.Message);
                        }
                        finally
                        {
                            var cleanupErrors = new List<Exception>();
                            string? cameraWarning = null;
                            long replaced = 0;
                            if(localImages?.RoundStarted==true)
                            {
                                try { await CompleteLocalImagesAsync(localImages,CancellationToken.None); }
                                catch(Exception error) { failed=true;cleanupErrors.Add(error); }
                            }
                            try { localImages?.Dispose(); }
                            catch(Exception error) { failed=true;cleanupErrors.Add(error); }
                            if (cameraFeed is not null)
                            {
                                replaced = cameraFeed.ReplacedFrames;
                                try { await cameraFeed.DisposeAsync(); }
                                catch (Exception ex)
                                {
                                    cameraWarning = $"[WARNING] 相机取流停止失败: {ex.Message}";
                                }
                            }
                            lock (_lock)
                            {
                                _cancellationTokens.Remove(workflowId);
                                _runningTasks.Remove(workflowId);
                            }

                            try { ReleaseExecution(workflowId); }
                            catch(Exception error) { failed=true;cleanupErrors.Add(error); }
                            // Notifications can invoke arbitrary subscribers. Publish only after
                            // every source and execution lease has had a chance to drain.
                            if(cleanupErrors.Count>0)
                                OnWorkflowExecutionError(workflowId,"运行清理失败："+string.Join("；",cleanupErrors.Select(error=>error.Message)));
                            if(cameraWarning is not null)
                                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,new WorkflowExecutionProgressEventArgs(workflowId,cameraWarning)));
                            if(replaced>0)
                                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,new WorkflowExecutionProgressEventArgs(workflowId,$"相机处理期间跳过 {replaced} 帧")));
                            // 取消可能恰好发生在 Task.Delay 完成之后、下一次 while 判断之前。
                            // 无论是异常退出还是循环边界退出，释放租约后都必须通知 UI 复位停止图标。
                            if (!failed && (canceled || cts.IsCancellationRequested))
                                OnWorkflowExecutionStopped(workflowId);
                        }
                    });

                    _runningTasks[workflowId] = task;
                }

                return true;
            }
            catch
            {
                ReleaseExecution(workflowId);
                throw;
            }
        }

        /// <summary>
        /// 停止指定工作流的连续执行
        /// </summary>
        public bool StopContinuousRun(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));

            var workflowId = workflow.RootWorkflowId;

            CancellationTokenSource? source;
            lock (_lock) _cancellationTokens.TryGetValue(workflowId, out source);
            if (source is null) return false;
            CancelSources(new[] { source });
            return true;
        }

        /// <summary>
        /// 开始所有工作流执行(单次)
        /// </summary>
        public async Task StartAllWorkflowsRun(IEnumerable<WorkflowTabViewModel> workflows)
        {
            if (workflows == null)
                throw new ArgumentNullException(nameof(workflows));

            var tasks = workflows.Select(w => RunSingleAsync(w)).ToList();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 运行解决方案的所有根流程。顺序由全局变量的输入来源和目标参数自动推导，
        /// 不需要额外维护 RunAll 编排配置。
        /// </summary>
        public async Task RunSolutionAsync(
            IEnumerable<WorkflowTabViewModel> workflows,
            Solution solution,
            CancellationToken cancellationToken = default)
        {
            if (workflows == null)
                throw new ArgumentNullException(nameof(workflows));
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            using var solutionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_lock)
            {
                if (_transitionBlocked || _solutionRunCts != null || _activeWorkflowIds.Count != 0 ||
                    (_communicationOperation && !_communicationOwner.Value))
                    throw new InvalidOperationException("已有方案执行、通讯操作或切换操作。");
                _solutionRunCts = solutionCancellation;
            }
            try
            {
            cancellationToken = solutionCancellation.Token;
            var tabs = workflows.Where(tab => tab != null).ToList();
            if (!_communicationOwner.Value)
                await InvokeOnUIAsync(() => PreparePublishedRun(null, solution));
            var tabsById = tabs.ToDictionary(tab => tab.RootWorkflowId, StringComparer.Ordinal);
            PublishedRunInputs? admitted;
            lock (_lock) admitted = tabs.Count == 0 ? null : _preparedRunInputs.GetValueOrDefault(tabs[0].RootWorkflowId);
            var executionDefinition = new Solution { Id = admitted?.SolutionId??solution.Id, Name = admitted?.SolutionName??solution.Name,
                Workflows = admitted?.Configuration.CopyWorkflows().ToList() ?? new(),
                GlobalVariables = admitted?.GlobalDefinitions.ToList() ?? new() };
            var plan = SolutionExecutionDependencyGraph.Compile(executionDefinition);
            var workflowPredecessors = BuildWorkflowPredecessors(plan, tabsById.Keys);
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var failed = new HashSet<string>(StringComparer.Ordinal);
            var pending = new HashSet<string>(tabsById.Keys, StringComparer.Ordinal);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ready = pending
                    .Where(id => workflowPredecessors[id].All(predecessor =>
                        completed.Contains(predecessor) || failed.Contains(predecessor)))
                    .OrderBy(id => tabs.FindIndex(tab => tab.RootWorkflowId == id))
                    .ToList();
                if (ready.Count == 0)
                    throw new InvalidOperationException("存在未完成的跨流程依赖，无法继续解决方案运行。");

                // WorkflowExecutionEngine 当前根会话互斥；此处按已推导的顺序提交。
                var workflowId = ready[0];
                var tab = tabsById[workflowId];
                // A failed workflow may still have successful producers. Let the next workflow
                // execute its independent nodes; missing producer values fail only their consumers.

                if (!TryAcquireExecution(workflowId, solutionParticipant: true))
                    throw new InvalidOperationException($"流程 {tab.Name} 已在运行，不能加入本次解决方案运行。");

                try
                {
                    tab.IsRunning = true;
                    using var localImages = await BeginLocalImagesAsync(tab, cancellationToken);
                    try
                    {
                        OnWorkflowExecutionStarted(workflowId);
                        using var executionResult = await ExecuteSingleTriggeredAsync(tab, cancellationToken, localImages: localImages);
                        // The next root activation retires this root's publication identities.
                        // Deliver its already queued node notifications before advancing roots.
                        if(_resultDispatcher is not null)
                            await _resultDispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Background,cancellationToken);
                        if(localImages.RoundStarted)
                            await CompleteLocalImagesAsync(localImages, cancellationToken);
                        if(executionResult.Status==ExecutionStatus.Cancelled)
                        {OnWorkflowExecutionStopped(workflowId);throw new OperationCanceledException(cancellationToken);}
                        if (executionResult.Success)
                        {
                            completed.Add(workflowId);
                            OnWorkflowExecutionCompleted(workflowId);
                        }
                        else
                        {
                            failed.Add(workflowId);
                            OnWorkflowExecutionError(workflowId, string.Join("; ", executionResult.Errors));
                        }
                    }
                    finally
                    {
                        if(localImages.RoundStarted)await CompleteLocalImagesAsync(localImages,CancellationToken.None);
                    }
                }
                finally
                {
                    tab.IsRunning = false;
                    pending.Remove(workflowId);
                    ReleaseExecution(workflowId);
                }
            }
            }
            finally { lock (_lock) { _solutionRunCts = null; if (!_communicationOwner.Value) RemovePreparedLocked(_preparedRunInputs.Keys.ToArray()); } }
        }

        /// <summary>
        /// 开始所有工作流的连续执行
        /// </summary>
        public void StartAllContinuousRun(IEnumerable<WorkflowTabViewModel> workflows)
        {
            if (workflows == null)
                throw new ArgumentNullException(nameof(workflows));

            foreach (var workflow in workflows)
            {
                StartContinuousRun(workflow);
            }
        }

        /// <summary>
        /// 停止所有工作流的连续执行
        /// </summary>
        public void StopAllContinuousRun()
        {
            CancellationTokenSource[] sources;
            lock (_lock) sources = _cancellationTokens.Values.Distinct().ToArray();
            CancelSources(sources);
        }

        private static void CancelSources(IEnumerable<CancellationTokenSource> sources)
        {
            var errors = new List<Exception>();
            foreach (var source in sources)
                try { source.Cancel(); }
                catch (ObjectDisposedException) { /* The captured run already drained. */ }
                catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("停止请求已发送，但部分取消回调失败。", errors);
        }

        /// <summary>
        /// 判断指定工作流是否正在运行
        /// </summary>
        public bool IsRunning(string workflowId)
        {
            lock (_lock)
            {
                return _activeWorkflowIds.Contains(workflowId);
            }
        }

        /// <summary>
        /// 判断是否有任何工作流正在运行
        /// </summary>
        public bool IsAnyRunning()
        {
            lock (_lock)
            {
                return _activeWorkflowIds.Count > 0 || _solutionRunCts != null || _communicationOperation;
            }
        }

        private bool TryAcquireExecution(string workflowId, bool solutionParticipant = false)
        {
            lock (_lock)
            {
                if (_transitionBlocked || (_solutionRunCts is not null && !solutionParticipant)) return false;
                if (_communicationOperation && !_communicationOwner.Value) return false;
                return _activeWorkflowIds.Add(workflowId);
            }
        }

        /// <summary>Non-waiting application lease shared with manual and continuous workflow execution.</summary>
        public async Task<bool> RunCommunicationOperationAsync(Func<CancellationToken, Task> operation, CancellationToken token,
            IReadOnlyList<CommunicationParameterAssignment>? assignments = null,
            SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? receiveInputs = null)
        {
            if (assignments is { Count: > 0 }) throw new InvalidOperationException("通信字段到工作流参数的正式来源身份尚未迁移，当前不可执行绑定。");
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (_lock)
            {
                if (_transitionBlocked || _solutionRunCts != null || _communicationOperation || _activeWorkflowIds.Count > 0) return false;
                _communicationOperation = true;
                _communicationRunCts = runCancellation;
            }
            token = runCancellation.Token;
            _communicationOwner.Value = true;
            _communicationAssignments.Value = assignments?.ToArray();
            _communicationInputs.Value = receiveInputs;
            _communicationCancellation.Value = token;
            try
            {
                token.ThrowIfCancellationRequested();
                await InvokeOnUIAsync(() => PreparePublishedRun(null));
                await operation(token); return true;
            }
            finally
            {
                _communicationOwner.Value = false;
                _communicationAssignments.Value = null;
                _communicationInputs.Value = null;
                _communicationCancellation.Value = default;
                lock (_lock)
                {
                    _communicationOperation = false;
                    _communicationRunCts = null;
                    RemovePreparedLocked(_preparedRunInputs.Keys.ToArray());
                }
            }
        }

        public Task RunCommunicationNodeAsync(string workflowId, string nodeId, CancellationToken token)
            => Task.FromException(new InvalidOperationException("通信单模块执行所需正式字段身份尚未迁移，当前不可用。"));

        private void ReleaseExecution(string workflowId)
        {
            lock (_lock)
            {
                _activeWorkflowIds.Remove(workflowId);
                if (_solutionRunCts is null && !_communicationOwner.Value) RemovePreparedLocked(new[]{workflowId});
            }
        }

        private void NotifyExecutionRejected(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(
                this,
                new WorkflowExecutionProgressEventArgs(
                    workflowId,
                    "[REJECTED] 工作流正在运行，已拒绝重复执行请求")));
        }

        /// <summary>
        /// 执行工作流内部逻辑 — 始终从根工作流（RootWorkflowId）读取节点
        /// </summary>
        private async Task<ExecutionResult> ExecuteWorkflowInternal(
            WorkflowTabViewModel workflow,
            CancellationToken cancellationToken,
            CameraFrame? cameraFrame = null,
            string? cameraNodeId = null,
            Dictionary<string, long>? sequenceNumbers = null,
            LocalImageExecution? localImages = null)
        {
            PublishedRunInputs runInputs;
            lock (_lock)
                runInputs = _preparedRunInputs.TryGetValue(workflow.RootWorkflowId, out var prepared) ? prepared
                    : throw new InvalidOperationException("本轮缺少已发布的配置快照。");
            var runConfiguration = runInputs.Configuration;
            var runGlobals = runInputs.ProducedGlobals.Capture().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            var frozenImageOverrides = localImages?.CaptureIteration();
            var snapshot = GetOrCreateExecutionSnapshot(workflow.RootWorkflowId, workflow.Name);
            await InvokeOnUIAsync(() =>
            {
                BeginRunPresentation(workflow.RootWorkflowId, workflow.Name, snapshot);
                ExecutionSnapshotUpdated?.Invoke(this, snapshot);
            });
            // 在开始时先发送一条开始日志
            InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] ====== 开始执行工作流 ======")));

            try
            {
                // 获取输入图像（可能为 null，工作流引擎会处理）
                var inputImage = cameraFrame is null ? await _inputProvider.GetInputImageAsync() : null;
                cancellationToken.ThrowIfCancellationRequested();
                var receiveInputs = _communicationInputs.Value ?? SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.Receives;
                receiveInputs = new WorkflowGlobalVariableReceiveService(receiveInputs,runInputs.ProducedGlobals,runInputs.GlobalDefinitions);

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 正在创建日志器...")));
                // 使用全局日志器（自动转发到UILogWriter）
                var logger = VisionLogger.Instance.ForSource(workflow.Name);

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 正在创建执行引擎...")));
                // ★ Phase 1: 使用共享的 _workflowEngine（子程序工作流已注册在其中），不再创建新引擎
                var pluginManager = new PluginManager(logger);
                var executionEngine = new WorkflowExecutionEngine(_workflowEngine, logger);

                // ★ 从引擎获取根工作流（始终读取 RootWorkflowId 对应的节点）
                var rootEngineWorkflow = runConfiguration!.CopyWorkflow(workflow.RootWorkflowId);
                if (rootEngineWorkflow == null)
                {
                    logger.LogError($"根工作流未找到: {workflow.RootWorkflowId}");
                    var missingResult = ExecutionResult.CreateFailure($"根工作流未找到: {workflow.RootWorkflowId}");
                    await InvokeOnUIAsync(() =>
                    {
                        snapshot.Complete(missingResult, _ => "工作流");
                        ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                    });
                    return missingResult;
                }

                // UI and engine must share the formal model; the execution copy deliberately
                // has different object identities and must not participate in this reference check.
                var formalRoot = _workflowEngine.GetWorkflow(workflow.RootWorkflowId);
                var definitionConsistencyError = formalRoot is null ? "正式工作流已删除。" : ValidateDefinitionConsistency(
                    workflow, formalRoot);
                if (definitionConsistencyError != null)
                {
                    logger.LogError(definitionConsistencyError);
                    var consistencyResult = ExecutionResult.CreateFailure(definitionConsistencyError);
                    await InvokeOnUIAsync(() =>
                    {
                        snapshot.Complete(consistencyResult, _ => "工作流");
                        ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                    });
                    return consistencyResult;
                }

                // Execution copies intentionally contain model-only nodes. Presentation must
                // address the admitted formal UI instances, never try to cast those copies.
                var presentationNodes=new Dictionary<string,SunEyeVision.UI.Shared.WorkflowNode>(StringComparer.Ordinal);
                var presentationFlows=new HashSet<string>(StringComparer.Ordinal);
                void CapturePresentationNodes(SunEyeVision.Workflow.Workflow definition)
                {
                    if(!presentationFlows.Add(definition.Id))return;
                    foreach(var item in definition.Nodes)
                    {
                        if(item is SunEyeVision.UI.Shared.WorkflowNode ui)presentationNodes[item.Id]=ui;
                        if(!string.IsNullOrEmpty(item.SubroutineId)&&_workflowEngine.GetWorkflow(item.SubroutineId) is { } child)CapturePresentationNodes(child);
                    }
                }
                CapturePresentationNodes(formalRoot!);
                SunEyeVision.UI.Shared.WorkflowNode? PresentationNode(string id)
                    =>ReferenceEquals(_workflowEngine.GetWorkflow(workflow.RootWorkflowId),formalRoot)
                        &&presentationNodes.TryGetValue(id,out var original)
                        &&ReferenceEquals(FindNodeRecursive(formalRoot!,id),original)?original:null;

                // ★ 订阅节点状态事件：引擎状态 → 画布节点（解决状态断点）
                //   引擎在 Running/Completed/Failed/Skipped 时发事件，此处映射到 UI 节点 Status。
                //   找不到节点记日志跳过（Fail-Forward），不中断流程。
                executionEngine.NodeStatusChanged += (_, e) =>
                {
                    var node = PresentationNode(e.NodeId);
                    var snapshotNodeName = GetSnapshotNodeName(rootEngineWorkflow, e.NodeId);
                    var statusText = e.Status switch
                    {
                        NodeStatus.Running => "运行中",
                        NodeStatus.Completed => "成功",
                        NodeStatus.Failed => "失败",
                        NodeStatus.Skipped => "跳过",
                        _ => "等待中"
                    };
                    if (node is SunEyeVision.UI.Shared.WorkflowNode uiNode)
                    {
                        InvokeOnUI(() =>
                        {
                            if(!ReferenceEquals(PresentationNode(e.NodeId),uiNode))return;
                            uiNode.Status = statusText;
                            snapshot.UpdateNodeStatus(e.NodeId, snapshotNodeName, statusText);
                            ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                        });
                    }
                    else
                    {
                        logger.LogWarning($"节点状态事件找不到画布节点: {e.NodeId}");
                        InvokeOnUI(() =>
                        {
                            snapshot.UpdateNodeStatus(e.NodeId, snapshotNodeName, statusText);
                            ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                        });
                    }
                };

                // 引擎在每个节点结束时立即发布结果；快照窗口因而能在流程运行中更新耗时，
                // 而不必等整个流程结束再回填一张静态表。
                executionEngine.NodeExecutionFinished += (node, nodeResult) =>
                {
                    InvokeOnUI(() =>
                    {
                        snapshot.UpdateNodeResult(node.Id, GetSnapshotNodeName(rootEngineWorkflow, node.Id), nodeResult);
                        ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                    });
                };

                // 每轮先复制参数，再覆盖本轮已发布的输入快照，避免 UI 后续切图影响本轮。
                var runtimeParameters = new Dictionary<string, object>(runInputs.RuntimeParameters);
                var inputSnapshot = localImages == null ? _runtimeInputSnapshots.GetSnapshot(workflow.RootWorkflowId) : null;
                if (localImages != null)
                {
                    runtimeParameters.Remove("CurrentImagePath");
                    runtimeParameters.Remove("CurrentImageIndex");
                    runtimeParameters.Remove("CurrentImageVersion");
                    if (localImages.Consumed.Count == 1 && localImages.Consumed[0].Entry is { } entry)
                    {
                        runtimeParameters["CurrentImagePath"] = entry.Address;
                        runtimeParameters["CurrentImageIndex"] = entry.OriginalIndex;
                        runtimeParameters["CurrentImageVersion"] = localImages.Consumed[0].IterationVersion;
                    }
                }
                if (inputSnapshot != null)
                {
                    runtimeParameters["CurrentImagePath"] = inputSnapshot.ImagePath;
                    runtimeParameters["CurrentImageIndex"] = inputSnapshot.ImageIndex;
                    runtimeParameters["CurrentImageVersion"] = inputSnapshot.Version;
                }

                foreach (var param in runtimeParameters)
                {
                    logger.LogInfo($"运行时参数: {param.Key} = {param.Value}");
                }

                // Every entry comes from this run's published configuration. Copy the
                // fallback as well: sequence numbers/communication assignments are per-run.
                var sourceInputValues = frozenImageOverrides ?? runInputs.LocalImages.Overrides.ToDictionary(
                    pair => pair.Key, pair => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(pair.Value));
                if (sequenceNumbers is not null)
                {
                    foreach (var pair in sourceInputValues)
                    {
                        var configured = (long)pair.Value["input.sequence"]!;
                        if (!sequenceNumbers.TryGetValue(pair.Key, out var current))
                            sequenceNumbers[pair.Key] = current = configured;
                        ((Dictionary<string, object?>)pair.Value)["input.sequence"] = current;
                    }
                }
                var hasCameraSource = runInputs.LocalImages.Policies.Values.Any(policy=>policy.SourceKind==ImageSourceKind.Camera);
                var sourceInputs=sourceInputValues.ToDictionary(pair=>pair.Key,pair=>(SunEyeVision.Plugin.Contracts.IHostInputPreparer)new ImageSourceHostInputPreparer(
                    pair.Value.TryGetValue("input.local-address",out var address)?address as string:null,
                    (long)pair.Value["input.sequence"]!,
                    hasCameraSource?SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime:null,
                    cameraNodeId==pair.Key?cameraFrame:null) { LocalReadAdmission=localImages?.AdmissionFor(pair.Key) },StringComparer.Ordinal);
                // Communication property assignments belong to per-run parameter overrides;
                // Source selections are separately frozen in host inputs.
                var nodeParameterOverrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
                var bindingOverrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
                foreach (var group in (_communicationAssignments.Value ?? Array.Empty<CommunicationParameterAssignment>()).GroupBy(a => a.NodeId))
                {
                    var values = group.ToDictionary(a => a.Property, a => (object?)a.Value);
                    bindingOverrides[group.Key] = values;
                    if (!nodeParameterOverrides.TryGetValue(group.Key, out var existing)) existing = new Dictionary<string, object?>();
                    var merged = new Dictionary<string, object?>(existing);
                    foreach (var value in values) merged[value.Key] = value.Value;
                    nodeParameterOverrides[group.Key] = merged;
                }

                // ★ 引擎内部统一完成节点类型转换，主/子工作流共享同一逻辑
                logger.LogInfo($"开始执行工作流: {rootEngineWorkflow.Name}");
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 开始执行工作流...")));

                var runRequest = new WorkflowRunRequest(
                    workflow.RootWorkflowId,
                    inputImage as IVisionImage,
                    runGlobals,
                    runtimeParameters,
                    hostInputs: sourceInputs,
                    nodeParameterOverrides: nodeParameterOverrides,
                    nodeBindingOverrides: bindingOverrides,
                    communicationInputs: receiveInputs,
                    communicationOutputs: SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.Sends,
                    cameraCaptureService: hasCameraSource
                        ? SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime
                        : null,
                    cameraFrames: cameraFrame is not null && cameraNodeId is not null
                        ? new Dictionary<string, CameraFrame> { [cameraNodeId] = cameraFrame }
                        : null) { Configuration = runConfiguration, ResourcePaths = runInputs.ResourcePaths,
                            ExecutionOwner = runInputs.ExecutionOwner,GlobalProducers=runInputs.GlobalProducers,ProducedGlobals=runInputs.ProducedGlobals,ParameterValues=runInputs.ParameterValues };
                var result = await executionEngine.ExecuteWorkflowAsync(runRequest, cancellationToken);
                if (sequenceNumbers is not null)
                {
                    foreach (var pair in result.NodeResults)
                    {
                        if (sourceInputValues.ContainsKey(pair.Key) && pair.Value.Success)
                            sequenceNumbers[pair.Key] = checked(sequenceNumbers[pair.Key] + 1);
                    }
                }
                await InvokeOnUIAsync(() =>
                {
                    snapshot.Complete(result, nodeId => GetSnapshotNodeName(rootEngineWorkflow, nodeId));
                    ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                });

                // 输出结果
                logger.LogInfo($"工作流执行完成: {rootEngineWorkflow.Name}");
                var outcomeText = result.Status switch
                {
                    ExecutionStatus.Cancelled => "已取消",
                    ExecutionStatus.Success => "成功",
                    _ => "失败"
                };
                logger.LogInfo($"执行结果: {outcomeText}");
                logger.LogInfo($"执行时间: {result.ExecutionTime.TotalMilliseconds:F2} ms");

                // 触发节点执行结果事件
                if (result.NodeResults != null && result.NodeResults.Count > 0)
                {
                    foreach (var nodeResult in result.NodeResults)
                    {
                        var node = PresentationNode(nodeResult.Key);
                        if (node is SunEyeVision.UI.Shared.WorkflowNode uiNode)
                        {
                            // 创建一个包含结果的 DeclaredOutputSnapshot
                            var toolResults = CreateToolResultsFromNodeResult(nodeResult.Value);
                            // 方案 §6 画布三态：流程控制结果 → node.Status（OK=命中/NG=未命中）。
                            // 用 SDK 层 IControlFlowResult 接口判断（UI 不直接引用工具项目），
                            // NextNodeIds 非空 = 命中 → OK；空 = 未命中 → NG。
                            if (toolResults?.Output is IControlFlowResult flowResult)
                            {
                                uiNode.Status = flowResult.NextNodeIds != null && flowResult.NextNodeIds.Count > 0 ? "OK" : "NG";
                            }
                            else if (toolResults?.IsSuccess==true)
                            {
                                uiNode.Status = toolResults.Judgment switch
                                {
                                    SunEyeVision.Plugin.Contracts.BusinessJudgment.Pass => "OK",
                                    SunEyeVision.Plugin.Contracts.BusinessJudgment.Fail => "NG",
                                    _ => "成功"
                                };
                            }
                            OnNodeExecutionCompleted(uiNode, toolResults, cameraFrame is not null);
                        }
                    }
                }

                if (result.Status == ExecutionStatus.Cancelled)
                {
                    var message = result.CompletionMessage ?? "工作流执行已取消";
                    logger.LogWarning(message);
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,
                        new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, $"[CANCELLED] {message}")));
                }
                else if (!result.Success && result.Errors.Any())
                {
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, $"[ERROR] 工作流执行失败，错误数量: {result.Errors.Count}")));
                    foreach (var error in result.Errors)
                    {
                        logger.LogError($"错误: {error}");
                    }
                }
                else
                {
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] ====== 工作流执行结束 ======")));
                }

                return result;
            }
            catch (Exception ex)
            {
                await InvokeOnUIAsync(() =>
                {
                    snapshot.Complete(ExecutionResult.CreateFailure(ex.Message), _ => "工作流");
                    ExecutionSnapshotUpdated?.Invoke(this, snapshot);
                });
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, $"[ERROR] ExecuteWorkflowInternal异常: {ex.Message}")));
                throw;
            }
        }

        internal void ForgetWorkspaceSnapshot(string workflowId)
        {
            lock(_lock)
            {
                if(_activeWorkflowIds.Contains(workflowId))throw new InvalidOperationException("运行中的流程不能移除工作区快照。");
                _executionSnapshots.Remove(workflowId);RemovePreparedLocked(new[]{workflowId});_localImageCursors.Remove(workflowId);
            }
        }

        private WorkflowExecutionSnapshot GetOrCreateExecutionSnapshot(string workflowId, string workflowName)
        {
            lock (_lock)
            {
                if (!_executionSnapshots.TryGetValue(workflowId, out var snapshot))
                {
                    snapshot = new WorkflowExecutionSnapshot(workflowId, workflowName);
                    _executionSnapshots.Add(workflowId, snapshot);
                }
                return snapshot;
            }
        }

        /// <summary>
        /// 执行入口的定义一致性校验。
        /// 根 Tab 未下钻时，UI、Engine、Solution 必须引用同一份工作流定义；
        /// 下钻子程序时 Tab 正在展示子工作流集合，因此只校验根定义已注册。
        /// </summary>
        private string? ValidateDefinitionConsistency(
            WorkflowTabViewModel tab,
            SunEyeVision.Workflow.Workflow engineWorkflow)
        {
            if (!tab.IsInSubroutine)
            {
                if (!ReferenceEquals(tab.WorkflowNodes, engineWorkflow.Nodes) ||
                    !ReferenceEquals(tab.WorkflowConnections, engineWorkflow.Connections))
                {
                    return $"工作流定义引用不一致：Tab、Engine 的节点或连接集合不是同一实例。" +
                           $" workflow={engineWorkflow.Name}({engineWorkflow.Id})";
                }
            }

            var solution = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
                .ServiceInitializer.SolutionManager?.CurrentSolution;
            var solutionWorkflow = solution?.Workflows
                .FirstOrDefault(item => string.Equals(item.Id, engineWorkflow.Id, StringComparison.Ordinal));
            if (solutionWorkflow != null && !ReferenceEquals(solutionWorkflow, engineWorkflow))
            {
                return $"工作流定义引用不一致：Solution 与 Engine 不是同一实例。" +
                       $" workflow={engineWorkflow.Name}({engineWorkflow.Id})";
            }

            return null;
        }

        private string GetSnapshotNodeName(SunEyeVision.Workflow.Workflow rootWorkflow, string nodeId)
        {
            var path = new List<string>();
            return TryFindNodeSnapshotPath(rootWorkflow, nodeId, new HashSet<string>(), path)
                ? string.Join(" / ", path)
                : nodeId;
        }

        private bool TryFindNodeSnapshotPath(
            SunEyeVision.Workflow.Workflow workflow,
            string nodeId,
            HashSet<string> visited,
            List<string> path)
        {
            if (!visited.Add(workflow.Id))
                return false;

            var direct = workflow.Nodes.FirstOrDefault(node => node.Id == nodeId);
            if (direct != null)
            {
                path.Add(GetNodeDisplayName(direct));
                return true;
            }

            foreach (var parent in workflow.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.SubroutineId)))
            {
                var subWorkflow = _workflowEngine.GetWorkflow(parent.SubroutineId!);
                if (subWorkflow == null)
                    continue;

                var childPath = new List<string>();
                if (TryFindNodeSnapshotPath(subWorkflow, nodeId, visited, childPath))
                {
                    path.Add(GetNodeDisplayName(parent));
                    path.AddRange(childPath);
                    return true;
                }
            }

            return false;
        }

        private static string GetNodeDisplayName(SunEyeVision.Workflow.WorkflowNodeBase node)
        {
            return !string.IsNullOrWhiteSpace(node.DispName)
                ? node.DispName
                : !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
        }

        private static Dictionary<string, HashSet<string>> BuildWorkflowPredecessors(
            SolutionExecutionDependencyGraph plan,
            IEnumerable<string> workflowIds)
        {
            var knownIds = new HashSet<string>(workflowIds, StringComparer.Ordinal);
            var predecessors = knownIds.ToDictionary(
                id => id,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

            foreach (var dependency in plan.Dependencies.Where(item => item.Kind == SolutionDependencyKind.GlobalVariable))
            {
                var sourceWorkflowId = GetWorkflowId(dependency.SourceScopedNodeId);
                var targetWorkflowId = GetWorkflowId(dependency.TargetScopedNodeId);
                if (!string.Equals(sourceWorkflowId, targetWorkflowId, StringComparison.Ordinal) &&
                    knownIds.Contains(sourceWorkflowId) && knownIds.Contains(targetWorkflowId))
                {
                    predecessors[targetWorkflowId].Add(sourceWorkflowId);
                }
            }

            return predecessors;
        }

        private static string GetWorkflowId(string scopedNodeId)
        {
            var separatorIndex = scopedNodeId.IndexOf('/');
            return separatorIndex > 0 ? scopedNodeId[..separatorIndex] : scopedNodeId;
        }

        private readonly record struct CameraSourceBinding(string NodeId, string CameraId, int TimeoutMs);

        private void PreparePublishedRun(string? workflowId, Solution? solutionDefinition = null)
        {
            // Run has no authority to accept editor drafts. Capture once at the complete-run boundary,
            // before local-image/camera waits; child workflows use the same frozen configuration.
            WorkflowPublishedConfiguration publication;
            if(CapturePublishedConfiguration is {} capture)publication=capture()??throw new InvalidOperationException("正式配置快照入口返回空值。");
            else throw new InvalidOperationException("运行准备缺少当前文档的正式配置快照入口。");
            var configuration=publication.Configuration;
            var flows=_workflowEngine.GetAllWorkflows(); // UI cursor ownership only; never a second execution definition.
            var definitions=GlobalVariable.CaptureDefinitions(publication.GlobalDefinitions.ToArray());
            var definition=new Solution{Id=publication.SolutionId,Name=publication.SolutionName,
                Workflows=configuration.CopyWorkflows().ToList(),GlobalVariables=definitions.ToList()};
            var operations=new List<WorkflowGlobalProducerOperation>();
            foreach(var variable in definitions.Where(variable=>variable.ProducerBinding is not null))
            {
                var validation=GlobalProducerBindingRules.Validate(definition,variable.ProducerBinding!,variable,configuration.Directory);
                if(validation.State is not (SunEyeVision.Plugin.Contracts.BindingCompatibility.Compatible or SunEyeVision.Plugin.Contracts.BindingCompatibility.Conditional))
                {variable.BindingValidationPending=validation.Reason;continue;}
                if(!GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out var target,out var clr,out var reason))
                {variable.BindingValidationPending=reason;continue;}
                operations.Add(new(variable.Id,variable.Name,variable.ProducerBinding!.SourceReference!,target,clr));
            }
            var initial=definitions.Where(variable=>variable.ProducerBinding is null&&string.IsNullOrEmpty(variable.RebindingReason)
                &&string.IsNullOrEmpty(variable.BindingValidationPending)&&GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out _,out _,out _))
                .Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value)).ToArray();
            var globals=new WorkflowGlobalRunValues(publication.SolutionId,definitions,initial);
            var runtime = _runtimeParameters.ToDictionary(p => p.Key,
                p => p.Value is Array array ? array.Clone() : p.Value, StringComparer.Ordinal);
            lock (_lock)
            {
                var owners = flows.ToDictionary(flow => flow.Id, StringComparer.Ordinal);
                var sourceIds = flows.SelectMany(flow => flow.Nodes).Where(node => ImageSourceResolver.IsImageSource(node.ToolType)).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var pair in _localImageCursors.ToArray())
                {
                    if (!pair.Value.Root.TryGetTarget(out var owner) || !owners.TryGetValue(pair.Key, out var current) || !ReferenceEquals(owner, current))
                        _localImageCursors.Remove(pair.Key);
                    else
                    {
                        foreach (var sourceId in pair.Value.Sources.Keys.Where(id => !sourceIds.Contains(id)).ToArray()) pair.Value.Sources.Remove(sourceId);
                        foreach (var sourceId in pair.Value.ResetVersions.Keys.Where(id => !sourceIds.Contains(id)).ToArray()) pair.Value.ResetVersions.Remove(sourceId);
                    }
                }
            }
            var resourcePaths=publication.ResourcePaths;
            try
            {
                var ids=workflowId is null?configuration.CopyWorkflows().Select(flow=>flow.Id).ToArray():new[]{workflowId};
                var captured=ids.ToDictionary(id=>id,id=>new PublishedRunInputs(configuration,globals,
                    CaptureLocalImageInputs(id,configuration,resourcePaths),definitions,runtime,resourcePaths,
                    publication.ExecutionOwner,Array.AsReadOnly(operations.ToArray()),publication.SolutionId,publication.SolutionName,publication.ParameterValues),StringComparer.Ordinal);
                if(captured.Count==0)globals.Dispose();
                lock(_lock)
                {
                    RemovePreparedLocked(workflowId is null?_preparedRunInputs.Keys.ToArray():ids);
                    foreach(var entry in captured)_preparedRunInputs[entry.Key]=entry.Value;
                }
            }
            catch{globals.Dispose();throw;}
        }

        private void RemovePreparedLocked(IEnumerable<string> ids)
        {
            var retired=new HashSet<WorkflowGlobalRunValues>();
            foreach(var id in ids)if(_preparedRunInputs.Remove(id,out var input))retired.Add(input.ProducedGlobals);
            foreach(var values in retired)if(!_preparedRunInputs.Values.Any(input=>ReferenceEquals(input.ProducedGlobals,values)))values.Dispose();
        }

        private async Task<ExecutionResult> ExecuteSingleTriggeredAsync(
            WorkflowTabViewModel workflow,
            CancellationToken cancellationToken,
            Dictionary<string, long>? sequenceNumbers = null,
            LocalImageExecution? localImages = null)
        {
            var cameraSource = ResolveSingleCameraSource(workflow, localImages?.BaseOverrides);
            if (cameraSource is null)
                return await ExecuteWorkflowInternal(workflow, cancellationToken,
                    sequenceNumbers: sequenceNumbers, localImages: localImages);

            // 单次执行可从 WPF 命令直接进入；SDK 参数读取、启动取流、软件触发及
            // 停止取流均可能同步调用厂商 DLL，必须在工作线程进行。
            var feed = await Task.Run(() => CameraWorkflowFrameFeed.StartAsync(
                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime,
                new CameraDeviceRef(cameraSource.Value.CameraId),
                TimeSpan.FromMilliseconds(cameraSource.Value.TimeoutMs),
                cancellationToken), cancellationToken);
            ExecutionResult? result = null;
            Exception? failure = null;
            try
            {
                using var frame = await Task.Run(() => feed.NextFrameAsync(cancellationToken), cancellationToken);
                result = await ExecuteWorkflowInternal(workflow, cancellationToken,
                    frame, cameraSource.Value.NodeId, sequenceNumbers, localImages);
                return result;
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                try { await Task.Run(async () => await feed.DisposeAsync()); }
                catch (Exception cleanup)
                {
                    var failures = new List<Exception>();
                    if (failure is not null) failures.Add(failure);
                    failures.Add(cleanup);
                    // A completed result would otherwise be lost if cleanup aborts the return.
                    if (result is not null) try { result.Dispose(); } catch (Exception error) { failures.Add(error); }
                    throw new AggregateException("相机单次运行或取流清理失败。", failures);
                }
            }
        }

        /// <summary>
        /// 常规单相机工作流由帧就绪驱动。多个相机节点暂沿用既有执行路径，
        /// 本轮不增加图像源数量限制或猜测多相机同步关系。
        /// </summary>
        private CameraSourceBinding? ResolveSingleCameraSource(WorkflowTabViewModel workflow,
            Dictionary<string, IReadOnlyDictionary<string, object?>>? frozenOverrides = null)
        {
            PublishedRunInputs? inputs;
            lock (_lock) _preparedRunInputs.TryGetValue(workflow.RootWorkflowId, out inputs);
            if (inputs is null) return null;
            var overrides = frozenOverrides ?? inputs.LocalImages.Overrides;
            var sources = inputs.LocalImages.Policies
                .Where(pair => overrides.ContainsKey(pair.Key)&&pair.Value.SourceKind==ImageSourceKind.Camera)
                .ToArray();
            if (sources.Length != 1)
                return null;

            var source = sources[0];
            var cameraId = source.Value.CameraDeviceId;
            var timeout = source.Value.CameraTimeoutMs;
            if (string.IsNullOrWhiteSpace(cameraId) || timeout <= 0)
                return null;
            return new CameraSourceBinding(source.Key, cameraId, timeout);
        }

        /// <summary>
        /// 递归在工作流树中查找指定 ID 的节点（含子工作流）
        /// </summary>
        private SunEyeVision.Workflow.WorkflowNodeBase? FindNodeRecursive(
            SunEyeVision.Workflow.Workflow workflow, string nodeId, HashSet<string>? visited = null, WorkflowRunConfiguration? configuration = null)
        {
            visited ??= new HashSet<string>();
            if (!visited.Add(workflow.Id)) return null;

            var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null) return node;

            // 递归搜索子工作流
            foreach (var n in workflow.Nodes)
            {
                if (!string.IsNullOrEmpty(n.SubroutineId))
                {
                    var subWf = configuration is null ? _workflowEngine.GetWorkflow(n.SubroutineId) : configuration.CopyWorkflow(n.SubroutineId);
                    if (subWf != null)
                    {
                        var found = FindNodeRecursive(subWf, nodeId, visited, configuration);
                        if (found != null) return found;
                    }
                }
            }

            return null;
        }

        /// <summary>Called on the UI thread once per actual iteration, before any node is dispatched.</summary>
        internal void BeginRunPresentation(string workflowId, string name, WorkflowExecutionSnapshot snapshot)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            void Reset(string id)
            {
                if (!visited.Add(id)) return;
                var flow = _workflowEngine.GetWorkflow(id);
                if (flow == null) return;
                foreach (var node in flow.Nodes)
                {
                    node.Status = "等待中";
                    node.LastResult = null;
                    if (node.OutputCache is SunEyeVision.UI.Shared.NodeOutputCache cache)
                        cache.InvalidateExecutionResult();
                    if (!string.IsNullOrWhiteSpace(node.SubroutineId)) Reset(node.SubroutineId);
                }
            }
            Reset(workflowId);
            var definition = _workflowEngine.GetWorkflow(workflowId);
            if (definition != null) snapshot.SynchronizeDefinition(name, definition.Nodes);
            snapshot.Begin(name);
        }

        private void OnWorkflowExecutionStarted(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionStarted?.Invoke(this, new WorkflowExecutionEventArgs(workflowId)));
        }

        private void OnWorkflowExecutionCompleted(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionCompleted?.Invoke(this, new WorkflowExecutionEventArgs(workflowId)));
        }

        private void OnWorkflowExecutionStopped(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionStopped?.Invoke(this, new WorkflowExecutionEventArgs(workflowId)));
        }

        private void OnWorkflowExecutionError(string workflowId, string errorMessage)
        {
            InvokeOnUI(() => WorkflowExecutionError?.Invoke(this, new WorkflowExecutionEventArgs(workflowId, errorMessage)));
        }

        /// <summary>
        /// 触发节点执行完成事件
        /// </summary>
        private void OnNodeExecutionCompleted(SunEyeVision.UI.Shared.WorkflowNode node, DeclaredOutputSnapshot? result, bool isCameraDriven)
        {
            if (result is null || result.ExecutionIdentity?.IsCurrent == false) return;
            var queuedAt = Stopwatch.GetTimestamp();
            var token = _communicationOwner.Value ? _communicationCancellation.Value : CancellationToken.None;
            void Publish()
            {
                    if (!token.IsCancellationRequested && result.ExecutionIdentity?.IsCurrent != false)
                        NodeExecutionCompleted?.Invoke(this, new NodeExecutionResultEventArgs(node, result, isCameraDriven,
                            Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds));
            }
            _resultNotifications.Enqueue(result.ExecutionIdentity?.Domain??node.Id,result,Publish);
        }

        /// <summary>
        /// 从节点执行结果创建 DeclaredOutputSnapshot
        /// </summary>
        private DeclaredOutputSnapshot? CreateToolResultsFromNodeResult(NodeExecutionResult nodeResult)
            => nodeResult.Result;

        private void InvokeOnUI(Action action)
        {
            var token = _communicationOwner.Value ? _communicationCancellation.Value : CancellationToken.None;
            if (_synchronizationContext != null)
            {
                _synchronizationContext.Post(_ => { if (!token.IsCancellationRequested) action(); }, null);
            }
            else
            {
                action();
            }
        }

        private Task InvokeOnUIAsync(Action action)
        {
            var token = _communicationOwner.Value ? _communicationCancellation.Value : CancellationToken.None;
            if (token.IsCancellationRequested) return Task.FromCanceled(token);
            if (_synchronizationContext == null || SynchronizationContext.Current == _synchronizationContext)
            {
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _synchronizationContext.Post(_ =>
            {
                if (token.IsCancellationRequested) { completion.TrySetCanceled(token); return; }
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }, null);
            return token.CanBeCanceled ? completion.Task.WaitAsync(token) : completion.Task;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if(_resultDispatcher is not null)_resultDispatcher.ShutdownStarted-=ResultDispatcherShutdown;
            var errors=new List<Exception>();
            try{_resultNotifications.Dispose();}catch(Exception error){errors.Add(error);}
            try{StopAllContinuousRun();}catch(Exception error){errors.Add(error);}

            lock (_lock)
            {
                foreach (var cts in _cancellationTokens.Values)
                {
                    try{cts.Dispose();}catch(Exception error){errors.Add(error);}
                }
                _cancellationTokens.Clear();_transitionBlocked=true;
                if(_activeWorkflowIds.Count==0&&_solutionRunCts is null&&!_communicationOperation)
                    try{RemovePreparedLocked(_preparedRunInputs.Keys.ToArray());}catch(Exception error){errors.Add(error);}
                _localImageCursors.Clear();
            }
            if(errors.Count>0)throw new AggregateException("执行管理器关闭时发生错误，已尝试释放结果通知及其余运行资源。",errors);
        }
    }

    /// <summary>
    /// 工作流执行事件参数
    /// </summary>
    public class WorkflowExecutionEventArgs : EventArgs
    {
        public string WorkflowId { get; }
        public string? ErrorMessage { get; }
        public DateTime Timestamp { get; }

        public WorkflowExecutionEventArgs(string workflowId, string? errorMessage = null)
        {
            WorkflowId = workflowId;
            ErrorMessage = errorMessage;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 工作流执行进度事件参数
    /// </summary>
    public class WorkflowExecutionProgressEventArgs : EventArgs
    {
        public string WorkflowId { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public WorkflowExecutionProgressEventArgs(string workflowId, string message)
        {
            WorkflowId = workflowId;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 节点执行结果事件参数
    /// </summary>
    public class NodeExecutionResultEventArgs : EventArgs
    {
        /// <summary>
        /// UI 节点模型
        /// </summary>
        public SunEyeVision.UI.Shared.WorkflowNode Node { get; }

        /// <summary>
        /// 执行结果
        /// </summary>
        public DeclaredOutputSnapshot Result { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public NodeExecutionResultEventArgs(SunEyeVision.UI.Shared.WorkflowNode node, DeclaredOutputSnapshot result,
            bool isCameraDriven = false, double uiQueueDelayMs = 0)
        {
            Node = node;
            Result = result;
            IsCameraDriven = isCameraDriven;
            UiQueueDelayMs = uiQueueDelayMs;
            Timestamp = DateTime.Now;
        }

        /// <summary>工作流本轮由相机帧启动；展示可合并，但结果仍必须逐轮入缓存。</summary>
        public bool IsCameraDriven { get; }
        public double UiQueueDelayMs { get; }
    }
}
