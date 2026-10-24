using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SunEyeVision.Workflow;

/// <summary>Node editor candidate. A successful commit consumes it; failures keep its edits.</summary>
public sealed class NodeConfigurationEditDraft
{
    internal Guid Owner { get; }
    internal string Signature { get; }
    internal bool Consumed { get; set; }
    public ScopedNodeIdentity Identity { get; }
    public WorkflowNodeBase Node { get; }
    internal NodeConfigurationEditDraft(Guid owner, ScopedNodeIdentity identity, WorkflowNodeBase node, string signature)
    { Owner = owner; Identity = identity; Node = node; Signature = signature; }
}

/// <summary>
/// Host-facing node edit gateway. It preserves live node/variable identities and runtime values.
/// It must be wired by the UI host; it is not a control, scheduler or complete solution importer.
/// External topology or binding edits require a new gateway baseline, not silent overwrite.
/// </summary>
public sealed class NodeConfigurationEditHost
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly object _gate = new();
    private readonly Solution _design;
    private readonly BindingConfigurationStore _store;
    private bool _projecting;

    public NodeConfigurationEditHost(Solution design)
    {
        _design = design ?? throw new ArgumentNullException(nameof(design));
        _store = new(design);
        Apply(_store.Capture());
    }

    /// <summary>Parameter/binding publication history, not a complete live solution export.
    /// Independently persisted preview preferences must be saved from the live solution.</summary>
    public BindingConfigurationSnapshot Capture() => _store.Capture();
    public bool OwnsNode(string workflowId, WorkflowNodeBase node)
    { lock (_gate) return _design.Workflows.Any(w => w.Id == workflowId && w.Nodes.Any(n => ReferenceEquals(n, node))); }
    public bool CanUndo => _store.CanUndo;
    public bool CanRedo => _store.CanRedo;

    public NodeConfigurationEditDraft BeginEdit(string workflowId, string nodeId)
    {
        lock (_gate)
        {
            EnsureBaseline();
            var node = Find(_store.Capture().CopyConfiguration(), new(workflowId, nodeId));
            return new(_owner, new(workflowId, nodeId), node, NodeEditSignature(node));
        }
    }

    /// <summary>Validate a private editor candidate without publishing or consuming it.</summary>
    public void Validate(NodeConfigurationEditDraft editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        lock (_gate)
        {
            CheckEditor(editor);
            var baseline = _store.Capture().CopyConfiguration();
            var validator = new OutputBindingChangeService();
            validator.Reset(baseline);
            var candidate = _store.Capture().CopyConfiguration();
            SetEditorCandidate(candidate, editor);
            validator = validator.ForCandidate(candidate);
            validator.ReconcileAll();
            SolutionExecutionDependencyGraph.Compile(candidate, checkReadiness: false);
            var flow = candidate.Workflows.Single(w => w.Id == editor.Identity.WorkflowId);
            if (OutputBindingChangeService.GetExecutionInputIssues(flow, flow.Id).TryGetValue(editor.Identity.NodeId, out var issues) && issues.Count > 0)
                throw new InvalidOperationException(issues[0].Message);
        }
    }

    private void CheckEditor(NodeConfigurationEditDraft editor)
    {
        if (editor.Owner != _owner || editor.Consumed) throw new InvalidOperationException("编辑草稿已提交或不属于当前方案。");
        EnsureBaseline();
        var node = Find(_store.Capture().CopyConfiguration(), editor.Identity);
        if (NodeEditSignature(node) != editor.Signature)
            throw new InvalidOperationException("当前节点正式配置已变化，草稿已保留，请重新确认后编辑。");
        if (editor.Node.Id != node.Id || editor.Node.ToolType != node.ToolType)
            throw new InvalidOperationException("参数编辑不能更换节点身份或工具类型。");
    }
    private static void SetEditorCandidate(Solution candidate, NodeConfigurationEditDraft editor)
    {
        var node = Find(candidate, editor.Identity);
        node.Parameters = ToolParameterSnapshot.Create(editor.Node.Parameters);
        node.ParamSettings = Copy(editor.Node.ParamSettings);
    }
    public BindingConfigurationPublication Commit(NodeConfigurationEditDraft editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        lock (_gate)
        {
            if (editor.Owner != _owner || editor.Consumed) throw new InvalidOperationException("编辑草稿已提交或不属于当前方案。");
            EnsureBaseline();
            var draft = _store.BeginEdit(); var node = Find(draft.Configuration, editor.Identity);
            if (NodeEditSignature(node) != editor.Signature)
                throw new InvalidOperationException("当前节点正式配置已变化，草稿已保留，请重新确认后编辑。");
            if (editor.Node.Id != node.Id || editor.Node.ToolType != node.ToolType)
                throw new InvalidOperationException("参数编辑不能更换节点身份或工具类型。");
            // Node editors may edit parameters and bindings, not canvas identity/layout.
            node.Parameters = ToolParameterSnapshot.Create(editor.Node.Parameters);
            node.ParamSettings = Copy(editor.Node.ParamSettings);
            var publication = _store.Publish(draft, Apply);
            editor.Consumed = true;
            return publication;
        }
    }

    public BindingConfigurationSnapshot Undo()
    { lock (_gate) { EnsureBaseline(); return _store.Undo(_store.Capture().Version, Apply); } }
    public BindingConfigurationSnapshot Redo()
    { lock (_gate) { EnsureBaseline(); return _store.Redo(_store.Capture().Version, Apply); } }

    private void EnsureBaseline()
    {
        if (_projecting) throw new InvalidOperationException("配置投影过程中不能重入编辑或恢复。");
        var formal = _store.Capture().CopyConfiguration();
        if (ShapeSignature(formal) != ShapeSignature(_design))
            throw new InvalidOperationException("正式方案在编辑事务之外发生变化，不能覆盖；请重新建立编辑基线。");
    }

    private static string ShapeSignature(Solution model) => JsonSerializer.Serialize(new
    {
        model.Id,
        Workflows = model.Workflows.Select(w => new { w.Id, w.IsSubroutine,
            Nodes = w.Nodes.Select(n => new { n.Id, Signature = NodeEditSignature(n) }),
            Connections = w.Connections.Select(c => new { c.Id, c.SourceNodeId, c.SourcePort, c.TargetNodeId, c.TargetPort }) }),
        // Device values and variable runtime values are not node editor configuration.
        Variables = model.GlobalVariables.Select(v => new { v.Id, v.Name, v.Type, v.ProducerBinding,
            v.ConsumerBindings, v.RebindingReason, v.BindingValidationPending })
    }, WorkflowSerializationOptions.Compact);

    // Display-source preferences are owned and persisted by the preview host. This gateway
    // never publishes them; including them in its CAS would reject unrelated parameter edits.
    // Keep the configuration and dependency guards; do not silently adopt a changed baseline.
    private static string NodeEditSignature(WorkflowNodeBase node) => JsonSerializer.Serialize(new
    {
        node.Name, node.DispName, node.ToolType, node.SubroutineId, node.IsEnabled,
        Parameters = JsonSerializer.Serialize(node.Parameters, node.Parameters.GetType(), WorkflowSerializationOptions.Compact),
        node.ParamSettings, node.ImageSourceConfig
    }, WorkflowSerializationOptions.Compact);

    private void Apply(BindingConfigurationSnapshot snapshot)
    {
        var candidate = snapshot.CopyConfiguration();
        var originalNodes = _design.Workflows.SelectMany(w => w.Nodes.Select(n => (Id: new ScopedNodeIdentity(w.Id, n.Id), Node: n))).ToDictionary(p => p.Id, p => p.Node);
        var nextNodes = candidate.Workflows.SelectMany(w => w.Nodes.Select(n => (Id: new ScopedNodeIdentity(w.Id, n.Id), Node: n))).ToDictionary(p => p.Id, p => p.Node);
        if (!originalNodes.Keys.ToHashSet().SetEquals(nextNodes.Keys) || !_design.GlobalVariables.Select(v => v.Id).ToHashSet().SetEquals(candidate.GlobalVariables.Select(v => v.Id)))
            throw new InvalidOperationException("节点编辑投影不能增删节点或变量。");
        var changes = new List<(Action Apply, Action Undo)>();
        foreach (var pair in nextNodes)
        {
            var target = originalNodes[pair.Key]; var next = pair.Value;
            if (NodeEditSignature(target) == NodeEditSignature(next)) continue;
            var oldParameters = target.Parameters; var oldSettings = target.ParamSettings;
            changes.Add((() => { target.Parameters = next.Parameters; target.ParamSettings = next.ParamSettings; },
                () => { target.Parameters = oldParameters; target.ParamSettings = oldSettings; }));
        }
        foreach (var variable in candidate.GlobalVariables)
        {
            var target = _design.GlobalVariables.Single(v => v.Id == variable.Id);
            if (BindingSignature(target) == BindingSignature(variable)) continue;
            var oldProducer = target.ProducerBinding; var oldConsumers = target.ConsumerBindings;
            var oldRequired = target.RebindingReason; var oldPending = target.BindingValidationPending;
            changes.Add((() => SetBindings(target, variable.ProducerBinding, variable.ConsumerBindings, variable.RebindingReason, variable.BindingValidationPending),
                () => SetBindings(target, oldProducer, oldConsumers, oldRequired, oldPending)));
        }
        var applied = new Stack<Action>();
        _projecting = true;
        try { foreach (var change in changes) { applied.Push(change.Undo); change.Apply(); } }
        catch (Exception error)
        {
            var failures = new List<Exception> { error };
            while (applied.Count > 0)
                try { applied.Pop()(); } catch (Exception rollbackError) { failures.Add(rollbackError); }
            throw new AggregateException("配置投影失败，已尝试恢复全部原状态。", failures);
        }
        finally { _projecting = false; }
        // No running result space is touched. The UI applies the publication's invalidation
        // list to design results only and rebinds editor controls after acceptance.
    }

    private static string BindingSignature(GlobalVariable variable) => JsonSerializer.Serialize(new
    { variable.ProducerBinding, variable.ConsumerBindings, variable.RebindingReason, variable.BindingValidationPending }, WorkflowSerializationOptions.Compact);

    private static void SetBindings(GlobalVariable target, ProducerBinding? producer, List<ConsumerBinding> consumers,
        string required, string pending)
    {
        // Notification handlers can throw after a setter has already changed its backing
        // field. Attempt every field even on rollback, then report all notification failures.
        var failures = new List<Exception>();
        try { target.ProducerBinding = producer; } catch (Exception error) { failures.Add(error); }
        try { target.ConsumerBindings = consumers; } catch (Exception error) { failures.Add(error); }
        target.RebindingReason = required; target.BindingValidationPending = pending;
        if (failures.Count > 0) throw new AggregateException(failures);
    }

    private static WorkflowNodeBase Find(Solution configuration, ScopedNodeIdentity identity) =>
        configuration.Workflows.SingleOrDefault(w => w.Id == identity.WorkflowId)?.Nodes.SingleOrDefault(n => n.Id == identity.NodeId)
        ?? throw new InvalidOperationException("编辑来源节点已删除。");
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, WorkflowSerializationOptions.Compact), WorkflowSerializationOptions.Compact)
        ?? throw new InvalidOperationException("无法复制编辑绑定设置。");
}
