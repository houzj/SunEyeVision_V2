using System.Reflection;
using System.IO;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Script.Models;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

[Collection("ScriptRuntimePool")]
public sealed class ScriptAdmissionTests
{
    private static ScriptParameters Parameters(string? code = null)
    {
        var p = new ScriptParameters(); p.Inputs.Clear(); p.Outputs.Clear();
        p.ScriptCode = code ?? "public partial class UserScript { public override void Init() {} public override void Process() {} }";
        return p;
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EarlyTerminalPathsDrainCentralAdmission(int mode)
    {
        var p = Parameters(mode == 0 ? "invalid C# source" : null);
        if (mode == 1) { p.Outputs.Add(new ScriptOutputConfig { Name = "same", TypeName = "Double" }); p.Outputs.Add(new ScriptOutputConfig { Name = "same", TypeName = "Double" }); }
        using var cancel = new CancellationTokenSource(); if (mode == 2) cancel.Cancel();
        var owner = new ToolExecutionOwner();
        var request = new ToolExecutionRequest(p) { NodeId = "script", ExecutionOwner = owner, CancellationToken = cancel.Token };
        var lease = new CountingLease();
        typeof(ToolExecutionRequest).GetProperty("InputResources", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(request, lease);
        var result = await new ToolExecutor(new ParameterResolver()).ExecuteAsync(new ScriptTool(), request);
        Assert.Equal(mode == 2 ? ExecutionStatus.Cancelled : ExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.ExecutionIdentity); Assert.Same(request.ExecutionIdentity, result.ExecutionIdentity);
        Assert.Equal(0, owner.ActiveRuns); Assert.Equal(1, lease.Count);
        Assert.False(result.ExecutionIdentity.TryInvokeCallback(() => throw new Exception()));
        Assert.Equal(mode != 2, result.ExecutionIdentity.TryPublish(() => { }));
        (result as IDisposable)?.Dispose(); owner.Retire();
    }
    [Fact]
    public async Task ImplicitCentralOwnerIsTheSameParameterOwnerUsedByDirectExecution()
    {
        var p = Parameters("invalid C# source");
        var executor = new ToolExecutor(new ParameterResolver()); var tool = new ScriptTool();
        var a = new ToolExecutionRequest(p) { NodeId = "script" };
        var ar = await executor.ExecuteAsync(tool, a);
        var b = new ToolExecutionRequest(p) { NodeId = "script" };
        var br = await executor.ExecuteAsync(tool, b);
        var c = new ToolExecutionRequest(p) { NodeId = "script" };
        var cr = await tool.ExecuteAsync(c);
        Assert.Same(a.ExecutionIdentity!.Owner, b.ExecutionIdentity!.Owner);
        Assert.Same(b.ExecutionIdentity.Owner, c.ExecutionIdentity!.Owner);
        Assert.Equal(a.ExecutionIdentity.Domain, c.ExecutionIdentity.Domain);
        Assert.NotEqual(a.ExecutionIdentity.RequestId, b.ExecutionIdentity.RequestId);
        Assert.Equal(0, c.ExecutionIdentity.Owner.ActiveRuns);
        foreach (var result in new[] { ar, br, cr }) (result as IDisposable)?.Dispose();
        c.ExecutionIdentity.Owner.Retire();
    }
    [Fact]
    public async Task DirectDuplicateCannotExecuteOrCloseTheCentralBody()
    {
        var root = Path.Combine(Path.GetTempPath(), "suneye-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var entered = Path.Combine(root, "entered"); var release = Path.Combine(root, "release");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var p = Parameters($$"""
            public partial class UserScript {
              public override void Init() {}
              public override void Process() {
                System.IO.File.AppendAllText(@"{{entered}}", "body");
                while (!System.IO.File.Exists(@"{{release}}")) { ThrowIfCancellationRequested(); System.Threading.Thread.Sleep(1); }
              }
            }
            """);
        var owner = new ToolExecutionOwner();
        var request = new ToolExecutionRequest(p) { ExecutionOwner = owner, NodeId = "script", CancellationToken = cancel.Token };
        var tool = new ScriptTool();
        var first = new ToolExecutor(new ParameterResolver()).ExecuteAsync(tool, request).AsTask();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(entered) && !first.IsCompleted && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.True(File.Exists(entered), first.IsCompleted ? (await first).ErrorMessage : "body did not enter");
            Assert.Equal(1, owner.ActiveRuns);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tool.ExecuteAsync(request).AsTask());
            Assert.Equal(1, owner.ActiveRuns);
            Assert.True(request.ExecutionIdentity!.TryInvokeCallback(() => { }));
            Assert.Equal("body", File.ReadAllText(entered));
        }
        finally
        {
            File.WriteAllText(release, "go");
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var result = await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(0, owner.ActiveRuns);
        Assert.False(request.ExecutionIdentity!.TryInvokeCallback(() => throw new Exception()));
        (result as IDisposable)?.Dispose(); owner.Retire();
        File.Delete(entered); File.Delete(release); Directory.Delete(root);
    }
    private sealed class CountingLease : IDisposable
    { public int Count; public void Dispose() => Interlocked.Increment(ref Count); }
}
