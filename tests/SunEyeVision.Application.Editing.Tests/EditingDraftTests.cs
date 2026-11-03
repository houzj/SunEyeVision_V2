using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using Xunit;
using SunEyeVision.TestFixtures;

namespace SunEyeVision.Application.Editing.Tests;

public sealed class EditingDraftTests
{
    private static (DocumentSession Session, WorkflowDefinition Flow, GlobalVariable Variable) Fixture()
    {
        var path = Environment.GetEnvironmentVariable("SUNEYE_EDITING_TEST_PAYLOAD");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("必须提供本工作区正式封包目录。");
        var directory = new ReleasePayloadAuthority(path).PrepareRuntimeDirectory();
        var document = new SolutionDefinition("solution", "solution");
        var flow = new WorkflowDefinition("root", "root");
        // Fixture only: production creates membership through its session/repository.
        var workflows = (List<WorkflowDefinition>)typeof(SolutionDefinition).GetField("_workflows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(document)!;
        workflows.Add(flow);
        var variable = new GlobalVariable("var", new long[] { 1, 2 }, "Integer");
        typeof(SolutionDefinition).GetProperty(nameof(SolutionDefinition.GlobalVariables))!.SetValue(document, new[] { variable });
        return (new DocumentSession(document, directory), flow, variable);
    }

    private static NodeDefinition AddImageNode(DocumentSession session, WorkflowDefinition flow)
    {
        var directory = session.Directory;
        var factory = typeof(RuntimeDirectorySnapshot).GetMethod("CreateDefinition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var definition = (IToolDefinition)factory.Invoke(directory, new object[] { "ImageLoad" })!;
        var settings = (IToolSettingsFields)definition;
        var values = settings.ReadSettings(definition.CreateSettings()).CopyValues()
            .ToDictionary(item => item.Key, item => (ParameterSetting)new ConstantSetting(item.Value), StringComparer.Ordinal);
        if (definition is IToolResultDisplayProvider display)
            foreach (var item in display.CreateDisplaySettings().CopyValues()) values.Add(item.Key, new ConstantSetting(item.Value));
        var declarations = definition.Describe().Inputs.Concat(ResultDisplayFields.EditorSchema(definition).Inputs);
        var node = new NodeDefinition("image", "image", definition.ToolId, new ToolConfiguration(definition.ToolId, new FieldSettings(declarations, values)));
        var nodes = (List<NodeDefinition>)typeof(WorkflowDefinition).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flow)!;
        nodes.Add(node);
        return node;
    }

    [Fact]
    public void Node_candidate_is_private_frozen_and_rejects_stale_session()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var node = AddImageNode(session, fixture.Flow);
        var opening = node.Configuration;
        var host = new NodeConfigurationEditHost(session);
        var draft = host.BeginEdit("root", node.Id);
        Assert.True(host.OwnsNode("root", node));
        Assert.False(host.HasPendingChanges(draft));
        draft.ApplyFields(new Dictionary<string, ParameterSetting> { ["sequence.initial"] = new ConstantSetting(9L) });
        Assert.True(host.HasPendingChanges(draft));
        var proposal = DocumentEditProposal.Prepare(host, nodes: new[] { draft });
        Assert.Same(opening, node.Configuration);
        Assert.True(proposal.Matches(draft));
        draft.ApplyFields(new Dictionary<string, ParameterSetting> { ["sequence.initial"] = new ConstantSetting(10L) });
        Assert.False(proposal.Matches(draft));
        Assert.ThrowsAny<Exception>(() => draft.ApplyFields(new Dictionary<string, ParameterSetting> { ["not-declared"] = new ConstantSetting(1L) }));
        Assert.Same(opening, node.Configuration);
        session.Dispose();
        Assert.Throws<InvalidOperationException>(() => draft.ApplyFields(new Dictionary<string, ParameterSetting>()));
    }

    [Fact]
    public void Variable_candidate_tracks_in_place_arrays_and_preserves_formal_values()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var host = new NodeConfigurationEditHost(session);
        using var draft = host.BeginVariableEdit();
        Assert.False(host.HasPendingChanges(draft));
        var proposed = Assert.Single(draft.Variables);
        var proposal = DocumentEditProposal.Prepare(host, variables: draft);
        Assert.True(proposal.Matches(draft));
        ((long[])proposed.Value!)[0] = 7;
        Assert.False(proposal.Matches(draft));
        Assert.Equal(1L, ((long[])fixture.Variable.Value!)[0]);
        proposal.MarkConsumed(); // Exercise post-acceptance rebase in isolation; no document transaction is claimed.
        using var rebased = host.RebaseAcceptedDraft(draft, proposal, new Dictionary<GlobalVariable, GlobalVariable>());
        Assert.True(host.HasPendingChanges(rebased));
        var retained = DocumentEditProposal.Prepare(host, variables: rebased);
        Assert.Equal(7L, ((long[])retained.Variables!.ExplicitValues[proposed.Id]!)[0]);
        Assert.Throws<InvalidOperationException>(() => draft.SetValue(proposed.Id, double.NaN));
        rebased.SetValue(proposed.Id, new long[] { 3, 4 });
        Assert.Equal(1L, ((long[])fixture.Variable.Value!)[0]);
        rebased.Variables.Clear();
        var removed = DocumentEditProposal.Prepare(host, variables: rebased);
        Assert.Empty(removed.Variables!.Candidates);
        Assert.Empty(removed.Variables.ExplicitValues);
    }

    [Fact]
    public void Root_candidate_is_private_and_tracks_post_freeze_edits()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var host = new NodeConfigurationEditHost(session);
        var draft = host.BeginRootEdit("root");
        Assert.True(host.OwnsRoot(draft));
        Assert.False(host.HasPendingChanges(draft));
        var opening = fixture.Flow.RootConfiguration;
        var proposal = DocumentEditProposal.Prepare(host, roots: new[] { draft });
        Assert.True(proposal.Matches(draft));
        Assert.False(proposal.Matches(host.BeginRootEdit("root")));
        draft.Configuration = draft.Configuration with { RunParameters = draft.Configuration.RunParameters };
        Assert.False(proposal.Matches(draft));
        Assert.Same(opening, fixture.Flow.RootConfiguration);
    }

    [Fact]
    public void Variable_candidate_detects_nested_producer_metadata_changed_in_place()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var host = new NodeConfigurationEditHost(session);
        using var draft = host.BeginVariableEdit();
        var variable = Assert.Single(draft.Variables);
        variable.ProducerBinding = new ProducerBinding { WorkflowName = "before" };
        var proposal = DocumentEditProposal.Prepare(host, variables: draft);
        Assert.True(proposal.Matches(draft));
        variable.ProducerBinding.WorkflowName = "after";
        Assert.False(proposal.Matches(draft));
        Assert.Null(fixture.Variable.ProducerBinding);
    }

    [Fact]
    public void Same_id_replacement_remains_new_and_never_acquires_old_actual_identity()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var host = new NodeConfigurationEditHost(session);
        using var draft = host.BeginVariableEdit();
        draft.SetValue(fixture.Variable.Id, new long[] { 9, 10 });
        draft.Variables.Clear();
        var replacement = new GlobalVariable("replacement", 2L, "Integer") { Id = fixture.Variable.Id };
        draft.Variables.Add(replacement);
        var proposal = DocumentEditProposal.Prepare(host, variables: draft);
        var origin = Assert.Single(proposal.Variables!.Origins);
        Assert.Same(replacement, origin.DraftInstance);
        Assert.Null(origin.Actual);
        Assert.Empty(proposal.Variables.ExplicitValues);
        Assert.Same(fixture.Variable, Assert.Single(proposal.Variables.Opening.Entries).Actual);
    }

    [Fact]
    public void New_variable_rebase_requires_the_accepted_actual_instance()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var host = new NodeConfigurationEditHost(session);
        using var draft = host.BeginVariableEdit();
        var added = new GlobalVariable("added", 3L, "Integer");
        draft.Variables.Add(added);
        var proposal = DocumentEditProposal.Prepare(host, variables: draft);
        added.Name = "edited after prepare";
        // Fixture simulates only the accepted membership used by the rebase algorithm.
        var actual = new GlobalVariable("added", 3L, "Integer") { Id = added.Id };
        typeof(SolutionDefinition).GetProperty(nameof(SolutionDefinition.GlobalVariables))!
            .SetValue(session.Document, new[] { fixture.Variable, actual });
        proposal.MarkConsumed();
        Assert.Throws<InvalidOperationException>(() => host.RebaseAcceptedDraft(draft, proposal,
            new Dictionary<GlobalVariable, GlobalVariable> { [added] = fixture.Variable }));
        using var rebased = host.RebaseAcceptedDraft(draft, proposal,
            new Dictionary<GlobalVariable, GlobalVariable> { [added] = actual });
        var next = DocumentEditProposal.Prepare(host, variables: rebased);
        var origin = next.Variables!.Origins.Single(item => item.CandidateId == added.Id);
        Assert.Same(actual, origin.Actual);
        Assert.Equal("edited after prepare", next.Variables.Candidates.Single(item => item.Id == added.Id).Name);
    }

    [Fact]
    public void Combined_proposals_reject_duplicate_scope_and_consume_only_frozen_proposals()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        var host = new NodeConfigurationEditHost(session);
        var root = host.BeginRootEdit("root");
        using var variables = host.BeginVariableEdit();
        var stamp = new object();
        var first = DocumentEditProposal.Prepare(host, roots: new[] { root }, token: stamp);
        var second = DocumentEditProposal.Prepare(host, variables: variables);
        Assert.Throws<InvalidOperationException>(() => DocumentEditProposal.Combine(session, new[] { first, first }));
        var combined = DocumentEditProposal.Combine(session, new[] { first, second });
        combined.EnsureCanConsume();
        combined.MarkConsumed(); // Simulates only the accepted-proposal acknowledgement, not a document commit.
        Assert.True(first.IsConsumed); Assert.True(second.IsConsumed);
        Assert.Same(stamp, first.Token);
        Assert.True(first.Matches(root)); Assert.True(second.Matches(variables));
        Assert.Throws<InvalidOperationException>(() => combined.EnsureCanConsume());
    }

    [Fact]
    public void Participants_prepare_one_batch_without_publishing_and_close_cancels()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        using var editors = new DocumentEditSession(session);
        var node = AddImageNode(session, fixture.Flow);
        var opening = node.Configuration;
        var nodeDraft = editors.Host.BeginEdit("root", node.Id);
        using var variableDraft = editors.Host.BeginVariableEdit();
        nodeDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["sequence.initial"] = new ConstantSetting(9L) });
        var one = new Participant("node", host => DocumentEditProposal.Prepare(host, nodes: new[] { nodeDraft }));
        var two = new Participant("variables", host => DocumentEditProposal.Prepare(host, variables: variableDraft));
        one.OnCancel = () => editors.Unregister(one.ScopeId);
        editors.Register(one); editors.Register(two);
        var prepared = editors.Prepare();
        Assert.Equal(2, prepared.Parts.Count);
        Assert.True(prepared.Combined.Matches(nodeDraft));
        Assert.True(prepared.Combined.Matches(variableDraft));
        Assert.Same(opening, node.Configuration);
        Assert.Throws<InvalidOperationException>(() => editors.Register(new Participant("node", _ => throw new Exception())));
        editors.Dispose();
        Assert.True(one.Cancelled && two.Cancelled);
        Assert.Null(editors.LastReleaseError);
        Assert.Throws<InvalidOperationException>(() => editors.Prepare());
    }

    private sealed class Participant(string key, Func<NodeConfigurationEditHost, DocumentEditProposal> prepare) : IDocumentEditParticipant
    {
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => key;
        public bool HasPendingChanges => true;
        public bool IsValid => !Cancelled && (Check?.Invoke() ?? true);
        public bool Cancelled { get; private set; }
        public Action? OnCancel { get; set; }
        public Func<bool>? Check { get; set; }
        public Action<DocumentEditProposal, DocumentEditReceipt>? OnAcknowledge { get; set; }
        public void Invalidate() => Cancelled = true;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host) => prepare(host);
        public void Acknowledge(DocumentEditProposal prepared, DocumentEditReceipt receipt) => OnAcknowledge?.Invoke(prepared, receipt);
        public void Cancel() { Cancelled = true; OnCancel?.Invoke(); }
    }

    [Fact]
    public void Apply_publishes_real_variable_value_and_acknowledges_without_reentrant_edit()
    {
        // 显式中立测试声明，只验证会话事务，不冒充正式工具封包。
        var component = new ToolComponentRegistration(new ToolComponentMetadata("editing-probe", "ResultWorkspaceProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(WorkspaceProbeDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot)
            .GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { new[] { component } })!;
        var document = new SolutionDefinition("editing-test", "editing-test");
        var workflows = (List<WorkflowDefinition>)typeof(SolutionDefinition).GetField("_workflows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(document)!;
        workflows.Add(new WorkflowDefinition("root", "root"));
        var variable = new GlobalVariable("var", new long[] { 1, 2 }, "Integer");
        typeof(SolutionDefinition).GetProperty(nameof(SolutionDefinition.GlobalVariables))!.SetValue(document, new[] { variable });
        using var session = new DocumentSession(document, directory);
        var edits = session.Editing;
        var draft = edits.Host.BeginVariableEdit();
        draft.SetValue(variable.Id, new long[] { 3, 4 });
        var participant = new Participant("variables", host => DocumentEditProposal.Prepare(host, variables: draft));
        var acknowledged = false;
        participant.OnAcknowledge = (proposal, receipt) =>
        {
            Assert.Throws<InvalidOperationException>(() => edits.Apply());
            Assert.Same(session, receipt.Session);
            draft = edits.Host.RebaseAcceptedDraft(draft, proposal, receipt.AcceptedVariables);
            acknowledged = true;
        };
        edits.Register(participant);
        var result = edits.Apply(participant.ScopeId);
        Assert.True(acknowledged);
        Assert.Equal(new long[] { 3, 4 }, Assert.IsType<long[]>(variable.Value));
        Assert.Null(result.History);
        draft.Dispose();
    }

    [Fact]
    public void Preparing_a_scope_that_unregisters_is_rejected_without_document_changes()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        using var editors = new DocumentEditSession(session);
        var node = AddImageNode(session, fixture.Flow);
        var original = node.Configuration;
        var draft = editors.Host.BeginEdit("root", node.Id);
        Participant? participant = null;
        participant = new Participant("node", host =>
        {
            var proposal = DocumentEditProposal.Prepare(host, nodes: new[] { draft });
            editors.Unregister(participant!.ScopeId);
            return proposal;
        });
        editors.Register(participant);
        Assert.Throws<InvalidOperationException>(() => editors.Prepare());
        Assert.Same(original, node.Configuration);
    }

    [Fact]
    public void Closing_continues_after_one_participant_cancel_fails()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        using var editors = new DocumentEditSession(session);
        var first = new Participant("one", _ => throw new Exception()) { OnCancel = () => throw new InvalidOperationException("cancel failed") };
        var second = new Participant("two", _ => throw new Exception());
        editors.Register(first); editors.Register(second);
        editors.Dispose();
        Assert.True(first.Cancelled && second.Cancelled);
        Assert.IsType<AggregateException>(editors.LastReleaseError);
        Assert.Equal(0, editors.RegisteredScopeCount);
    }

    [Fact]
    public void Invalid_scope_cleanup_rejects_reentrant_registration()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        using var editors = new DocumentEditSession(session);
        var rejected = false;
        var stale = new Participant("stale", _ => throw new Exception());
        stale.OnCancel = () =>
        {
            try { editors.Register(new Participant("nested", _ => throw new Exception())); }
            catch (InvalidOperationException) { rejected = true; }
        };
        editors.Register(stale); stale.Invalidate();
        editors.Register(new Participant("replacement", _ => throw new Exception()));
        Assert.True(rejected);
        Assert.Equal(1, editors.RegisteredScopeCount);
    }

    [Fact]
    public void Invalid_scope_can_unregister_during_validity_check()
    {
        var fixture = Fixture(); using var session = fixture.Session;
        using var editors = new DocumentEditSession(session);
        var stale = new Participant("stale", _ => throw new Exception());
        editors.Register(stale);
        stale.Check = () => { editors.Unregister(stale.ScopeId); return false; };
        editors.Register(new Participant("replacement", _ => throw new Exception()));
        Assert.Equal(1, editors.RegisteredScopeCount);
        Assert.True(stale.Cancelled);
    }
}
