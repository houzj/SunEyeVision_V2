using System.Collections.ObjectModel;
using System.Diagnostics;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>One private editor invocation against explicitly retained published inputs.</summary>
public sealed class ExecutionDebugInvocation : IDisposable
{
    private readonly ExecutionWorkflowSnapshot _workflow;
    private readonly ExecutionNodeSnapshot _node;
    private readonly ExecutionBindingPlan.NodePlan _plan;
    private readonly IReadOnlyList<DeclaredOutputReader> _readers;
    private readonly IReadOnlyList<DeclaredOutputReader> _displayReaders;
    private readonly IReadOnlyDictionary<string, ExecutionWorkflowSnapshot> _sourceWorkflows;
    private readonly IReadOnlyList<ExecutionGlobalProducerOperation> _producers;
    private readonly IReadOnlySet<string> _referencedVariableIds;
    private readonly IReadOnlySet<string>? _selectedInputs;
    private readonly ExecutionVariableValues? _variables;
    private readonly IHostInputPreparer? _hostInput;
    private readonly IReadOnlyImage? _image;
    private readonly IDisposable? _imageLease;
    private readonly Func<bool> _isCurrent;
    private readonly Action<string> _log;
    private RuntimeGenerationLease? _generation;
    private CancellationTokenSource? _cancellation;
    private int _state;
    private IReadOnlyDictionary<string, object?> _accepted = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    public ToolExecutionIdentity Identity { get; }
    public IReadOnlyList<RunIdentity> SourceIdentities => Array.AsReadOnly(_readers.Select(reader => reader.Snapshot.Identity).ToArray());
    public IReadOnlyList<RunIdentity> DisplaySourceIdentities => Array.AsReadOnly(_displayReaders.Select(reader => reader.Snapshot.Identity).ToArray());
    public IReadOnlyDictionary<string, object?> AcceptedParameters => new ReadOnlyDictionary<string, object?>(
        Volatile.Read(ref _accepted).ToDictionary(pair => pair.Key,
            pair => ValueRules.Freeze(_node.SettingsSchema.Inputs.Single(port => port.Id == pair.Key), pair.Value), StringComparer.Ordinal));

    private ExecutionDebugInvocation(ExecutionWorkflowSnapshot workflow, ExecutionNodeSnapshot node,
        ToolExecutionIdentity identity, RuntimeGenerationLease generation, CancellationTokenSource cancellation,
        IReadOnlyList<DeclaredOutputReader> readers, IReadOnlyList<DeclaredOutputReader> displayReaders,
        IReadOnlyList<ExecutionWorkflowSnapshot> sourceWorkflows,
        IReadOnlyList<ExecutionGlobalProducerOperation> producers, ExecutionVariableValues? variables, IHostInputPreparer? hostInput,
        IReadOnlyImage? image, IDisposable? imageLease, Func<bool> isCurrent, Action<string> log, IReadOnlySet<string>? selectedInputs)
    {
        _workflow = workflow; _node = node; Identity = identity; _generation = generation; _cancellation = cancellation;
        _readers = Array.AsReadOnly(readers.ToArray()); _displayReaders = Array.AsReadOnly(displayReaders.ToArray()); _variables = variables; _hostInput = hostInput;
        _sourceWorkflows = new ReadOnlyDictionary<string, ExecutionWorkflowSnapshot>(sourceWorkflows.ToDictionary(flow => flow.WorkflowId, StringComparer.Ordinal));
        _producers = Array.AsReadOnly(producers.ToArray());
        _image = image; _imageLease = imageLease; _isCurrent = isCurrent; _log = log;
        _selectedInputs = selectedInputs;
        _plan = new ExecutionBindingPlan(workflow, identity.RunId, variables, Array.Empty<OutputReference>(), identity.Owner.Id,
            CapturePublishedDeclarations(_sourceWorkflows, _readers)).ForNode(node.NodeId);
        var selectedVariables = _plan.Variables.Where(pair => selectedInputs is null || selectedInputs.Contains(pair.Key)).Select(pair => pair.Value).ToArray();
        var referencedVariableIds = selectedVariables.Select(plan => plan.Setting.Reference.VariableId)
            .Concat(selectedVariables.Where(plan => plan.VariableIndex is not null).Select(plan => plan.VariableIndex!.Reference.VariableId))
            .Concat(_plan.References.Where(pair => selectedInputs is null || selectedInputs.Contains(pair.Key)).SelectMany(pair => VariableIndices(pair.Value.Source))).ToHashSet(StringComparer.Ordinal);
        _referencedVariableIds = referencedVariableIds;
        foreach (var source in variables?.Sources.Where(source => source.Producer is not null && !variables.IsBlocked(source.Reference.VariableId)
            && referencedVariableIds.Contains(source.Reference.VariableId))
            ?? Array.Empty<BindingVariableSource>())
            if (_producers.Count(operation => operation.VariableId == source.Reference.VariableId) != 1)
                throw new InvalidOperationException("调试变量缺少唯一准入的生产操作，不能使用过期值预览。");
    }

    private static IEnumerable<string> VariableIndices(BindingReferencePlan plan)
    {
        if (plan.VariableIndex is { } index) yield return index.Reference.VariableId;
        if (plan.Index is { } nested) foreach (var id in VariableIndices(nested)) yield return id;
    }

    /// <remarks>Application supplies actual retained readers after its double-read/session check. Ownership transfers only on success.</remarks>
    internal static ExecutionDebugInvocation Capture(ExecutionWorkflowSnapshot workflow, ExecutionNodeSnapshot node,
        ToolExecutionOwner debugOwner, IReadOnlyList<DeclaredOutputReader> readers, IReadOnlyList<DeclaredOutputReader> displayReaders,
        IReadOnlyList<ExecutionWorkflowSnapshot> sourceWorkflows, IReadOnlyList<ExecutionGlobalProducerOperation> producers,
        ExecutionVariableValues? variables,
        IHostInputPreparer? hostInput, IReadOnlyImage? image, IDisposable? imageLease,
        Func<bool> isCurrent, Action<string> log, CancellationToken cancellation, IReadOnlyCollection<string>? previewInputs = null)
    {
        ArgumentNullException.ThrowIfNull(workflow); ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(debugOwner);
        ArgumentNullException.ThrowIfNull(readers); ArgumentNullException.ThrowIfNull(displayReaders); ArgumentNullException.ThrowIfNull(isCurrent); ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(sourceWorkflows); ArgumentNullException.ThrowIfNull(producers);
        if (!workflow.Nodes.Any(item => ReferenceEquals(item.Node, node)) || ReferenceEquals(debugOwner, workflow.Owner)
            || !workflow.Directory.IsActive || workflow.Owner.IsRetired || !isCurrent())
            throw new InvalidOperationException("调试节点、方案会话或独立运行 owner 已失效。");
        if ((image is null) != (imageLease is null)) throw new ArgumentException("调试图像必须携带本次 reader 租约。", nameof(image));
        var selected = previewInputs?.ToHashSet(StringComparer.Ordinal);
        if (previewInputs is not null && (selected!.Count == 0 || selected.Count != previewInputs.Count ||
            selected.Any(id => string.IsNullOrWhiteSpace(id) || !node.Schema.Inputs.Any(port => port.Id == id))))
            throw new ArgumentException("预览输入必须非空、唯一且属于已声明的输入端口。", nameof(previewInputs));
        if (selected is not null && hostInput is not null) throw new ArgumentException("只读输入预览不启动宿主输入。", nameof(hostInput));
        var scopes = sourceWorkflows.ToDictionary(flow => flow.WorkflowId, StringComparer.Ordinal);
        if (!scopes.TryGetValue(workflow.WorkflowId, out var own) || !ReferenceEquals(own, workflow)
            || scopes.Values.Any(flow => !ReferenceEquals(flow.Owner, workflow.Owner) || !ReferenceEquals(flow.Directory, workflow.Directory)))
            throw new InvalidOperationException("调试来源流程不属于同一方案和目录代次。 ");
        if (readers.Concat(displayReaders).Any(reader => reader.Snapshot.Identity.DocumentSessionId != workflow.Owner.Id
            || reader.Snapshot.SourceWorkflowId is not { } scope || !scopes.TryGetValue(scope, out var sourceFlow)
            || !sourceFlow.Nodes.Any(item => item.Node.NodeId == reader.Snapshot.Identity.NodeId)
            || reader.Snapshot.ExecutionIdentity is not { } sourceIdentity || !ReferenceEquals(sourceIdentity.Owner, workflow.Owner)
            || sourceIdentity.NodeId != ExecutionNodeSnapshot.MakeOwnerDomainId(scope, reader.Snapshot.Identity.NodeId)
            || sourceIdentity.ToInvocationIdentity(reader.Snapshot.Identity.NodeId) != reader.Snapshot.Identity
            || !reader.Snapshot.IsSuccess || !reader.Snapshot.IsComplete))
            throw new InvalidOperationException("预览来源不属于当前方案中实际捕获的流程及节点。 ");
        _ = CapturePublishedDeclarations(scopes, readers);
        _ = CapturePublishedDeclarations(scopes, displayReaders);
        var generation = workflow.Directory.AcquireInvocation("调试执行");
        CancellationTokenSource? linked = null; ToolExecutionIdentity? identity = null; ExecutionVariableValues? privateVariables = null;
        try
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, generation.RetirementToken);
            identity = debugOwner.Begin(node.OwnerDomainId, Guid.NewGuid(), FoundationContract.Major + "." + FoundationContract.Minor, linked.Token);
            privateVariables = variables?.Copy();
            var result = new ExecutionDebugInvocation(workflow, node, identity, generation, linked, readers, displayReaders, sourceWorkflows, producers, privateVariables,
                hostInput, image, imageLease, isCurrent, log, selected);
            if (!isCurrent() || !identity.IsCurrent || !workflow.Directory.IsActive)
                throw new InvalidOperationException("调试捕获期间方案、节点或目录发生变化。 ");
            return result;
        }
        catch
        {
            privateVariables?.Dispose(); identity?.CompleteCallbacks(); linked?.Dispose(); generation.Dispose(); throw;
        }
    }

    public async Task<DeclaredOutputSnapshot> ExecuteAsync()
    {
        if (_selectedInputs is not null) throw new InvalidOperationException("只读输入预览不能执行工具。 ");
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0) throw new InvalidOperationException("调试快照只能执行一次。 ");
        RuntimeGenerationLease? generation = null; IDisposable? admission = null; PreparedHostInvocation? host = null;
        var resources = new FrozenOutputResources(); ToolOutcome? outcome = null; DeclaredOutputSnapshot? result = null;
        var transferred = false; Exception? failure = null; var timer = Stopwatch.StartNew();
        try
        {
            generation = Interlocked.Exchange(ref _generation, null) ?? throw new InvalidOperationException("调试目录租约缺失。 ");
            Identity.CancellationToken.ThrowIfCancellationRequested();
            try { admission = Identity.Owner.EnterRun(Identity); }
            catch (InvalidOperationException) when (Identity.CancellationToken.IsCancellationRequested) { Identity.CancellationToken.ThrowIfCancellationRequested(); throw; }
            if (!_isCurrent()) throw new InvalidOperationException("调试草稿已失效。 ");
            RecomputeProducers();
            var snapshot = CapturePublishedSnapshot();
            var accepted = new Dictionary<string, object?>(StringComparer.Ordinal);
            var resolved = ExecutionBindingInputResolver.ResolvePublished(_node, _plan, snapshot, _variables, (id, value) =>
            {
                var port = _node.SettingsSchema.Inputs.Single(item => item.Id == id);
                var frozen = ValueRules.Freeze(port, value);
                if (!Identity.TryInvokeCallback(() => accepted[id] = frozen)) throw new InvalidOperationException("私有参数写入身份已失效。 ");
            });
            Volatile.Write(ref _accepted, new ReadOnlyDictionary<string, object?>(accepted));
            var settings = _node.MaterializeSettings(resolved.Settings);
            var inputs = new Dictionary<string, object?>(resolved.Inputs, StringComparer.Ordinal);
            var context = new DebugContext(Identity.ToInvocationIdentity(_node.NodeId), Identity.CancellationToken, resources, _log);
            if (_node.Definition is IToolExecutionFilter filter && !filter.ShouldExecute(settings))
                outcome = ToolOutcome.Suppress();
            else
            {
            IToolExecutionContext bound = context;
            if (_node.Directory.GetHostInputs(_node.Configuration.ToolId) is not null)
            {
                host = await HostInputCoordinator.PrepareAsync(_node, _hostInput ?? throw new InvalidOperationException("调试缺少宿主输入。"),
                    settings, Identity, _node.NodeId, context).ConfigureAwait(false);
                foreach (var pair in host.Values) if (!inputs.TryAdd(pair.Key, pair.Value)) throw new InvalidOperationException("调试宿主输入与绑定冲突。 ");
                bound = host.Context;
            }
            else if (_hostInput is not null) throw new InvalidOperationException("工具未声明该调试宿主输入。 ");
            if (_node.Schema.WorkflowImageInputId is { } imagePort && !inputs.ContainsKey(imagePort) && _image is not null) inputs.Add(imagePort, _image);
            outcome = await ToolRuntimeExecutor.ExecuteAsync(_node.Definition, settings, new InputValues(_node.Schema.Inputs, inputs), bound).ConfigureAwait(false);
            }
            var owned = new GenerationResourceOwner(resources, generation); generation = null; transferred = true;
            result = DeclaredOutputSnapshot.Capture(_node.Schema, 0, Identity, _node.NodeId, outcome, owned,
                timer.Elapsed.TotalMilliseconds, fields: _node.ResultFields, sourceWorkflowId: _workflow.WorkflowId);
            if (!_isCurrent() || !Identity.IsCurrent) throw new InvalidOperationException("调试结果已失效，拒绝发布。 ");
            if (result.IsSuccess && _node.HasResultDisplay)
            {
                try
                {
                    var ancestors = new HashSet<string>(StringComparer.Ordinal);
                    var pending = new Stack<string>(_workflow.Predecessors[_node.NodeId]);
                    while (pending.TryPop(out var id)) if (ancestors.Add(id))
                        foreach (var predecessor in _workflow.Predecessors[id]) pending.Push(predecessor);
                    using var sources = new ResultDisplaySourceSet(_displayReaders.Where(reader => reader.Snapshot.SourceWorkflowId == _workflow.WorkflowId
                        && ancestors.Contains(reader.Snapshot.Identity.NodeId))
                        .Select(reader => reader.Snapshot));
                    result.AttachDisplaySources(sources);
                }
                catch (Exception error) when (error is not OutOfMemoryException && !Identity.CancellationToken.IsCancellationRequested)
                { result.RejectDisplaySources("显示来源捕获失败：" + error.Message); }
            }
            return result;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            Interlocked.Exchange(ref _state, 2); var errors = new List<Exception>();
            try { Identity.CompleteCallbacks(); } catch (Exception error) { errors.Add(error); }
            foreach (var reader in _readers.Concat(_displayReaders).Distinct<DeclaredOutputReader>(ReferenceEqualityComparer.Instance)) try { reader.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { _variables?.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (host is not null) try { await host.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            try { _hostInput?.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (!ReferenceEquals(_imageLease, _hostInput)) try { _imageLease?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { admission?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { Interlocked.Exchange(ref _cancellation, null)?.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (!transferred)
            {
                try { (outcome?.Output as IDisposable)?.Dispose(); } catch (Exception error) { errors.Add(error); }
                if (!ReferenceEquals(outcome?.Output, resources)) try { resources.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { generation?.Dispose(); } catch (Exception error) { errors.Add(error); }
            }
            if ((failure is not null || errors.Count > 0) && result is not null) try { result.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) { if (failure is not null) errors.Insert(0, failure); throw new AggregateException("调试及资源清理失败。", errors); }
        }
    }

    public void Dispose()
    {
        var previous = Interlocked.CompareExchange(ref _state, 2, 0);
        if (previous == 2) return;
        if (previous == 1) throw new InvalidOperationException("调试正在执行，必须等待排空。 ");
        var errors = new List<Exception>();
        try { Identity.CompleteCallbacks(); } catch (Exception error) { errors.Add(error); }
        foreach (var reader in _readers.Concat(_displayReaders).Distinct<DeclaredOutputReader>(ReferenceEqualityComparer.Instance)) try { reader.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { _variables?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { _hostInput?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (!ReferenceEquals(_imageLease, _hostInput)) try { _imageLease?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Interlocked.Exchange(ref _generation, null)?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Interlocked.Exchange(ref _cancellation, null)?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count > 0) throw new AggregateException("调试资源释放失败。", errors);
    }

    public ExecutionInputPreviewLease CaptureInputs()
    {
        var selected = _selectedInputs ?? throw new InvalidOperationException("该调试快照不是只读输入预览。 ");
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0) throw new InvalidOperationException("预览快照只能使用一次。 ");
        try
        {
            if (!_isCurrent() || !Identity.IsCurrent) throw new InvalidOperationException("预览草稿已失效。 ");
            RecomputeProducers();
            var resolved = ExecutionBindingInputResolver.ResolvePublished(_node, _plan, CapturePublishedSnapshot(), _variables,
                (_, _) => throw new InvalidOperationException("只读预览不得写参数当前值。"), selected);
            var inputs = new Dictionary<string, object?>(resolved.Inputs, StringComparer.Ordinal);
            if (_node.Schema.WorkflowImageInputId is { } imagePort && selected.Contains(imagePort) && !inputs.ContainsKey(imagePort) && _image is not null)
                inputs.Add(imagePort, _image);
            var values = new InputValues(_node.Schema.Inputs.Where(port => selected.Contains(port.Id)), inputs);
            if (!_isCurrent() || !Identity.IsCurrent) throw new InvalidOperationException("预览捕获期间来源已变化。 ");
            return new ExecutionInputPreviewLease(values, SourceIdentities,
                () => Volatile.Read(ref _state) == 1 && _isCurrent() && Identity.IsCurrent, ReleasePreview);
        }
        catch (Exception failure)
        {
            try { ReleasePreview(); } catch (Exception cleanup) { throw new AggregateException("预览捕获和释放失败。", failure, cleanup); }
            throw;
        }
    }

    private PublishedBindingSnapshot CapturePublishedSnapshot()
    {
        var candidates = _readers.Where(reader => reader.Snapshot.SourceWorkflowId == _workflow.WorkflowId && reader.Values is not null)
            .Select(reader => new BindingNodeResult(new(_workflow.WorkflowId, reader.Snapshot.Identity.NodeId),
                reader.Snapshot.Identity, reader.Snapshot.SchemaRevision, reader.Snapshot.Schema.Fingerprint,
                reader.Snapshot.IsSuccess, reader.Values!)).ToArray();
        return new PublishedBindingSnapshot(new(_workflow.WorkflowId, _node.NodeId), Identity.ToInvocationIdentity(_node.NodeId),
            _workflow.DocumentRevision, candidates, _variables?.Capture(Identity.ToInvocationIdentity(_node.NodeId)));
    }

    private static IReadOnlyDictionary<BindingNodeId, BindingSource> CapturePublishedDeclarations(
        IReadOnlyDictionary<string, ExecutionWorkflowSnapshot> flows, IReadOnlyList<DeclaredOutputReader> readers)
    {
        var declarations = new Dictionary<BindingNodeId, BindingSource>();
        foreach (var reader in readers)
        {
            var result = reader.Snapshot;
            var flow = flows[result.SourceWorkflowId!];
            var sourceNode = flow.Nodes.Single(item => item.Node.NodeId == result.Identity.NodeId).Node;
            var schema = new ToolSchema(Array.Empty<Port>(), sourceNode.Schema.Outputs, outputDisplay: sourceNode.Schema.OutputDisplay);
            if (schema.Fingerprint != result.Schema.Fingerprint || reader.Values is null)
                throw new InvalidOperationException("来源声明已变化或结果不可读，请重新运行来源节点。 ");
            var id = new BindingNodeId(flow.WorkflowId, sourceNode.NodeId);
            declarations.Add(id, new BindingSource(id, result.Identity.ScopeId, schema, result.SchemaRevision));
        }
        return declarations;
    }

    private void ReleasePreview()
    {
        if (Interlocked.CompareExchange(ref _state, 2, 1) != 1) return;
        var errors = new List<Exception>();
        try { Identity.CompleteCallbacks(); } catch (Exception error) { errors.Add(error); }
        foreach (var reader in _readers.Concat(_displayReaders).Distinct<DeclaredOutputReader>(ReferenceEqualityComparer.Instance)) try { reader.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { _variables?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (!ReferenceEquals(_imageLease, _hostInput)) try { _imageLease?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Interlocked.Exchange(ref _generation, null)?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Interlocked.Exchange(ref _cancellation, null)?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("预览 reader 释放失败。", errors);
    }
    private sealed class DebugContext(RunIdentity identity, CancellationToken cancellation,
        IOutputResources resources, Action<string> log) : IToolExecutionContext
    { public RunIdentity Identity => identity; public CancellationToken Cancellation => cancellation; public IOutputResources Resources => resources; public void Log(string message) => log(message); }

    private void RecomputeProducers()
    {
        if (_variables is null) return;
        foreach (var source in _variables.Sources.Where(source => source.Producer is not null
            && !_variables.IsBlocked(source.Reference.VariableId) && _referencedVariableIds.Contains(source.Reference.VariableId)))
        {
            Identity.CancellationToken.ThrowIfCancellationRequested();
            var operation = _producers.Single(item => item.VariableId == source.Reference.VariableId);
            if (!_variables.Matches(operation.VariableId, operation.VariableName) || operation.Source.ScopeId != source.Producer!.Value.ScopeId
                || operation.Source.NodeId != source.Producer.Value.NodeId || !_sourceWorkflows.TryGetValue(operation.Source.ScopeId!, out var flow))
                throw new InvalidOperationException("调试变量生产操作与实际冻结声明不一致。 ");
            var consumer = new BindingNodeId(flow.WorkflowId, "debug-global-producer:" + operation.VariableId);
            var declarations = CapturePublishedDeclarations(_sourceWorkflows, _readers).Values
                .Where(item => item.Node.ScopeId == flow.WorkflowId).ToArray();
            var scope = new BindingScopeSnapshot(consumer, Identity.Owner.Id, _workflow.DocumentRevision,
                declarations, declarations.Select(item => item.Node), Array.Empty<KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>>());
            var compiled = new ParameterBindingService().CompileBinding(operation.Source, operation.Target, scope);
            Diagnostic? issue = compiled.Diagnostic ?? (compiled.Plan is null ? new Diagnostic("variable-binding", "变量生产声明不可用。") : null); object? value = null;
            if (compiled.Plan is { } plan)
            {
                var candidates = _readers.Where(reader => reader.Snapshot.SourceWorkflowId == flow.WorkflowId && reader.Values is not null)
                    .Select(reader => new BindingNodeResult(new(flow.WorkflowId, reader.Snapshot.Identity.NodeId), reader.Snapshot.Identity,
                        reader.Snapshot.SchemaRevision, reader.Snapshot.Schema.Fingerprint, reader.Snapshot.IsSuccess, reader.Values!)).ToArray();
                var snapshot = new PublishedBindingSnapshot(consumer, Identity.ToInvocationIdentity(consumer.NodeId), _workflow.DocumentRevision, candidates);
                var resolved = new ParameterBindingService().ResolvePublishedBinding(plan, snapshot);
                issue = resolved.Success ? null : resolved.Diagnostic ?? new Diagnostic("variable-value", "变量生产值不可用。");
                if (resolved.Success)
                {
                    try { value = ExecutionGlobalProducers.Materialize(operation.Target, operation.ValueType, resolved.Value); }
                    catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
                    { issue = new Diagnostic("variable-value", error.Message, operation.VariableId); }
                }
            }
            _variables.Publish(new Dictionary<string, (object?, Diagnostic?)> { [operation.VariableId] = (value, issue) }, Identity.CancellationToken);
        }
    }
}

public sealed class ExecutionInputPreviewLease : IDisposable
{
    private readonly Func<bool> _current;
    private Action? _release;
    private int _checking;
    internal ExecutionInputPreviewLease(InputValues inputs, IReadOnlyList<RunIdentity> sources, Func<bool> current, Action release)
    { Inputs = inputs; SourceIdentities = sources; _current = current; _release = release; }
    public InputValues Inputs { get; }
    public IReadOnlyList<RunIdentity> SourceIdentities { get; }
    public bool IsCurrent
    {
        get
        {
            var release = Volatile.Read(ref _release);
            if (release is null || Interlocked.Exchange(ref _checking, 1) != 0) return false;
            try
            {
                var valid = _current() && ReferenceEquals(release, Volatile.Read(ref _release));
                if (!valid) Dispose();
                return valid;
            }
            catch (Exception failure)
            {
                try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                return false;
            }
            finally { Volatile.Write(ref _checking, 0); }
        }
    }
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
