using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>设计期复制的独立候选；准备失败不改动源定义或目的定义。</summary>
public sealed record WorkflowFragment(IReadOnlyList<WorkflowNodeBase> Nodes,
    IReadOnlyList<WorkflowConnection> Connections, IReadOnlyList<Workflow> OwnedWorkflows);

public static class WorkflowFragmentCopy
{
    private sealed class Scope
    {
        public required string SourceId, TargetId;
        public required WorkflowNodeBase[] SourceNodes;
        public required WorkflowConnection[] SourceEdges;
        public required bool Owned;
        public Scope? Parent;
        public List<Scope> Children = new();
        public Dictionary<string, WorkflowNodeBase> Nodes = new(StringComparer.Ordinal);
        public Dictionary<string, WorkflowConnection> Edges = new(StringComparer.Ordinal);
        public Workflow? Target;
    }
    public static WorkflowFragment Prepare(Workflow source, IEnumerable<string> selectedIds, string targetWorkflowId,
        Func<string, Workflow?> resolve, Func<WorkflowNodeBase, string, WorkflowNodeBase> create)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(create);
        var selected = selectedIds.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0 || selected.Any(id => source.Nodes.All(n => n.Id != id)))
            throw new InvalidOperationException("复制节点已不存在或未选择节点。");
        Scope Build(Workflow flow, WorkflowNodeBase[] nodes, string targetId, Scope? parent, HashSet<string> path)
        {
            if (!path.Add(flow.Id)) throw new InvalidOperationException("不能复制循环引用的子程序。");
            try
            {
                var ids = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
                if (ids.Count != nodes.Length) throw new InvalidOperationException("源工作流存在重复节点身份。");
                // Validate provenance against the complete source graph before selection trims its edges.
                // An explicitly broken reference remains copyable, with its repair reason intact.
                foreach(var original in nodes)
                    if(original.RuntimeConfiguration is {} runtime)
                        foreach(var (fieldId,setting) in runtime.Fields.Capture())
                            if(setting is ReferenceSetting {OriginId:not null} reference&&string.IsNullOrEmpty(reference.RebindingReason)
                                &&WorkflowBindingContext.ReferenceOwnershipError(flow,original,fieldId,reference,flow.Id) is {} error)
                                throw new InvalidOperationException("不能复制无效的自动来源归属："+error);
                var scope = new Scope { SourceId = flow.Id, TargetId = targetId, SourceNodes = nodes,
                    SourceEdges = flow.Connections.Where(e => ids.Contains(e.TargetNodeId) && (ids.Contains(e.SourceNodeId) || flow.Id == targetId)).ToArray(),
                    Owned = parent != null, Parent = parent };
                if (parent != null) scope.Target = new Workflow(targetId, flow.Name) { IsSubroutine = true,
                    AdditionalData = flow.AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone()) };
                foreach (var original in nodes)
                {
                    RuntimeNodeAdmission.RequireShape(original);
                    var clone = create(original, targetId);
                    if (ReferenceEquals(clone, original) || string.IsNullOrWhiteSpace(clone.Id) || clone.Id == original.Id)
                        throw new InvalidOperationException("复制工厂必须返回独立节点及新身份。");
                    RuntimeNodeAdmission.RequireShape(clone);
                    if(!ReferenceEquals(original.RuntimeConfiguration!.Directory,clone.RuntimeConfiguration!.Directory)
                        ||original.ToolType!=clone.ToolType)
                        throw new InvalidOperationException("复制源与目标工具或运行目录代次不一致。");
                    clone.Position = original.Position; clone.Width = original.Width; clone.Height = original.Height;
                    clone.IsEnabled = original.IsEnabled;
                    clone.AdditionalData = original.AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone());
                    clone.SelectedDisplayReference = original.SelectedDisplayReference;
                    clone.RuntimeConfiguration = original.RuntimeConfiguration?.Copy();
                    scope.Nodes.Add(original.Id, clone);
                    if (RuntimeNodeAdmission.IsPausedControlFlow(original.ToolType,original.SubroutineId)) throw new InvalidOperationException("Group / 控制流复制尚未接入。");
                    clone.SubroutineId = null;
                }
                foreach (var edge in scope.SourceEdges)
                {
                    // VM.Core.ModulePaste：同容器保留未选中前驱入线；跨容器只接复制映射内的前驱，不复制外部出线。
                    var sourceId = scope.Nodes.TryGetValue(edge.SourceNodeId, out var copiedSource) ? copiedSource.Id : edge.SourceNodeId;
                    if (copiedSource == null && flow.Nodes.All(n => n.Id != sourceId)) throw new InvalidOperationException("复制入线的前驱不存在。");
                    ExecutionConnectionPolicy.EnsureCanAdd(scope.Edges.Values, new WorkflowConnection { SourceNodeId = sourceId, TargetNodeId = scope.Nodes[edge.TargetNodeId].Id });
                    scope.Edges.Add(edge.Id, new WorkflowConnection { Id = Guid.NewGuid().ToString(),
                        SourceNodeId = sourceId, TargetNodeId = scope.Nodes[edge.TargetNodeId].Id,
                        SourcePort = edge.SourcePort, TargetPort = edge.TargetPort, ManualPathPoints = edge.ManualPathPoints?.ToArray() });
                }
                if (scope.Target != null)
                {
                    foreach (var n in scope.Nodes.Values) scope.Target.Nodes.Add(n);
                    foreach (var e in scope.Edges.Values) scope.Target.Connections.Add(e);
                }
                return scope;
            }
            finally { path.Remove(flow.Id); }
        }
        var root = Build(source, source.Nodes.Where(n => selected.Contains(n.Id)).ToArray(), targetWorkflowId, null, new(StringComparer.Ordinal));
        IEnumerable<Scope> All(Scope scope) { yield return scope; foreach (var child in scope.Children) foreach (var item in All(child)) yield return item; }
        var scopes = All(root).ToArray();
        var newIds = scopes.SelectMany(p => p.Nodes.Values).Select(n => n.Id).ToArray();
        if (newIds.Distinct(StringComparer.Ordinal).Count() != newIds.Length) throw new InvalidOperationException("复制工厂返回重复节点身份。");
        Scope? FindScope(string oldId, Scope current)
        {
            for (var p = current; p != null; p = p.Parent) if (p.SourceId == oldId) return p;
            // Prefer this owned branch; shared source subprograms become independent copies per owner.
            var candidates = All(current).Where(p => p.SourceId == oldId).ToArray();
            if (candidates.Length == 0) candidates = scopes.Where(p => p.SourceId == oldId).ToArray();
            if (candidates.Length > 1) throw new InvalidOperationException("复制引用的子程序身份存在歧义。");
            return candidates.SingleOrDefault();
        }
        (string Node, string Scope, string? Name) Remap(string nodeId, string? scopeId, Scope current)
        {
            var oldScope = scopeId ?? current.SourceId; var sourceScope = FindScope(oldScope, current);
            if (sourceScope?.Nodes.TryGetValue(nodeId, out var clone) == true) return (clone.Id, sourceScope.TargetId, clone.DispName);
            if (sourceScope?.Nodes.Values.FirstOrDefault(n => n.Id == nodeId) is { } alreadyMapped) return (nodeId, sourceScope.TargetId, alreadyMapped.DispName);
            if (sourceScope?.Owned == true && nodeId == "__SubroutineInputs__") return (nodeId, sourceScope.TargetId, null);
            return (nodeId, oldScope, null);
        }
        OutputReference Reference(OutputReference reference, Scope scope)
        {
            var mapped = Remap(reference.NodeId, reference.ScopeId, scope);
            var selection = reference.Selection switch
            {
                BindingSelection.AtField index => new BindingSelection.AtField(Reference(index.Reference, scope),index.ConstructionBehavior),
                _ => reference.Selection
            };
            return new OutputReference(mapped.Node, reference.PortId, reference.Type, reference.MemberFieldPath, selection, mapped.Scope, reference.Storage);
        }
        string? Edge(string? id, Scope scope) => id != null && scope.Edges.TryGetValue(id, out var edge) ? edge.Id : null;
        foreach (var scope in scopes)
            foreach (var clone in scope.Nodes.Values)
            {
                if(clone.RuntimeConfiguration is {} runtime)clone.RuntimeConfiguration=runtime.MapReferences(reference=>Reference(reference,scope),origin=>origin.StartsWith("connection:",StringComparison.Ordinal)&&scope.Edges.TryGetValue(origin[11..],out var edge)?"connection:"+edge.Id:null);

                if(clone.SelectedDisplayReference is {} displayReference)
                    clone.SelectedDisplayReference=SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(Reference(SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Deserialize(displayReference),scope));
            }
        return new(root.Nodes.Values.ToArray(), root.Edges.Values.ToArray(), scopes.Where(p => p.Target != null).Select(p => p.Target!).ToArray());
    }
    private static T? Copy<T>(T? value) where T : class => value == null ? null
        : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, WorkflowSerializationOptions.Compact), WorkflowSerializationOptions.Compact);

}
