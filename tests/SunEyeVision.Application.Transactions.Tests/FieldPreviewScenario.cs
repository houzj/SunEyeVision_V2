using SunEyeVision.TestFixtures;
using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

internal static class FieldPreviewScenario
{
    internal static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reject(Action operation)
        {
            try { operation(); } catch (Exception e) when (e is InvalidOperationException or ArgumentException) { return; }
            throw new Exception("Invalid field operation was accepted.");
        }
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("field-preview", "FieldPreview",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(FieldPreviewDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { registration } })!;
        using var manager = new DocumentSessionManager(directory);
        var session = manager.CreateNew(); var flow = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(flow.Id);
        var first = session.CreateNode(graph, "FieldPreview", new CanvasPoint(0, 0));
        var second = session.CreateNode(graph, "FieldPreview", new CanvasPoint(0, 100));
        graph.AddConnection(new ConnectionDefinition("preview-edge", first.Id, second.Id, "count", "input.image"));
        session.CommitGraphEdit(graph, "Field preview graph");
        Check(session.CheckWorkflowReadiness(flow.Id).Any(issue => issue.Node.NodeId == first.Id), "Unbound source must remain unready.");
        var host = session.Editing.Host; var draft = host.BeginEdit(flow.Id, second.Id);
        var original = draft.Configuration; var formal = second.Configuration;
        var frozen = DocumentEditProposal.Prepare(host, nodes: new[] { draft });
        var history = session.HistoryEntries.Count; var revision = session.ChangeRevision;
        var reference = new ReferenceSetting(new OutputReference(first.Id, "count", DataType.Integer, scopeId: flow.Id));
        var changes = new Dictionary<string, ParameterSetting> { ["threshold"] = reference, ["limit"] = reference };
        host.ValidateFieldProposals(draft, changes, new[] { "limit", "threshold" });
        Check(ReferenceEquals(original, draft.Configuration) && ReferenceEquals(formal, second.Configuration) && frozen.Matches(draft) &&
            history == session.HistoryEntries.Count && revision == session.ChangeRevision, "Preview must not mutate the draft, formal state or history.");
        Reject(() => host.ValidateFieldProposals(draft, changes, new[] { "threshold", "threshold" }));
        Reject(() => draft.ApplyFields(changes, new[] { "threshold" }));
        Check(frozen.Matches(draft), "Rejected field order must not invalidate a prepared draft.");
        draft.ApplyFields(changes, new[] { "limit", "threshold" });
        // Debug acceptance now runs through DocumentDebugSession/DocumentDebugInvocation.
        // Atomic candidate preparation, stale-reader rejection and cancellation are covered by
        // Execution.Tests/DebugAcceptanceRaceTests and the real RuntimeRunChainProbe.
        session.CommitEdit(DocumentEditProposal.Prepare(host, nodes: new[] { draft }));
        Check(second.Configuration.ParameterBindingOrder.SequenceEqual(new[] { "limit", "threshold" }), "Accepted parameter order was lost.");
        Check(session.Undo() && second.Configuration.ParameterBindingOrder.Count == 0 && session.Redo() &&
            second.Configuration.ParameterBindingOrder.SequenceEqual(new[] { "limit", "threshold" }), "Order must use the same undo history.");
        var unreadyDraft = host.BeginEdit(flow.Id, first.Id);
        unreadyDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(8L) });
        session.CommitEdit(DocumentEditProposal.Prepare(host, nodes: new[] { unreadyDraft }));
        Check(session.CheckWorkflowReadiness(flow.Id).Any(issue => issue.Node.NodeId == first.Id), "Editing cannot bypass execution readiness.");
        var presentation = session.GetNodeResultPresentation(flow.Id, second);
        Check(presentation.Schema.Outputs.Any(port => port.Id == "count") && presentation.Projection is null, "Declaration fallback must use the admitted schema.");
        Reject(() => session.GetNodeResultPresentation(flow.Id, first, host.BeginEdit(flow.Id, second.Id)));
        var stale = host.BeginEdit(flow.Id, second.Id);
        var remove = session.PrepareGraphEdit(flow.Id); remove.DeleteNodesAndConnections(new[] { second }, Array.Empty<ConnectionDefinition>());
        session.CommitGraphEdit(remove, "Remove preview target");
        Reject(() => host.ValidateFieldProposals(stale, new Dictionary<string, ParameterSetting>()));
        Console.WriteLine("PASS field preview isolation, accepted order, history, editing/readiness separation and actual-node presentation");
    }
}

public sealed class FieldPreviewDefinition : IToolDefinition<TransactionSettings, WorkspaceProbeOutput>, IToolSettingsFields,
    IToolCatalogProvider, IToolParameterBindingNames, IToolExecutionInputs, IToolManualReferenceSources
{
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = new[] { new ManualReferenceSource("count") };
    public string ToolId => "FieldPreview";
    public ToolCatalogDescriptor Catalog { get; } = new("Field preview", "Session contract probe", "generic", "Tests", 0);
    public ToolSchema Describe() => new(new[] { new Port("input.image", DataType.Integer, Required: false, EditModes: FieldEditModes.Reference) },
        new[] { new Port("count", DataType.Integer) });
    public ToolSchema SettingsSchema { get; } = new(new[] {
        new Port("threshold", DataType.Integer, EditModes: FieldEditModes.Constant | FieldEditModes.Reference),
        new Port("limit", DataType.Integer, EditModes: FieldEditModes.Constant | FieldEditModes.Reference) }, Array.Empty<Port>());
    public IReadOnlyDictionary<string, string> ParameterBindingNames { get; } = new Dictionary<string, string> { ["threshold"] = "Threshold", ["limit"] = "Limit" };
    public IReadOnlyList<string> SelectExecutionInputs(FieldSettings fields) => new[] { "input.image" };
    public IReadOnlyList<Diagnostic> ValidateExecutionInputs(FieldSettings fields) => fields.Read("input.image") is ReferenceSetting
        ? Array.Empty<Diagnostic>() : new[] { new Diagnostic("input-missing", "Input is required to execute.", "input.image") };
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs, new Dictionary<string, object?> { ["threshold"] = ((TransactionSettings)settings).Threshold, ["limit"] = 2L });
    public object MaterializeSettings(InputValues values) => new TransactionSettings { Threshold = values.Get<long>("threshold") };
    public IReadOnlyList<Diagnostic> Validate(TransactionSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<TransactionSettings, WorkspaceProbeOutput> CreateSession() => throw new NotSupportedException("Declaration probe only.");
}
