using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class DocumentHistoryCoordinatorTests
{
    private sealed class State { public int A, B; }
    private sealed class Change(State state, bool second, int next) : IDocumentHistoryOperation
    {
        private int? _before;
        public bool FailApply, FailRollback;
        public Action? DuringApply;
        public int Disposed;
        public string Description => second ? "修改流程B" : "修改流程A";
        public IReadOnlyList<DocumentHistoryScope> Scopes => new[] { new DocumentHistoryScope(second ? "B" : "A", "root") };
        private int Value { get => second ? state.B : state.A; set { if (second) state.B = value; else state.A = value; } }
        public PreparedDocumentHistoryChange Prepare(DocumentHistoryDirection direction)
        {
            var current = Value;
            var previousBefore = _before;
            if (direction == DocumentHistoryDirection.Backward && _before is null) throw new InvalidOperationException("尚未执行。");
            var target = direction == DocumentHistoryDirection.Forward ? next : _before!.Value;
            return new(() =>
            {
                _before ??= current; Value = target; DuringApply?.Invoke();
                if (FailApply) throw new InvalidOperationException("故障注入：部分修改后失败");
            }, () =>
            {
                if (FailRollback) throw new InvalidOperationException("故障注入：回滚失败");
                Value = current; _before = previousBefore;
            });
        }
        public void Dispose() => Disposed++;
    }

    [Fact]
    public void Different_roots_share_one_cursor_and_new_edit_discards_all_redo_payloads()
    {
        using var history = new DocumentHistoryCoordinator(Guid.NewGuid()); var state = new State();
        var a = new Change(state, false, 3); var b = new Change(state, true, 7);
        var first = history.Execute(a); var second = history.Execute(b);
        Assert.True(history.Undo()); Assert.Equal((3, 0), (state.A, state.B));
        Assert.True(history.Undo()); Assert.Equal((0, 0), (state.A, state.B));
        Assert.True(history.Redo()); Assert.Equal((3, 0), (state.A, state.B));
        var third = history.Execute(new Change(state, false, 9));
        Assert.False(history.CanRedo); Assert.Equal(1, b.Disposed); Assert.Equal(0, a.Disposed);
        Assert.Equal(new long[] { 1, 3 }, history.Entries.Select(x => x.Sequence));
        Assert.NotEqual(first.Id, third.Id); Assert.NotEqual(second.Id, third.Id);
    }

    [Fact]
    public void Failed_new_edit_rolls_back_partial_model_mutation_and_keeps_redo_and_sequence()
    {
        using var history = new DocumentHistoryCoordinator(Guid.NewGuid()); var state = new State();
        history.Execute(new Change(state, false, 3)); history.Undo();
        var revision = history.Revision; var failed = new Change(state, true, 8) { FailApply = true };
        Assert.Throws<InvalidOperationException>(() => history.Execute(failed));
        Assert.Equal((0, 0), (state.A, state.B)); Assert.Equal(revision, history.Revision);
        Assert.True(history.CanRedo); Assert.Equal(0, history.Position); Assert.Equal(0, failed.Disposed);
        failed.FailApply = false; Assert.Equal(2, history.Execute(failed).Sequence);
    }

    [Fact]
    public void Failed_undo_keeps_cursor_and_model_then_can_retry_the_same_operation()
    {
        using var history = new DocumentHistoryCoordinator(Guid.NewGuid()); var state = new State();
        var change = new Change(state, false, 6); history.Execute(change); change.FailApply = true;
        var revision = history.Revision;
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Equal(6, state.A); Assert.Equal(1, history.Position); Assert.Equal(revision, history.Revision);
        change.FailApply = false; Assert.True(history.Undo()); Assert.Equal(0, state.A);
    }

    [Fact]
    public void Failed_rollback_faults_history_instead_of_claiming_atomic_success()
    {
        using var history = new DocumentHistoryCoordinator(Guid.NewGuid()); var state = new State();
        var change = new Change(state, false, 9) { FailApply = true, FailRollback = true };
        Assert.Throws<AggregateException>(() => history.Execute(change));
        Assert.NotNull(history.Fault); Assert.False(history.CanUndo); Assert.False(history.CanRedo);
        Assert.Throws<InvalidOperationException>(() => history.Execute(new Change(state, true, 1)));
        // Explicit close still needs to release retained history after a fault; tested separately.
    }

    [Fact]
    public void Navigation_uses_stable_entry_and_rejects_stale_session_or_discarded_entry()
    {
        using var history = new DocumentHistoryCoordinator(Guid.NewGuid()); var state = new State();
        var first = history.Execute(new Change(state, false, 3)); var second = history.Execute(new Change(state, true, 4));
        history.NavigateTo(history.SessionId, null); Assert.Equal((0, 0), (state.A, state.B));
        history.NavigateTo(history.SessionId, second.Id); Assert.Equal((3, 4), (state.A, state.B));
        Assert.Throws<InvalidOperationException>(() => history.NavigateTo(Guid.NewGuid(), first.Id));
        history.Undo(); history.Execute(new Change(state, true, 5));
        Assert.Throws<InvalidOperationException>(() => history.NavigateTo(history.SessionId, second.Id));
    }

    [Fact]
    public void Capacity_and_close_release_only_owned_operations_once()
    {
        var history = new DocumentHistoryCoordinator(Guid.NewGuid(), 1); var state = new State();
        var first = new Change(state, false, 1); var second = new Change(state, true, 2);
        history.Execute(first); history.Execute(second);
        Assert.Equal(1, first.Disposed); Assert.Equal(0, second.Disposed); Assert.Single(history.Entries);
        history.Dispose(); history.Dispose(); Assert.Equal(1, second.Disposed);
        Assert.Throws<ObjectDisposedException>(() => history.Undo());
    }

    [Fact]
    public void Reentry_is_rejected_but_observer_failure_does_not_reverse_committed_edit()
    {
        using var history = new DocumentHistoryCoordinator(Guid.NewGuid()); var state = new State();
        var change = new Change(state, false, 2) { DuringApply = () => Assert.Throws<InvalidOperationException>(() => history.Undo()) };
        history.Changed += (_, _) => { Assert.True(history.CanUndo); Assert.Throws<InvalidOperationException>(() => history.Undo()); throw new Exception("观察者失败"); };
        history.Execute(change); Assert.Equal(2, state.A); Assert.Equal(1, history.Position); Assert.NotNull(history.LastObserverError);
    }
}
