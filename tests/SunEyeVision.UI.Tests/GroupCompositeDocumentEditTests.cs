using System;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GroupCompositeDocumentEditTests
{
    private sealed class GroupEditor : IDocumentEditParticipant
    {
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => "group:" + ScopeId;
        public NodeConfigurationEditDraft[] Drafts { get; }
        public int Accepted, Cancelled;
        public bool HasPendingChanges => Cancelled == 0;
        public GroupEditor(NodeConfigurationEditDraft[] drafts) => Drafts = drafts;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host) => DocumentEditProposal.ForNodes(host, Drafts, this);
        public void Acknowledge(DocumentEditProposal proposal, BindingConfigurationPublication publication)
        { Assert.Same(this, proposal.Token); Assert.True(proposal.Matches(Drafts)); Accepted++; }
        public void Cancel() => Cancelled++;
    }
    private static (Solution Model, DocumentEditSession Session, GroupEditor Editor) Fixture()
    {
        var parent = new SunEyeVision.Workflow.Workflow("parent", "parent");
        var child = new SunEyeVision.Workflow.Workflow("child", "child") { IsSubroutine = true };
        parent.Nodes.Add(new("same", "Group", "Group", "Subroutine")
            { SubroutineId = child.Id, Parameters = new SubroutineParameters { SubroutineId = child.Id } });
        child.Nodes.Add(new("same", "parse", "parse", "ProtocolParse") { Parameters = new ProtocolParseParameters() });
        child.Nodes.Add(new("second", "second", "second", "ProtocolParse") { Parameters = new ProtocolParseParameters() });
        var model = new Solution(); model.Workflows.Add(parent); model.Workflows.Add(child);
        var session = (DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),
            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { model }, null)!;
        var editor = new GroupEditor(new[] { session.Host.BeginEdit("parent", "same"),
            session.Host.BeginEdit("child", "same"), session.Host.BeginEdit("child", "second") });
        ((SubroutineParameters)editor.Drafts[0].Node.Parameters).LoopConfig.LoopInterval = 17;
        ((ProtocolParseParameters)editor.Drafts[1].Node.Parameters).Separator = ";";
        ((ProtocolParseParameters)editor.Drafts[2].Node.Parameters).Separator = "|";
        session.Register(editor);
        return (model, session, editor);
    }
    [Fact]
    public void Production_Group_context_owns_related_private_candidates_and_rebinds_all_after_atomic_save()
    {
        var f = Fixture(); f.Session.Unregister(f.Editor.ScopeId);
        var formal = f.Model.Workflows[0].Nodes[0]; var internalFlow = f.Model.Workflows[1];
        var rebinds = 0;
        using var context = new ProductionEditorContext(f.Session.Host, "parent", formal, () => true,
            _ => rebinds++, documentEdits: f.Session,
            allowRelated: (flow, node) => MainWindowViewModel.IsGroupInternalNode(f.Model, internalFlow, "parent", flow, node));
        var first = context.GetRelatedDraft("child", internalFlow.Nodes[0], p => EditorDraftAdapter.ForProduction(p).Validate());
        var second = context.GetRelatedDraft("child", internalFlow.Nodes[1], p => EditorDraftAdapter.ForProduction(p).Validate());
        Assert.Same(first, context.GetRelatedDraft("child", internalFlow.Nodes[0], _ => throw new Exception("must reuse registered validator")));
        ((SubroutineParameters)context.DraftNode.Parameters).LoopConfig.LoopInterval = 17;
        ((ProtocolParseParameters)first.Parameters).Separator = ";";
        ((ProtocolParseParameters)second.Parameters).Separator = "|";
        Assert.True(f.Session.HasPendingChanges); Original(f.Model);
        f.Session.ValidateAll(); Original(f.Model);
        Assert.True(context.TryCommitEditor(out var error), error);
        Assert.Equal(1, rebinds); Assert.False(f.Session.HasPendingChanges);
        var next = context.GetRelatedDraft("child", internalFlow.Nodes[0], p => EditorDraftAdapter.ForProduction(p).Validate());
        Assert.NotSame(first, next); Assert.NotSame(internalFlow.Nodes[0].Parameters, next.Parameters);
        Assert.Equal(";", ((ProtocolParseParameters)next.Parameters).Separator);
        ((ProtocolParseParameters)next.Parameters).Separator = "discard";
        context.Discard(); Assert.Equal(";", ((ProtocolParseParameters)internalFlow.Nodes[0].Parameters).Separator);
        Assert.Equal(0, f.Session.RegisteredScopeCount);
        f.Session.Host.Undo(); Original(f.Model);
    }
    [Fact]
    public void Production_Group_context_rejects_outside_identity_and_replaced_internal_instance()
    {
        var f = Fixture(); f.Session.Unregister(f.Editor.ScopeId);
        var flow = f.Model.Workflows[1]; var group = f.Model.Workflows[0].Nodes[0];
        using var context = new ProductionEditorContext(f.Session.Host, "parent", group, () => true, _ => { },
            documentEdits: f.Session,
            allowRelated: (id, node) => MainWindowViewModel.IsGroupInternalNode(f.Model, flow, "parent", id, node));
        Assert.Throws<InvalidOperationException>(() => context.GetRelatedDraft("parent", group, _ => { }));
        var imposter = new WorkflowNodeBase("same", "fake", "fake", "ProtocolParse");
        Assert.Throws<InvalidOperationException>(() => context.GetRelatedDraft("child", imposter, _ => { }));
        var old = flow.Nodes[0]; var candidate = context.GetRelatedDraft("child", old, _ => { });
        ((SubroutineParameters)context.DraftNode.Parameters).LoopConfig.LoopInterval = 17;
        flow.Nodes[0] = imposter;
        Assert.False(context.IsValid); Assert.False(context.TryCommitEditor(out _));
        Assert.False(context.IsActive); Assert.Equal(0, f.Session.RegisteredScopeCount);
        Assert.Equal(0, ((SubroutineParameters)group.Parameters).LoopConfig.LoopInterval);
    }
    [Fact]
    public void Related_editor_validation_failure_preserves_every_candidate_without_publishing()
    {
        var f = Fixture(); f.Session.Unregister(f.Editor.ScopeId);
        var flow = f.Model.Workflows[1];
        using var context = new ProductionEditorContext(f.Session.Host, "parent", f.Model.Workflows[0].Nodes[0], () => true, _ => { },
            documentEdits: f.Session, allowRelated: (id, node) => MainWindowViewModel.IsGroupInternalNode(f.Model, flow, "parent", id, node));
        var candidate = context.GetRelatedDraft("child", flow.Nodes[0], _ => throw new InvalidOperationException("invalid internal editor"));
        ((SubroutineParameters)context.DraftNode.Parameters).LoopConfig.LoopInterval = 17;
        ((ProtocolParseParameters)candidate.Parameters).Separator = ";";
        Assert.False(context.TryCommitEditor(out var error)); Assert.Contains("invalid internal editor", error);
        Original(f.Model); Assert.True(context.IsActive); Assert.True(context.HasPendingChanges);
        Assert.Same(candidate, context.GetRelatedDraft("child", flow.Nodes[0], _ => { }));
        Assert.False(f.Session.Host.CanUndo);
    }
    [Fact]
    public void Internal_node_resolution_uses_actual_Group_reachability_and_object_identity()
    {
        var f = Fixture(); var child = f.Model.Workflows[1];
        var nested = new SunEyeVision.Workflow.Workflow("nested", "nested") { IsSubroutine = true };
        nested.Nodes.Add(new("same", "nested item", "nested item", "ProtocolParse"));
        child.Nodes[0].SubroutineId = nested.Id; child.Nodes[0].Parameters = new SubroutineParameters { SubroutineId = nested.Id }; f.Model.Workflows.Add(nested);
        Assert.True(MainWindowViewModel.IsGroupInternalNode(f.Model, child, "parent", nested.Id, nested.Nodes[0]));
        Assert.False(MainWindowViewModel.IsGroupInternalNode(f.Model, child, "parent", "child", nested.Nodes[0]));
        Assert.False(MainWindowViewModel.IsGroupInternalNode(f.Model, child, "parent", "parent", f.Model.Workflows[0].Nodes[0]));
        nested.Nodes[0].SubroutineId = "parent"; nested.Nodes[0].Parameters = new SubroutineParameters { SubroutineId = "parent" };
        Assert.False(MainWindowViewModel.IsGroupInternalNode(f.Model, child, "parent", "parent", f.Model.Workflows[0].Nodes[0]));
        nested.Nodes[0].SubroutineId = child.Id;
        Assert.False(MainWindowViewModel.IsGroupInternalNode(f.Model, child, "parent", "missing", nested.Nodes[0]));
    }

    private sealed class PreparationCallback(Action callback) : IDocumentEditParticipant
    {
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => "callback:" + ScopeId;
        public bool HasPendingChanges => false;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
        { callback(); return DocumentEditProposal.ForVariables(host, host.BeginVariableEdit()); }
        public void Acknowledge(DocumentEditProposal proposal, BindingConfigurationPublication publication) { }
        public void Cancel() { }
    }
    [Fact]
    public void Group_acknowledgement_preserves_internal_edits_made_after_the_combined_proposal_was_frozen()
    {
        var f = Fixture(); f.Session.Unregister(f.Editor.ScopeId); var flow = f.Model.Workflows[1];
        var rebinds = 0;
        using var context = new ProductionEditorContext(f.Session.Host, "parent", f.Model.Workflows[0].Nodes[0], () => true, _ => rebinds++,
            documentEdits: f.Session, allowRelated: (id, node) => MainWindowViewModel.IsGroupInternalNode(f.Model, flow, "parent", id, node));
        var candidate = context.GetRelatedDraft("child", flow.Nodes[0], p => EditorDraftAdapter.ForProduction(p).Validate());
        ((ProtocolParseParameters)candidate.Parameters).Separator = "saved";
        var callback = new PreparationCallback(() => ((ProtocolParseParameters)candidate.Parameters).Separator = "newer");
        f.Session.Register(callback);
        f.Session.AcceptAll();
        Assert.Equal("saved", ((ProtocolParseParameters)flow.Nodes[0].Parameters).Separator);
        Assert.Same(candidate, context.GetRelatedDraft("child", flow.Nodes[0], _ => { }));
        Assert.Equal("newer", ((ProtocolParseParameters)candidate.Parameters).Separator);
        Assert.True(context.HasPendingChanges); Assert.Equal(0, rebinds);
        Assert.True(context.TryCommitEditor(out var error), error);
        Assert.Equal("newer", ((ProtocolParseParameters)flow.Nodes[0].Parameters).Separator);
        Assert.False(context.HasPendingChanges); Assert.Equal(1, rebinds);
    }

    private static void Original(Solution model)
    {
        Assert.Equal(0, ((SubroutineParameters)model.Workflows[0].Nodes[0].Parameters).LoopConfig.LoopInterval);
        Assert.All(model.Workflows[1].Nodes, n => Assert.Equal(",", ((ProtocolParseParameters)n.Parameters).Separator));
    }
    [Fact]
    public void One_Group_participant_accepts_parent_and_related_internal_candidates_in_one_history_entry()
    {
        var f = Fixture(); var before = f.Session.Host.Capture();
        f.Session.ValidateAll(); Original(f.Model); Assert.Same(before, f.Session.Host.Capture());
        var publication = f.Session.Apply(f.Editor.ScopeId)!;
        Assert.Equal(before.Version + 1, publication.Snapshot.Version); Assert.Equal(1, f.Editor.Accepted);
        Assert.Equal(17, ((SubroutineParameters)f.Model.Workflows[0].Nodes[0].Parameters).LoopConfig.LoopInterval);
        Assert.Equal(";", ((ProtocolParseParameters)f.Model.Workflows[1].Nodes[0].Parameters).Separator);
        Assert.Equal("|", ((ProtocolParseParameters)f.Model.Workflows[1].Nodes[1].Parameters).Separator);
        f.Session.Host.Undo(); Original(f.Model);
        f.Session.Host.Redo();
        Assert.Equal(17, ((SubroutineParameters)f.Model.Workflows[0].Nodes[0].Parameters).LoopConfig.LoopInterval);
        Assert.Equal("|", ((ProtocolParseParameters)f.Model.Workflows[1].Nodes[1].Parameters).Separator);
    }
    [Fact]
    public void Partial_acceptance_failure_restores_all_formal_nodes_and_keeps_candidates_retryable()
    {
        var f = Fixture(); var before = f.Session.Host.Capture();
        var injection = typeof(NodeConfigurationEditHost).GetField("_acceptanceFailureAfterRawMutation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        injection.SetValue(f.Session.Host, 1);
        Assert.ThrowsAny<Exception>(() => f.Session.AcceptAll());
        Original(f.Model); Assert.Same(before, f.Session.Host.Capture()); Assert.Equal(0, f.Editor.Accepted);
        Assert.False(f.Session.Host.CanUndo);
        injection.SetValue(f.Session.Host, -1);
        f.Session.AcceptAll(); Assert.Equal(1, f.Editor.Accepted);
        Assert.Equal(";", ((ProtocolParseParameters)f.Model.Workflows[1].Nodes[0].Parameters).Separator);
    }
    [Fact]
    public void Frozen_composite_matches_by_scoped_identity_and_rejects_duplicates_and_later_changes()
    {
        var f = Fixture(); var proposal = DocumentEditProposal.ForNodes(f.Session.Host, f.Editor.Drafts);
        Assert.True(proposal.Matches(f.Editor.Drafts.Reverse().ToArray()));
        Assert.False(proposal.Matches(new[] { f.Editor.Drafts[0], f.Editor.Drafts[0], f.Editor.Drafts[0] }));
        Assert.Throws<ArgumentException>(() => DocumentEditProposal.ForNodes(f.Session.Host, new[] { f.Editor.Drafts[0], f.Editor.Drafts[0] }));
        Assert.Throws<ArgumentException>(() => DocumentEditProposal.ForNodes(f.Session.Host, Array.Empty<NodeConfigurationEditDraft>()));
        ((ProtocolParseParameters)f.Editor.Drafts[1].Node.Parameters).Separator = "after-freeze";
        Assert.False(proposal.Matches(f.Editor.Drafts)); Original(f.Model);
    }
    [Fact]
    public void Overlapping_participants_reject_before_publication_and_close_cancels_whole_Group()
    {
        var f = Fixture();
        var overlap = new GroupEditor(new[] { f.Session.Host.BeginEdit("child", "same") });
        f.Session.Register(overlap);
        Assert.Throws<InvalidOperationException>(() => f.Session.AcceptAll()); Original(f.Model);
        Assert.Equal(0, f.Editor.Accepted); Assert.Equal(0, overlap.Accepted);
        f.Session.Close(); Assert.Equal(1, f.Editor.Cancelled); Assert.Equal(1, overlap.Cancelled);
        Original(f.Model); Assert.Equal(0, f.Session.RegisteredScopeCount);
    }
}
