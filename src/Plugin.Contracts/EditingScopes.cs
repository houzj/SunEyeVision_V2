namespace SunEyeVision.Plugin.Contracts;

/// <summary>Identity granted by the document owner. A view never creates an independent publishing scope.</summary>
public readonly record struct EditScopeIdentity
{
    public EditScopeIdentity(Guid documentSessionId, Guid editScopeId, BindingNodeId node, long baseConfigurationRevision)
    {
        if (documentSessionId == Guid.Empty || editScopeId == Guid.Empty || string.IsNullOrWhiteSpace(node.ScopeId)
            || string.IsNullOrWhiteSpace(node.NodeId) || baseConfigurationRevision < 0)
            throw new ArgumentException("A document, edit scope, node and nonnegative configuration revision are required.");
        DocumentSessionId = documentSessionId; EditScopeId = editScopeId; Node = node; BaseConfigurationRevision = baseConfigurationRevision;
    }
    public Guid DocumentSessionId { get; }
    public Guid EditScopeId { get; }
    public BindingNodeId Node { get; }
    public long BaseConfigurationRevision { get; }
}
public sealed record EditIssue(BindingNodeId? Node, string? VariableId, Diagnostic Diagnostic);
public sealed record ParameterEditAcknowledgement(EditScopeIdentity Identity,long DraftRevision,bool NewerDraftPreserved);
public sealed record VariableValueCommand(string VariableId, long ExpectedValueRevision, ConstantSetting NewValue);
public sealed record VariableValueReceipt(string VariableId, long ValueRevision);
public sealed record EditApplyResult(Guid DocumentSessionId, Guid TransactionId, bool Accepted, long ConfigurationRevision,
    IReadOnlyList<VariableValueReceipt> Values, IReadOnlyList<EditIssue> Issues, string? RefreshError);
public enum EditorPhase { Ready, Dirty, ValidationError, DebugRunning, Closing, Closed }
public sealed record EditorState(EditorPhase Phase, long ConfigurationRevision, long DraftRevision, IReadOnlyList<EditIssue> Issues);
public sealed record DebugRunReply(RunIdentity Identity, bool Admitted, IReadOnlyList<Diagnostic> Issues);
public sealed record FieldEditChange(EditScopeIdentity Scope, string FieldId, long DraftRevision,
    ParameterSetting? Pending, string? RawText, IReadOnlyList<Diagnostic> Issues);

/// <summary>Unset is Clear, distinct from a typed null or valid empty value. The scope owns the draft.</summary>
public interface IFieldEdit
{
    string FieldId { get; }
    Port Declaration { get; }
    ParameterSetting? Pending { get; }
    string? RawText { get; }
    IReadOnlyList<Diagnostic> Issues { get; }
    IDisposable Subscribe(Action<FieldEditChange> observer);
    bool Set(ParameterSetting setting);
    bool SetText(string rawText);
    void Clear();
}
public interface IParameterEditScope
{
    EditScopeIdentity Identity { get; }
    long DraftRevision { get; }
    IFieldEdit Field(string fieldId);
    IDisposable Subscribe(Action<EditorState> observer);
    ValueTask<EditApplyResult> ApplyAsync(CancellationToken cancellation);
    ValueTask CancelAsync(CancellationToken cancellation);
}
public sealed record GeometryEditRequest(EditScopeIdentity Scope, string FieldId, long ExpectedDraftRevision,
    CoordinateFrame Frame, ImageFrameAddress Image);
public interface IGeometryEditService
{
    ValueTask<IGeometryEditLease> BeginAsync(GeometryEditRequest request, CancellationToken cancellation);
}
public interface IGeometryEditLease : IAsyncDisposable
{
    Guid InteractionId { get; }
    EditScopeIdentity Scope { get; }
    ValueTask<bool> TrySetAsync(ParameterSetting geometryOrReference, long expectedDraftRevision, CancellationToken cancellation);
    ValueTask CancelGestureAsync(CancellationToken cancellation);
}
/// <summary>Restricted editor capabilities. Debug freezes this scope; production execution is not exposed.</summary>
public interface IToolEditorContext
{
    IParameterEditScope Scope { get; }
    IGeometryEditService Geometry { get; }
    EditorState State { get; }
    ValueTask<DebugRunReply> RequestRunAsync(CancellationToken cancellation);
    ValueTask RequestStopAsync(Guid ownedDebugRunId, CancellationToken cancellation);
}

/// <summary>A copied, immutable draft proposal. Only its creating session can acknowledge it after document publication.</summary>
public sealed class PreparedParameterEdit
{
    private readonly object _owner;
    private readonly IReadOnlyDictionary<string, Port> _ports;
    private readonly IReadOnlyDictionary<string, ParameterSetting> _changes;
    internal PreparedParameterEdit(object owner, long acceptanceGeneration, EditScopeIdentity identity, long draftRevision,
        IReadOnlyDictionary<string, Port> ports, IReadOnlyDictionary<string, ParameterSetting> changes)
    {
        _owner = owner; AcceptanceGeneration=acceptanceGeneration; Identity = identity; DraftRevision = draftRevision;
        _ports = ports; _changes = changes;
    }
    public EditScopeIdentity Identity { get; }
    public long DraftRevision { get; }
    public IReadOnlyDictionary<string, ParameterSetting> Changes => new System.Collections.ObjectModel.ReadOnlyDictionary<string, ParameterSetting>(
        _changes.ToDictionary(pair => pair.Key, pair => pair.Value is ConstantSetting constant
            ? (ParameterSetting)new ConstantSetting(ValueRules.Freeze(_ports[pair.Key], constant.Value)) : pair.Value, StringComparer.Ordinal));
    internal bool OwnedBy(object owner) => ReferenceEquals(_owner, owner);
    internal long AcceptanceGeneration{get;}
}
