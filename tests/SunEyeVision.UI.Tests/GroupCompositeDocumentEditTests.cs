using System;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Workflow;
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
