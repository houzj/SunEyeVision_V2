using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Workflow;

/// <summary>
/// 单次根运行的资源租约管理器。
/// 调度器只有同时取得节点全部租约时才允许启动节点，完成后必须释放。
/// </summary>
public sealed class WorkflowResourceLeaseManager
{
    private readonly object _gate = new();
    private readonly HashSet<string> _leasedResources = new(StringComparer.Ordinal);

    public bool TryAcquire(string workflowId, string nodeId, ToolExecutionProfile profile, out IDisposable lease)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("工作流 ID 不能为空。", nameof(workflowId));
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("节点 ID 不能为空。", nameof(nodeId));

        profile ??= ToolExecutionProfile.LegacyExclusive;
        var resourceKeys = GetResourceKeys(workflowId, nodeId, profile).ToList();
        lock (_gate)
        {
            if (resourceKeys.Any(_leasedResources.Contains))
            {
                lease = EmptyLease.Instance;
                return false;
            }

            foreach (var resourceKey in resourceKeys)
                _leasedResources.Add(resourceKey);
            lease = new ResourceLease(this, resourceKeys);
            return true;
        }
    }

    private static IEnumerable<string> GetResourceKeys(
        string workflowId,
        string nodeId,
        ToolExecutionProfile profile)
    {
        if (profile.ConcurrencyKind == ToolConcurrencyKind.WorkflowExclusive)
            yield return $"workflow:{workflowId}";

        if (profile.ConcurrencyKind == ToolConcurrencyKind.ResourceExclusive)
        {
            var keys = profile.ResourceKeys?.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
            if (keys == null || keys.Count == 0)
                yield return $"node:{workflowId}:{nodeId}";
            else
                foreach (var key in keys)
                    yield return $"resource:{key}";
        }
    }

    private void Release(IEnumerable<string> resourceKeys)
    {
        lock (_gate)
        {
            foreach (var resourceKey in resourceKeys)
                _leasedResources.Remove(resourceKey);
        }
    }

    private sealed class ResourceLease : IDisposable
    {
        private WorkflowResourceLeaseManager? _owner;
        private readonly IReadOnlyList<string> _resourceKeys;

        public ResourceLease(WorkflowResourceLeaseManager owner, IReadOnlyList<string> resourceKeys)
        {
            _owner = owner;
            _resourceKeys = resourceKeys;
        }

        public void Dispose()
        {
            var owner = System.Threading.Interlocked.Exchange(ref _owner, null);
            owner?.Release(_resourceKeys);
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();
        public void Dispose() { }
    }
}
