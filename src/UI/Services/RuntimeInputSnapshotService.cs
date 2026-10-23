using System;
using System.Collections.Generic;

namespace SunEyeVision.UI.Services
{
    /// <summary>运行态输入的线程安全快照。UI 负责发布选择，执行层只读取快照。</summary>
    public sealed record RuntimeInputSnapshot(
        string WorkflowId,
        string ImagePath,
        int ImageIndex,
        long Version,
        DateTimeOffset PublishedAt);

    public interface IRuntimeInputSnapshotService
    {
        RuntimeInputSnapshot Publish(string workflowId, string imagePath, int imageIndex);
        RuntimeInputSnapshot? GetSnapshot(string workflowId);
    }

    public sealed class RuntimeInputSnapshotService : IRuntimeInputSnapshotService
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, RuntimeInputSnapshot> _snapshots = new();
        private long _version;

        public RuntimeInputSnapshot Publish(string workflowId, string imagePath, int imageIndex)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
            var snapshot = new RuntimeInputSnapshot(
                workflowId, imagePath ?? string.Empty, imageIndex,
                System.Threading.Interlocked.Increment(ref _version), DateTimeOffset.UtcNow);
            lock (_gate)
            {
                _snapshots[workflowId] = snapshot;
            }
            return snapshot;
        }

        public RuntimeInputSnapshot? GetSnapshot(string workflowId)
        {
            lock (_gate)
            {
                return _snapshots.TryGetValue(workflowId, out var snapshot) ? snapshot : null;
            }
        }

    }
}
