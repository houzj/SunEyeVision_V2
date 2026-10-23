using System.Reflection;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

[CollectionDefinition("ScriptRuntimePool", DisableParallelization = true)]
public class ScriptRuntimePoolCollection { }

[Collection("ScriptRuntimePool")]
public class ScriptS3PoolOwnershipTests
{
    private static Type Pool => typeof(ScriptParameters).Assembly.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptSandboxPool")!;
    private static IDisposable Acquire(string key, string domain, CancellationToken token = default)
    {
        try { return (IDisposable)Pool.GetMethod("Acquire")!.Invoke(null, [key, domain, null, null, token])!; }
        catch (TargetInvocationException ex) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException!).Throw(); throw; }
    }
    private static (int Resident, int Active, int Retiring) Inspect()
        => ((int, int, int))Pool.GetMethod("Inspect", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
    private static void Clear() => Pool.GetMethod("Clear")!.Invoke(null, null);

    [Fact]
    public void SixteenActiveDomains_RejectSeventeenth_ClearWaitsForActualLeaseReturn()
    {
        Clear();
        var leases = new List<IDisposable>();
        try
        {
            for (var i = 0; i < 16; i++) leases.Add(Acquire("key-" + i, "domain-" + i));
            Assert.Equal((16, 16, 0), Inspect());
            Assert.Contains("16", Assert.Throws<InvalidOperationException>(() => Acquire("seventeen", "seventeen")).Message);
            Assert.Equal((16, 16, 0), Inspect());
            Clear();
            Assert.Equal((16, 16, 16), Inspect());
            Assert.Throws<InvalidOperationException>(() => Acquire("another", "another"));
            leases[0].Dispose();
            Assert.Equal((15, 15, 15), Inspect());
        }
        finally { foreach (var lease in leases) lease.Dispose(); Clear(); }
        Assert.Equal((0, 0, 0), Inspect());
    }

    [Fact]
    public async Task SameDomain_WaiterCancellation_DoesNotReleaseActiveLease()
    {
        Clear();
        using var first = Acquire("first", "same-domain");
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = Task.Run(() => { entered.SetResult(); return Acquire("second", "same-domain", cancel.Token); });
        await entered.Task;
        await Task.Delay(30);
        Assert.False(waiting.IsCompleted);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal((1, 1, 0), Inspect());
        Clear();
    }

    [Fact]
    public async Task RetiredDomain_CannotBeRecreatedByWaitingOrFutureAcquire()
    {
        Clear();
        var domain = "retire-" + Guid.NewGuid();
        using var first = Acquire("first", domain);
        var waiting = Task.Run(() => Acquire("second", domain));
        await Task.Delay(30);
        Pool.GetMethod("RetireDomain")!.Invoke(null, [domain]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(3)));
        first.Dispose();
        Assert.Throws<InvalidOperationException>(() => Acquire("third", domain));
        Assert.Equal((0, 0, 0), Inspect());
        var emptyDomain = "empty-" + Guid.NewGuid();
        Pool.GetMethod("RetireDomain")!.Invoke(null, [emptyDomain]);
        Assert.Throws<InvalidOperationException>(() => Acquire("first", emptyDomain));
    }

    [Fact]
    public async Task HundredRuns_ShareOnlySameNodeStaticState_AndOwnerRetiresDomains()
    {
        Clear();
        var owner = new ToolExecutionOwner();
        var parameters = new ScriptParameters();
        parameters.Inputs.Clear(); parameters.Outputs.Clear();
        parameters.Outputs.Add(new ScriptOutputConfig { Name = "value", TypeName = "Double" });
        parameters.ScriptCode = "public partial class UserScript { public static int Counter; public override void Init() {} public override void Process() { value = ++Counter; } }";
        var tool = new ScriptTool();
        try
        {
            for (var i = 1; i <= 100; i++)
            {
                using var result = (ScriptResults)await tool.ExecuteAsync(new ToolExecutionRequest(parameters)
                { ExecutionOwner = owner, WorkflowId = "workflow", NodeId = "a", RunId = Guid.NewGuid() });
                Assert.True(result.IsSuccess, result.ErrorMessage);
                Assert.Equal((double)i, result.OutputValues["value"]);
                Assert.False(result.OutputValues.ContainsKey("Counter"));
            }
            using var second = (ScriptResults)await tool.ExecuteAsync(new ToolExecutionRequest(parameters)
            { ExecutionOwner = owner, WorkflowId = "workflow", NodeId = "b", RunId = Guid.NewGuid() });
            Assert.True(second.IsSuccess, second.ErrorMessage);
            Assert.Equal(1d, second.OutputValues["value"]);
            Assert.Equal((2, 0, 0), Inspect());
        }
        finally { owner.Retire(); Clear(); }
        Assert.Equal((0, 0, 0), Inspect());
    }

    [Fact]
    public void OwnerRetirement_IsAtomicWithPublication_AndActiveRunBlocksSolutionClose()
    {
        var owner = new ToolExecutionOwner();
        var identity = owner.Begin("node", Guid.NewGuid(), "contract");
        var active = owner.EnterRun(identity);
        Assert.Throws<InvalidOperationException>(() => owner.Retire());
        Assert.True(identity.IsCurrent);
        owner.RetireNode("node");
        Assert.False(identity.TryPublish(() => throw new Exception("must not publish")));
        active.Dispose();
        var replacement = owner.Begin("node", Guid.NewGuid(), "contract");
        Assert.NotEqual(identity.Domain, replacement.Domain);
        owner.Retire();
        Assert.False(replacement.IsCurrent);
        Assert.Throws<InvalidOperationException>(() => owner.Begin("node", Guid.NewGuid(), "contract"));
    }

    [Fact]
    public void ContextReplacement_PreservesInputSnapshotLease_ThenReleasesExactlyOnce()
    {
        var released = 0;
        var result = new OwnedResult(() => released++);
        var context = new WorkflowContext();
        context.SetNodeResult("source", new NodeExecutionResult { Result = result, Success = true });
        result.Resources!.Dispose();
        using var snapshot = context.CaptureNodeResults();
        context.ClearNodeResults();
        Assert.Equal(0, released);
        Assert.Same(result, snapshot.Results["source"].Result);
        snapshot.Dispose(); snapshot.Dispose();
        Assert.Equal(1, released);
        Assert.Throws<ObjectDisposedException>(() => result.Resources.Retain());
    }

    private sealed class OwnedResult : GenericToolResults
    {
        internal OwnedResult(Action release) => Resources = new ResultResourceOwner(release);
    }

    [Fact]
    public void RetiredExecutedAssembly_ContextWeakReferenceIsCollectible()
    {
        var weak = ExecuteAndRetire();
        for (var attempt = 0; weak.IsAlive && attempt < 10; attempt++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Thread.Sleep(20);
        }
        Assert.False(weak.IsAlive);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ExecuteAndRetire()
    {
        Clear();
        var owner = new ToolExecutionOwner();
        var parameters = new ScriptParameters();
        parameters.Inputs.Clear(); parameters.Outputs.Clear();
        parameters.ScriptCode = "public partial class UserScript { public override void Init() {} public override void Process() {} }";
        using var result = (ScriptResults)new ScriptTool().ExecuteAsync(new ToolExecutionRequest(parameters)
        { ExecutionOwner = owner, NodeId = "collectible", RunId = Guid.NewGuid() }).AsTask().GetAwaiter().GetResult();
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var weak = (WeakReference[])Pool.GetMethod("GetContextReferences", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [result.ExecutionIdentity!.Domain])!;
        owner.Retire();
        Assert.Equal((0, 0, 0), Inspect());
        return Assert.Single(weak);
    }
}
