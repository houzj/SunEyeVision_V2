using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>History payload owns edit states only; it never owns runtime values or output resources.</summary>
    private sealed class GraphHistoryOperation(DocumentSession session, PreparedGraphEdit prepared,
        string description, string workflowId) : IDocumentHistoryOperation, IDocumentHistoryReplayEffects
    {
        public string Description { get; } = description;
        public IReadOnlyList<DocumentHistoryScope> Scopes { get; } =
            Array.AsReadOnly(prepared.Invalidated.Select(identity => identity.WorkflowId).Append(workflowId)
                .Distinct(StringComparer.Ordinal).Select(id => new DocumentHistoryScope(id, id)).ToArray());
        public IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes => prepared.Invalidated;

        public PreparedDocumentHistoryChange Prepare(DocumentHistoryDirection direction)
        {
            lock (session._gate)
            {
                session.EnsureOpen();
                var expected = direction == DocumentHistoryDirection.Forward ? prepared.Before : prepared.After;
                var target = direction == DocumentHistoryDirection.Forward ? prepared.After : prepared.Before;
                var expectedVariables = direction == DocumentHistoryDirection.Forward ? prepared.VariablesBefore : prepared.VariablesAfter;
                var targetVariables = direction == DocumentHistoryDirection.Forward ? prepared.VariablesAfter : prepared.VariablesBefore;
                if (session.Document.Workflows.Count != expected.Count ||
                    expected.Any(pair => session.Document.Workflows.Count(flow => ReferenceEquals(flow, pair.Key)) != 1 ||
                        !pair.Value.MatchesCurrent(pair.Key)) ||
                    session.Document.GlobalVariables.Count != prepared.ActualVariables.Count ||
                    prepared.ActualVariables.Where((variable, index) =>
                        !ReferenceEquals(session.Document.GlobalVariables[index], variable)).Any())
                    throw new InvalidOperationException("图历史所属方案、节点或变量实例已变化。");
                var currentVariables = GlobalVariable.CaptureDefinitions(prepared.ActualVariables.ToArray());
                if (!currentVariables.Select(VariableConfigurationSignature)
                    .SequenceEqual(expectedVariables.Select(VariableConfigurationSignature)))
                    throw new InvalidOperationException("变量配置已变化，不能回放图历史。");

                // Perform declaration validation before entering ValueGate. Runtime current values
                // always come from the live node; history never writes an old run value back.
                var resolved = new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance);
                foreach (var state in target.Values.SelectMany(graph => graph.Nodes))
                {
                    if (GraphState.SameConfiguration(state.Configuration, state.Node.Configuration))
                    {
                        resolved.Add(state.Node, state.Node.Configuration);
                        continue;
                    }
                    var desired = session.AdmitConfiguration(state.Node, state.Configuration);
                    var live = session.AdmitConfiguration(state.Node, state.Node.Configuration);
                    resolved.Add(state.Node, desired.PreserveCurrentValuesFrom(live).Configuration);
                }
                if (session.Document.GlobalVariables.Count != prepared.ActualVariables.Count ||
                    expected.Any(pair => session.Document.Workflows.Count(flow => ReferenceEquals(flow, pair.Key)) != 1 ||
                        !pair.Value.MatchesCurrent(pair.Key)) ||
                    prepared.ActualVariables.Where((variable, index) =>
                        !ReferenceEquals(session.Document.GlobalVariables[index], variable)).Any() ||
                    !GlobalVariable.CaptureDefinitions(prepared.ActualVariables.ToArray())
                        .Select(VariableConfigurationSignature).SequenceEqual(expectedVariables.Select(VariableConfigurationSignature)))
                    throw new InvalidOperationException("图历史准入期间正式状态已变化。");
                var restore = expected.ToDictionary(pair => pair.Key, pair => GraphState.Capture(pair.Key),
                    (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
                var restoreConfigurations = restore.Values.SelectMany(graph => graph.Nodes)
                    .ToDictionary(item => item.Node, item => item.Configuration,
                        (IEqualityComparer<NodeDefinition>)ReferenceEqualityComparer.Instance);
                var restoreVariableConfigurations = currentVariables.Select(variable => variable.CaptureConfiguration()).ToArray();
                // Never install references owned by the retained history payload: variable metadata
                // includes mutable producer/extension objects which UI could edit after replay.
                var targetVariableConfigurations = GlobalVariable.CaptureDefinitions(targetVariables.ToArray())
                    .Select(variable => variable.CaptureConfiguration()).ToArray();
                var variableChanged = currentVariables.Zip(targetVariables,
                    (current, desired) => VariableConfigurationSignature(current) != VariableConfigurationSignature(desired)).ToArray();

                void Install(IReadOnlyDictionary<WorkflowDefinition, GraphState> graphs,
                    IReadOnlyDictionary<NodeDefinition, ToolConfiguration> configurations,
                    IReadOnlyList<GlobalVariable.ConfigurationState> variableConfigurations)
                {
                    lock (session._gate)
                    lock (GlobalVariable.ValueGate)
                    {
                        session.EnsureOpen();
                        foreach (var (flow, graph) in graphs)
                            graph.Apply(flow, (node, _) => configurations[node]);
                        for (var index = 0; index < prepared.ActualVariables.Count; index++)
                            if (variableChanged[index])
                                prepared.ActualVariables[index].ApplyConfigurationRaw(variableConfigurations[index]);
                    }
                }
                return new PreparedDocumentHistoryChange(
                    () => Install(target, resolved, targetVariableConfigurations),
                    () => Install(restore, restoreConfigurations, restoreVariableConfigurations));
            }
        }

        public void Dispose() { }
    }
}
