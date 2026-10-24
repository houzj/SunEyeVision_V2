using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>
/// An isolated editing candidate for binding configuration. Not the live solution or a run.
/// Devices, communications and run values are outside this projection's publication boundary.
/// </summary>
public sealed class BindingConfigurationDraft
{
    internal Guid Owner { get; }
    public long BaseVersion { get; }
    public Solution Configuration { get; }
    internal BindingConfigurationDraft(Guid owner, long version, Solution configuration)
    { Owner = owner; BaseVersion = version; Configuration = configuration; }
}

/// <summary>One coherent configuration/dependency generation. Mutable model access returns a copy.</summary>
public sealed class BindingConfigurationSnapshot
{
    private readonly Solution _configuration;
    internal string SolutionId => _configuration.Id;
    public long Version { get; }
    public IReadOnlyList<SolutionExecutionDependency> Dependencies { get; }
    internal BindingConfigurationSnapshot(long version, Solution configuration, SolutionExecutionDependencyGraph graph)
    {
        Version = version;
        _configuration = configuration;
        Dependencies = Array.AsReadOnly(graph.Dependencies.ToArray());
    }
    public Solution CopyConfiguration() => BindingConfigurationStore.Copy(_configuration);
}

public sealed record BindingConfigurationPublication(
    BindingConfigurationSnapshot Snapshot,
    IReadOnlyList<OutputBindingChange> Changes,
    IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes);

/// <summary>
/// Core publication boundary for the workflow/global-binding projection. Validation and
/// detachment happen on a private candidate; only a successful complete generation is visible.
/// Host editor, execution and history integration must use this boundary before it is authoritative.
/// </summary>
public sealed class BindingConfigurationStore
{
    private readonly object _gate = new();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Stack<BindingConfigurationSnapshot> _undo = new();
    private readonly Stack<BindingConfigurationSnapshot> _redo = new();
    private BindingConfigurationSnapshot _published;
    private OutputBindingChangeService _validator;
    private bool _accepting;

    public BindingConfigurationStore(Solution configuration)
    {
        var candidate = Copy(configuration ?? throw new ArgumentNullException(nameof(configuration)));
        _validator = new();
        _validator.Reset(candidate);
        _validator.ReconcileAll();
        _published = new(1, candidate, SolutionExecutionDependencyGraph.Compile(candidate, checkReadiness: false));
    }

    public BindingConfigurationSnapshot Capture() { lock (_gate) return _published; }
    public bool IsCurrent(long version) { lock (_gate) return _published.Version == version; }
    public bool CanUndo { get { lock (_gate) return _undo.Count > 0; } }
    public bool CanRedo { get { lock (_gate) return _redo.Count > 0; } }

    public BindingConfigurationDraft BeginEdit()
    {
        lock (_gate) return new(_owner, _published.Version, _published.CopyConfiguration());
    }

    public BindingConfigurationPublication Publish(BindingConfigurationDraft draft)
        => Publish(draft, null);

    internal BindingConfigurationPublication Publish(BindingConfigurationDraft draft,
        Action<BindingConfigurationSnapshot>? accept, bool forcePublication = false,
        Action<Solution>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
        {
            var prepared = Prepare(draft, validate);
            // An explicit runtime-value command must execute CAS/accept even if its payload
            // equals the old history value. One accepted command is one publication.
            if (prepared.Unchanged && !forcePublication)
                return new(_published, Array.AsReadOnly(prepared.Changes.ToArray()), Array.Empty<ScopedNodeIdentity>());
            var next = new BindingConfigurationSnapshot(checked(_published.Version + 1), prepared.Configuration, prepared.Graph);
            var publication = new BindingConfigurationPublication(next,
                Array.AsReadOnly(prepared.Changes.ToArray()), Array.AsReadOnly(prepared.Invalidated));
            // Allocate the history slot before invoking the host's raw acceptance adapter.
            // After acceptance there are only non-callback state assignments/stack operations.
            _undo.EnsureCapacity(checked(_undo.Count + 1));
            Accept(next, accept);
            _undo.Push(_published);
            _redo.Clear();
            _validator = prepared.Validator;
            _published = next;
            return publication;
        }
    }

    internal void Validate(BindingConfigurationDraft draft, Action<Solution> validate)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(validate);
        lock (_gate) Prepare(draft, validate);
    }

    private sealed record PreparedConfiguration(Solution Configuration, OutputBindingChangeService Validator,
        IReadOnlyList<OutputBindingChange> Changes, SolutionExecutionDependencyGraph Graph,
        ScopedNodeIdentity[] Invalidated, bool Unchanged);

    // Validation and publication use the same reconciled private candidate. A failed prepare
    // never modifies the caller's draft, the live design or the published dependency graph.
    private PreparedConfiguration Prepare(BindingConfigurationDraft draft, Action<Solution>? validate)
    {
        if (_accepting) throw new InvalidOperationException("配置接受过程中不能重入提交或校验。");
        if (draft.Owner != _owner) throw new InvalidOperationException("配置草稿不属于当前方案发布器。");
        if (draft.BaseVersion != _published.Version) throw new InvalidOperationException("配置草稿版本已过期，请重新打开编辑。");
        if (draft.Configuration.Id != _published.SolutionId)
            throw new InvalidOperationException("不能通过绑定配置草稿切换方案身份。");
        var candidate = Copy(draft.Configuration);
        var previous = _published.CopyConfiguration();
        foreach (var oldFlow in previous.Workflows)
        {
            var nextFlow = candidate.Workflows.FirstOrDefault(w => w.Id == oldFlow.Id);
            if (nextFlow == null) continue;
            foreach (var edge in oldFlow.Connections)
                if (!nextFlow.Connections.Any(c => c.Id == edge.Id && c.SourceNodeId == edge.SourceNodeId &&
                    c.TargetNodeId == edge.TargetNodeId && c.SourcePort == edge.SourcePort && c.TargetPort == edge.TargetPort) &&
                    nextFlow.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId) is { } target)
                    ConnectionBindingPolicy.RemoveAutomaticImageBinding(target, edge.Id);
        }
        var validator = _validator.ForCandidate(candidate);
        var changes = validator.ReconcileAll();
        var graph = SolutionExecutionDependencyGraph.Compile(candidate, checkReadiness: false);
        validate?.Invoke(candidate);
        var invalidated = ChangedNodes(previous, candidate, _published.Dependencies, graph.Dependencies, validator.InvalidatedNodes);
        return new(candidate, validator, changes, graph, invalidated,
            invalidated.Length == 0 && ConfigurationSignature(previous) == ConfigurationSignature(candidate));
    }

    private static ScopedNodeIdentity[] ChangedNodes(Solution before, Solution after,
        IReadOnlyList<SolutionExecutionDependency> oldDependencies,
        IReadOnlyList<SolutionExecutionDependency> newDependencies,
        IReadOnlyList<ScopedNodeIdentity> reconciled)
    {
        var oldNodes = before.Workflows.SelectMany(w => w.Nodes.Select(n => (Id: new ScopedNodeIdentity(w.Id, n.Id), Node: n))).ToDictionary(p => p.Id, p => p.Node);
        var newNodes = after.Workflows.SelectMany(w => w.Nodes.Select(n => (Id: new ScopedNodeIdentity(w.Id, n.Id), Node: n))).ToDictionary(p => p.Id, p => p.Node);
        var affected = new HashSet<ScopedNodeIdentity>(reconciled);
        foreach (var id in oldNodes.Keys.Union(newNodes.Keys))
            if (!oldNodes.TryGetValue(id, out var oldNode) || !newNodes.TryGetValue(id, out var newNode) || NodeSignature(oldNode) != NodeSignature(newNode)) affected.Add(id);
        var oldEdges = before.Workflows.SelectMany(w => w.Connections.Select(c => (w.Id, c.Id, c.SourceNodeId, c.SourcePort, c.TargetNodeId, c.TargetPort))).ToHashSet();
        var newEdges = after.Workflows.SelectMany(w => w.Connections.Select(c => (w.Id, c.Id, c.SourceNodeId, c.SourcePort, c.TargetNodeId, c.TargetPort))).ToHashSet();
        foreach (var edge in oldEdges.Except(newEdges).Concat(newEdges.Except(oldEdges)))
        { affected.Add(new(edge.Item1, edge.SourceNodeId)); affected.Add(new(edge.Item1, edge.TargetNodeId)); }
        var oldVariables = before.GlobalVariables.ToDictionary(v => v.Id);
        var newVariables = after.GlobalVariables.ToDictionary(v => v.Id);
        var variableNames = new HashSet<string>(); var variableIds = new HashSet<string>();
        foreach (var id in oldVariables.Keys.Union(newVariables.Keys))
        {
            oldVariables.TryGetValue(id, out var oldVariable); newVariables.TryGetValue(id, out var newVariable);
            if (VariableSignature(oldVariable) == VariableSignature(newVariable)) continue;
            variableIds.Add(id);
            if (oldVariable != null) variableNames.Add(oldVariable.Name);
            if (newVariable != null) variableNames.Add(newVariable.Name);
        }
        foreach (var relation in BindingRelationCatalog.Describe(before).Concat(BindingRelationCatalog.Describe(after)))
            if (BindingInputCatalog.TryReference(relation.Input.Read(), Array.Empty<string>(), relation.Input.ReferenceOnly, out var source, out var path) &&
                (source == "__VariableId__" && variableIds.Contains(path) || source == "__GlobalVars__" && variableNames.Contains(PropertyPathParser.ExtractPropertyName(path.Split('.')[0]))))
                affected.Add(relation.Target);
        var scopedIds = oldNodes.Keys.Union(newNodes.Keys).ToDictionary(k => SolutionExecutionDependencyGraph.GetScopedNodeId(k.WorkflowId, k.NodeId));
        var dependencies = oldDependencies.Concat(newDependencies).ToArray();
        foreach (var dependency in dependencies.Where(d => d.GlobalVariableName != null && variableNames.Contains(d.GlobalVariableName)))
            if (scopedIds.TryGetValue(dependency.TargetScopedNodeId, out var target)) affected.Add(target);
        bool added;
        do
        {
            added = false;
            foreach (var edge in dependencies)
                if (scopedIds.TryGetValue(edge.SourceScopedNodeId, out var source) && affected.Contains(source) &&
                    scopedIds.TryGetValue(edge.TargetScopedNodeId, out var target)) added |= affected.Add(target);
        } while (added);
        return affected.OrderBy(n => n.WorkflowId, StringComparer.Ordinal).ThenBy(n => n.NodeId, StringComparer.Ordinal).ToArray();
    }

    internal static string NodeSignature(WorkflowNodeBase node) => JsonSerializer.Serialize(new
    {
        node.Name, node.DispName, node.ToolType, node.SubroutineId, node.IsEnabled,
        Parameters = JsonSerializer.Serialize(node.Parameters, node.Parameters.GetType(), WorkflowSerializationOptions.Compact),
        node.ParamSettings, node.SelectedDisplaySource, node.ImageSourceConfig
    }, WorkflowSerializationOptions.Compact);

    // Runtime initialization timestamps are not binding changes. Initial values are configuration.
    private static string VariableSignature(GlobalVariable? variable) => variable == null ? "" : JsonSerializer.Serialize(new
    { variable.Id, variable.Name, variable.Type, variable.Value, variable.ProducerBinding, variable.ConsumerBindings,
        variable.RebindingReason, variable.BindingValidationPending }, WorkflowSerializationOptions.Compact);

    internal static string ConfigurationSignature(Solution configuration) => JsonSerializer.Serialize(new
    {
        configuration.Id, configuration.Name, configuration.Description, configuration.SchemaVersion, configuration.Version,
        Workflows = configuration.Workflows.Select(w => new { w.Id, w.Name, w.IsSubroutine,
            Nodes = w.Nodes.Select(n => new { n.Id, n.GlobalIndex, n.LocalIndex, n.PositionX, n.PositionY, n.Width, n.Height, Signature = NodeSignature(n) }),
            Connections = w.Connections.Select(c => new { c.Id, c.SourceNodeId, c.SourcePort, c.TargetNodeId, c.TargetPort }) }),
        Variables = configuration.GlobalVariables.Select(v => new { Signature = VariableSignature(v), v.Group, v.Index, v.Description })
    }, WorkflowSerializationOptions.Compact);

    public BindingConfigurationSnapshot Undo(long expectedVersion) => Undo(expectedVersion, null);
    public BindingConfigurationSnapshot Redo(long expectedVersion) => Redo(expectedVersion, null);
    internal BindingConfigurationSnapshot Undo(long expectedVersion, Action<BindingConfigurationSnapshot>? accept) => Restore(expectedVersion, _undo, _redo, accept);
    internal BindingConfigurationSnapshot Redo(long expectedVersion, Action<BindingConfigurationSnapshot>? accept) => Restore(expectedVersion, _redo, _undo, accept);

    private BindingConfigurationSnapshot Restore(long expectedVersion,
        Stack<BindingConfigurationSnapshot> source, Stack<BindingConfigurationSnapshot> destination,
        Action<BindingConfigurationSnapshot>? accept)
    {
        lock (_gate)
        {
            if (_accepting) throw new InvalidOperationException("配置接受过程中不能重入恢复。");
            if (expectedVersion != _published.Version) throw new InvalidOperationException("恢复请求版本已过期。");
            if (source.Count == 0) throw new InvalidOperationException("没有可恢复的配置事务。");
            // Restore the entire accepted payload, not just canvas nodes. Do not reconcile
            // against the discarded newer contract and detach the restored bindings again.
            var configuration = source.Peek().CopyConfiguration();
            var graph = SolutionExecutionDependencyGraph.Compile(configuration, checkReadiness: false);
            var validator = new OutputBindingChangeService();
            validator.Reset(configuration);
            var next = new BindingConfigurationSnapshot(checked(_published.Version + 1), configuration, graph);
            destination.EnsureCapacity(checked(destination.Count + 1));
            Accept(next, accept);
            source.Pop(); destination.Push(_published);
            _validator = validator; _published = next;
            return next;
        }
    }

    private void Accept(BindingConfigurationSnapshot snapshot, Action<BindingConfigurationSnapshot>? accept)
    {
        if (accept == null) return;
        _accepting = true;
        try { accept(snapshot); }
        finally { _accepting = false; }
    }

    /// <summary>Stable-identity deep copy of binding configuration; excludes runtime results.</summary>
    internal static Solution Copy(Solution source)
    {
        var copy = new Solution { Id = source.Id, Name = source.Name, Description = source.Description,
            SchemaVersion = source.SchemaVersion, Version = source.Version };
        foreach (var workflow in source.Workflows)
        {
            var flow = new Workflow(workflow.Id, workflow.Name) { IsSubroutine = workflow.IsSubroutine };
            foreach (var node in workflow.Nodes)
                flow.Nodes.Add(new WorkflowNodeBase(node.Id, node.Name, node.DispName, node.ToolType)
                {
                    SubroutineId = node.SubroutineId, GlobalIndex = node.GlobalIndex, LocalIndex = node.LocalIndex,
                    IsEnabled = node.IsEnabled, PositionX = node.PositionX, PositionY = node.PositionY,
                    Width = node.Width, Height = node.Height, Parameters = ToolParameterSnapshot.Create(node.Parameters),
                    ParamSettings = JsonCopy(node.ParamSettings) ?? new ParamSettingContainer(), SelectedDisplaySource = JsonCopy(node.SelectedDisplaySource),
                    ImageSourceConfig = JsonCopy(node.ImageSourceConfig)
                });
            foreach (var connection in workflow.Connections)
                flow.Connections.Add(new WorkflowConnection { Id = connection.Id, SourceNodeId = connection.SourceNodeId,
                    TargetNodeId = connection.TargetNodeId, SourcePort = connection.SourcePort, TargetPort = connection.TargetPort });
            copy.Workflows.Add(flow);
        }
        foreach (var variable in source.GlobalVariables)
        {
            var clone = variable.Clone();
            clone.Id = variable.Id;
            clone.ModifiedTime = variable.ModifiedTime;
            // GlobalVariable.Clone deliberately shares Value for ordinary copy operations;
            // editing and history must not share mutable initial-value arrays or objects.
            if (variable.Value is { } value)
            {
                var type = value.GetType();
                clone.Value = JsonSerializer.Deserialize(JsonSerializer.Serialize(value, type, WorkflowSerializationOptions.Compact),
                    type, WorkflowSerializationOptions.Compact);
            }
            copy.GlobalVariables.Add(clone);
        }
        return copy;
    }

    private static T? JsonCopy<T>(T? source) where T : class => source == null ? null :
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, WorkflowSerializationOptions.Compact), WorkflowSerializationOptions.Compact)
        ?? throw new InvalidOperationException($"无法复制绑定配置 {typeof(T).Name}。");
}
