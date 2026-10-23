using System;

using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.SDK.Scripting
{
    /// <summary>
    /// 脚本上下文 — 内部实现，桥接宿主和脚本实例。
    /// Script 通过此对象访问外部委托，用户无需直接接触。
    /// </summary>
    public sealed class ScriptContext
    {
        private int _completed;
        private readonly object _workGate = new();
        private readonly List<Task> _tasks = new();
        private readonly List<Action> _cleanup = new();
        private bool _registrationClosed;
        public void RegisterTask(Task task)
        {
            ArgumentNullException.ThrowIfNull(task);
            lock (_workGate)
            {
                if (_registrationClosed) throw new InvalidOperationException("本轮任务登记已关闭。");
                _tasks.Add(task);
            }
        }
        public void RegisterCleanup(Action cleanup)
        {
            ArgumentNullException.ThrowIfNull(cleanup);
            lock (_workGate)
            {
                if (_registrationClosed) throw new InvalidOperationException("本轮清理登记已关闭。");
                _cleanup.Add(cleanup);
            }
        }
        public void DrainRegisteredTasks()
        {
            Task[] tasks;
            lock (_workGate) { _registrationClosed = true; tasks = _tasks.ToArray(); _tasks.Clear(); }
            Task.WhenAll(tasks).GetAwaiter().GetResult();
        }
        public void ReleaseRegisteredResources()
        {
            Action[] cleanup;
            lock (_workGate) { cleanup = _cleanup.ToArray(); _cleanup.Clear(); }
            List<Exception>? errors = null;
            for (var index = cleanup.Length - 1; index >= 0; index--)
                try { cleanup[index](); } catch (Exception ex) { (errors ??= new()).Add(ex); }
            if (errors != null) throw new AggregateException("登记资源清理失败", errors);
        }
        public CancellationToken CancellationToken { get; init; }
        public Guid RunId { get; init; }
        public Guid RootRunId { get; init; }
        public string? NodeId { get; init; }
        public string? ContractIdentity { get; init; }
        public Func<bool>? IsExecutionCurrent { get; init; }
        public void ThrowIfCancellationRequested()
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (System.Threading.Volatile.Read(ref _completed) != 0 || IsExecutionCurrent?.Invoke() == false)
                throw new OperationCanceledException("脚本运行身份已失效。", CancellationToken);
        }
        #region 委托注入

        public Func<string, object?>? GetGlobalValue { get; set; }
        public Action<string, object?>? SetGlobalValue { get; set; }
        public Action<string>? LogInfoAction { get; set; }
        public Action<string>? LogWarningAction { get; set; }
        public Action<string>? LogErrorAction { get; set; }

        #endregion

        #region 公开方法（由 Script 调用）

        public T? GetGlobal<T>(string name) where T : class
        {
            ThrowIfCancellationRequested();
            var value = GetGlobalValue?.Invoke(name);
            return value as T;
        }

        public void SetGlobal(string name, object? value)
        {
            ThrowIfCancellationRequested();
            SetGlobalValue?.Invoke(name, value);
        }

        public void LogInfo(string message) { if (CanPublish) LogInfoAction?.Invoke(message); }
        public void LogWarning(string message) { if (CanPublish) LogWarningAction?.Invoke(message); }
        public void LogError(string message) { if (CanPublish) LogErrorAction?.Invoke(message); }
        public void CompletePublications() => System.Threading.Interlocked.Exchange(ref _completed, 1);
        private bool CanPublish => System.Threading.Volatile.Read(ref _completed) == 0 && !CancellationToken.IsCancellationRequested && IsExecutionCurrent?.Invoke() != false;

        #endregion
    }
}
