using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private static EditState ForConfigurationHistory(EditState state) => new(
        state.Graphs.ToDictionary(pair => pair.Key, pair => pair.Value.WithoutCurrentValues(),
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance),
        state.Roots, state.Workspaces, state.Variables.ToArray(), StripRuntimeValues(state.Definitions));

    private sealed class ConfigurationHistoryOperation : IDocumentHistoryOperation, IDocumentHistoryReplayEffects
    {
        private readonly DocumentSession _session;
        private readonly EditState _before, _after;
        private PreparedEdit? _initial;
        private DocumentEditProposal? _proposal;
        public string Description => "提交参数、根流程与全局变量编辑";
        public IReadOnlyList<DocumentHistoryScope> Scopes { get; }
        public IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes { get; }
        internal ConfigurationHistoryOperation(DocumentSession session, PreparedEdit prepared, DocumentEditProposal proposal)
        {
            _session = session; _initial = prepared; _proposal = proposal;
            _before = ForConfigurationHistory(prepared.Before); _after = ForConfigurationHistory(prepared.After);
            Scopes = prepared.After.Graphs.Keys.Select(flow => new DocumentHistoryScope(flow.Id, flow.Id)).ToArray();
            InvalidatedNodes = prepared.Invalidated;
        }
        internal void ReleaseInitialState() { _initial = null; _proposal = null; }

        public PreparedDocumentHistoryChange Prepare(DocumentHistoryDirection direction)
        {
            lock (_session._gate)
            {
                _session.EnsureOpen();
                var expected = direction == DocumentHistoryDirection.Forward ? _before : _after;
                var target = direction == DocumentHistoryDirection.Forward ? _after : _before;
                var initial = _initial;
                if (!_session.MatchesEditState(expected)) throw new InvalidOperationException("配置历史所属实体或配置已变化。");
                var configs = new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance);
                foreach (var item in target.Graphs.Values.SelectMany(graph => graph.Nodes))
                {
                    if (initial is not null)
                        configs.Add(item.Node, initial.After.Graphs.Values.SelectMany(graph => graph.Nodes)
                            .Single(actual => ReferenceEquals(actual.Node, item.Node)).Configuration);
                    else if (GraphState.SameConfiguration(item.Configuration, item.Node.Configuration)) configs.Add(item.Node, item.Node.Configuration);
                    else configs.Add(item.Node, _session.AdmitConfiguration(item.Node, item.Configuration)
                        .PreserveCurrentValuesFrom(_session.AdmitConfiguration(item.Node, item.Node.Configuration)).Configuration);
                }
                if (!_session.MatchesEditState(expected)) throw new InvalidOperationException("历史准入回调期间正式状态已变化。");
                var restore = _session.CaptureEditState();
                var definitions = GlobalVariable.CaptureDefinitions(target.Definitions.ToArray());
                var restoreValues = new Dictionary<GlobalVariable, GlobalVariable.ValueState>(ReferenceEqualityComparer.Instance);
                var writes = initial?.ValueWrites;
                var installed = false;
                lock (GlobalVariable.ValueGate)
                {
                    foreach (var variable in expected.Variables.Concat(target.Variables).Distinct<GlobalVariable>(ReferenceEqualityComparer.Instance))
                        restoreValues.Add(variable, variable.CaptureValueRaw());
                    if (initial is not null) _session.CheckExplicitValueRevisions(initial, _proposal!);
                    for (var index = 0; index < target.Variables.Length; index++)
                    {
                        var variable = target.Variables[index]; var type = definitions[index].Type;
                        var value = writes?.GetValueOrDefault(variable, variable.CaptureValueRaw().Value) ?? variable.CaptureValueRaw().Value;
                        if (GlobalVariable.IsPureValueDeclaration(type) && (variable.Type != type || writes?.ContainsKey(variable) == true))
                            if (value is not null || !type.EndsWith("[]", StringComparison.Ordinal))
                                VariableValueDeclarations.ValidateEditableValue(type, value);
                        if (writes?.ContainsKey(variable) == true) _ = checked(variable.CaptureValueRaw().Revision + 1);
                    }
                }
                void Install(EditState state, IReadOnlyList<GlobalVariable> variableDefinitions,
                    Func<NodeDefinition, ToolConfiguration, ToolConfiguration> resolve, bool rollback)
                {
                    lock (_session._gate)
                    lock (GlobalVariable.ValueGate)
                    {
                        _session.EnsureOpen();
                        if (rollback && !installed) return;
                        if (!rollback)
                        {
                            if (!_session.MatchesEditState(expected)) throw new InvalidOperationException("安装前正式状态已变化。");
                            if (initial is not null) _session.CheckExplicitValueRevisions(initial, _proposal!);
                            // No rollback may overwrite a value written after Prepare but before
                            // Apply. Capture the actual rollback values only once installation starts.
                            restoreValues.Clear();
                            foreach (var variable in expected.Variables.Concat(target.Variables).Distinct<GlobalVariable>(ReferenceEqualityComparer.Instance))
                                restoreValues.Add(variable, variable.CaptureValueRaw());
                            installed = true;
                        }
                        foreach (var (flow, graph) in state.Graphs) graph.Apply(flow, resolve);
                        foreach (var (flow, configuration) in state.Roots) flow.RootConfiguration = configuration;
                        foreach (var (flow, workspace) in state.Workspaces)
                            flow.WorkspaceSettings = MergeWorkspaceRunSettings(workspace, flow.WorkspaceSettings);
                        for (var index = 0; index < state.Variables.Length; index++)
                        {
                            var variable = state.Variables[index];
                            variable.ApplyConfigurationRaw(variableDefinitions[index].CaptureConfiguration());
                            if (rollback) variable.ApplyValueRaw(restoreValues[variable]);
                            else if (writes?.TryGetValue(variable, out var value) == true)
                            {
                                var revision = checked(variable.CaptureValueRaw().Revision + 1);
                                variable.ApplyValueRaw(new(GlobalVariable.CopyEditableValue(value), revision));
                            }
                        }
                        _session.Document.GlobalVariables = state.Variables;
                        if (rollback) installed = false;
                    }
                }
                var restoreDefinitions = GlobalVariable.CaptureDefinitions(restore.Definitions.ToArray());
                return new PreparedDocumentHistoryChange(
                    () => Install(target, definitions, (node, _) => configs[node], false),
                    () => Install(restore, restoreDefinitions, (_, configuration) => configuration, true));
            }
        }
        public void Dispose() => ReleaseInitialState();
    }
}
