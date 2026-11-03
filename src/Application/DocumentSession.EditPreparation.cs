using System.Collections.ObjectModel;
using System.Text.Json;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private sealed record EditState(IReadOnlyDictionary<WorkflowDefinition, GraphState> Graphs,
        IReadOnlyDictionary<WorkflowDefinition, RootWorkflowConfiguration> Roots,
        IReadOnlyDictionary<WorkflowDefinition, RootWorkspaceSettings> Workspaces,
        GlobalVariable[] Variables, IReadOnlyList<GlobalVariable> Definitions);
    private sealed record PreparedEdit(EditState Before, EditState After,
        IReadOnlyDictionary<GlobalVariable, object?> ValueWrites,
        IReadOnlyDictionary<GlobalVariable, GlobalVariable> AcceptedVariables,
        IReadOnlyList<ScopedNodeIdentity> Invalidated);

    private EditState CaptureEditState() => new(
        Document.Workflows.ToDictionary(flow => flow, GraphState.Capture,
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance),
        Document.Workflows.ToDictionary(flow => flow, flow => flow.RootConfiguration,
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance),
        Document.Workflows.ToDictionary(flow => flow, flow => flow.WorkspaceSettings,
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance),
        Document.GlobalVariables.ToArray(), GlobalVariable.CaptureDefinitions(Document.GlobalVariables.ToArray()));

    private bool MatchesEditState(EditState state) => Document.Workflows.Count == state.Graphs.Count &&
        state.Graphs.All(pair => Document.Workflows.Count(flow => ReferenceEquals(flow, pair.Key)) == 1 &&
            pair.Value.MatchesCurrent(pair.Key) && ReferenceEquals(pair.Key.RootConfiguration, state.Roots[pair.Key]) &&
            SameWorkspaceRunSettings(pair.Key.WorkspaceSettings, state.Workspaces[pair.Key])) &&
        Document.GlobalVariables.Count == state.Variables.Length &&
        state.Variables.Where((variable, index) => !ReferenceEquals(variable, Document.GlobalVariables[index])).Any() == false &&
        GlobalVariable.CaptureDefinitions(Document.GlobalVariables.ToArray()).Select(VariableConfigurationSignature)
            .SequenceEqual(state.Definitions.Select(VariableConfigurationSignature));

    private static bool SameEditableValue(object? first, object? second) =>
        JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);

    private PreparedEdit PrepareEditCore(DocumentEditProposal proposal)
    {
        EnsureOpen();
        if (!ReferenceEquals(proposal.Session, this)) throw new InvalidOperationException("编辑提案属于另一个方案会话。");
        proposal.EnsureCanConsume();
        var before = CaptureEditState();
        bool Current() { EnsureOpen(); return MatchesEditState(before); }
        var replacements = new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance);
        foreach (var item in proposal.Nodes)
        {
            var live = RequireCurrent(item.Opening);
            replacements.Add(live.Node, item.Candidate.MergeCurrentValues(live.Configuration, item.Opening.Configuration).Configuration);
        }
        var graphs = before.Graphs.ToDictionary(pair => pair.Key, pair => pair.Value.WithConfigurations(replacements),
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
        var roots = new Dictionary<WorkflowDefinition, RootWorkflowConfiguration>(before.Roots,
            ReferenceEqualityComparer.Instance);
        var workspaces = new Dictionary<WorkflowDefinition, RootWorkspaceSettings>(before.Workspaces, ReferenceEqualityComparer.Instance);
        foreach (var item in proposal.Roots)
        {
            ValidateScopeCore(item.Scope);
            if (item.Scope.Flow.IsSubroutine || !ReferenceEquals(item.Opening, item.Scope.Flow.RootConfiguration) ||
                !SameWorkspaceRunSettings(item.OpeningWorkspace, item.Scope.Flow.WorkspaceSettings))
                throw new InvalidOperationException("根流程配置已变化。");
            RootConfigurationValidation.Validate(item.Candidate, graphs[item.Scope.Flow], AdmitConfiguration);
            roots[item.Scope.Flow] = item.Candidate;
            item.Workspace.Validate();
            workspaces[item.Scope.Flow] = MergeWorkspaceRunSettings(item.Workspace, item.Scope.Flow.WorkspaceSettings);
        }

        var accepted = new Dictionary<GlobalVariable, GlobalVariable>(ReferenceEqualityComparer.Instance);
        var writes = new Dictionary<GlobalVariable, object?>(ReferenceEqualityComparer.Instance);
        var variables = before.Variables;
        IReadOnlyList<GlobalVariable> definitions = before.Definitions;
        if (proposal.Variables is { } edited)
        {
            if (!ReferenceEquals(edited.Session, this) || edited.Opening.Entries.Count != before.Variables.Length ||
                edited.Opening.Entries.Where((entry, index) => !ReferenceEquals(entry.Actual, before.Variables[index]) ||
                    VariableConfigurationSignature(entry.Opening) != VariableConfigurationSignature(before.Definitions[index])).Any())
                throw new InvalidOperationException("变量目录或配置已改变，草稿未接受。");
            if (edited.Candidates.Count != edited.Origins.Count) throw new InvalidOperationException("变量草稿来源目录不完整。");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var next = new List<GlobalVariable>();
            definitions = GlobalVariable.CaptureDefinitions(edited.Candidates.ToArray());
            for (var index = 0; index < definitions.Count; index++)
            {
                var candidate = definitions[index]; var origin = edited.Origins[index];
                if (candidate.Id != origin.CandidateId || origin.DraftInstance.Id != origin.CandidateId ||
                    string.IsNullOrWhiteSpace(candidate.Id) || !ids.Add(candidate.Id) || !names.Add(candidate.Name))
                    throw new InvalidOperationException("变量身份或名称为空、重复或已变化。");
                var validation = candidate.Validate();
                if (!validation.IsValid) throw new InvalidOperationException(validation.Errors[0]);
                GlobalVariable target;
                GlobalVariable? opening = null;
                if (origin.Actual is { } actual)
                {
                    var entry = edited.Opening.Entries.SingleOrDefault(item => ReferenceEquals(item.Actual, actual));
                    if (entry is null || actual.Id != candidate.Id || before.Variables.Count(item => ReferenceEquals(item, actual)) != 1)
                        throw new InvalidOperationException("变量草稿来源不是当前实际实例。");
                    target = actual; opening = entry.Opening;
                }
                else
                {
                    if (before.Variables.Any(item => item.Id == candidate.Id))
                        throw new InvalidOperationException("新变量不能接管已有变量身份。");
                    target = GlobalVariable.CaptureDefinitions(new[] { candidate })[0];
                    if (!GlobalVariable.IsPureValueDeclaration(candidate.Type) && candidate.Value is not null)
                        throw new InvalidOperationException("新增复杂变量值缺少正式创建与所有权合同。");
                }
                if (edited.ExplicitValues.TryGetValue(candidate.Id, out var value))
                {
                    VariableValueDeclarations.ValidateEditableValue(candidate.Type, value);
                    if (!SameEditableValue(value, candidate.Value)) throw new InvalidOperationException("SetValue后当前值又被直接修改。");
                    writes.Add(target, GlobalVariable.CopyEditableValue(value));
                }
                else if (opening is not null && GlobalVariable.IsPureValueDeclaration(candidate.Type) &&
                    !SameEditableValue(candidate.Value, opening.Value))
                    throw new InvalidOperationException("变量当前值必须通过SetValue显式提交。");
                if (GlobalVariable.IsPureValueDeclaration(candidate.Type) && (opening is null || opening.Type != candidate.Type))
                {
                    var retainedValue = writes.GetValueOrDefault(target, opening is null ? candidate.Value : target.Value);
                    if (retainedValue is not null || !candidate.Type.EndsWith("[]", StringComparison.Ordinal))
                        VariableValueDeclarations.ValidateEditableValue(candidate.Type, retainedValue);
                }
                else if (opening is not null && candidate.Type != opening.Type)
                    throw new InvalidOperationException("复杂变量类型切换缺少正式资源转换合同。");
                next.Add(target); accepted.Add(origin.DraftInstance, target);
            }
            variables = next.ToArray();
        }

        foreach (var variable in definitions.Where(variable => variable.ProducerBinding is not null))
        {
            var previous = before.Definitions.SingleOrDefault(item => item.Id == variable.Id);
            if (previous?.ProducerBinding?.ReferenceExpression == variable.ProducerBinding!.ReferenceExpression && previous.Type == variable.Type) continue;
            var validation = GlobalProducerBindingRules.Validate(variable.ProducerBinding, variable,
                id => graphs.SingleOrDefault(pair => pair.Key.Id == id).Value?.Nodes.Select(item => item.Node).ToArray(),
                node => { var state = graphs.Values.SelectMany(graph => graph.Nodes).Single(item => ReferenceEquals(item.Node, node));
                    return AdmitConfiguration(node, state.Configuration).Schema; }, Current);
            if (validation.State is not (BindingCompatibility.Compatible or BindingCompatibility.Conditional))
                throw new InvalidOperationException(validation.Reason);
        }
        var beforeDependencies = GraphDependencySnapshot.Create(Document, before.Graphs, before.Definitions, AdmitConfiguration);
        var reconciled = GraphBindingReconciliation.Prepare(Document, graphs, definitions, AdmitConfiguration, Current);
        var afterDependencies = GraphDependencySnapshot.Create(Document, reconciled.Graphs, reconciled.Variables, AdmitConfiguration);
        foreach (var (flow, graph) in reconciled.Graphs)
        {
            var participants = proposal.Nodes.Where(item => item.Opening.Scope.WorkflowId == flow.Id)
                .Select(item => item.Opening.Node.Id).ToHashSet(StringComparer.Ordinal);
            CandidateInputValidation.Validate(_documentId, flow.Id, ResultOwnerId, ChangeRevision, graph, reconciled.Variables,
                participants, proposal.Variables is not null, AdmitConfiguration, requireExecutionInputs: false);
            CandidateDisplayValidation.Validate(flow.Id, ResultOwnerId, ChangeRevision, graph,
                participants, proposal.Variables is not null, AdmitConfiguration);
        }
        var changedVariables = before.Definitions.Select(variable => variable.Id).Union(reconciled.Variables.Select(variable => variable.Id))
            .Where(id => { var old = before.Definitions.SingleOrDefault(variable => variable.Id == id);
                var next = reconciled.Variables.SingleOrDefault(variable => variable.Id == id);
                return old is null || next is null || VariableConfigurationSignature(old) != VariableConfigurationSignature(next); })
            .Concat(writes.Keys.Select(variable => variable.Id)).ToHashSet(StringComparer.Ordinal);
        var seeds = new HashSet<ScopedNodeIdentity>(reconciled.Invalidated);
        foreach (var collection in new[] { before.Graphs, reconciled.Graphs })
            foreach (var (flow, graph) in collection)
                foreach (var item in graph.Nodes)
                    if (BindingRelationCatalog.VariableReferences(AdmitConfiguration(item.Node, item.Configuration))
                        .Any(reference => reference.Reference.SolutionId == _documentId && changedVariables.Contains(reference.Reference.VariableId)))
                        seeds.Add(new(flow.Id, item.Node.Id));
        var invalidated = GraphInvalidationProjection.Compare(before.Graphs.ToDictionary(pair => pair.Key.Id, pair => pair.Value),
            reconciled.Graphs.ToDictionary(pair => pair.Key.Id, pair => pair.Value), beforeDependencies, afterDependencies,
            seeds, (node, config) => AdmitConfiguration(node, config).ExecutionConfigurationIdentity);
        if (!Current()) throw new InvalidOperationException("编辑准备回调期间正式方案已变化。");
        proposal.EnsureCanConsume();
        return new PreparedEdit(before, new EditState(reconciled.Graphs, roots, workspaces, variables, reconciled.Variables),
            new ReadOnlyDictionary<GlobalVariable, object?>(writes),
            new ReadOnlyDictionary<GlobalVariable, GlobalVariable>(accepted), invalidated);
    }

    private void CheckExplicitValueRevisions(PreparedEdit prepared, DocumentEditProposal proposal)
    {
        foreach (var (variable, _) in prepared.ValueWrites)
        {
            var opening = proposal.Variables?.Opening.Entries.SingleOrDefault(item => ReferenceEquals(item.Actual, variable));
            if (opening is not null && variable.CaptureValueRaw().Revision != opening.Opening.ValueRevision)
                throw new InvalidOperationException("变量值在编辑期间已更新，整批未发布。");
            _ = checked(variable.CaptureValueRaw().Revision + 1);
        }
    }
}
