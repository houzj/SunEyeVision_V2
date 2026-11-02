using SunEyeVision.Plugin.Contracts;
using BindingCompatibility = SunEyeVision.Plugin.Contracts.BindingCompatibility;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

public sealed record OutputBindingChange(string NodeName, string InputName, string Reason, string NodeId, string? WorkflowId = null);
public sealed record ExecutionInputIssue(string InputKey, string InputName, string Message);

/// <summary>Design-time formal reconciliation. No controls, no hardware, no scheduler.</summary>
public sealed class OutputBindingChangeService
{
    private Solution? _solution;
    private Dictionary<ScopedNodeIdentity, ToolSchema?> _contracts = new();
    public IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes { get; private set; } = Array.Empty<ScopedNodeIdentity>();
    public IReadOnlyList<string> InvalidatedNodeIds => InvalidatedNodes.Select(n => n.NodeId).Distinct().ToArray();

    public void Reset(Solution? solution)
    {
        _solution = solution; InvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
        Capture();
    }

    /// <summary>历史已恢复图与配置后接纳其合同基线；保留宿主读取器，不再次裁剪恢复的绑定。</summary>
    public void AcceptHistoryConfiguration(Solution? solution)
    {
        _solution = solution;
        InvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
        Capture();
    }

    public IReadOnlyList<OutputBindingChange> Commit(string sourceId)
    {
        var owners = (_solution?.Workflows.Where(w => w.Nodes.Any(n => n.Id == sourceId)).Select(w => w.Id)
            ?? Array.Empty<string>()).Concat(_contracts.Keys.Where(k => k.NodeId == sourceId).Select(k => k.WorkflowId)).Distinct().ToArray();
        if (owners.Length > 1) throw new InvalidOperationException("节点ID在多个工作流中重复，请提供工作流作用域。");
        return Reconcile(owners.Length == 1 ? new(owners[0], sourceId) : null, false);
    }
    public IReadOnlyList<OutputBindingChange> Commit(string workflowId, string nodeId) => Reconcile(new(workflowId, nodeId), false);
    public IReadOnlyList<OutputBindingChange> ReconcileAll() => Reconcile(null, true);

    // Candidate validation must compare with the published contracts, not capture the edited
    // candidate as its own baseline. Delegates remain attached only to candidate parameters.
    internal OutputBindingChangeService ForCandidate(Solution candidate) => new()
    {
        _solution = candidate,
        _contracts = new(_contracts)

    };

    /// <summary>Read-only validation of the frozen local execution model; never modifies design configuration.</summary>
    public static void EnsureExecutable(Workflow workflow, string? sourceWorkflowId = null)
    {
        var first = GetExecutionInputIssues(workflow, sourceWorkflowId).SelectMany(p => p.Value).FirstOrDefault();
        if (first != null) throw new InvalidOperationException(first.Message);
    }

    /// <summary>Read-only node-scoped validation of a frozen plan. Invalid inputs never abort unrelated nodes.</summary>
    public static IReadOnlyDictionary<string,IReadOnlyList<ExecutionInputIssue>> GetExecutionInputIssues(Workflow workflow,string? sourceWorkflowId=null)
    {
        var issues=new Dictionary<string,IReadOnlyList<ExecutionInputIssue>>(StringComparer.Ordinal);
        foreach(var node in workflow.Nodes)
        {
            if(node.RuntimeConfiguration is null){issues.Add(node.Id,new[]{new ExecutionInputIssue("runtime-configuration","Runtime","节点缺少已准入 Runtime 配置。")});continue;}
            var errors=RuntimeInputBindings.Validate(workflow,node,sourceWorkflowId??workflow.Id);
            if(errors.Count>0)issues.Add(node.Id,errors);
        }
        return issues;
    }
    public IReadOnlyList<string> GetCandidateNodeIds(string workflowId, string targetId)
    {
        var flow = _solution?.Workflows.FirstOrDefault(w => w.Id == workflowId);
        if (flow == null) return Array.Empty<string>();
        if (!flow.Nodes.Any(node => node.Id == targetId)) return Array.Empty<string>();
        var graph = SolutionExecutionDependencyGraph.Compile(_solution!, checkReadiness: false);
        var target = SolutionExecutionDependencyGraph.GetScopedNodeId(workflowId, targetId);
        // 候选不能是目标或其下游。一次遍历整个方案的后继边，保留经全局变量跨流程再返回的依赖。
        var successors = graph.Dependencies.GroupBy(edge => edge.SourceScopedNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.TargetScopedNodeId).ToArray(), StringComparer.Ordinal);
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(target);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!excluded.Add(current)) continue;
            if (successors.TryGetValue(current, out var children))
                foreach (var child in children) pending.Push(child);
        }
        return flow.Nodes.Select(node => node.Id)
            .Where(id => !excluded.Contains(SolutionExecutionDependencyGraph.GetScopedNodeId(workflowId, id))).ToArray();
    }

    /// <summary>Ordinary manual parameter references require an actual control predecessor.
    /// Subscription dependencies must not create their own qualification.</summary>
    public IReadOnlyList<string> GetManualParameterCandidateNodeIds(string workflowId, string targetId)
    {
        var flow = _solution?.Workflows.FirstOrDefault(w => w.Id == workflowId);
        if (flow == null || !flow.Nodes.Any(node => node.Id == targetId)) return Array.Empty<string>();
        var incoming = flow.Connections.GroupBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).ToArray(), StringComparer.Ordinal);
        var predecessors = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(targetId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!incoming.TryGetValue(current, out var sources)) continue;
            foreach (var source in sources)
                if (predecessors.Add(source)) pending.Push(source);
        }
        predecessors.Remove(targetId);
        return GetCandidateNodeIds(workflowId, targetId).Where(predecessors.Contains).ToArray();
    }
    /// <summary>Apply the same frozen manual-source eligibility used at acceptance and execution.</summary>
    public static bool IsOrdinaryNumericParameter(Port port)
        => port.Type.Shape==ValueShape.Scalar && port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64;
    public static Func<OutputReference,bool> CaptureManualParameterEligibility(Workflow workflow,string targetId)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if(!workflow.Nodes.Any(node=>node.Id==targetId))return _=>false;
        var frozen=new ManualReferenceQualification(workflow,workflow.Id,targetId);
        return reference=>reference is not null && frozen.Validate(reference) is null;
    }
    public IReadOnlyList<string> GetExistingBindingSourceNodeIds(string workflowId,string targetId)
    {
        var flow=_solution?.Workflows.FirstOrDefault(w=>w.Id==workflowId);
        var node=flow?.Nodes.FirstOrDefault(n=>n.Id==targetId);
        if(flow is null||node?.RuntimeConfiguration is null)return Array.Empty<string>();
        var ids=flow.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        return BindingRelationCatalog.References(node,true).Values.SelectMany(setting=>WorkflowBindingContext.ReferenceNodes(setting.Reference,workflowId))
            .Where(reference=>reference.ScopeId==workflowId&&ids.Contains(reference.NodeId)).Select(reference=>reference.NodeId).Distinct(StringComparer.Ordinal).ToArray();
    }

    public ToolSchema? ReadContract(WorkflowNodeBase node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var runtime=node.RuntimeConfiguration??throw new InvalidOperationException("节点缺少已准入 Runtime 配置："+node.Id);
        RuntimeNodeAdmission.Require(runtime.Directory,node);
        return runtime.Schema;
    }

    private IReadOnlyList<OutputBindingChange> Reconcile(ScopedNodeIdentity? committed,bool all)
    {
        InvalidatedNodes=Array.Empty<ScopedNodeIdentity>();
        if(_solution is null)return Array.Empty<OutputBindingChange>();
        var nodes=_solution.Workflows.SelectMany(flow=>flow.Nodes.Select(node=>(Id:new ScopedNodeIdentity(flow.Id,node.Id),Node:node))).ToDictionary(item=>item.Id,item=>item.Node);
        var current=nodes.ToDictionary(pair=>pair.Key,pair=>ReadContract(pair.Value));
        var changes=new List<(Action Apply,Action Undo)>();var notices=new List<OutputBindingChange>();var invalidated=new HashSet<ScopedNodeIdentity>();
        var pendingConfigurations=new Dictionary<WorkflowNodeBase,(RuntimeToolConfiguration Before,RuntimeToolConfiguration After)>();
        foreach(var variable in _solution.GlobalVariables)
        {
            if(variable.ProducerBinding is not {} producer)continue;
            var validation=GlobalProducerBindingRules.Validate(_solution,producer,variable,source=>current.GetValueOrDefault(new(producer.SourceReference.ScopeId!,source.Id)));
            var oldPending=variable.BindingValidationPending;
            if(validation.State==BindingCompatibility.Unresolved)
                changes.Add((()=>variable.BindingValidationPending=validation.Reason,()=>variable.BindingValidationPending=oldPending));
            else if(validation.State is BindingCompatibility.Compatible or BindingCompatibility.Conditional)
                changes.Add((()=>variable.BindingValidationPending="",()=>variable.BindingValidationPending=oldPending));
            else
            {
                var oldReason=variable.RebindingReason;var consumers=variable.ConsumerBindings.ToList();
                changes.Add((()=>{variable.ProducerBinding=null;variable.RebindingReason=validation.Reason;variable.BindingValidationPending="";variable.ConsumerBindings.Clear();},
                    ()=>{variable.ProducerBinding=producer;variable.RebindingReason=oldReason;variable.BindingValidationPending=oldPending;variable.ConsumerBindings=consumers;}));
                foreach(var consumer in consumers)
                {
                    var targets=nodes.Where(pair=>pair.Key.NodeId==consumer.NodeId&&(string.IsNullOrEmpty(consumer.WorkflowId)||pair.Key.WorkflowId==consumer.WorkflowId)).ToArray();
                    if(targets.Length!=1)continue;
                    var target=targets[0];var original=target.Value.RuntimeConfiguration??throw new InvalidOperationException("节点缺少 Runtime 配置。");
                    var runtime=pendingConfigurations.TryGetValue(target.Value,out var pending)?pending.After:original;
                    var key=consumer.InputKey??consumer.ParameterName;
                    if(runtime.Fields.Read(key) is VariableSetting setting)
                    {
                        var replacement=runtime.WithFieldSettings(new Dictionary<string,ParameterSetting>{{key,setting with{RebindingReason="全局变量输入来源已变化，请重新绑定"}}});
                        pendingConfigurations[target.Value]=(original,replacement);
                    }
                    invalidated.Add(target.Key);notices.Add(new(target.Value.Name,key,"全局变量输入来源已变化，请重新绑定",target.Value.Id,target.Key.WorkflowId));
                }
            }
        }
        foreach(var (node,configuration) in pendingConfigurations)
            changes.Add((()=>node.RuntimeConfiguration=configuration.After,()=>node.RuntimeConfiguration=configuration.Before));
        var applied=new Stack<Action>();
        try{foreach(var change in changes){applied.Push(change.Undo);change.Apply();}SolutionExecutionDependencyGraph.Compile(_solution,checkReadiness:false);}
        catch{while(applied.Count>0)applied.Pop()();throw;}
        foreach(var pair in current)
            if(_contracts.TryGetValue(pair.Key,out var old)&&old is not null&&pair.Value is not null&&
                (old.Fingerprint!=pair.Value.Fingerprint||System.Text.Json.JsonSerializer.Serialize(old.OutputDisplay)!=System.Text.Json.JsonSerializer.Serialize(pair.Value.OutputDisplay)))
                invalidated.Add(pair.Key);
        foreach(var missing in _contracts.Keys.Except(current.Keys))invalidated.Add(missing);
        bool added;
        do
        {
            added=false;
            foreach(var (identity,node) in nodes)
            {
                if(invalidated.Contains(identity)||node.RuntimeConfiguration is null)continue;
                if(BindingRelationCatalog.References(node,true).Values.SelectMany(setting=>WorkflowBindingContext.ReferenceNodes(setting.Reference,identity.WorkflowId))
                    .Any(source=>invalidated.Contains(new(source.ScopeId,source.NodeId))))added|=invalidated.Add(identity);
            }
        }while(added);
        InvalidatedNodes=invalidated.ToArray();
        foreach(var id in invalidated)if(nodes.TryGetValue(id,out var node)){node.LastResult=null;node.OutputCache=null;}
        Capture();return notices.Distinct().ToArray();
    }
    private void Capture()
    {
        _contracts=_solution is null?new():_solution.Workflows.SelectMany(flow=>flow.Nodes.Select(node=>(Id:new ScopedNodeIdentity(flow.Id,node.Id),Node:node)))
            .ToDictionary(item=>item.Id,item=>ReadContract(item.Node));
    }
}
