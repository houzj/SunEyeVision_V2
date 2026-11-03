using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>One root reservation across all workflows and final result acceptance.</summary>
internal sealed class ExecutionRunBatch : IDisposable
{
    private readonly object _gate = new();
    private bool _activeWorkflow, _closed;
    internal ExecutionRunBatch(ToolExecutionOwner owner, RuntimeDirectorySnapshot directory)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        if (!directory.IsActive) throw new InvalidOperationException("运行目录已退役。 ");
        RootId = Guid.NewGuid();
        owner.ActivateRoot(RootId);
    }
    internal ToolExecutionOwner Owner { get; }
    internal RuntimeDirectorySnapshot Directory { get; }
    internal Guid RootId { get; }
    internal IDisposable Enter(ExecutionWorkflowSnapshot workflow)
    {
        lock (_gate)
        {
            if (_closed || _activeWorkflow || !ReferenceEquals(workflow.Owner, Owner)
                || !ReferenceEquals(workflow.Directory, Directory) || !Directory.IsActive)
                throw new InvalidOperationException("运行批次已关闭、正执行其他流程或目录/owner不一致。 ");
            _activeWorkflow = true;
            return new WorkflowLease(this);
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed) return;
            if (_activeWorkflow) throw new InvalidOperationException("流程运行尚未排空，不能释放根运行预约。 ");
            Owner.CompleteRoot(RootId);
            _closed = true;
        }
    }
    private sealed class WorkflowLease(ExecutionRunBatch batch) : IDisposable
    {
        private ExecutionRunBatch? _batch = batch;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _batch, null);
            if (current is not null) lock (current._gate) current._activeWorkflow = false;
        }
    }
}
