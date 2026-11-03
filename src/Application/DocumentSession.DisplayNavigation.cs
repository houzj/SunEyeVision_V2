using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed record NodeDisplayNavigationChange(Guid EditSessionId, string WorkflowId, NodeDefinition Node,
    long NavigationRevision, string? DisplayReference);

public sealed partial class DocumentSession
{
    public event EventHandler<NodeDisplayNavigationChange>? NodeDisplayNavigationChanged;

    /// <summary>Persisted image selection only. Does not run a tool, mutate its parameters or add undo history.</summary>
    public void SetNodeDisplayReference(string workflowId, NodeDefinition node, string? canonicalOutputReference)
    {
        ArgumentNullException.ThrowIfNull(node);
        NodeDisplayNavigationChange change;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope = OpenScope(workflowId);
            var target = RequireNode(scope, node.Id);
            if (!ReferenceEquals(target.Node, node)) throw new InvalidOperationException("显示导航目标不是当前实际节点。");
            string? canonical = null;
            if (canonicalOutputReference is not null)
            {
                var reference = ParameterSettingCanonical.Deserialize(canonicalOutputReference);
                if (reference.Selection is not (BindingSelection.AllItems or BindingSelection.AtConstant) ||
                    ParameterReferenceRules.SelectedType(reference) != TypeRef.ResourceOf(ResourceKind.Image))
                    throw new ArgumentException("显示导航只接受单幅图像或明确的静态图像索引。", nameof(canonicalOutputReference));
                var graph = GraphState.Capture(scope.Flow);
                var incoming = graph.Edges.GroupBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).ToArray(), StringComparer.Ordinal);
                var eligible = new HashSet<string>(StringComparer.Ordinal) { node.Id };
                var pending = new Stack<string>(); pending.Push(node.Id);
                while (pending.TryPop(out var current))
                    if (incoming.TryGetValue(current, out var sources))
                        foreach (var source in sources) if (eligible.Add(source)) pending.Push(source);
                var declarations = graph.Nodes.Where(item => eligible.Contains(item.Node.Id))
                    .Select(item => new BindingSource(new(workflowId, item.Node.Id), Guid.Empty,
                        RequireNode(scope, item.Node.Id).Configuration.Schema, 0)).ToArray();
                var bindingScope = new ResultDisplayBindingScope(new(workflowId, node.Id), ResultOwnerId, ChangeRevision, declarations);
                var compiled = new ParameterBindingService().CompileBinding(reference,
                    new Port("display-image", TypeRef.ResourceOf(ResourceKind.Image)), bindingScope);
                if (compiled.Plan is null) throw new ArgumentException(compiled.Diagnostic?.Message ?? "图像来源不属于前序或自身声明。", nameof(canonicalOutputReference));
                ValidateScopeCore(scope);
                if (!graph.MatchesCurrent(scope.Flow)) throw new InvalidOperationException("显示导航准备期间图已改变。");
                canonical = ParameterSettingCanonical.Serialize(reference);
            }
            if (node.SelectedDisplayReference == canonical) return;
            var revision = checked(_navigationRevision + 1);
            node.SelectedDisplayReference = canonical;
            _navigationRevision = revision;
            change = new(EditSessionId, workflowId, node, revision, canonical);
        }
        var failures = new List<Exception>();
        _notifying = true;
        try
        {
            foreach (EventHandler<NodeDisplayNavigationChange> handler in NodeDisplayNavigationChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { handler(this, change); } catch (Exception error) { failures.Add(error); }
        }
        finally { _notifying = false; }
        LastObserverError = failures.Count == 0 ? null : new AggregateException("图像来源导航观察者失败。", failures);
    }
}
