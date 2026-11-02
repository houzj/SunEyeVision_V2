using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

/// <summary>Stable-identity configuration of a complete root/subworkflow execution generation.</summary>
public sealed class WorkflowRunConfiguration
{
    private readonly IReadOnlyDictionary<string, Workflow> _workflows;
    private readonly RuntimeDirectorySnapshot _directory;
    private WorkflowRunConfiguration(IEnumerable<Workflow> workflows,RuntimeDirectorySnapshot directory,long documentRevision)
    {
        _directory=directory??throw new ArgumentNullException(nameof(directory));
        if(!_directory.IsActive)throw new InvalidOperationException("运行目录已退役。");
        if(documentRevision<0)throw new ArgumentOutOfRangeException(nameof(documentRevision));
        DocumentRevision=documentRevision;
        var shell = new Solution { Workflows = workflows.ToList(), RuntimeDirectory = _directory };
        foreach (var flow in shell.Workflows)
        {
            if (flow.IsSubroutine) throw new InvalidOperationException("Group / 控制流当前尚未接入正式运行。");
            foreach (var node in flow.Nodes) RuntimeNodeAdmission.Require(_directory,node);
        }
        var copied = BindingConfigurationStore.Copy(shell).Workflows;
        var originals = shell.Workflows.ToDictionary(w => w.Id, StringComparer.Ordinal);
        foreach (var flow in copied) flow.ExecutionOwner = originals[flow.Id].ExecutionOwner;
        _workflows = copied.ToDictionary(w => w.Id, StringComparer.Ordinal);
    }
    public long DocumentRevision {get;}
    public RuntimeDirectorySnapshot Directory => _directory;
    public static WorkflowRunConfiguration Capture(IEnumerable<Workflow> workflows,RuntimeDirectorySnapshot directory,long documentRevision=0) => new(workflows,directory,documentRevision);
    public static WorkflowRunConfiguration Capture(string rootWorkflowId, Func<string, Workflow?> resolve,RuntimeDirectorySnapshot directory,long documentRevision=0)
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
        return new(sources,directory,documentRevision);
    }
    public RootWorkspaceSettings GetRootSettings(string id)
    {
        var flow=_workflows.GetValueOrDefault(id)??throw new InvalidOperationException("运行快照没有该根流程。");
        if(flow.IsSubroutine)throw new InvalidOperationException("不能将Group当作根流程读取运行策略。");
        return flow.WorkspaceSettings;
    }
    internal Workflow? Find(string id) => _workflows.GetValueOrDefault(id);
    internal IEnumerable<Workflow> Definitions => _workflows.Values;
    internal void RequireDirectory(RuntimeDirectorySnapshot expected)
    {
        if(!ReferenceEquals(_directory,expected)||!_directory.IsActive)
            throw new InvalidOperationException("运行配置的目录代次已变更或退役。");
    }
    public IReadOnlyList<Workflow> CopyWorkflows()
    {
        RequireDirectory(_directory);
        var copy = BindingConfigurationStore.Copy(new Solution { Workflows = _workflows.Values.ToList(), RuntimeDirectory = _directory }).Workflows;
        foreach (var flow in copy) flow.ExecutionOwner = _workflows[flow.Id].ExecutionOwner;
        return copy;
    }
    public Workflow? CopyWorkflow(string id)
    {
        RequireDirectory(_directory);
        if (!_workflows.TryGetValue(id, out var flow)) return null;
        var copy = BindingConfigurationStore.Copy(new Solution { Workflows = new() { flow }, RuntimeDirectory = _directory }).Workflows[0];
        copy.ExecutionOwner = flow.ExecutionOwner;
        return copy;
    }
}
