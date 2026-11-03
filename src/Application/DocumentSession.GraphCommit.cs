using System.Collections.Immutable;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Execution;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>A new live document always starts with one actual root workflow.</summary>
    public static DocumentSession CreateNew(RuntimeDirectorySnapshot directory, string name = "未命名方案")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var document = new SolutionDefinition(Guid.NewGuid().ToString("N"), name);
        document.MutableWorkflows.Add(new WorkflowDefinition(Guid.NewGuid().ToString("N"), "流程1"));
        return new DocumentSession(document, directory);
    }

    public bool CanUndo => !_closed && !_closing && !_preparing && !_notifying && History.CanUndo;
    public bool CanRedo => !_closed && !_closing && !_preparing && !_notifying && History.CanRedo;
    public IReadOnlyList<DocumentHistoryEntry> HistoryEntries => History.Entries;
    public Exception? LastInvalidationError { get; private set; }

    /// <summary>Publish one reconciled graph proposal through the document's single history cursor.
    /// A semantic no-op creates neither a history entry nor a document notification.</summary>
    public DocumentChangeSet? CommitGraphEdit(GraphEditDraft draft, string description)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        DocumentChangeSet? change;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope = OpenScope(draft.Workflow.Id);
            var prepared = PrepareGraphEditCore(scope, draft);
            if (prepared.After.All(pair => pair.Value.MatchesCurrent(pair.Key)) &&
                prepared.VariablesBefore.Select(VariableConfigurationSignature)
                    .SequenceEqual(prepared.VariablesAfter.Select(VariableConfigurationSignature)))
                return null;

            // Plugin callbacks have completed. Keep reentrant edits/close blocked until both
            // the authoritative graph and its history cursor have committed.
            _preparing = true;
            try
            {
                var effects = ProjectGraphChange(prepared.Before, prepared.After);
                var variables = prepared.ActualVariables.Select((variable, index) => (variable, index))
                    .Where(item => VariableConfigurationSignature(prepared.VariablesBefore[item.index]) !=
                        VariableConfigurationSignature(prepared.VariablesAfter[item.index]))
                    .Select(item => new VariableChange(item.variable, VariableChangeKind.Updated,
                        prepared.VariablesBefore[item.index].Id, item.index)).ToArray();
                // Allocate the receipt before installation; observers never participate in rollback.
                var revision = checked(ChangeRevision + 1);
                change = new DocumentChangeSet(EditSessionId, revision, DocumentChangeOrigin.Edit,
                    effects.Nodes.ToImmutableArray(), effects.Removed.ToImmutableArray(),
                    effects.Connections.ToImmutableArray(), variables.ToImmutableArray(),
                    prepared.Invalidated.ToImmutableArray());
                var operation = new GraphHistoryOperation(this, prepared, description, scope.WorkflowId);
                try { History.Execute(operation); }
                catch { operation.Dispose(); throw; }
                ChangeRevision = revision;
                RetireChangedDomains(change);
            }
            finally { _preparing = false; }
        }
        mutation.Dispose();
        NotifyChanged(change);
        return change;
    }

    public bool Undo() => ReplayGraphHistory(DocumentChangeOrigin.Undo);
    public bool Redo() => ReplayGraphHistory(DocumentChangeOrigin.Redo);

    private bool ReplayGraphHistory(DocumentChangeOrigin origin)
    {
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        DocumentChangeSet change;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            if (origin == DocumentChangeOrigin.Undo ? !History.CanUndo : !History.CanRedo) return false;
            var before = CaptureEditState();
            var revision = checked(ChangeRevision + 1);
            _preparing = true;
            try
            {
                if (!(origin == DocumentChangeOrigin.Undo ? History.Undo() : History.Redo())) return false;
                var after = CaptureEditState();
                var effects = ProjectGraphChange(before.Graphs, after.Graphs);
                var variableChanges = ProjectVariableChanges(before, after);
                change = new DocumentChangeSet(EditSessionId, revision, origin,
                    effects.Nodes.ToImmutableArray(), effects.Removed.ToImmutableArray(),
                    effects.Connections.ToImmutableArray(), variableChanges.ToImmutableArray(),
                    History.LastInvalidatedNodes.ToImmutableArray(),
                    after.Roots.Where(pair => before.Roots[pair.Key] != pair.Value)
                        .Select(pair => new WorkflowChange(pair.Key, WorkflowChangedAspect.RootConfiguration)).ToImmutableArray());
                ChangeRevision = revision;
                RetireChangedDomains(change);
            }
            finally { _preparing = false; }
        }
        mutation.Dispose();
        NotifyChanged(change);
        return true;
    }

    private static GraphChangeProjection.Changes ProjectGraphChange(
        IReadOnlyDictionary<WorkflowDefinition, GraphState> before,
        IReadOnlyDictionary<WorkflowDefinition, GraphState> after)
    {
        var parts = before.Select(pair => GraphChangeProjection.Compare(pair.Key.Id, pair.Value, after[pair.Key])).ToArray();
        return new(parts.SelectMany(part => part.Nodes).ToArray(), parts.SelectMany(part => part.Removed).ToArray(),
            parts.SelectMany(part => part.Connections).ToArray());
    }

    // Authoritative edit/history is already committed. A failing resource observer cannot
    // roll it back, and every invalidated domain must still be retired before new admission.
    private void RetireChangedDomains(DocumentChangeSet change)
    {
        var failures = new List<Exception>();
        foreach (var identity in change.ExecutionInvalidatedNodes)
            try { _owner.RetireNode(ExecutionNodeSnapshot.MakeOwnerDomainId(identity.WorkflowId, identity.NodeId)); }
            catch (Exception error) { failures.Add(error); }
        LastInvalidationError = failures.Count == 0 ? null : new AggregateException("编辑已提交，部分结果域清理观察者失败。", failures);
    }
}
