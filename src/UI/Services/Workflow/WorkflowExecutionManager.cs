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
    public class WorkflowExecutionManager
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
            foreach (var source in sources) { try { source.Cancel(); } catch (ObjectDisposedException) { } }
            var timer = Stopwatch.StartNew();
            while (IsAnyRunning())
            {
                if (timer.Elapsed > timeout) throw new TimeoutException("工作流尚未停止，已取消方案切换，原方案保持打开。");
                await Task.Delay(50);
            }
        }
        private bool _communicationOperation;
        private readonly AsyncLocal<bool> _communicationOwner = new();
        private readonly AsyncLocal<CancellationToken> _communicationCancellation = new();
        private SynchronizationContext? _synchronizationContext;

        /// <summary>
        /// 当前运行时参数（从 MainWindowViewModel 设置）
        /// </summary>
        private Dictionary<string, object> _runtimeParameters = new();

        /// <summary>
        /// 当前全局变量（从 SolutionManager 获取，用于 __GlobalVars__ 绑定解析）
        /// </summary>
        private IDictionary<string, object?>? _globalVariables;

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
        }

        /// <summary>
        /// 设置运行时参数
        /// </summary>
        /// <param name="parameters">运行时参数字典</param>
        public void SetRuntimeParameters(Dictionary<string, object> parameters)
        {
            _runtimeParameters = parameters ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// 设置全局变量（用于 __GlobalVars__ 绑定解析）
        /// </summary>
        /// <param name="globalVariables">全局变量字典（变量名 -> 变量值）</param>
        public void SetGlobalVariables(IDictionary<string, object?>? globalVariables)
        {
            _globalVariables = globalVariables;
        }

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
            bool advanceLocalImage = true)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));

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
                OnWorkflowExecutionStarted(workflowId);
                var execution = await ExecuteSingleTriggeredAsync(workflow, cancellationToken);
                if (execution.Status == ExecutionStatus.Cancelled)
                    OnWorkflowExecutionStopped(workflowId);
                else if (execution.Success)
                {
                    if (advanceLocalImage && HasLocalImageSource(workflow))
                        await _runtimeInputSnapshots.AdvanceAsync(cancellationToken);
                    OnWorkflowExecutionCompleted(workflowId);
                }
                else
                    OnWorkflowExecutionError(workflowId, string.Join("; ", execution.Errors));
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
                ReleaseExecution(workflowId);
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
                        OnWorkflowExecutionStarted(workflowId);

                        var canceled = false;
                        var failed = false;
                        CameraWorkflowFrameFeed? cameraFeed = null;
                        CameraSourceBinding? cameraSource = null;
                        var nextCameraSummary = DateTime.UtcNow.AddSeconds(1);
                        var sequenceNumbers = new Dictionary<string, long>();

                        try
                        {
                            // Task 本身不传入 cts.Token，保证即使 Stop 紧随 Start，
                            // 清理 finally 仍会执行并归还执行租约。
                            cts.Token.ThrowIfCancellationRequested();
                            cameraSource = ResolveSingleCameraSource(workflow);
                            if (cameraSource is not null)
                            {
                                cameraFeed = await CameraWorkflowFrameFeed.StartAsync(
                                    SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime,
                                    new CameraDeviceRef(cameraSource.Value.CameraId),
                                    TimeSpan.FromMilliseconds(cameraSource.Value.TimeoutMs),
                                    cts.Token);
                            }
                            while (true)
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                ExecutionResult execution;
                                var stopAfterCurrent = false;
                                if (cameraFeed is not null && cameraSource is not null)
                                {
                                    // 相机先产出本轮帧，节点只消费该帧。软件触发下一次命令
                                    // 仅在上一轮处理完成之后由 NextFrameAsync 发出。
                                    using var frame = await cameraFeed.NextFrameAsync(cts.Token, continuous: true);
                                    execution = await ExecuteWorkflowInternal(workflow, cts.Token,
                                        cameraFrame: frame, cameraNodeId: cameraSource.Value.NodeId,
                                        sequenceNumbers: sequenceNumbers);
                                }
                                else
                                {
                                    cts.Token.ThrowIfCancellationRequested();
                                    execution = await ExecuteWorkflowInternal(workflow, cts.Token,
                                        sequenceNumbers: sequenceNumbers);
                                    if (execution.Success)
                                    {
                                        var stopAtLast = ShouldStopAfterCurrentLocalImage(workflow);
                                        if (stopAtLast)
                                            stopAfterCurrent = true;
                                        else if (HasLocalImageSource(workflow))
                                            await _runtimeInputSnapshots.AdvanceAsync(cts.Token);
                                    }
                                }
                                // 完成通知表示本轮执行结束（也供停止交互复位），不表示成功。
                                // 只有 Success 才允许推进本地图片和 SN。
                                OnWorkflowExecutionCompleted(workflowId);
                                cts.Token.ThrowIfCancellationRequested();
                                if (!execution.Success)
                                {
                                    OnWorkflowExecutionError(workflowId, string.Join("; ", execution.Errors));
                                    if (cameraFeed is null)
                                        await Task.Delay(250, cts.Token);
                                }
                                if (stopAfterCurrent)
                                {
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

                                if (cameraFeed is null)
                                    await Task.Delay(10, cts.Token);
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
                            if (cameraFeed is not null)
                            {
                                var replaced = cameraFeed.ReplacedFrames;
                                try { await cameraFeed.DisposeAsync(); }
                                catch (Exception ex)
                                {
                                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,
                                        new WorkflowExecutionProgressEventArgs(workflowId,
                                            $"[WARNING] 相机取流停止失败: {ex.Message}")));
                                }
                                if (replaced > 0)
                                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,
                                        new WorkflowExecutionProgressEventArgs(workflowId,
                                            $"相机处理期间跳过 {replaced} 帧")));
                            }
                            lock (_lock)
                            {
                                _cancellationTokens.Remove(workflowId);
                                _runningTasks.Remove(workflowId);
                            }

                            ReleaseExecution(workflowId);
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

            lock (_lock)
            {
                if (_cancellationTokens.TryGetValue(workflowId, out var cts))
                {
                    cts.Cancel();
                    return true;
                }
            }

            return false;
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
                if (_transitionBlocked || _solutionRunCts != null ||
                    (_communicationOperation && !_communicationOwner.Value))
                    throw new InvalidOperationException("已有方案执行、通讯操作或切换操作。");
                _solutionRunCts = solutionCancellation;
            }
            try
            {
            cancellationToken = solutionCancellation.Token;
            var tabs = workflows.Where(tab => tab != null).ToList();
            var tabsById = tabs.ToDictionary(tab => tab.RootWorkflowId, StringComparer.Ordinal);
            var plan = SolutionExecutionDependencyGraph.Compile(solution);
            var runtimeGlobals = solution.GlobalVariables
                .ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.Ordinal);
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
                if (workflowPredecessors[workflowId].Any(failed.Contains))
                {
                    pending.Remove(workflowId);
                    failed.Add(workflowId);
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this,
                        new WorkflowExecutionProgressEventArgs(workflowId, "[SKIPPED] 跨流程上游失败。")));
                    continue;
                }

                if (!TryAcquireExecution(workflowId))
                    throw new InvalidOperationException($"流程 {tab.Name} 已在运行，不能加入本次解决方案运行。");

                try
                {
                    tab.IsRunning = true;
                    OnWorkflowExecutionStarted(workflowId);
                    var executionResult = await ExecuteSingleTriggeredAsync(tab, cancellationToken, runtimeGlobals);
                    if (executionResult.Success)
                    {
                        ApplyProducedGlobalValues(solution, workflowId, executionResult, runtimeGlobals);
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
                    tab.IsRunning = false;
                    pending.Remove(workflowId);
                    ReleaseExecution(workflowId);
                }
            }
            }
            finally { lock (_lock) _solutionRunCts = null; }
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
            lock (_lock)
            {
                foreach (var cts in _cancellationTokens.Values)
                {
                    cts.Cancel();
                }
            }
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

        private bool TryAcquireExecution(string workflowId)
        {
            lock (_lock)
            {
                if (_transitionBlocked) return false;
                if (_communicationOperation && !_communicationOwner.Value) return false;
                return _activeWorkflowIds.Add(workflowId);
            }
        }

        /// <summary>Non-waiting application lease shared with manual and continuous workflow execution.</summary>
        public async Task<bool> RunCommunicationOperationAsync(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (_lock)
            {
                if (_transitionBlocked || _solutionRunCts != null || _communicationOperation || _activeWorkflowIds.Count > 0) return false;
                _communicationOperation = true;
                _communicationRunCts = runCancellation;
            }
            token = runCancellation.Token;
            _communicationOwner.Value = true;
            _communicationCancellation.Value = token;
            try { token.ThrowIfCancellationRequested(); await operation(token); return true; }
            finally
            {
                _communicationOwner.Value = false;
                _communicationCancellation.Value = default;
                lock (_lock)
                {
                    _communicationOperation = false;
                    _communicationRunCts = null;
                }
            }
        }

        public async Task RunCommunicationNodeAsync(string workflowId, string nodeId, CancellationToken token)
        {
            var workflow = _workflowEngine.GetWorkflow(workflowId) ?? throw new InvalidOperationException("触发流程已删除。");
            var node = FindNodeRecursive(workflow, nodeId) ?? throw new InvalidOperationException("触发模块已删除。");
            if (!node.IsEnabled) throw new InvalidOperationException("触发模块已禁用，未执行。");
            var parameters = ToolParameterSnapshot.Create(node.Parameters);
            var overrides = CreateImageLoadParameterOverrides(workflow, VisionLogger.Instance, new HashSet<string>());
            if (overrides.TryGetValue(node.Id, out var nodeOverrides))
                foreach (var item in nodeOverrides)
                {
                    var property = parameters.GetType().GetProperty(item.Key);
                    if (property?.CanWrite != true) throw new InvalidOperationException($"模块输入快照无效：{item.Key}。");
                    property.SetValue(parameters, item.Value);
                }
            var bindings = ParameterBindingSet.FromSettings(node.ParamSettings);
            if (bindings.Bindings.Any(binding => binding.BindingType != BindingType.Constant &&
                    !string.IsNullOrWhiteSpace(binding.SourceNodeId) && binding.SourceNodeId != "__GlobalVars__"))
                throw new InvalidOperationException("单模块执行缺少上游节点结果，请执行完整流程；不使用上一次的缓存结果。");
            var globals = new Dictionary<string, object?>(_globalVariables ?? new Dictionary<string, object?>());
            var image = await _inputProvider.GetInputImageAsync() as IVisionImage;
            token.ThrowIfCancellationRequested();
            var tool = ToolRegistry.CreateToolInstance(node.ToolType) ?? throw new InvalidOperationException("触发模块的工具尚未加载。");
            try
            {
                OnWorkflowExecutionStarted(workflowId);
                var executor = new ToolExecutor(new ParameterResolver(), VisionLogger.Instance);
                var result = await executor.ExecuteAsync(tool, new ToolExecutionRequest(parameters)
                {
                    NodeId = node.Id, NodeName = node.Name, ParameterBindings = bindings,
                    GlobalVariables = globals, ResolvedInputImage = image, CancellationToken = token,
                    CameraCaptureService = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime
                });
                if (result.IsCancelled) throw new OperationCanceledException(token);
                if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage ?? "触发模块执行失败。");
                OnWorkflowExecutionCompleted(workflowId);
            }
            catch (OperationCanceledException) { OnWorkflowExecutionStopped(workflowId); throw; }
            catch (Exception ex) { OnWorkflowExecutionError(workflowId, ex.Message); throw; }
            finally { if (tool is IDisposable disposable) disposable.Dispose(); }
        }

        private void ReleaseExecution(string workflowId)
        {
            lock (_lock)
            {
                _activeWorkflowIds.Remove(workflowId);
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
            IDictionary<string, object?>? globalVariablesOverride = null,
            CameraFrame? cameraFrame = null,
            string? cameraNodeId = null,
            Dictionary<string, long>? sequenceNumbers = null)
        {
            var snapshot = GetOrCreateExecutionSnapshot(workflow.RootWorkflowId, workflow.Name);
            await InvokeOnUIAsync(() =>
            {
                snapshot.Begin(workflow.Name);
                ExecutionSnapshotUpdated?.Invoke(this, snapshot);
            });
            // 在开始时先发送一条开始日志
            InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] ====== 开始执行工作流 ======")));

            try
            {
                // 获取输入图像（可能为 null，工作流引擎会处理）
                var inputImage = cameraFrame is null ? await _inputProvider.GetInputImageAsync() : null;
                cancellationToken.ThrowIfCancellationRequested();

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 正在创建日志器...")));
                // 使用全局日志器（自动转发到UILogWriter）
                var logger = VisionLogger.Instance.ForSource(workflow.Name);

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 正在创建执行引擎...")));
                // ★ Phase 1: 使用共享的 _workflowEngine（子程序工作流已注册在其中），不再创建新引擎
                var pluginManager = new PluginManager(logger);
                var executionEngine = new WorkflowExecutionEngine(_workflowEngine, pluginManager, logger);

                // ★ 从引擎获取根工作流（始终读取 RootWorkflowId 对应的节点）
                var rootEngineWorkflow = _workflowEngine.GetWorkflow(workflow.RootWorkflowId);
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

                var definitionConsistencyError = ValidateDefinitionConsistency(
                    workflow,
                    rootEngineWorkflow);
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

                // ★ 订阅节点状态事件：引擎状态 → 画布节点（解决状态断点）
                //   引擎在 Running/Completed/Failed/Skipped 时发事件，此处映射到 UI 节点 Status。
                //   找不到节点记日志跳过（Fail-Forward），不中断流程。
                executionEngine.NodeStatusChanged += (_, e) =>
                {
                    var node = FindNodeRecursive(rootEngineWorkflow, e.NodeId);
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
                var runtimeParameters = new Dictionary<string, object>(_runtimeParameters);
                var inputSnapshot = _runtimeInputSnapshots.GetSnapshot(workflow.RootWorkflowId);
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

                // 从各节点 InputSource 提取本轮路径快照。路径只覆盖深拷贝后的执行参数，
                // 不再写回方案节点的 Parameters.FilePath，避免切图或执行污染设计数据。
                var nodeParameterOverrides = CreateImageLoadParameterOverrides(
                    rootEngineWorkflow,
                    logger,
                    new HashSet<string>());
                if (sequenceNumbers is not null)
                {
                    foreach (var pair in nodeParameterOverrides)
                    {
                        var configured = (long)pair.Value["SequenceNumber"]!;
                        if (!sequenceNumbers.TryGetValue(pair.Key, out var current))
                            sequenceNumbers[pair.Key] = current = configured;
                        ((Dictionary<string, object?>)pair.Value)["SequenceNumber"] = current;
                    }
                }
                var hasCameraSource = nodeParameterOverrides.Values.Any(values =>
                    values.TryGetValue("SourceKind", out var kind)
                    && kind is SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera);

                // ★ 引擎内部统一完成节点类型转换，主/子工作流共享同一逻辑
                logger.LogInfo($"开始执行工作流: {rootEngineWorkflow.Name}");
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 开始执行工作流...")));

                var runRequest = new WorkflowRunRequest(
                    workflow.RootWorkflowId,
                    inputImage as IVisionImage,
                    globalVariablesOverride ?? _globalVariables,
                    runtimeParameters,
                    nodeParameterOverrides: nodeParameterOverrides,
                    cameraCaptureService: hasCameraSource
                        ? SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime
                        : null,
                    cameraFrames: cameraFrame is not null && cameraNodeId is not null
                        ? new Dictionary<string, CameraFrame> { [cameraNodeId] = cameraFrame }
                        : null);
                var result = await executionEngine.ExecuteWorkflowAsync(runRequest, cancellationToken);
                if (sequenceNumbers is not null)
                {
                    foreach (var pair in result.NodeResults)
                    {
                        if (nodeParameterOverrides.ContainsKey(pair.Key) && pair.Value.Success)
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
                        var node = FindNodeRecursive(rootEngineWorkflow, nodeResult.Key);
                        if (node is SunEyeVision.UI.Shared.WorkflowNode uiNode)
                        {
                            // 创建一个包含结果的 ToolResults
                            var toolResults = CreateToolResultsFromNodeResult(nodeResult.Value);
                            // 方案 §6 画布三态：流程控制结果 → node.Status（OK=命中/NG=未命中）。
                            // 用 SDK 层 IControlFlowResult 接口判断（UI 不直接引用工具项目），
                            // NextNodeIds 非空 = 命中 → OK；空 = 未命中 → NG。
                            if (toolResults is IControlFlowResult flowResult)
                            {
                                uiNode.Status = flowResult.NextNodeIds != null && flowResult.NextNodeIds.Count > 0 ? "OK" : "NG";
                            }
                            else if (toolResults.IsSuccess)
                            {
                                uiNode.Status = toolResults.JudgmentStatus switch
                                {
                                    "OK" => "OK",
                                    "NG" => "NG",
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

        /// <summary>
        /// 把生产节点的结果写入当前解决方案运行的变量快照。这里刻意不修改 Solution.GlobalVariables.Value，
        /// 防止一次运行把设计期初值污染到方案文件；下一消费者流程只读取 runtimeGlobals。
        /// </summary>
        private static void ApplyProducedGlobalValues(
            Solution solution,
            string workflowId,
            ExecutionResult executionResult,
            IDictionary<string, object?> runtimeGlobals)
        {
            foreach (var variable in solution.GlobalVariables.Where(variable =>
                         string.Equals(variable.ProducerBinding?.WorkflowId, workflowId, StringComparison.Ordinal)))
            {
                var producer = variable.ProducerBinding!;
                if (!executionResult.NodeResults.TryGetValue(producer.NodeId, out var nodeResult) ||
                    nodeResult.Result == null)
                {
                    throw new InvalidOperationException($"全局变量 {variable.Name} 的输入来源未产生结果：{producer.DisplayPath}。");
                }

                runtimeGlobals[variable.Name] = OutputVariableResolver.Resolve(
                    nodeResult.Result,
                    producer.ResultName,
                    producer.NodeId);
            }
        }

        /// <summary>
        /// 创建测试用的Mat — 已废弃，工作流不再提供默认输入图像
        /// </summary>
        // 已删除：工作流不再提供默认输入图像，缺少输入图像的节点将直接报错

        /// <summary>
        /// 递归遍历工作流（含子工作流），从每个 ImageLoad 节点的 InputSource
        /// 读取当前选中的图像路径，生成本轮参数覆盖快照。
        /// </summary>
        /// <remarks>
        /// 使用 WorkflowNodeBase 基类而非 SunEyeVision.UI.Shared.WorkflowNode，确保子程序工作流中的节点
        /// （类型为 WorkflowNodeBase）也能正确注入路径。
        /// IsImageLoadNode 通过 ToolType 字符串判断（在基类上可访问）。
        /// InputSource 在 WorkflowNodeBase 上为 object?，通过 as 转换访问。
        /// </remarks>
        private Dictionary<string, IReadOnlyDictionary<string, object?>> CreateImageLoadParameterOverrides(
            SunEyeVision.Workflow.Workflow workflow, ILogger logger, HashSet<string> visitedIds)
        {
            var overrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
            CollectImageLoadParameterOverrides(workflow, logger, visitedIds, overrides);
            return overrides;
        }

        private readonly record struct CameraSourceBinding(string NodeId, string CameraId, int TimeoutMs);

        private bool ShouldStopAfterCurrentLocalImage(WorkflowTabViewModel workflow)
        {
            var root = _workflowEngine.GetWorkflow(workflow.RootWorkflowId);
            if (root is null) return false;
            foreach (var node in root.Nodes)
            {
                var config = node.ImageSourceConfig;
                if (!IsImageLoadToolType(node.ToolType) || config is null
                    || config.SourceKind != SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local
                    || !config.AutoSwitchEnabled || !config.StopAtLastImage)
                    continue;
                var currentPath = _runtimeInputSnapshots.GetSnapshot(workflow.RootWorkflowId)?.ImagePath;
                if (string.IsNullOrWhiteSpace(currentPath)) continue;
                var runnables = config.RunMode == 1
                    ? config.FilePaths.Where(path => config.ForRunPaths.Contains(path)).ToList()
                    : config.FilePaths;
                if (runnables.Count > 0 && string.Equals(runnables[^1], currentPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool HasLocalImageSource(WorkflowTabViewModel workflow)
        {
            var root = _workflowEngine.GetWorkflow(workflow.RootWorkflowId);
            return root?.Nodes.Any(node => IsImageLoadToolType(node.ToolType)
                && node.ImageSourceConfig?.SourceKind != SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera) == true;
        }

        private async Task<ExecutionResult> ExecuteSingleTriggeredAsync(
            WorkflowTabViewModel workflow,
            CancellationToken cancellationToken,
            IDictionary<string, object?>? globalVariablesOverride = null,
            Dictionary<string, long>? sequenceNumbers = null)
        {
            var cameraSource = ResolveSingleCameraSource(workflow);
            if (cameraSource is null)
                return await ExecuteWorkflowInternal(workflow, cancellationToken, globalVariablesOverride,
                    sequenceNumbers: sequenceNumbers);

            // 单次执行可从 WPF 命令直接进入；SDK 参数读取、启动取流、软件触发及
            // 停止取流均可能同步调用厂商 DLL，必须在工作线程进行。
            var feed = await Task.Run(() => CameraWorkflowFrameFeed.StartAsync(
                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime,
                new CameraDeviceRef(cameraSource.Value.CameraId),
                TimeSpan.FromMilliseconds(cameraSource.Value.TimeoutMs),
                cancellationToken), cancellationToken);
            try
            {
                using var frame = await Task.Run(() => feed.NextFrameAsync(cancellationToken), cancellationToken);
                return await ExecuteWorkflowInternal(workflow, cancellationToken, globalVariablesOverride,
                    frame, cameraSource.Value.NodeId, sequenceNumbers);
            }
            finally
            {
                await Task.Run(async () => await feed.DisposeAsync());
            }
        }

        /// <summary>
        /// 常规单相机工作流由帧就绪驱动。多个相机节点暂沿用既有执行路径，
        /// 本轮不增加图像源数量限制或猜测多相机同步关系。
        /// </summary>
        private CameraSourceBinding? ResolveSingleCameraSource(WorkflowTabViewModel workflow)
        {
            var root = _workflowEngine.GetWorkflow(workflow.RootWorkflowId);
            if (root is null)
                return null;

            var logger = VisionLogger.Instance.ForSource(workflow.Name);
            var overrides = CreateImageLoadParameterOverrides(root, logger, new HashSet<string>());
            var sources = overrides
                .Where(pair => pair.Value.TryGetValue("SourceKind", out var kind)
                    && kind is ImageSourceKind.Camera)
                .ToArray();
            if (sources.Length != 1)
                return null;

            var source = sources[0];
            var cameraId = source.Value.TryGetValue("CameraDeviceId", out var id)
                ? id as string : null;
            var timeout = source.Value.TryGetValue("CameraTimeoutMs", out var timeoutValue)
                && timeoutValue is int value ? value : 3000;
            if (string.IsNullOrWhiteSpace(cameraId) || timeout <= 0)
                return null;
            return new CameraSourceBinding(source.Key, cameraId, timeout);
        }

        private void CollectImageLoadParameterOverrides(
            SunEyeVision.Workflow.Workflow workflow,
            ILogger logger,
            HashSet<string> visitedIds,
            Dictionary<string, IReadOnlyDictionary<string, object?>> overrides)
        {
            if (workflow == null || !visitedIds.Add(workflow.Id)) return;

            foreach (var wn in workflow.Nodes)
            {
                if (wn == null) continue;

                // ★ 递归优先：子工作流遍历必须在 IsImageLoadToolType 判断之前，
                //   确保 Subroutine 节点（非 ImageLoad 类型）也能触发子工作流递归
                if (!string.IsNullOrEmpty(wn.SubroutineId))
                {
                    var subWorkflow = _workflowEngine.GetWorkflow(wn.SubroutineId);
                    if (subWorkflow != null)
                        CollectImageLoadParameterOverrides(subWorkflow, logger, visitedIds, overrides);
                }

                // 通过 ToolType 判断是否为图像载入节点（基类属性，无需强制转换为 SunEyeVision.UI.Shared.WorkflowNode）
                if (!IsImageLoadToolType(wn.ToolType))
                    continue;

                var config = wn.ImageSourceConfig;
                if (config?.SourceKind == SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera)
                {
                    overrides[wn.Id] = new Dictionary<string, object?>
                    {
                        ["SourceKind"] = SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera,
                        ["CameraDeviceId"] = config.CameraDeviceId,
                        ["CameraTimeoutMs"] = config.CameraTimeoutMs,
                        ["OutputFormat"] = config.OutputFormat,
                        ["SequenceNumber"] = config.SNInitialValue,
                        ["FilePath"] = string.Empty
                    };
                    continue;
                }

                var inputSource = wn.InputSource as ImageInputSource;
                var currentImage = inputSource?.CurrentImage;

                // InputSource 有当前图 → 在执行参数快照中覆盖路径。
                if (currentImage != null && !string.IsNullOrEmpty(currentImage.FilePath))
                {
                    overrides[wn.Id] = new Dictionary<string, object?>
                    {
                        ["SourceKind"] = SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local,
                        ["OutputFormat"] = config?.OutputFormat ?? SunEyeVision.Plugin.SDK.Vision.Contracts.ImageOutputFormat.Original,
                        ["SequenceNumber"] = config?.SNInitialValue ?? 1L,
                        ["FilePath"] = currentImage.FilePath
                    };
                    logger.LogInfo($"从 InputSource 创建路径快照: '{wn.Name}' -> '{currentImage.FilePath}'");
                }
                else
                {
                    // 显式覆盖为空，防止执行快照使用设计期残留路径。
                    overrides[wn.Id] = new Dictionary<string, object?>
                    {
                        ["SourceKind"] = SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local,
                        ["OutputFormat"] = config?.OutputFormat ?? SunEyeVision.Plugin.SDK.Vision.Contracts.ImageOutputFormat.Original,
                        ["SequenceNumber"] = config?.SNInitialValue ?? 1L,
                        ["FilePath"] = string.Empty
                    };
                    logger.LogWarning($"InputSource 无有效图像，已在运行快照清空 '{wn.Name}' 的 FilePath");
                }
            }
        }

        /// <summary>
        /// 判断工具类型是否为图像载入节点（基于 ToolType 字符串匹配）
        /// 用于 WorkflowNodeBase 基类，无需强制转换为 SunEyeVision.UI.Shared.WorkflowNode
        /// </summary>
        private static bool IsImageLoadToolType(string toolType)
        {
            return toolType != null && (
                toolType.Contains("ImageLoad", StringComparison.OrdinalIgnoreCase) ||
                toolType.Contains("图片载入", StringComparison.OrdinalIgnoreCase) ||
                toolType.Contains("ImageSource", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 递归在工作流树中查找指定 ID 的节点（含子工作流）
        /// </summary>
        private SunEyeVision.Workflow.WorkflowNodeBase? FindNodeRecursive(
            SunEyeVision.Workflow.Workflow workflow, string nodeId, HashSet<string>? visited = null)
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
                    var subWf = _workflowEngine.GetWorkflow(n.SubroutineId);
                    if (subWf != null)
                    {
                        var found = FindNodeRecursive(subWf, nodeId, visited);
                        if (found != null) return found;
                    }
                }
            }

            return null;
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
        private void OnNodeExecutionCompleted(SunEyeVision.UI.Shared.WorkflowNode node, ToolResults result, bool isCameraDriven)
        {
            var queuedAt = Stopwatch.GetTimestamp();
            InvokeOnUI(() => NodeExecutionCompleted?.Invoke(this,
                new NodeExecutionResultEventArgs(node, result, isCameraDriven,
                    Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds)));
        }

        /// <summary>
        /// 从节点执行结果创建 ToolResults
        /// </summary>
        private ToolResults CreateToolResultsFromNodeResult(NodeExecutionResult nodeResult)
        {
            // 优先使用原始工具结果
            if (nodeResult.Result != null)
            {
                return nodeResult.Result;
            }

            var results = new GenericToolResults
            {
                Status = nodeResult.Success ? ExecutionStatus.Success : ExecutionStatus.Failed,
                ExecutionTimeMs = (long)(nodeResult.Duration?.TotalMilliseconds ?? 0),
                Timestamp = DateTime.Now
            };

            // 添加错误消息
            if (nodeResult.ErrorMessages != null)
            {
                foreach (var error in nodeResult.ErrorMessages)
                {
                    results.AddWarning(error);
                }
                if (nodeResult.ErrorMessages.Count > 0)
                {
                    results.ErrorMessage = string.Join("; ", nodeResult.ErrorMessages);
                }
            }

            return results;
        }

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
            StopAllContinuousRun();

            lock (_lock)
            {
                foreach (var cts in _cancellationTokens.Values)
                {
                    cts.Dispose();
                }
                _cancellationTokens.Clear();
            }
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
        public ToolResults Result { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public NodeExecutionResultEventArgs(SunEyeVision.UI.Shared.WorkflowNode node, ToolResults result,
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
