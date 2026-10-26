using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.Workflow;
/// <summary>恢复VM执行边合同：按有向节点对唯一，不以端口对区分边；拒绝自连与执行环。</summary>
public static class ExecutionConnectionPolicy
{
    public static string? Rejection(IEnumerable<WorkflowConnection> connections, WorkflowConnection candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.SourceNodeId) || string.IsNullOrWhiteSpace(candidate.TargetNodeId)) return "连线端点不能为空。";
        if (candidate.SourceNodeId == candidate.TargetNodeId) return "节点不能连接自身。";
        var edges = connections.ToArray();
        if (edges.Any(c => c.SourceNodeId == candidate.SourceNodeId && c.TargetNodeId == candidate.TargetNodeId))
            return "这两个节点之间已有执行连线，换端口不能再添加第二条线。";
        var outgoing = edges.GroupBy(c => c.SourceNodeId).ToDictionary(g => g.Key, g => g.Select(c => c.TargetNodeId).ToArray());
        var pending = new Stack<string>(); var visited = new HashSet<string>(); pending.Push(candidate.TargetNodeId);
        while (pending.Count > 0)
        {
            var id = pending.Pop(); if (id == candidate.SourceNodeId) return "连接将形成执行环，已取消。";
            if (!visited.Add(id)) continue;
            if (outgoing.TryGetValue(id, out var targets)) foreach (var next in targets) pending.Push(next);
        }
        return null;
    }
    public static void EnsureCanAdd(IEnumerable<WorkflowConnection> connections, WorkflowConnection candidate)
    {
        var reason = Rejection(connections, candidate);
        if (reason != null) throw new InvalidOperationException(reason);
    }
}