using SunEyeVision.UI.Services;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class LocalImageSequenceTests
{
    private static LocalImageSequenceEntry Entry(string id, int index) => new(id, "assets/" + id, "C:/resolved/" + id, index);
    private static LocalImageSourcePlan Plan(string node, params LocalImageSequenceEntry[] entries) => new(node, entries, null, true, false);

    [Fact]
    public void UnselectedCanStartAtFirstButInvalidExplicitSelectionCannotReplaceAnActiveSession()
    {
        var service = new LocalImageSequenceService();
        var plan = Plan("source", Entry("a", 0));
        var generation = service.Begin("w", new[] { plan });
        Assert.Equal("a", service.GetInputs("w", generation)[0].Entry!.EntryId);
        Assert.Throws<InvalidOperationException>(() => service.Begin("w", new[] { plan with { SelectedEntryId = "removed" } }));
        Assert.Equal("a", service.GetInputs("w", generation)[0].Entry!.EntryId);
        var empty = service.Begin("empty", new[] { Plan("source") });
        Assert.Null(service.GetInputs("empty", empty)[0].Entry);
        Assert.Throws<InvalidOperationException>(() => service.Begin("empty", new[] { Plan("source") with { SelectedEntryId = "missing" } }));
    }

    [Fact]
    public void SequencesFreezeEntriesAndAdvanceAllSourcesOfOnlyTheCompletedWorkflow()
    {
        var service = new LocalImageSequenceService();
        var entries = new[] { Entry("a", 0), Entry("b", 1) };
        var generation = service.Begin("w1", new[] { Plan("source1", entries), Plan("source2", Entry("c", 3), Entry("d", 7)) });
        var other = service.Begin("w2", new[] { Plan("source1", entries) });
        entries[0] = Entry("edited", 9);
        var inputs = service.GetInputs("w1", generation);
        Assert.Equal("a", inputs[0].Entry!.EntryId);
        Assert.Equal(LocalImageCompletion.Advanced, service.Complete("w1", generation, inputs, true));
        Assert.Equal(new[] { "b", "d" }, service.GetInputs("w1", generation).Select(input => input.Entry!.EntryId));
        Assert.Equal("a", service.GetInputs("w2", other)[0].Entry!.EntryId);
        Assert.Equal(LocalImageCompletion.Stale, service.Complete("w1", generation, inputs, true));
    }

    [Fact]
    public void FailureAndManualExecutionRetainCurrentEntryAndStaleGenerationsAreRejected()
    {
        var service = new LocalImageSequenceService();
        var plan = Plan("source", Entry("a", 0), Entry("b", 1)) with { SelectedEntryId = "b" };
        var old = service.Begin("w", new[] { plan });
        var input = service.GetInputs("w", old);
        Assert.Equal(LocalImageCompletion.Retained, service.Complete("w", old, input, false));
        input = service.GetInputs("w", old);
        Assert.Equal(LocalImageCompletion.Retained, service.Complete("w", old, input, true, advance: false));
        var current = service.Begin("w", new[] { plan });
        Assert.Equal(LocalImageCompletion.Stale, service.Complete("w", old, input, true));
        service.End("w", old);
        Assert.Equal("b", service.GetInputs("w", current)[0].Entry!.EntryId);
    }

    [Fact]
    public void LastEntryUsesFrozenFilteredSequenceAndStopsBeforeAnySourceAdvances()
    {
        var service = new LocalImageSequenceService();
        var plan = Plan("one", Entry("selected-last", 4)) with { StopAtLast = true };
        var generation = service.Begin("w", new[] { plan, Plan("two", Entry("a", 0), Entry("b", 1)) });
        var input = service.GetInputs("w", generation);
        Assert.Equal(LocalImageCompletion.StopAtEnd, service.Complete("w", generation, input, true));
        Assert.Equal(input.Select(value => value.Entry), service.GetInputs("w", generation).Select(value => value.Entry));
        Assert.Equal(LocalImageCompletion.Stale, service.Complete("w", generation, input, true));
    }

    [Fact]
    public void WrongSourceIdentityCannotAdvanceAndWrapUsesEntryIdentityRatherThanPath()
    {
        var service = new LocalImageSequenceService();
        var a = Entry("a", 2); var b = a with { EntryId = "b", OriginalIndex = 5 };
        var generation = service.Begin("w", new[] { Plan("source", a, b) with { SelectedEntryId = "b" } });
        var inputs = service.GetInputs("w", generation);
        Assert.Equal(LocalImageCompletion.Stale, service.Complete("w", generation,
            new[] { inputs[0] with { SourceNodeId = "other" } }, true));
        Assert.Equal(LocalImageCompletion.Advanced, service.Complete("w", generation, inputs, true));
        Assert.Equal("a", service.GetInputs("w", generation)[0].Entry!.EntryId);
    }
}
