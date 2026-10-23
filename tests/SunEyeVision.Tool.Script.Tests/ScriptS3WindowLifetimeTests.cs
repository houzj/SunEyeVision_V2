using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using CommonControl;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.Views;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

[Collection("ScriptRuntimePool")]
public class ScriptS3WindowLifetimeTests
{
    [Fact]
    public void HundredOpenRunCloseGenerations_DrainSessionsAndRetiredContexts()
    {
        RunSta(async () =>
        {
            var contexts = new List<WeakReference>();
            var lifetimes = new List<WeakReference[]>();
            var pool = typeof(ScriptParameters).Assembly.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptSandboxPool")!;
            for (var cycle = 0; cycle < 100; cycle++)
            {
                contexts.AddRange(await OpenRunClose(cycle, pool, lifetimes));
            }
            for (var attempt = 0; contexts.Any(weak => weak.IsAlive) && attempt < 10; attempt++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Task.Delay(20);
            }
            Assert.Equal(100, contexts.Count);
            Console.WriteLine($"ALC={contexts.Count(weak => weak.IsAlive)} slot={lifetimes.Count(refs => refs[0].IsAlive)} sandbox={lifetimes.Count(refs => refs[1].IsAlive)} assembly={lifetimes.Count(refs => refs[2].IsAlive)}");
            Assert.All(contexts, weak => Assert.False(weak.IsAlive));
        });
    }

    [Fact]
    public void DispatcherQueuedPublication_RechecksOwnerAndReleasesDiscardedResources()
    {
        RunSta(async () =>
        {
            var owner = new ToolExecutionOwner();
            var identity = owner.Begin("node", Guid.NewGuid(), "contract");
            var released = 0; var published = 0;
            using var resources = new SunEyeVision.Plugin.SDK.Execution.Results.ResultResourceOwner(() => released++);
            var lease = resources.Retain();
            var operation = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                try { identity.TryPublish(() => published++); }
                finally { lease.Dispose(); }
            }, DispatcherPriority.Background);
            resources.Dispose(); owner.RetireNode("node");
            await operation;
            Assert.Equal(0, published); Assert.Equal(1, released);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThreeWindows_CollectibleAssemblyCacheDiagnostic(bool declaredOutput)
    {
        RunSta(async () =>
        {
            var before = CaptureCacheKeys();
            var contexts = new List<WeakReference>();
            var lifetimes = new List<WeakReference[]>();
            var pool = typeof(ScriptParameters).Assembly.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptSandboxPool")!;
            for (var cycle = 0; cycle < 3; cycle++) contexts.AddRange(await OpenRunClose(cycle, pool, lifetimes, declaredOutput));
            for (var attempt = 0; contexts.Any(weak => weak.IsAlive) && attempt < 10; attempt++)
            { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Task.Delay(20); }
            var after = CaptureCacheKeys();
            foreach (var row in before) Console.WriteLine("before=" + row);
            foreach (var row in after) Console.WriteLine("after=" + row);
            foreach (var row in after.Except(before)) Console.WriteLine("added=" + row);
            Console.WriteLine($"output={declaredOutput} ALC={contexts.Count(weak => weak.IsAlive)} slot={lifetimes.Count(refs => refs[0].IsAlive)} sandbox={lifetimes.Count(refs => refs[1].IsAlive)} assembly={lifetimes.Count(refs => refs[2].IsAlive)}");
            Assert.All(contexts, weak => Assert.False(weak.IsAlive));
        });
    }

    private static string[] CaptureCacheKeys()
    {
        var rows = new List<string>();
        foreach (var assembly in new[] { typeof(System.Windows.DependencyObject).Assembly, typeof(System.Windows.Media.Visual).Assembly, Assembly.Load("System.Xaml") })
        {
            var helper = assembly.GetTypes().FirstOrDefault(type => type.Name == "SafeSecurityHelper");
            rows.Add($"runtime={assembly.Location}|version={assembly.GetName().Version}|sha256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(assembly.Location)))}");
            var gate = helper?.GetField("syncObject", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? new object();
            lock (gate)
            {
                var cache = helper?.GetField("_assemblies", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as System.Collections.IDictionary;
                foreach (var key in cache?.Keys.Cast<object>().OfType<Assembly>().Where(key => key.IsCollectible) ?? [])
                    rows.Add($"cache={assembly.GetName().Name}/{helper?.FullName}|key={key.FullName}|IsCollectible={key.IsCollectible}|IsDynamic={key.IsDynamic}");
            }
        }
        return rows.ToArray();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> OpenRunClose(int cycle, Type pool, List<WeakReference[]> lifetimes, bool declaredOutput = true)
    {
        WeakReference[] references = [];
        var p = new ScriptParameters(); p.Inputs.Clear(); p.Outputs.Clear();
        if (declaredOutput) p.Outputs.Add(new ScriptOutputConfig { Name = "value", TypeName = "Double" });
        // Code and reference generations both change; no stale success fallback.
        p.ScriptCode = $"public partial class UserScript {{ public override void Init() {{}} public override void Process() {{ {(declaredOutput ? $"value = {cycle};" : "")} }} }}";
        if (cycle % 2 == 0 && !p.AssemblyReferences.Contains("System.Net.Http")) p.AssemblyReferences.Add("System.Net.Http");
        if (cycle % 2 != 0) p.AssemblyReferences.Remove("System.Net.Http");
        var control = new ScriptDebugControl(); control.SetParameters(p);
        var core = (ScriptEditorCore)control.FindName("EditorCore");
        var shell = new DefaultDebugWindow(control);
        Assert.Same(Dispatcher.CurrentDispatcher, DefaultDebugWindow.DebugWindowChrome.Dispatcher);
        if (cycle == 0)
            Console.WriteLine($"S3 window/Chrome thread={Environment.CurrentManagedThreadId}/{DefaultDebugWindow.DebugWindowChrome.Dispatcher.Thread.ManagedThreadId}");
        ToolExecutionOwner? owner = null;
        try
        {
            await control.InitializeAsync(); shell.Show();
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var execution = (Task<object>)typeof(ScriptDebugControl).GetMethod("ExecuteToolAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, null)!;
            using var result = (ScriptResults)await execution.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(result.IsSuccess, result.ErrorMessage);
            if (declaredOutput) Assert.Equal((double)cycle, result.OutputValues["value"]);
            owner = result.ExecutionIdentity!.Owner;
            references = (WeakReference[])pool.GetMethod("GetContextReferences", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [result.ExecutionIdentity.Domain])!;
            lifetimes.Add((WeakReference[])pool.GetMethod("GetLifetimeReferences", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [result.ExecutionIdentity.Domain])!);
        }
        finally { shell.Close(); }
        await control.ViewPreparation.WaitAsync(TimeSpan.FromSeconds(10));
        await core.AnalysisIdle.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(core.AnalysisIdle.IsCompletedSuccessfully);
        Assert.Equal(0, owner!.ActiveRuns);
        owner.Retire();
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return references;
    }

    private static void RunSta(Func<Task> action)
    {
        ScriptTestDispatcher.Run(action, 180);
    }
}
