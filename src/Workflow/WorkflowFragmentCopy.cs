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
                var scope = new Scope { SourceId = flow.Id, TargetId = targetId, SourceNodes = nodes,
                    SourceEdges = flow.Connections.Where(e => ids.Contains(e.TargetNodeId) && (ids.Contains(e.SourceNodeId) || flow.Id == targetId)).ToArray(),
                    Owned = parent != null, Parent = parent };
                if (parent != null) scope.Target = new Workflow(targetId, flow.Name) { IsSubroutine = true,
                    AdditionalData = flow.AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone()) };
                foreach (var original in nodes)
                {
                    var clone = create(original, targetId);
                    if (ReferenceEquals(clone, original) || string.IsNullOrWhiteSpace(clone.Id) || clone.Id == original.Id)
                        throw new InvalidOperationException("复制工厂必须返回独立节点及新身份。");
                    clone.Position = original.Position; clone.Width = original.Width; clone.Height = original.Height;
                    clone.IsEnabled = original.IsEnabled;
                    clone.Parameters = ToolParameterSnapshot.Create(original.Parameters);
                    clone.ParamSettings = ToolParameterSnapshot.CopySettings(original.ParamSettings)!;
                    clone.ParamSettings.NodeId = clone.Id;
                    clone.AdditionalData = original.AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone());
                    clone.SelectedDisplaySource = Copy(original.SelectedDisplaySource);
                    clone.ImageSourceConfig = Copy(original.ImageSourceConfig);
                    if (clone.ImageSourceConfig != null) clone.ImageSourceConfig.NodeId = clone.Id;
                    scope.Nodes.Add(original.Id, clone);
                    if (!string.IsNullOrEmpty(original.SubroutineId))
                    {
                        var child = resolve(original.SubroutineId) ?? throw new InvalidOperationException("复制的子程序定义不存在。");
                        var nested = Build(child, child.Nodes.ToArray(), Guid.NewGuid().ToString(), scope, path);
                        scope.Children.Add(nested); clone.SubroutineId = nested.TargetId;
                        nested.Target!.Name = clone.Name;
                        if (clone.Parameters is SubroutineParameters parameters) { parameters.SubroutineId = nested.TargetId; parameters.SubroutineName = clone.Name; }
                    }
                    else clone.SubroutineId = null;
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
            var selection = reference.Selection is BindingSelection.AtField index
                ? new BindingSelection.AtField(Reference(index.Reference, scope)) : reference.Selection;
            return new OutputReference(mapped.Node, reference.PortId, reference.Type, reference.MemberFieldPath, selection, mapped.Scope, reference.Storage);
        }
        string? Edge(string? id, Scope scope) => id != null && scope.Edges.TryGetValue(id, out var edge) ? edge.Id : null;
        object? Visit(object? value, Scope scope, HashSet<object> seen)
        {
            if (value == null || value is string || value.GetType().IsValueType || value is Type) return value;
            if (value is OutputReference reference) return Reference(reference, scope);
            if (value is AvailableDataSource data)
            {
                var mapped = Remap(data.SourceNodeId, data.SourceWorkflowId, scope);
                // Copy configuration through its persistence contract; runtime values are never carried over.
                var json = JsonSerializer.SerializeToNode(data, WorkflowSerializationOptions.Compact)!;
                json[nameof(data.SourceNodeId)] = mapped.Node; json[nameof(data.SourceWorkflowId)] = mapped.Scope;
                json[nameof(data.AutomaticConnectionId)] = Edge(data.AutomaticConnectionId, scope);
                if (mapped.Name != null) json[nameof(data.SourceNodeName)] = mapped.Name;
                return json.Deserialize<AvailableDataSource>(WorkflowSerializationOptions.Compact)!;
            }
            if (value is SunEyeVision.Plugin.SDK.Models.SubroutineParameterTarget target)
            {
                var mapped = Remap(target.NodeId, target.WorkflowId, scope);
                target.NodeId = mapped.Node; target.WorkflowId = mapped.Scope;
                return target;
            }
            if (value is BindingSourceIdentity identity)
            {
                var mapped = Remap(identity.SourceNodeId, identity.WorkflowId, scope);
                var dot = identity.Expression.IndexOf('.');
                var expression = dot > 0 && identity.Expression[..dot] == identity.SourceNodeId
                    ? mapped.Node + identity.Expression[dot..] : identity.Expression;
                return identity with { Expression = expression, WorkflowId = mapped.Scope, SourceNodeId = mapped.Node };
            }
            if (!seen.Add(value)) return value;
            if (value is ImageSourceReference image)
            {
                var mapped = Remap(image.SourceNodeId, image.SourceWorkflowId, scope);
                image.SourceNodeId = mapped.Node; image.SourceWorkflowId = mapped.Scope;
                if (mapped.Name != null) image.SourceNodeName = mapped.Name;
                image.AutomaticConnectionId = Edge(image.AutomaticConnectionId, scope); return image;
            }
            if (value is ParamSetting setting)
            {
                if (setting.ReferenceExpression != null)
                {
                    var mappedReference = Reference(BindingReferenceSerializer.Deserialize(setting.ReferenceExpression), scope);
                    setting.ReferenceExpression = BindingReferenceSerializer.Serialize(mappedReference);
                    setting.SourceNodeId = mappedReference.NodeId; setting.SourceWorkflowId = mappedReference.ScopeId;
                }
                else if (setting.SourceNodeId != null)
                {
                    var mapped = Remap(setting.SourceNodeId, setting.SourceWorkflowId, scope);
                    setting.SourceNodeId = mapped.Node; setting.SourceWorkflowId = mapped.Scope;
                    if (mapped.Name != null) setting.SourceNodeName = mapped.Name;
                }
                setting.ConstantValue = Visit(setting.ConstantValue, scope, seen); return setting;
            }
            if (value is IDictionary dictionary)
            { foreach (var key in dictionary.Keys.Cast<object>().ToArray()) dictionary[key] = Visit(dictionary[key], scope, seen); return value; }
            if (value is IList list)
            { for (var i = 0; i < list.Count; i++) { var item = Visit(list[i], scope, seen); if (!ReferenceEquals(item, list[i])) list[i] = item; } return value; }
            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null))
            {
                var childScope = value is SubroutineParameters sub && property.Name is nameof(sub.Outputs) or nameof(sub.DisplayConfigs) or nameof(sub.RunParameterGroups)
                    ? scope.Children.SingleOrDefault(c => c.TargetId == sub.SubroutineId) ?? scope : scope;
                var old = property.GetValue(value); var mapped = Visit(old, childScope, seen);
                if (!ReferenceEquals(old, mapped) && property.CanWrite) property.SetValue(value, mapped);
            }
            return value;
        }
        foreach (var scope in scopes)
            foreach (var clone in scope.Nodes.Values)
            {
                // 仅重写模型声明的输入表达式；路径后缀、普通文本和扩展JSON不做字符串替换。
                foreach (var input in BindingInputCatalog.Describe(clone.Parameters))
                {
                    var expression = input.Read();
                    var child = clone.Parameters is SubroutineParameters sub ? scope.Children.SingleOrDefault(c => c.TargetId == sub.SubroutineId) : null;
                    var inputScope = input.InternalProjection && child != null ? child : scope;
                    if (!BindingInputCatalog.TryReference(expression, inputScope.SourceNodes.Select(n => n.Id), input.ReferenceOnly, out var sourceId, out var path)) continue;
                    var oldScope = input.ReadWorkflowId?.Invoke();
                    if (child != null && oldScope == child.TargetId) oldScope = child.SourceId;
                    if (clone.Parameters.BindingSourceIdentities.TryGetValue(input.Key, out var saved) && saved.Expression == expression) oldScope = saved.WorkflowId;
                    var mapped = Remap(sourceId, oldScope ?? inputScope.SourceId, inputScope);
                    if (mapped.Node != sourceId) input.Write(mapped.Node + "." + path);
                }
                var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
                Visit(clone.Parameters, scope, seen); Visit(clone.ParamSettings, scope, seen); Visit(clone.SelectedDisplaySource, scope, seen);
            }
        return new(root.Nodes.Values.ToArray(), root.Edges.Values.ToArray(), scopes.Where(p => p.Target != null).Select(p => p.Target!).ToArray());
    }
    private static T? Copy<T>(T? value) where T : class => value == null ? null
        : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, WorkflowSerializationOptions.Compact), WorkflowSerializationOptions.Compact);

}
