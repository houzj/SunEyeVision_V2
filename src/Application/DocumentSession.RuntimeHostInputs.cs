using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Capture host facts only for an actual admitted node in the current document.</summary>
    public HostInputFactoryContext? CaptureHostInputContext(string workflowId, NodeDefinition actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        EnsureOwnerCanEdit();
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope = OpenScope(workflowId);
            var view = RequireNode(scope, actual.Id);
            if (!ReferenceEquals(view.Node, actual)) throw new InvalidOperationException("宿主输入节点不是当前流程的实际实例。");
            return CaptureHostInputContextCore(scope, view);
        }
    }

    private HostInputFactoryContext? CaptureHostInputContextCore(WorkflowScope scope, AdmittedNodeView view)
    {
        if (view.Configuration.HostDeclarations.HostInputs is not { } declaration) return null;
        var configuration = view.Configuration.Configuration;
        var fields = new FieldSettings(configuration.Fields.Declarations.Values, configuration.Fields.Capture());
        var lifetime = CaptureNodeLifetime(scope, view.Node);
        return new HostInputFactoryContext(new ScopedNodeIdentity(scope.WorkflowId, view.Node.Id), EditSessionId,
            lifetime.Generation, lifetime.Retirement, configuration.ToolId, view.Configuration.Schema,
            view.Configuration.SettingsSchema, fields, configuration.CaptureCurrentValues(), declaration,
            new SunEyeVision.Persistence.SolutionResourceResolver(_resourceBaseDirectory,_embeddedFiles), () =>
            {
                if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("宿主输入上下文必须由方案所属线程使用。");
                lock (_gate)
                {
                    EnsureOpen(); ValidateScopeCore(scope);
                    if (lifetime.Retirement.IsCancellationRequested ||
                        scope.Flow.Nodes.Count(node => node.Id == view.Node.Id) != 1 ||
                        !scope.Flow.Nodes.Any(node => ReferenceEquals(node, view.Node)) ||
                        !ReferenceEquals(view.Node.Configuration, configuration))
                        throw new InvalidOperationException("宿主输入所属节点或配置已失效。");
                }
            }, configurationRevisions:CaptureNodeFieldRevisions(view));
    }
}
