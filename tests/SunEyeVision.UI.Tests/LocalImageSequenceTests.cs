using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class LocalImageSequenceTests
{
    private static LocalImageSequenceEntry Entry(string id, int index) => new(id, "assets/" + id, "C:/resolved/" + id, index);
    private static LocalImageSourcePlan Plan(string node, params LocalImageSequenceEntry[] entries) => new(node, entries, entries.FirstOrDefault()?.EntryId, true, false);

    [Fact]
    public void Unselected_first_read_uses_fallback_without_skipping_and_retains_stop_and_cancellation_rules()
    {
        foreach(var direction in new[]{-1,0,1})
        {
            var service=new LocalImageSequenceService();
            var plan=Plan("source",Entry("a",0),Entry("b",1)) with{SelectedEntryId=null,StopAtLast=true};
            var generation=service.Begin("root",new[]{plan});var ticket=service.GetInputs("root",generation)[0];
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(()=>service.AcceptRead(ticket,Guid.NewGuid(),true,cancelled.Token,direction));
            var first=service.AcceptRead(ticket,Guid.NewGuid(),true,CancellationToken.None,direction);
            Assert.Equal(direction<0?"b":"a",first.Entry.EntryId);Assert.False(first.StopAfterCurrent);
            service.FinishReadRound("root",generation,ticket.IterationVersion);
            var next=service.GetInputs("root",generation)[0];
            var second=service.AcceptRead(next,Guid.NewGuid(),true,CancellationToken.None);
            Assert.Equal(direction<0?"a":"b",second.Entry.EntryId);
        }
        var single=new LocalImageSequenceService();
        var one=single.Begin("one",new[]{Plan("source",Entry("only",0)) with{SelectedEntryId=null,StopAtLast=true}});
        Assert.True(single.AcceptRead(single.GetInputs("one",one)[0],Guid.NewGuid(),true,CancellationToken.None).StopAfterCurrent);
    }
    [Fact]
    public void ReadAcceptanceAdvancesOnlyThatSourceAndIsIdempotentForTheRequest()
    {
        var service = new LocalImageSequenceService();
        var generation = service.Begin("w", new[] {
            Plan("one", Entry("a", 0), Entry("b", 1), Entry("c", 2)) with { StopAtLast = true },
            Plan("two", Entry("x", 0), Entry("y", 1))
        });
        var tickets = service.GetInputs("w", generation);
        var request = Guid.NewGuid();
        var receipt = service.AcceptRead(tickets[0], request, advance: true, CancellationToken.None);
        Assert.Equal("b", receipt.Entry.EntryId);
        Assert.False(receipt.StopAfterCurrent);
        Assert.Equal(receipt, service.AcceptRead(tickets[0], request, advance: true, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => service.AcceptRead(tickets[0], Guid.NewGuid(), true, CancellationToken.None));
        Assert.Equal("x", service.GetInputs("w", generation)[1].Entry!.EntryId);
    }

    [Fact]
    public void FinishedRoundRetainsConsumedPositionAndStopsAtTheActualLastEntry()
    {
        var service = new LocalImageSequenceService();
        var generation = service.Begin("w", new[] {
            Plan("one", Entry("a", 0), Entry("missing-b", 1), Entry("c", 2)) with { StopAtLast = true },
            Plan("two", Entry("x", 0), Entry("y", 1))
        });
        var first = service.GetInputs("w", generation);
        var accepted = service.AcceptRead(first[0], Guid.NewGuid(), true, CancellationToken.None);
        Assert.Equal("missing-b", accepted.Entry.EntryId);
        var closed = service.FinishReadRound("w", generation, first[0].IterationVersion);
        Assert.Single(closed.Accepted);
        Assert.False(closed.StopAfterCurrent);
        Assert.Throws<InvalidOperationException>(() => service.FinishReadRound("w", generation, first[0].IterationVersion));
        var next = service.GetInputs("w", generation);
        Assert.Equal("missing-b", next[0].Entry!.EntryId);
        Assert.Equal("x", next[1].Entry!.EntryId);
        var last = service.AcceptRead(next[0], Guid.NewGuid(), true, CancellationToken.None);
        Assert.Equal("c", last.Entry.EntryId);
        Assert.True(service.FinishReadRound("w", generation, next[0].IterationVersion).StopAfterCurrent);
    }
    [Fact]
    public void ManualNavigationWrapsOnceWithoutApplyingAutomaticAdvanceOrStop()
    {
        var service=new LocalImageSequenceService();
        var generation=service.Begin("w",new[]{Plan("source",Entry("a",0),Entry("b",1),Entry("c",2)) with {StopAtLast=true}});
        var first=service.GetInputs("w",generation)[0];
        var previous=service.AcceptRead(first,Guid.NewGuid(),true,CancellationToken.None,navigationDelta:-1);
        Assert.Equal("c",previous.Entry.EntryId);Assert.False(previous.StopAfterCurrent);
        service.FinishReadRound("w",generation,first.IterationVersion);
        var second=service.GetInputs("w",generation)[0];
        var next=service.AcceptRead(second,Guid.NewGuid(),true,CancellationToken.None,navigationDelta:1);
        Assert.Equal("a",next.Entry.EntryId);Assert.False(next.StopAfterCurrent);
        service.FinishReadRound("w",generation,second.IterationVersion);
    }
    [Fact]
    public void CancellationAfterAcceptanceKeepsTheConsumedPosition()
    {
        var service=new LocalImageSequenceService();
        var generation=service.Begin("w",new[]{Plan("source",Entry("a",0),Entry("b",1),Entry("c",2))});
        var ticket=service.GetInputs("w",generation)[0];
        using var cancellation=new CancellationTokenSource();
        var receipt=service.AcceptRead(ticket,Guid.NewGuid(),true,cancellation.Token);
        cancellation.Cancel();
        var round=service.FinishReadRound("w",generation,ticket.IterationVersion);
        Assert.Single(round.Accepted);Assert.Equal("b",receipt.Entry.EntryId);
        Assert.Equal("b",service.GetInputs("w",generation)[0].Entry!.EntryId);
        Assert.Throws<OperationCanceledException>(()=>service.AcceptRead(service.GetInputs("w",generation)[0],Guid.NewGuid(),true,cancellation.Token));
    }
    [Fact]
    public void CancelledAndStaleReadTicketsCannotChangeTheSequence()
    {
        var service = new LocalImageSequenceService();
        var generation = service.Begin("w", new[] { Plan("source", Entry("a", 0), Entry("b", 1)) });
        var ticket = service.GetInputs("w", generation)[0];
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.AcceptRead(ticket, Guid.NewGuid(), true, cancellation.Token));
        Assert.Equal("a", service.GetInputs("w", generation)[0].Entry!.EntryId);
        service.End("w", generation);
        Assert.Throws<InvalidOperationException>(() => service.AcceptRead(ticket, Guid.NewGuid(), true, CancellationToken.None));
    }
    [Fact]
    public void UnselectedCanStartAtFirstButInvalidExplicitSelectionCannotReplaceAnActiveSession()
    {
        var service = new LocalImageSequenceService();
        var plan = Plan("source", Entry("a", 0)) with {SelectedEntryId=null};
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
        var service=new LocalImageSequenceService();var entries=new[]{Entry("a",0),Entry("b",1)};
        var generation=service.Begin("w1",new[]{Plan("source1",entries),Plan("source2",Entry("c",3),Entry("d",7))});
        var other=service.Begin("w2",new[]{Plan("source1",entries)});entries[0]=Entry("edited",9);
        var inputs=service.GetInputs("w1",generation);Assert.Equal("a",inputs[0].Entry!.EntryId);
        Assert.Equal("b",service.AcceptRead(inputs[0],Guid.NewGuid(),true,CancellationToken.None).Entry.EntryId);
        Assert.Equal("d",service.AcceptRead(inputs[1],Guid.NewGuid(),true,CancellationToken.None).Entry.EntryId);
        var round=service.FinishReadRound("w1",generation,inputs[0].IterationVersion);Assert.Equal(2,round.Accepted.Count);
        Assert.Equal(new[]{"b","d"},service.GetInputs("w1",generation).Select(input=>input.Entry!.EntryId));
        Assert.Equal("a",service.GetInputs("w2",other)[0].Entry!.EntryId);
        Assert.Throws<InvalidOperationException>(()=>service.FinishReadRound("w1",generation,inputs[0].IterationVersion));
    }

    [Fact]
    public void FailureAndManualExecutionRetainCurrentEntryAndStaleGenerationsAreRejected()
    {
        var service=new LocalImageSequenceService();
        var plan=Plan("source",Entry("a",0),Entry("b",1)) with {SelectedEntryId="b"};
        var old=service.Begin("w",new[]{plan});var input=service.GetInputs("w",old)[0];
        Assert.Equal("b",service.AcceptRead(input,Guid.NewGuid(),false,CancellationToken.None).Entry.EntryId);
        service.FinishReadRound("w",old,input.IterationVersion);
        var current=service.Begin("w",new[]{plan});
        Assert.Throws<InvalidOperationException>(()=>service.AcceptRead(input,Guid.NewGuid(),true,CancellationToken.None));
        service.End("w",old);Assert.Equal("b",service.GetInputs("w",current)[0].Entry!.EntryId);
    }

    [Fact]
    public void LastEntryUsesFrozenFilteredSequenceAndStopsBeforeAnySourceAdvances()
    {
        var service=new LocalImageSequenceService();
        var plan=Plan("one",Entry("selected-last",4)) with {StopAtLast=true};
        var generation=service.Begin("w",new[]{plan,Plan("two",Entry("a",0),Entry("b",1))});
        var input=service.GetInputs("w",generation);
        Assert.True(service.AcceptRead(input[0],Guid.NewGuid(),true,CancellationToken.None).StopAfterCurrent);
        Assert.True(service.FinishReadRound("w",generation,input[0].IterationVersion).StopAfterCurrent);
        Assert.Equal("a",service.GetInputs("w",generation)[1].Entry!.EntryId);
        Assert.Throws<InvalidOperationException>(()=>service.FinishReadRound("w",generation,input[0].IterationVersion));
    }

    [Fact]
    public void WrongSourceIdentityCannotAdvanceAndWrapUsesEntryIdentityRatherThanPath()
    {
        var service=new LocalImageSequenceService();
        var a=Entry("a",2);var b=a with {EntryId="b",OriginalIndex=5};
        var generation=service.Begin("w",new[]{Plan("source",a,b) with {SelectedEntryId="b"}});
        var input=service.GetInputs("w",generation)[0];
        Assert.Throws<InvalidOperationException>(()=>service.AcceptRead(input with {SourceNodeId="other"},Guid.NewGuid(),true,CancellationToken.None));
        Assert.Equal("a",service.AcceptRead(input,Guid.NewGuid(),true,CancellationToken.None).Entry.EntryId);
        service.FinishReadRound("w",generation,input.IterationVersion);
        Assert.Equal("a",service.GetInputs("w",generation)[0].Entry!.EntryId);
    }
}
