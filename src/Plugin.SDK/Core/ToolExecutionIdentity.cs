using System;
using System.Collections.Generic;
using System.Threading;

namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>方案/节点的运行准入Owner；退役与受控发布共享同一临界区。</summary>
public sealed class ToolExecutionOwner
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Guid> _domains = new(StringComparer.Ordinal);
    private sealed class ScopeAdmissions(Guid root)
    {
        internal Guid Root = root;
        internal readonly Dictionary<Guid, Guid> Requests = new();
    }
    private readonly Dictionary<string, ScopeAdmissions> _latest = new(StringComparer.Ordinal);
    private Guid _activeRoot;
    private bool _retired;
    private int _activeRuns;
    public int ActiveRuns { get { lock (_gate) return _activeRuns; } }
    public Guid Id { get; } = Guid.NewGuid();
    public event Action<string>? DomainRetired;

    /// <summary>Called only by the host's active root session, never by a late node admission.</summary>
    public void ActivateRoot(Guid rootRunId)
    {
        if(rootRunId==Guid.Empty)throw new ArgumentException("A root session identity is required.",nameof(rootRunId));
        lock(_gate)
        {
            if(_retired)throw new InvalidOperationException("运行Owner已退役。");
            if(_activeRoot!=Guid.Empty)throw new InvalidOperationException("A root session is already active.");
            if(_activeRuns!=0)throw new InvalidOperationException("Existing executions must drain before activating a root session.");
            _activeRoot=rootRunId;
            _latest.Clear();
        }
    }
    public void CompleteRoot(Guid rootRunId)
    {
        lock(_gate)
        {
            if(_activeRoot!=rootRunId)return;
            if(_activeRuns!=0)throw new InvalidOperationException("Root execution has not drained.");
            _activeRoot=Guid.Empty;
        }
    }

    public ToolExecutionIdentity Begin(string nodeId, Guid runId, string contractIdentity,
        CancellationToken cancellationToken = default, Func<bool>? admission = null, Guid rootRunId = default)
    {
        lock (_gate)
        {
            if (_retired) throw new InvalidOperationException("运行Owner已退役。");
            var root=rootRunId==Guid.Empty?runId:rootRunId;
            if((rootRunId!=Guid.Empty && _activeRoot!=root) || (rootRunId==Guid.Empty && _activeRoot!=Guid.Empty))
                throw new InvalidOperationException("Node admission does not belong to the host's active root session.");
            if (!_domains.TryGetValue(nodeId, out var generation)) _domains[nodeId] = generation = Guid.NewGuid();
            var requestId = Guid.NewGuid();
            if(!_latest.TryGetValue(nodeId,out var admitted) || admitted.Root!=root)
                _latest[nodeId]=admitted=new ScopeAdmissions(root);
            admitted.Requests[runId]=requestId;
            return new ToolExecutionIdentity(this, nodeId, generation, runId, requestId, contractIdentity, cancellationToken, admission, rootRunId);
        }
    }

    internal bool Publish(ToolExecutionIdentity identity, Action? publish, bool callback = false)
    {
        lock (_gate)
        {
            if (_retired || (callback && !identity.CallbacksOpen) || identity.CancellationToken.IsCancellationRequested ||
                !_domains.TryGetValue(identity.NodeId, out var generation) || generation != identity.NodeGeneration ||
                !_latest.TryGetValue(identity.NodeId, out var admitted) || admitted.Root!=identity.RootRunId ||
                !admitted.Requests.TryGetValue(identity.RunId,out var request) || request != identity.RequestId || identity.Admission?.Invoke() == false)
                return false;
            publish?.Invoke();
            return true;
        }
    }
    internal void CompleteCallbacks(ToolExecutionIdentity identity)
    {
        lock (_gate) identity.CloseCallbacks();
    }
    internal void RefineContract(ToolExecutionIdentity identity, string contractIdentity)
    {
        lock (_gate)
        {
            if (!Publish(identity, null)) throw new InvalidOperationException("运行身份已失效，不能修改合同元数据。");
            identity.SetContractIdentity(contractIdentity);
        }
    }

    public IDisposable EnterRun(ToolExecutionIdentity identity)
    {
        lock (_gate)
        {
            if (!identity.CallbacksOpen || !Publish(identity, null) || !identity.TryStartExecution())
                throw new InvalidOperationException("运行身份已失效、已执行或回调已关闭。");
            _activeRuns++;
            return new RunLease(this);
        }
    }
    private sealed class RunLease(ToolExecutionOwner owner) : IDisposable
    {
        private ToolExecutionOwner? _owner = owner;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            if (current != null) lock (current._gate) current._activeRuns--;
        }
    }

    public void RetireNode(string nodeId)
    {
        string? domain = null;
        lock (_gate)
            if (_domains.Remove(nodeId, out var generation))
            { _latest.Remove(nodeId); domain = Domain(nodeId, generation); }
        if (domain != null) DomainRetired?.Invoke(domain);
    }

    public void Retire()
    {
        List<string> domains = new();
        lock (_gate)
        {
            if (_retired) return;
            if (_activeRuns != 0) throw new InvalidOperationException("脚本仍在运行或清理，方案保持打开；请停止并等待排空。");
            _retired = true;
            foreach (var domain in _domains) domains.Add(Domain(domain.Key, domain.Value));
            _domains.Clear(); _latest.Clear();
        }
        foreach (var domain in domains) DomainRetired?.Invoke(domain);
    }
    internal string Domain(string nodeId, Guid generation) => $"{Id:N}/{nodeId}/{generation:N}";
}

public sealed class ToolExecutionIdentity
{
    private sealed class AttemptState(string contractIdentity)
    {
        internal string ContractIdentity = contractIdentity;
        internal bool CallbacksOpen = true;
        internal bool ExecutionStarted;
    }
    private readonly AttemptState _attempt;
    internal ToolExecutionIdentity(ToolExecutionOwner owner, string nodeId, Guid generation, Guid runId, Guid requestId,
        string contractIdentity, CancellationToken cancellationToken, Func<bool>? admission, Guid rootRunId = default)
        : this(owner,nodeId,generation,runId,requestId,cancellationToken,admission,rootRunId,new AttemptState(contractIdentity)) { }
    private ToolExecutionIdentity(ToolExecutionOwner owner, string nodeId, Guid generation, Guid runId, Guid requestId,
        CancellationToken cancellationToken, Func<bool>? admission, Guid rootRunId, AttemptState attempt)
    { Owner = owner; NodeId = nodeId; NodeGeneration = generation; RunId = runId; RootRunId = rootRunId == Guid.Empty ? runId : rootRunId; RequestId = requestId; CancellationToken = cancellationToken; Admission = admission; _attempt=attempt; }
    public ToolExecutionOwner Owner { get; }
    public string NodeId { get; }
    public Guid NodeGeneration { get; }
    public Guid RunId { get; }
    public Guid RootRunId { get; }
    public Guid RequestId { get; }
    public string ContractIdentity => Volatile.Read(ref _attempt.ContractIdentity);
    public CancellationToken CancellationToken { get; }
    internal Func<bool>? Admission { get; }
    internal bool CallbacksOpen => _attempt.CallbacksOpen;
    internal bool HasStartedExecution => _attempt.ExecutionStarted;
    internal bool TryStartExecution()
    { if(_attempt.ExecutionStarted)return false;_attempt.ExecutionStarted=true;return true; }
    internal void CloseCallbacks() => _attempt.CallbacksOpen=false;
    internal void SetContractIdentity(string value) => Volatile.Write(ref _attempt.ContractIdentity,value);
    public string Domain => Owner.Domain(NodeId, NodeGeneration);
    public bool IsCurrent => Owner.Publish(this, null);
    public bool TryPublish(Action publish) => Owner.Publish(this, publish);
    public bool TryInvokeCallback(Action callback) => Owner.Publish(this, callback, callback: true);
    public void CompleteCallbacks() => Owner.CompleteCallbacks(this);
    public ToolExecutionIdentity WithContractIdentity(string contractIdentity)
    {
        Owner.RefineContract(this,contractIdentity);
        return new(Owner, NodeId, NodeGeneration, RunId, RequestId, CancellationToken, Admission, RootRunId, _attempt);
    }
    /// <summary>Project the existing admission identity without parsing the composite SDK node key or minting another request.</summary>
    public SunEyeVision.Plugin.Contracts.RunIdentity ToInvocationIdentity(string localNodeId)
    {
        if (string.IsNullOrWhiteSpace(localNodeId)) throw new ArgumentException("A scope-local node identity is required.", nameof(localNodeId));
        return new(Owner.Id, RootRunId, RunId, localNodeId, RequestId);
    }
}
