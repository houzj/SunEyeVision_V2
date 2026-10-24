using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SunEyeVision.Plugin.SDK.Models;

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

/// <summary>A private variable/configuration candidate belonging to one document host generation.
/// Value assignment is an explicit command, including assigning the opening value again.</summary>
public sealed class GlobalVariableConfigurationEditDraft
{
    internal sealed record OpeningValue(GlobalVariable Instance, long Revision);
    internal Guid Owner { get; }
    internal bool Consumed { get; set; }
    internal Solution Baseline { get; }
    internal IReadOnlyDictionary<string, OpeningValue> OpeningValues { get; }
    internal Dictionary<string, object?> ExplicitValues { get; } = new(StringComparer.Ordinal);
    public long BaseVersion { get; }
    public Solution Configuration { get; }
    public bool HasValueEdits => ExplicitValues.Count != 0;

    internal GlobalVariableConfigurationEditDraft(Guid owner, long version, Solution configuration,
        Solution baseline, IReadOnlyDictionary<string, OpeningValue> openingValues)
    { Owner = owner; BaseVersion = version; Configuration = configuration; Baseline = baseline; OpeningValues = openingValues; }

    public void SetValue(string variableId, object? value)
    {
        if (Consumed) throw new InvalidOperationException("变量草稿已提交，请使用新的编辑草稿。");
        var variable = Configuration.GlobalVariables.SingleOrDefault(v => v.Id == variableId)
            ?? throw new InvalidOperationException("要编辑的变量已删除或不属于当前草稿。");
        NodeConfigurationEditHost.ValidateEditableValue(variable.Type, value);
        var owned = GlobalVariable.CopyEditableValue(value);
        // Keep the command separate from the editable display object's array storage.
        ExplicitValues[variableId] = owned;
        variable.Value = GlobalVariable.CopyEditableValue(owned);
    }
}

/// <summary>One document's existing configuration publication gateway. Node and variable
/// candidates are composed and validated together before one Store publication.</summary>
public sealed class NodeConfigurationEditHost
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly object _gate = new();
    private readonly Solution _design;
    private readonly List<GlobalVariable> _variableCollection;
    private readonly BindingConfigurationStore _store;
    private readonly Dictionary<ScopedNodeIdentity, WorkflowNodeBase> _nodeInstances;
    // Configuration history can restore a removed variable without replaying an old Value.
    // This is host-local history backing, not a resource owner or a global registry.
    private readonly Dictionary<string, GlobalVariable> _variableInstances;
    private bool _projecting;
    // Deterministic acceptance-failure injection only; disabled in production. Tests set
    // this private counter by reflection. It is not an observer/callback under ValueGate.
    private int _acceptanceFailureAfterRawMutation = -1;
    private Exception? _lastRefreshError;
    public Exception? LastRefreshError { get { lock (_gate) return _lastRefreshError; } }

    public NodeConfigurationEditHost(Solution design)
    {
        _design = design ?? throw new ArgumentNullException(nameof(design));
        _variableCollection = design.GlobalVariables;
        _nodeInstances = Nodes(design);
        CheckVariableDefinitions(design);
        _variableInstances = design.GlobalVariables.ToDictionary(v => v.Id, StringComparer.Ordinal);
        _store = new(design);
        var projection = new Projection();
        try { AcceptProjection(_store.Capture(), null, projection); }
        catch { projection.Restore(this); throw; }
        finally { ReleaseProjection(projection); }
        Emit(projection.Notifications);
    }

    /// <summary>Configuration/dependency history, never the authority for live runtime values.
    /// Save and run capture live values independently with GlobalVariable.CaptureValues.</summary>
    public BindingConfigurationSnapshot Capture() => _store.Capture();
    public bool CanUndo => _store.CanUndo;
    public bool CanRedo => _store.CanRedo;
    public bool OwnsNode(string workflowId, WorkflowNodeBase node)
    { lock (_gate) return _design.Workflows.Any(w => w.Id == workflowId && w.Nodes.Any(n => ReferenceEquals(n, node))); }

    public NodeConfigurationEditDraft BeginEdit(string workflowId, string nodeId)
    {
        lock (_gate)
        {
            EnsureBaseline();
            var identity = new ScopedNodeIdentity(workflowId, nodeId);
            var node = Find(_store.Capture().CopyConfiguration(), identity);
            return new(_owner, identity, node, NodeEditSignature(node));
        }
    }

    public GlobalVariableConfigurationEditDraft BeginVariableEdit()
    {
        lock (_gate)
        {
            EnsureBaseline();
            var published = _store.Capture();
            var configuration = published.CopyConfiguration();
            var captures = GlobalVariable.CaptureValues(_variableCollection);
            var openings = new Dictionary<string, GlobalVariableConfigurationEditDraft.OpeningValue>(StringComparer.Ordinal);
            foreach (var variable in _variableCollection)
            {
                var captured = captures[variable.Id];
                openings.Add(variable.Id, new(variable, captured.Revision));
                // The existing native/complex display remains read-only. Never inspect a
                // borrowed native payload to implement the four primitive value editors.
                if (!variable.IsComplexValue)
                    configuration.GlobalVariables.Single(v => v.Id == variable.Id).Value = GlobalVariable.CopyEditableValue(captured.Value);
            }
            return new(_owner, published.Version, configuration, BindingConfigurationStore.Copy(configuration), openings);
        }
    }

    public void Validate(NodeConfigurationEditDraft editor)
    { ArgumentNullException.ThrowIfNull(editor); ValidateBatch(new[] { editor }, null); }
    public BindingConfigurationPublication Commit(NodeConfigurationEditDraft editor)
    { ArgumentNullException.ThrowIfNull(editor); return CommitBatch(new[] { editor }, null); }

    public void ValidateBatch(IReadOnlyList<NodeConfigurationEditDraft> editors, GlobalVariableConfigurationEditDraft? variables)
    {
        ArgumentNullException.ThrowIfNull(editors);
        lock (_gate)
        {
            var prepared = Compose(editors, variables);
            _store.Validate(prepared.Draft, c => ValidateCandidate(c, prepared.Participants, variables != null));
            lock (GlobalVariable.ValueGate)
            {
                CheckValueWrites(variables);
                CheckValueTypes(prepared.Draft.Configuration, variables);
            }
        }
    }

    public BindingConfigurationPublication CommitBatch(IReadOnlyList<NodeConfigurationEditDraft> editors,
        GlobalVariableConfigurationEditDraft? variables)
    {
        ArgumentNullException.ThrowIfNull(editors);
        BindingConfigurationPublication publication;
        List<(GlobalVariable Variable, bool ValueChanged)> notifications;
        lock (_gate)
        {
            var prepared = Compose(editors, variables);
            var projection = new Projection();
            try
            {
                publication = _store.Publish(prepared.Draft,
                    snapshot => AcceptProjection(snapshot, variables, projection),
                    forcePublication: variables?.HasValueEdits == true,
                    validate: c => ValidateCandidate(c, prepared.Participants, variables != null));
                foreach (var editor in editors) editor.Consumed = true;
                if (variables != null) variables.Consumed = true;
                notifications = projection.Notifications;
            }
            catch { projection.Restore(this); throw; }
            finally { ReleaseProjection(projection); }
        }
        Emit(notifications);
        return publication;
    }

    public BindingConfigurationSnapshot Undo() => RestoreHistory(undo: true);
    public BindingConfigurationSnapshot Redo() => RestoreHistory(undo: false);
    private BindingConfigurationSnapshot RestoreHistory(bool undo)
    {
        BindingConfigurationSnapshot snapshot;
        List<(GlobalVariable Variable, bool ValueChanged)> notifications;
        lock (_gate)
        {
            EnsureBaseline();
            var projection = new Projection();
            try
            {
                var version = _store.Capture().Version;
                // No explicit value write set is replayed by configuration history.
                snapshot = undo ? _store.Undo(version, s => AcceptProjection(s, null, projection))
                    : _store.Redo(version, s => AcceptProjection(s, null, projection));
                notifications = projection.Notifications;
            }
            catch { projection.Restore(this); throw; }
            finally { ReleaseProjection(projection); }
        }
        Emit(notifications);
        return snapshot;
    }

    private sealed record PreparedBatch(BindingConfigurationDraft Draft, HashSet<ScopedNodeIdentity> Participants);
    private PreparedBatch Compose(IReadOnlyList<NodeConfigurationEditDraft> editors, GlobalVariableConfigurationEditDraft? variables)
    {
        EnsureBaseline();
        var draft = _store.BeginEdit();
        var baseline = draft.Configuration;
        var byId = new Dictionary<ScopedNodeIdentity, NodeConfigurationEditDraft>();
        foreach (var editor in editors)
        {
            if (editor == null || editor.Owner != _owner || editor.Consumed)
                throw new InvalidOperationException("编辑草稿已提交或不属于当前方案。");
            if (!byId.TryAdd(editor.Identity, editor)) throw new InvalidOperationException("同一节点有重复的编辑参与者。");
            var current = Find(baseline, editor.Identity);
            if (NodeEditSignature(current) != editor.Signature)
                throw new InvalidOperationException("当前节点正式配置已变化，草稿已保留，请重新确认后编辑。");
            if (editor.Node.Id != current.Id || editor.Node.ToolType != current.ToolType)
                throw new InvalidOperationException("参数编辑不能更换节点身份或工具类型。");
        }
        var participants = byId.Keys.ToHashSet();
        if (variables != null)
        {
            if (variables.Owner != _owner || variables.Consumed || variables.BaseVersion != _store.Capture().Version)
                throw new InvalidOperationException("变量草稿不属于当前方案或配置版本已过期，草稿已保留。");
            if (VariableDraftStructure(variables.Configuration) != VariableDraftStructure(variables.Baseline))
                throw new InvalidOperationException("变量编辑不能改变方案、节点身份、画布或拓扑。");
            CheckVariableDefinitions(variables.Configuration);
            var variableNodes = Nodes(variables.Configuration);
            var openingNodes = Nodes(variables.Baseline);
            foreach (var pair in variableNodes)
            {
                var changed = ParametersSignature(pair.Value) != ParametersSignature(openingNodes[pair.Key]);
                if (!changed) continue;
                participants.Add(pair.Key);
                if (byId.TryGetValue(pair.Key, out var editor))
                {
                    var nodeChanged = ParametersSignature(editor.Node) != ParametersSignature(Find(baseline, pair.Key));
                    if (nodeChanged && ParametersSignature(editor.Node) != ParametersSignature(pair.Value))
                        throw new InvalidOperationException("变量绑定和节点草稿同时修改了同一节点参数/绑定，请保留草稿并消除重叠后重试。");
                    byId.Remove(pair.Key);
                }
                SetParameters(Find(draft.Configuration, pair.Key), pair.Value);
            }
            var openingVariables = variables.Baseline.GlobalVariables.ToDictionary(v => v.Id, StringComparer.Ordinal);
            var historicalValues = baseline.GlobalVariables.ToDictionary(v => v.Id, v => v.Value, StringComparer.Ordinal);
            draft.Configuration.GlobalVariables.Clear();
            foreach (var variable in variables.Configuration.GlobalVariables)
            {
                if (openingVariables.TryGetValue(variable.Id, out var opening))
                {
                    if (!variable.IsComplexValue && !variables.ExplicitValues.ContainsKey(variable.Id) && !SameEditableValue(variable.Value, opening.Value))
                        throw new InvalidOperationException("当前值编辑必须通过变量草稿 SetValue 显式提交。");
                }
                else if (_variableInstances.ContainsKey(variable.Id))
                    throw new InvalidOperationException("新变量不能复用本编辑历史中已有的稳定Id。");
                var copy = CopyVariable(variable);
                if (variables.ExplicitValues.TryGetValue(variable.Id, out var value))
                {
                    ValidateEditableValue(copy.Type, value);
                    if (!SameEditableValue(variable.Value, value))
                        throw new InvalidOperationException("变量当前值在 SetValue 之后又被直接修改，草稿已保留。");
                    copy.Value = GlobalVariable.CopyEditableValue(value);
                }
                else if (historicalValues.TryGetValue(variable.Id, out var historicalValue))
                    copy.Value = GlobalVariable.CopyEditableValue(historicalValue);
                // Only explicit commands change an existing live value. Store values remain
                // a copied configuration payload and are never fed back by Apply/Undo/Redo.
                draft.Configuration.GlobalVariables.Add(copy);
            }
        }
        foreach (var editor in byId.Values) SetParameters(Find(draft.Configuration, editor.Identity), editor.Node);
        return new(draft, participants);
    }

    private static void ValidateCandidate(Solution candidate, HashSet<ScopedNodeIdentity> participants, bool validateAllBindings)
    {
        CheckVariableDefinitions(candidate);
        foreach (var flow in candidate.Workflows)
        {
            var issues = OutputBindingChangeService.GetExecutionInputIssues(flow, flow.Id);
            if (validateAllBindings && issues.Values.SelectMany(i => i).FirstOrDefault() is { } batchIssue)
                throw new InvalidOperationException(batchIssue.Message);
            foreach (var node in flow.Nodes.Where(n => participants.Contains(new(flow.Id, n.Id))))
            {
                var result = node.Parameters.Validate();
                if (!result.IsValid) throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "节点参数无效。");
                if (issues.TryGetValue(node.Id, out var inputIssues) && inputIssues.Count > 0)
                    throw new InvalidOperationException(inputIssues[0].Message);
            }
        }
    }

    private static void CheckVariableDefinitions(Solution configuration)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in configuration.GlobalVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Id) || !ids.Add(variable.Id))
                throw new InvalidOperationException("变量Id不能为空或重复。");
            var result = variable.Validate();
            if (!result.IsValid) throw new InvalidOperationException(result.Errors[0]);
            if (!names.Add(variable.Name)) throw new InvalidOperationException("变量名称不能重复（不区分大小写）。");
        }
    }

    internal static void ValidateEditableValue(string type, object? value)
    {
        var valid = type switch
        {
            "Integer" => value is int or long or int[] or long[],
            "Double" => value is float f ? float.IsFinite(f) : value is double d ? double.IsFinite(d)
                : value is float[] fs ? fs.All(float.IsFinite) : value is double[] ds && ds.All(double.IsFinite),
            "String" => value is string or string[],
            "Boolean" => value is bool or bool[],
            _ => false
        };
        if (!valid) throw new InvalidOperationException("当前值与变量类型不匹配，或该类型不支持文本值编辑。");
    }

    private void CheckValueWrites(GlobalVariableConfigurationEditDraft? variables)
    {
        if (variables == null) return;
        foreach (var pair in variables.ExplicitValues)
        {
            if (variables.OpeningValues.TryGetValue(pair.Key, out var opening))
            {
                var live = _variableCollection.SingleOrDefault(v => v.Id == pair.Key);
                if (live == null || !ReferenceEquals(live, opening.Instance) || live.CaptureValueRaw().Revision != opening.Revision)
                    throw new InvalidOperationException("变量当前值已在编辑期间更新，整批未发布；草稿已保留，请重新确认当前值。");
                _ = checked(opening.Revision + 1);
            }
            else if (_variableCollection.Any(v => v.Id == pair.Key))
                throw new InvalidOperationException("新变量Id已被正式变量占用。");
        }
    }

    private void CheckValueTypes(Solution candidate, GlobalVariableConfigurationEditDraft? variables)
    {
        foreach (var next in candidate.GlobalVariables)
        {
            if (variables != null && variables.ExplicitValues.TryGetValue(next.Id, out var explicitValue))
            { ValidateEditableValue(next.Type, explicitValue); continue; }
            var known = _variableInstances.TryGetValue(next.Id, out var existing);
            var value = known ? existing!.CaptureValueRaw().Value : next.CaptureValueRaw().Value;
            if (known && existing!.Type == next.Type) continue;
            if (!next.IsComplexValue) ValidateEditableValue(next.Type, value);
            else if (value != null && SdkTypeVocabulary.ToTypeVocabulary(value.GetType()) != next.Type)
                throw new InvalidOperationException("变量新类型与当前运行值不兼容，请显式编辑合法值后再提交。");
        }
    }

    private void RawMutationCompleted()
    {
        if (_acceptanceFailureAfterRawMutation < 0) return;
        if (--_acceptanceFailureAfterRawMutation <= 0)
            throw new InvalidOperationException("Injected configuration acceptance failure after raw mutation.");
    }
    private sealed class Projection
    {
        internal bool HoldsValueGate;
        internal bool Applied;
        internal List<GlobalVariable>? PreviousOrder;
        internal readonly List<(WorkflowNodeBase Node, SunEyeVision.Plugin.SDK.Execution.Parameters.ToolParameters Parameters,
            SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSettingContainer Settings)> Nodes = new();
        internal readonly List<(GlobalVariable Variable, GlobalVariable.ConfigurationState Configuration, GlobalVariable.ValueState Value)> Variables = new();
        internal readonly List<string> AddedInstances = new();
        internal readonly List<(GlobalVariable Variable, bool ValueChanged)> Notifications = new();

        internal void Restore(NodeConfigurationEditHost host)
        {
            if (!Applied) return;
            foreach (var node in Nodes) { node.Node.Parameters = node.Parameters; node.Node.ParamSettings = node.Settings; }
            foreach (var variable in Variables)
            {
                variable.Variable.ApplyConfigurationRaw(variable.Configuration);
                variable.Variable.ApplyValueRaw(variable.Value);
            }
            if (PreviousOrder != null) { host._variableCollection.Clear(); host._variableCollection.AddRange(PreviousOrder); }
            foreach (var id in AddedInstances) host._variableInstances.Remove(id);
            Applied = false;
        }
    }

    private void AcceptProjection(BindingConfigurationSnapshot snapshot, GlobalVariableConfigurationEditDraft? variables, Projection projection)
    {
        // All cloning/serialization and allocation of the candidate happen before ValueGate.
        var candidate = snapshot.CopyConfiguration();
        var nextNodes = Nodes(candidate);
        if (!_nodeInstances.Keys.ToHashSet().SetEquals(nextNodes.Keys))
            throw new InvalidOperationException("配置编辑不能增删节点。");
        var plans = candidate.GlobalVariables.Select(v => (Next: v,
            Target: _variableInstances.TryGetValue(v.Id, out var existing) ? existing : v,
            State: v.CaptureConfiguration())).ToArray();
        var nextOrder = plans.Select(p => p.Target).ToArray();
        var valueWrites = variables?.ExplicitValues.ToDictionary(p => p.Key,
            p => GlobalVariable.CopyEditableValue(p.Value), StringComparer.Ordinal);
        _variableInstances.EnsureCapacity(checked(_variableInstances.Count + plans.Count(p => !_variableInstances.ContainsKey(p.Next.Id))));
        projection.Variables.EnsureCapacity(plans.Length);
        projection.Notifications.EnsureCapacity(plans.Length);
        projection.AddedInstances.EnsureCapacity(plans.Length);
        _variableCollection.EnsureCapacity(Math.Max(_variableCollection.Count, nextOrder.Length));
        projection.PreviousOrder = _variableCollection.ToList();
        foreach (var pair in nextNodes)
        {
            var target = _nodeInstances[pair.Key];
            if (ParametersSignature(target) != ParametersSignature(pair.Value))
                projection.Nodes.Add((target, target.Parameters, target.ParamSettings));
        }
        var configChanged = plans.ToDictionary(p => p.Next.Id,
            p => VariableConfigurationSignature(p.Target) != VariableConfigurationSignature(p.Next), StringComparer.Ordinal);
        Monitor.Enter(GlobalVariable.ValueGate);
        projection.HoldsValueGate = true;
        _projecting = true;
        CheckValueWrites(variables);
        CheckValueTypes(candidate, variables);
        foreach (var plan in plans)
        {
            var current = plan.Target.CaptureValueRaw();
            if (valueWrites?.ContainsKey(plan.Next.Id) == true) _ = checked(current.Revision + 1);
            projection.Variables.Add((plan.Target, plan.Target.CaptureConfiguration(), current));
        }
        // Everything below is raw model/list assignment. It invokes no virtual method,
        // property setter, logger, serializer, WPF object or subscription callback.
        projection.Applied = true;
        foreach (var pair in nextNodes)
            if (projection.Nodes.Any(p => ReferenceEquals(p.Node, _nodeInstances[pair.Key])))
            {
                _nodeInstances[pair.Key].Parameters = pair.Value.Parameters;
                _nodeInstances[pair.Key].ParamSettings = pair.Value.ParamSettings;
                RawMutationCompleted();
            }
        foreach (var plan in plans)
        {
            if (!_variableInstances.ContainsKey(plan.Next.Id))
            { projection.AddedInstances.Add(plan.Next.Id); _variableInstances.Add(plan.Next.Id, plan.Target); }
            if (configChanged[plan.Next.Id])
            { plan.Target.ApplyConfigurationRaw(plan.State with { ModifiedTime = plan.Target.ModifiedTime }); RawMutationCompleted(); }
            var valueChanged = valueWrites?.ContainsKey(plan.Next.Id) == true;
            if (valueChanged)
            {
                var previous = plan.Target.CaptureValueRaw();
                plan.Target.ApplyValueRaw(new(valueWrites![plan.Next.Id], checked(previous.Revision + 1)));
                RawMutationCompleted();
            }
            if (configChanged[plan.Next.Id] || valueChanged) projection.Notifications.Add((plan.Target, valueChanged));
        }
        _variableCollection.Clear(); _variableCollection.AddRange(nextOrder);
        RawMutationCompleted();
        // Keep ValueGate held until Store.Publish/Undo/Redo has completed its generation
        // assignment. The caller releases it in finally, then releases Host._gate.
    }

    private void ReleaseProjection(Projection projection)
    {
        _projecting = false;
        if (projection.HoldsValueGate) { projection.HoldsValueGate = false; Monitor.Exit(GlobalVariable.ValueGate); }
    }

    private void Emit(List<(GlobalVariable Variable, bool ValueChanged)> notifications)
    {
        var errors = new List<Exception>();
        foreach (var notification in notifications)
            notification.Variable.EmitConfigurationNotifications(notification.ValueChanged, errors);
        lock (_gate) _lastRefreshError = errors.Count == 0 ? null : new AggregateException("配置已发布，但部分视图通知失败。", errors);
    }

    private void EnsureBaseline()
    {
        if (_projecting) throw new InvalidOperationException("配置投影过程中不能重入编辑或恢复。");
        if (!ReferenceEquals(_design.GlobalVariables, _variableCollection)) throw new InvalidOperationException("正式变量集合已被替换。");
        var nodes = Nodes(_design);
        if (nodes.Count != _nodeInstances.Count || nodes.Any(p => !_nodeInstances.TryGetValue(p.Key, out var original) || !ReferenceEquals(original, p.Value)))
            throw new InvalidOperationException("正式节点身份或拓扑已在编辑事务之外变化。");
        if (_variableCollection.Any(v => !_variableInstances.TryGetValue(v.Id, out var original) || !ReferenceEquals(original, v)))
            throw new InvalidOperationException("正式变量实例已在编辑事务之外变化。");
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
        Variables = model.GlobalVariables.Select(VariableConfigurationSignature)
    }, WorkflowSerializationOptions.Compact);

    private static string NodeEditSignature(WorkflowNodeBase node) => JsonSerializer.Serialize(new
    {
        node.Name, node.DispName, node.ToolType, node.SubroutineId, node.IsEnabled,
        Parameters = ParametersSignature(node), node.ImageSourceConfig
    }, WorkflowSerializationOptions.Compact);
    private static string ParametersSignature(WorkflowNodeBase node) => JsonSerializer.Serialize(new
    {
        Type = node.Parameters.GetType().FullName,
        Parameters = JsonSerializer.Serialize(node.Parameters, node.Parameters.GetType(), WorkflowSerializationOptions.Compact), node.ParamSettings
    }, WorkflowSerializationOptions.Compact);
    private static string VariableConfigurationSignature(GlobalVariable variable) => JsonSerializer.Serialize(new
    {
        variable.Id, variable.Name, variable.Type, variable.Group, variable.Index, variable.Description,
        variable.ProducerBinding, variable.ConsumerBindings, variable.RebindingReason, variable.BindingValidationPending,
        variable.AdditionalData
    }, WorkflowSerializationOptions.Compact);
    private static string VariableDraftStructure(Solution model) => JsonSerializer.Serialize(new
    {
        model.Id, model.Name, model.Description, model.SchemaVersion, model.Version,
        Workflows = model.Workflows.Select(w => new { w.Id, w.Name, w.IsSubroutine,
            Nodes = w.Nodes.Select(n => new { n.Id, n.Name, n.DispName, n.ToolType, n.SubroutineId, n.IsEnabled,
                n.GlobalIndex, n.LocalIndex, n.PositionX, n.PositionY, n.Width, n.Height, n.SelectedDisplaySource, n.ImageSourceConfig }),
            Connections = w.Connections.Select(c => new { c.Id, c.SourceNodeId, c.SourcePort, c.TargetNodeId, c.TargetPort }) })
    }, WorkflowSerializationOptions.Compact);
    private static bool SameEditableValue(object? a, object? b) => JsonSerializer.Serialize(new { Type = a?.GetType().FullName, Value = a }, WorkflowSerializationOptions.Compact)
        == JsonSerializer.Serialize(new { Type = b?.GetType().FullName, Value = b }, WorkflowSerializationOptions.Compact);
    private static GlobalVariable CopyVariable(GlobalVariable variable)
    {
        var shell = new Solution(); shell.GlobalVariables.Add(variable);
        return BindingConfigurationStore.Copy(shell).GlobalVariables[0];
    }
    private static Dictionary<ScopedNodeIdentity, WorkflowNodeBase> Nodes(Solution model) => model.Workflows
        .SelectMany(w => w.Nodes.Select(n => (Identity: new ScopedNodeIdentity(w.Id, n.Id), Node: n)))
        .ToDictionary(p => p.Identity, p => p.Node);
    private static WorkflowNodeBase Find(Solution configuration, ScopedNodeIdentity identity) =>
        configuration.Workflows.SingleOrDefault(w => w.Id == identity.WorkflowId)?.Nodes.SingleOrDefault(n => n.Id == identity.NodeId)
        ?? throw new InvalidOperationException("编辑来源节点已删除。");
    private static void SetParameters(WorkflowNodeBase target, WorkflowNodeBase source)
    { target.Parameters = ToolParameterSnapshot.Create(source.Parameters); target.ParamSettings = Copy(source.ParamSettings); }
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, WorkflowSerializationOptions.Compact), WorkflowSerializationOptions.Compact)
        ?? throw new InvalidOperationException("无法复制编辑绑定设置。");
}
