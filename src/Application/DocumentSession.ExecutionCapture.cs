using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using System.Runtime.CompilerServices;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    // The whole run is frozen by the production manager; this signs one already captured node.
    // The execution assembly gets no session or mutable graph and cannot self-admit a definition.
    internal ExecutionNodeSnapshot IssueExecutionNode(AdmittedNodeView captured)
    {
        ArgumentNullException.ThrowIfNull(captured);
        lock (_gate)
        {
            if (!_preparing) throw new InvalidOperationException("节点执行快照必须在整批捕获门禁中签发。 ");
            _ = RequireCurrent(captured, executionOnly: true);
            var config = captured.Configuration;
            // The identity string describes execution fields, but accepted runtime values can
            // produce a new configuration object with the same canonical payload. Keep the
            // live configuration reference in the cache key so parameter acceptance never
            // receives a snapshot bound to an older configuration instance.
            var cacheKey = captured.Scope.WorkflowId + "\u001f" + captured.Node.Id + "\u001f" +
                RuntimeHelpers.GetHashCode(config.Configuration) + "\u001f" + config.ExecutionConfigurationIdentity;
            if (_executionSnapshotCache.TryGetValue(cacheKey, out var cached))
                return cached;
            var snapshot = new ExecutionNodeSnapshot(captured.Scope.WorkflowId, captured.Node.Id, config.Configuration,
                config.Definition, config.Directory, config.Schema, config.SettingsSchema, config.DisplaySchema,
                config.ReferenceBehaviors, config.BindingNames, config.ActiveInputs, config.DormantParameterDefaults, config.HostDeclarations,
                config.ManualReferenceSources, _owner);
            _ = RequireCurrent(captured, executionOnly: true);
            _executionSnapshotCache[cacheKey] = snapshot;
            return snapshot;
        }
    }
}
