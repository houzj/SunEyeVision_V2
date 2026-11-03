using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestFixtures;
using Xunit;

namespace SunEyeVision.Execution.Tests;

// Neutral owner/empty-graph checks only; this is not a deployed plugin or UI test.
public sealed class RunReservationTests
{
    [Fact]
    public void Empty_run_keeps_document_reserved_until_owner_disposes_batch()
    {
        var component = new ToolComponentRegistration(new ToolComponentMetadata("reservation-probe", "ResultWorkspaceProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(WorkspaceProbeDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { component } })!;
        using var session = DocumentSession.CreateNew(directory);
        var flow = session.GetWorkflows().Single();
        var batch = session.CaptureRunBatch(new[] { flow.Id });
        Assert.Throws<InvalidOperationException>(() => session.Dispose());
        using var result = batch.RunAsync(_ => { }).GetAwaiter().GetResult();
        Assert.True(result.Workflows[flow.Id].Success);
        Assert.Throws<InvalidOperationException>(() => session.Dispose());
        batch.Dispose();
        var draft = session.PrepareGraphEdit(flow.Id);
        Assert.Null(session.CommitGraphEdit(draft, "无变化"));
        session.Dispose();
    }
}
