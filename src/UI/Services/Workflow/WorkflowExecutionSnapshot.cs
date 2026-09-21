using System;
using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Workflow;

/// <summary>供 UI 展示的工作流长期执行快照；每个工作流只保留最近一次。</summary>
public sealed class WorkflowExecutionSnapshot : ViewModelBase
{
    private const string SubroutineInputsNodeId = "__SubroutineInputs__";
    private ExecutionStatus _status = ExecutionStatus.NotExecuted;
    private DateTime? _startedAt;
    private DateTime? _endedAt;
    private TimeSpan _elapsed;

    public WorkflowExecutionSnapshot(string workflowId, string workflowName)
    {
        WorkflowId = workflowId;
        WorkflowName = workflowName;
    }

    public string WorkflowId { get; }
    public string WorkflowName { get; private set; }
    public ExecutionStatus Status { get => _status; private set => SetProperty(ref _status, value); }
    public DateTime? StartedAt { get => _startedAt; private set => SetProperty(ref _startedAt, value); }
    public DateTime? EndedAt { get => _endedAt; private set => SetProperty(ref _endedAt, value); }
    public TimeSpan Elapsed { get => _elapsed; private set => SetProperty(ref _elapsed, value); }
    public ObservableCollection<WorkflowNodeExecutionSnapshot> Nodes { get; } = new();

    public string StatusText => Status switch
    {
        ExecutionStatus.Running => "运行中…",
        ExecutionStatus.Success => "成功",
        ExecutionStatus.Cancelled => "已取消",
        ExecutionStatus.Failed or ExecutionStatus.Timeout => "失败",
        _ => "尚未运行"
    };

    public string StatusBarText => Status switch
    {
        ExecutionStatus.Success => $"{Elapsed.TotalMilliseconds:F2} ms",
        ExecutionStatus.Running => $"运行中 {Elapsed.TotalMilliseconds:F2} ms",
        _ => StatusText
    };

    internal void Begin(string workflowName)
    {
        WorkflowName = workflowName;
        Status = ExecutionStatus.Running;
        StartedAt = DateTime.Now;
        EndedAt = null;
        Elapsed = TimeSpan.Zero;
        foreach (var node in Nodes)
            node.ResetForRun();
        Notify();
    }

    /// <summary>
    /// 同步设计期节点清单。快照本身长期存在，未运行时也必须能呈现该清单；
    /// 运行中已有的状态和耗时不被此方法重置。
    /// </summary>
    internal void SynchronizeDefinition(string workflowName, IEnumerable<WorkflowNodeBase> nodes)
    {
        WorkflowName = workflowName;
        foreach (var workflowNode in nodes)
        {
            var displayName = !string.IsNullOrWhiteSpace(workflowNode.DispName)
                ? workflowNode.DispName
                : !string.IsNullOrWhiteSpace(workflowNode.Name) ? workflowNode.Name : workflowNode.Id;
            var snapshotNode = Nodes.FirstOrDefault(item => item.NodeId == workflowNode.Id);
            if (snapshotNode == null)
                Nodes.Add(new WorkflowNodeExecutionSnapshot(workflowNode.Id, displayName, "尚未运行"));
            else
                snapshotNode.UpdateName(displayName);
        }
        Notify();
    }

    internal void UpdateNodeStatus(string nodeId, string nodeName, string status)
    {
        if (IsInternalNode(nodeId))
            return;

        var node = Nodes.FirstOrDefault(item => item.NodeId == nodeId);
        if (node == null)
        {
            Nodes.Add(new WorkflowNodeExecutionSnapshot(nodeId, nodeName, status));
        }
        else
        {
            node.Update(nodeName, status, null, null);
        }
    }

    internal void UpdateNodeResult(string nodeId, string nodeName, NodeExecutionResult result)
    {
        if (IsInternalNode(nodeId))
            return;

        var status = result.Status switch
        {
            ExecutionStatus.Success => "成功",
            ExecutionStatus.Cancelled => "已取消",
            ExecutionStatus.Timeout => "超时",
            ExecutionStatus.Failed => "失败",
            _ => result.Success ? "成功" : "失败"
        };
        var node = Nodes.FirstOrDefault(item => item.NodeId == nodeId);
        if (node == null)
        {
            Nodes.Add(new WorkflowNodeExecutionSnapshot(
                nodeId,
                nodeName,
                status,
                result.Duration ?? TimeSpan.Zero,
                result.Result?.ExecutionTimeMs ?? 0));
        }
        else
        {
            node.Update(nodeName, status, result.Duration ?? TimeSpan.Zero, result.Result?.ExecutionTimeMs ?? 0);
        }
    }

    internal void Complete(ExecutionResult result, Func<string, string> nodeNameResolver)
    {
        Status = result.Status;
        EndedAt = DateTime.Now;
        Elapsed = result.ExecutionTime;
        foreach (var item in result.NodeResults.Values
                     .Where(item => !IsInternalNode(item.NodeId))
                     .OrderBy(node => node.StartTime))
        {
            UpdateNodeResult(item.NodeId, nodeNameResolver(item.NodeId), item);
        }
        Notify();
    }

    internal void Tick()
    {
        if (Status == ExecutionStatus.Running && StartedAt.HasValue)
        {
            Elapsed = DateTime.Now - StartedAt.Value;
            OnPropertyChanged(nameof(StatusBarText));
        }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(WorkflowName));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBarText));
    }

    private static bool IsInternalNode(string nodeId) =>
        string.Equals(nodeId, SubroutineInputsNodeId, StringComparison.Ordinal);
}

public sealed class WorkflowNodeExecutionSnapshot : ViewModelBase
{
    private string _nodeName;
    private string _status;
    private TimeSpan _moduleElapsed;
    private double _algorithmElapsedMs;

    public WorkflowNodeExecutionSnapshot(string nodeId, string nodeName, string status,
        TimeSpan moduleElapsed = default, double algorithmElapsedMs = 0)
    {
        NodeId = nodeId;
        _nodeName = nodeName;
        _status = status;
        ModuleElapsed = moduleElapsed;
        AlgorithmElapsedMs = algorithmElapsedMs;
    }
    public string NodeId { get; }
    public string NodeName { get => _nodeName; private set => SetProperty(ref _nodeName, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public TimeSpan ModuleElapsed { get => _moduleElapsed; private set => SetProperty(ref _moduleElapsed, value); }
    public double AlgorithmElapsedMs { get => _algorithmElapsedMs; private set => SetProperty(ref _algorithmElapsedMs, value); }

    internal void Update(string nodeName, string status, TimeSpan? moduleElapsed, double? algorithmElapsedMs)
    {
        NodeName = nodeName;
        Status = status;
        if (moduleElapsed.HasValue)
            ModuleElapsed = moduleElapsed.Value;
        if (algorithmElapsedMs.HasValue)
            AlgorithmElapsedMs = algorithmElapsedMs.Value;
    }

    internal void UpdateName(string nodeName) => NodeName = nodeName;

    internal void ResetForRun()
    {
        Status = "等待中";
        ModuleElapsed = TimeSpan.Zero;
        AlgorithmElapsedMs = 0;
    }
}
