using System.Collections.Immutable;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    public void ValidateEdit(DocumentEditProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            _preparing = true;
            try
            {
                var prepared = PrepareEditCore(proposal);
                lock (GlobalVariable.ValueGate) CheckExplicitValueRevisions(prepared, proposal);
            }
            finally { _preparing = false; }
        }
    }

    public DocumentEditReceipt CommitEdit(DocumentEditProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        DocumentEditReceipt receipt;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            _preparing = true;
            try
            {
                var prepared = PrepareEditCore(proposal);
                var effects = ProjectGraphChange(prepared.Before.Graphs, prepared.After.Graphs);
                var variableChanges = ProjectVariableChanges(prepared.Before, prepared.After, prepared.ValueWrites.Keys);
                var workflowChanges = ProjectWorkflowConfigurationChanges(prepared.Before, prepared.After);
                var rootChanged = workflowChanges.Length != 0;
                var configurationChanged = effects.Nodes.Count != 0 || effects.Removed.Count != 0 || effects.Connections.Count != 0 ||
                    rootChanged || prepared.Before.Variables.Length != prepared.After.Variables.Length ||
                    prepared.Before.Variables.Where((variable, index) => !ReferenceEquals(variable, prepared.After.Variables[index])).Any() ||
                    !prepared.Before.Definitions.Select(VariableConfigurationSignature)
                        .SequenceEqual(prepared.After.Definitions.Select(VariableConfigurationSignature));
                var currentValuesChanged = prepared.After.Graphs.Values.SelectMany(graph => graph.Nodes)
                    .Any(item => !SameNodeCurrentValues(item.Node.Configuration, item.Configuration));
                var nodeChanges = effects.Nodes.ToList();
                foreach (var (flow, graph) in prepared.After.Graphs)
                    foreach (var item in graph.Nodes.Where(item => !SameNodeCurrentValues(item.Node.Configuration, item.Configuration)))
                    {
                        var index = nodeChanges.FindIndex(change => ReferenceEquals(change.Node, item.Node));
                        if (index < 0) nodeChanges.Add(new(new(flow.Id, item.Node.Id), item.Node, NodeChangedAspect.CurrentValues));
                        else nodeChanges[index] = nodeChanges[index] with { Aspects = nodeChanges[index].Aspects | NodeChangedAspect.CurrentValues };
                    }
                var changed = configurationChanged || currentValuesChanged || prepared.ValueWrites.Count != 0;
                var revision = changed ? checked(ChangeRevision + 1) : ChangeRevision;
                var change = changed ? new DocumentChangeSet(EditSessionId, revision, DocumentChangeOrigin.Edit,
                    nodeChanges.ToImmutableArray(), effects.Removed.ToImmutableArray(), effects.Connections.ToImmutableArray(),
                    variableChanges.ToImmutableArray(), prepared.Invalidated.ToImmutableArray(),
                    workflowChanges) : null;
                DocumentHistoryEntry? history = null;
                if (changed)
                {
                    var operation = new ConfigurationHistoryOperation(this, prepared, proposal);
                    try
                    {
                        if (configurationChanged) history = History.Execute(operation);
                        else
                        {
                            var direct = operation.Prepare(DocumentHistoryDirection.Forward);
                            try { direct.Apply(); }
                            catch (Exception error)
                            {
                                try { direct.Rollback(); }
                                catch (Exception rollback) { throw new AggregateException("值提交和回滚均失败。", error, rollback); }
                                throw;
                            }
                        }
                        operation.ReleaseInitialState();
                    }
                    catch { operation.Dispose(); throw; }
                    if (!configurationChanged) operation.Dispose();
                    ChangeRevision = revision;
                    RetireChangedDomains(change!);
                }
                proposal.MarkConsumed();
                receipt = new DocumentEditReceipt(this, change, history, prepared.AcceptedVariables);
            }
            finally { _preparing = false; }
        }
        mutation.Dispose();
        if (receipt.Change is { } committed) CompleteChangePublication(committed);
        return receipt;
    }

    private static bool SameNodeCurrentValues(ToolConfiguration first, ToolConfiguration second) =>
        first.CaptureCurrentValues().Keys.Union(second.CaptureCurrentValues().Keys, StringComparer.Ordinal)
            .All(id => !first.CurrentValueChangedFrom(id, second));

    private static IReadOnlyList<VariableChange> ProjectVariableChanges(EditState before, EditState after,
        IEnumerable<GlobalVariable>? written = null)
    {
        var writes = new HashSet<GlobalVariable>(written ?? Array.Empty<GlobalVariable>(), ReferenceEqualityComparer.Instance);
        var result = new List<VariableChange>();
        for (var index = 0; index < before.Variables.Length; index++)
        {
            var variable = before.Variables[index];
            if (!after.Variables.Any(item => ReferenceEquals(item, variable)))
                result.Add(new(variable, VariableChangeKind.Removed, variable.Id, index));
        }
        for (var index = 0; index < after.Variables.Length; index++)
        {
            var variable = after.Variables[index];
            var oldIndex = Array.FindIndex(before.Variables, item => ReferenceEquals(item, variable));
            if (oldIndex < 0) result.Add(new(variable, VariableChangeKind.Added, null, null, writes.Contains(variable)));
            else if (oldIndex != index || writes.Contains(variable) || VariableConfigurationSignature(before.Definitions[oldIndex]) !=
                VariableConfigurationSignature(after.Definitions[index]))
                result.Add(new(variable, VariableChangeKind.Updated, before.Definitions[oldIndex].Id, oldIndex, writes.Contains(variable)));
        }
        return result;
    }
}
