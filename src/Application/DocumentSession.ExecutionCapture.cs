using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

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
            EnsureOwnerCanEdit();
            _preparing = true;
            try
            {
                _ = RequireCurrent(captured, executionOnly: true);
                var config = captured.Configuration;
                return new ExecutionNodeSnapshot(captured.Scope.WorkflowId, captured.Node.Id, config.Configuration,
                    config.Definition, config.Directory, config.Schema, config.SettingsSchema, config.DisplaySchema,
                    config.ReferenceBehaviors, config.BindingNames, config.ActiveInputs, config.HostDeclarations, _owner);
            }
            finally { _preparing = false; }
        }
    }
}
