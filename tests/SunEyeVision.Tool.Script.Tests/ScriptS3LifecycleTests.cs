using System.IO;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.Script.Models;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

public class ScriptS3LifecycleTests
{
    private static ScriptParameters Parameters(string body)
    {
        var parameters = new ScriptParameters();
        parameters.Inputs.Clear(); parameters.Outputs.Clear();
        parameters.ScriptCode = "public partial class UserScript { " + body + " }";
        return parameters;
    }

    [Theory]
    [InlineData("Init")]
    [InlineData("Process")]
    public void MainFailure_StillReleasesExactlyOnce_AndKeepsCleanupFailure(string failureStage)
    {
        var marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var p = Parameters($$"""
                public override void Init() { {{(failureStage == "Init" ? "throw new System.Exception(\"primary-init\");" : "")}} }
                public override void Process() { {{(failureStage == "Process" ? "throw new System.Exception(\"primary-process\");" : "")}} }
                public override void Release() { System.IO.File.AppendAllText(@"{{marker}}", "R"); throw new System.Exception("cleanup-error"); }
                """);
            using var result = new ScriptTool().Run(p);
            Assert.False(result.IsSuccess);
            Assert.Equal("R", File.ReadAllText(marker));
            Assert.Contains(result.CompileErrors, error => error.Contains("primary-"));
            Assert.Contains(result.CompileErrors, error => error.Contains("cleanup-error"));
            Assert.Empty(result.OutputValues);
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact]
    public void InvalidInput_IsRejectedBeforeUserConstructor()
    {
        var marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var p = Parameters($$"""
                public UserScript() { System.IO.File.WriteAllText(@"{{marker}}", "constructed"); }
                public override void Init() {} public override void Process() {}
                """);
            p.Inputs.Add(new ScriptInputConfig { Name = "number", TypeName = "Double" });
            var type = typeof(ScriptParameters).Assembly.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptLifecycleManager")!;
            using var manager = (IDisposable)Activator.CreateInstance(type)!;
            var errors = (List<string>)type.GetMethod("Execute")!.Invoke(manager,
                [p.GeneratedCode, p.ScriptCode, null, null, null, new Dictionary<string, object?> { ["number"] = new object() }, p.AssemblyReferences])!;
            Assert.NotEmpty(errors);
            Assert.False(File.Exists(marker));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CooperativeCancellation_RunsReleaseBeforeTaskCompletes(bool releaseThrows)
    {
        var entered = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var released = entered + ".released";
        using var cancel = new CancellationTokenSource();
        try
        {
            var p = Parameters($$"""
                public override void Init() {}
                public override void Process() { System.IO.File.WriteAllText(@"{{entered}}", "entered"); while(true) { ThrowIfCancellationRequested(); System.Threading.Thread.Sleep(1); } }
                public override void Release() { System.IO.File.WriteAllText(@"{{released}}", "released"); {{(releaseThrows ? "throw new System.Exception(\"cleanup-during-cancel\");" : "")}} }
                """);
            var execution = new ScriptTool().ExecuteAsync(new ToolExecutionRequest(p) { CancellationToken = cancel.Token }).AsTask();
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(entered) && !execution.IsCompleted && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.True(File.Exists(entered));
            cancel.Cancel();
            var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(TimeSpan.FromSeconds(10)));
            if (releaseThrows) Assert.Contains("cleanup-during-cancel", cancelled.Message);
            Assert.True(File.Exists(released));
        }
        finally
        {
            cancel.Cancel();
            if (File.Exists(entered)) File.Delete(entered);
            if (File.Exists(released)) File.Delete(released);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredImageAliases_AreCopiedBeforeRelease_AndBorrowedInputIsNotOwned(bool aliasInput)
    {
        var tracker = new ImageTracker();
        var input = new TestImage(tracker);
        var p = Parameters("public override void Init() {} public override void Process() { a = " + (aliasInput ? "img" : "img.Clone()") + "; b = a; } public override void Release() { " + (aliasInput ? "" : "a.Dispose();") + " }");
        p.Inputs.Add(new ScriptInputConfig { Name = "img", TypeName = "Image", BindingSource = "__GlobalVars__.image" });
        p.Outputs.Add(new ScriptOutputConfig { Name = "a", TypeName = "Image" });
        p.Outputs.Add(new ScriptOutputConfig { Name = "b", TypeName = "Image" });
        using var result = (ScriptResults)await new ScriptTool().ExecuteAsync(new ToolExecutionRequest(p)
        { GlobalVariables = new Dictionary<string, object?> { ["image"] = input } });
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.OutputValues.Count);
        Assert.Same(result.OutputValues["a"], result.OutputValues["b"]);
        Assert.False(((TestImage)result.OutputValues["a"]!).Disposed);
        Assert.False(input.Disposed);
        Assert.Equal(aliasInput ? 2 : 3, tracker.Clones);
        Assert.Null(p.ResolvedInputValues);
        var monitoredInput = (TestImage)result.ResolvedInputValues["img"]!;
        Assert.NotSame(input, monitoredInput);
        Assert.False(monitoredInput.Disposed);
        var monitorLease = result.Resources!.Retain();
        result.Dispose(); result.Dispose();
        Assert.False(monitoredInput.Disposed);
        monitorLease.Dispose();
        Assert.True(monitoredInput.Disposed);
        Assert.Equal(aliasInput ? 2 : 3, tracker.Disposals);
        Assert.False(input.Disposed);
    }

    [Fact]
    public async Task GlobalPreparationFailure_PreservesRequestIdentity_AndDoesNotOwnOriginal()
    {
        var original = new UnsupportedGlobal();
        var owner = new ToolExecutionOwner();
        var parameters = Parameters("public override void Init() {} public override void Process() {}");
        using var request = new ToolExecutionRequest(parameters)
        {
            ExecutionOwner = owner, NodeId = "prepare-failure", RunId = Guid.NewGuid(),
            GlobalVariables = new Dictionary<string, object?> { ["unsupported"] = original }
        };
        var executor = new ToolExecutor(new SunEyeVision.Plugin.SDK.Execution.Parameters.ParameterResolver());
        var result = await executor.ExecuteAsync(new ScriptTool(), request);
        Assert.Equal(SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.ExecutionIdentity);
        Assert.Same(request.ExecutionIdentity, result.ExecutionIdentity);
        Assert.True(result.ExecutionIdentity.IsCurrent);
        owner.RetireNode("standalone/prepare-failure");
        Assert.False(result.ExecutionIdentity.IsCurrent);
        Assert.False(original.Disposed);
        (result as IDisposable)?.Dispose();
    }

    private sealed class UnsupportedGlobal : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task CompletedRun_RejectsRetainedRawGlobalAndLogDelegates()
    {
        var callbacks = new List<Action>();
        var globals = new Dictionary<string, object?> { ["callbacks"] = callbacks, ["flag"] = 1d };
        var p = Parameters("""
            public override void Init() {}
            public override void Process() {
                var callbacks = (System.Collections.Generic.List<System.Action>)Context.GetGlobalValue("callbacks");
                var write = Context.SetGlobalValue; var log = Context.LogInfoAction;
                callbacks.Add(() => write("flag", 42d)); callbacks.Add(() => log("late-log"));
            }
            """);
        var owner = new ToolExecutionOwner();
        using var result = (ScriptResults)await new ScriptTool().ExecuteAsync(new ToolExecutionRequest(p) { ExecutionOwner = owner, GlobalVariables = globals });
        try
        {
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(result.ExecutionIdentity!.IsCurrent);
            var logs = result.LogMessages.Count;
            Assert.Throws<InvalidOperationException>(() => callbacks[0]());
            callbacks[1]();
            Assert.Equal(1d, globals["flag"]);
            Assert.Equal(logs, result.LogMessages.Count);
        }
        finally { callbacks.Clear(); owner.Retire(); }
    }

    [Fact]
    public void RegisteredTaskAndCleanup_FinishBeforeReleaseAndReturn()
    {
        var marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var p = Parameters($$"""
                public override void Init() { Context.RegisterCleanup(() => System.IO.File.AppendAllText(@"{{marker}}", "C")); }
                public override void Process() { Context.RegisterTask(System.Threading.Tasks.Task.Run(() => { System.Threading.Thread.Sleep(30); System.IO.File.AppendAllText(@"{{marker}}", "T"); })); }
                public override void Release() { if(System.IO.File.ReadAllText(@"{{marker}}") != "T") throw new System.Exception("task-not-drained"); System.IO.File.AppendAllText(@"{{marker}}", "R"); }
                """);
            using var result = new ScriptTool().Run(p);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal("TRC", File.ReadAllText(marker));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact]
    public void ForeignCancellation_IsFailure_NotCurrentCancellation()
    {
        using var result = new ScriptTool().Run(Parameters("public override void Init() {} public override void Process() { throw new System.OperationCanceledException(\"foreign-token\", new System.Threading.CancellationToken(true)); }"));
        Assert.False(result.IsSuccess); Assert.False(result.IsCancelled);
        Assert.Contains(result.CompileErrors, error => error.Contains("foreign-token"));
    }

    private sealed class ImageTracker { public int Clones; public int Disposals; }
    private sealed class TestImage(ImageTracker tracker) : IVisionImage
    {
        public bool Disposed { get; private set; }
        public int Width => 1; public int Height => 1; public int Channels => 1; public bool IsEmpty => false;
        public byte[] GetPixelData() => [1];
        public IVisionImage Clone() { if (Disposed) throw new ObjectDisposedException(nameof(TestImage)); tracker.Clones++; return new TestImage(tracker); }
        public void Dispose() { if (!Disposed) { Disposed = true; tracker.Disposals++; } }
    }
}
