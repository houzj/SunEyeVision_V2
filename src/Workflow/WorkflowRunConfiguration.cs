using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Workflow;

/// <summary>Stable-identity configuration of a complete root/subworkflow execution generation.</summary>
public sealed class WorkflowRunConfiguration
{
    private readonly IReadOnlyDictionary<string, Workflow> _workflows;
    private WorkflowRunConfiguration(IEnumerable<Workflow> workflows)
    {
        var shell = new Solution { Workflows = workflows.ToList() };
        var copied = BindingConfigurationStore.Copy(shell).Workflows;
        var originals = shell.Workflows.ToDictionary(w => w.Id, StringComparer.Ordinal);
        foreach (var flow in copied) flow.ExecutionOwner = originals[flow.Id].ExecutionOwner;
        _workflows = copied.ToDictionary(w => w.Id, StringComparer.Ordinal);
    }
    public static WorkflowRunConfiguration Capture(IEnumerable<Workflow> workflows) => new(workflows);
    public static WorkflowRunConfiguration Capture(string rootWorkflowId, Func<string, Workflow?> resolve)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var sources = new List<Workflow>();
        void Visit(string id)
        {
            if (!visited.Add(id)) return;
            var flow = resolve(id);
            if (flow is null) return; // The existing definition validator reports missing references.
            sources.Add(flow);
            foreach (var child in flow.Nodes.Select(n => n.SubroutineId).Where(id => !string.IsNullOrWhiteSpace(id))) Visit(child!);
        }
        Visit(rootWorkflowId);
        return new(sources);
    }
    internal Workflow? Find(string id) => _workflows.GetValueOrDefault(id);
    public IReadOnlyList<Workflow> CopyWorkflows()
    {
        var copy = BindingConfigurationStore.Copy(new Solution { Workflows = _workflows.Values.ToList() }).Workflows;
        foreach (var flow in copy) flow.ExecutionOwner = _workflows[flow.Id].ExecutionOwner;
        return copy;
    }
    public Workflow? CopyWorkflow(string id)
    {
        if (!_workflows.TryGetValue(id, out var flow)) return null;
        var copy = BindingConfigurationStore.Copy(new Solution { Workflows = new() { flow } }).Workflows[0];
        copy.ExecutionOwner = flow.ExecutionOwner;
        return copy;
    }
}
