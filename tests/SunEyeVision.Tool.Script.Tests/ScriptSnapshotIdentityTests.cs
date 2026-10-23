using System.Reflection;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CommonControl;
using SunEyeVision.Tool.Script.Models;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

public class ScriptSnapshotIdentityTests
{
    private static readonly Assembly Tool = typeof(ScriptParameters).Assembly;
    private static Type Engine(string name) => Tool.GetType("SunEyeVision.Tool.Script.ScriptEngine." + name)!;
    private static object Snapshot(string[] names, string? directory = null)
        => Engine("ScriptReferenceSnapshot").GetMethod("Capture")!.Invoke(null, [names, directory])!;
    private static string Identity(object snapshot) => (string)snapshot.GetType().GetProperty("Identity")!.GetValue(snapshot)!;
    private static object Compile(string[] parts, string[]? names = null, string? directory = null)
        => Engine("ScriptCompiler").GetMethod("Compile")!.Invoke(Activator.CreateInstance(Engine("ScriptCompiler")),
            [parts, "Test", directory, names])!;
    private static T Value<T>(object result, string name) => (T)result.GetType().GetProperty(name)!.GetValue(result)!;

    private static void WriteLibrary(string path, int value, string assemblyName = "SnapshotFixture")
    {
        var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(
            $"public static class Fixture {{ public static int Value => {value}; }}")], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = File.Create(path);
        Assert.True(compilation.Emit(output).Success);
    }

    [Fact]
    public void SamePathAndTimestampReplacement_ChangesIdentityAndCompiledBehavior()
    {
        var root = Path.Combine(Path.GetTempPath(), "s1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "SnapshotFixture.dll");
            WriteLibrary(path, 1);
            var timestamp = File.GetLastWriteTimeUtc(path);
            var names = new ScriptParameters().AssemblyReferences.Concat(["SnapshotFixture"]).ToArray();
            var first = Snapshot(names, root);
            var code = new[] { "public static class Probe { public static int Get() => Fixture.Value; }" };
            var a = Compile(code, names, root);
            WriteLibrary(path, 2);
            File.SetLastWriteTimeUtc(path, timestamp);
            var second = Snapshot(names, root);
            var b = Compile(code, names, root);
            Assert.NotEqual(Identity(first), Identity(second));
            Assert.True(Value<bool>(a, "Success"));
            Assert.True(Value<bool>(b, "Success"));
            Assert.NotEqual(Value<string>(a, "CacheKey"), Value<string>(b, "CacheKey"));
            Assert.False(Value<bool>(b, "CacheHit"));
            Assert.Equal(1, ExecuteFixture(names, root, first));
            Assert.Equal(2, ExecuteFixture(names, root, second));
            File.Delete(path);
            var missing = Compile(code, names, root);
            Assert.False(Value<bool>(missing, "Success"));
            Assert.False(Value<bool>(missing, "CacheHit"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static int ExecuteFixture(string[] names, string root, object snapshot)
    {
        // Load the captured dependency bytes, including the old image after the file was replaced.
        var sandbox = Activator.CreateInstance(Engine("ScriptSandbox"), ["s1-" + Guid.NewGuid(), root, snapshot])!;
        try
        {
            var field = Engine("ScriptSandbox").GetField("_alc", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var context = (System.Runtime.Loader.AssemblyLoadContext)field.GetValue(sandbox)!;
            return (int)context.Assemblies.Single(a => a.GetName().Name == "SnapshotFixture")
                .GetType("Fixture")!.GetProperty("Value")!.GetValue(null)!;
        }
        finally { ((IDisposable)sandbox).Dispose(); }
    }

    [Fact]
    public void Editor_UnloadReloadAndGeneratedFields_UseNewSessionAndSemanticCache()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var editor = new ScriptEditorCore();
                var generated = "public partial class UserScript { public int input; }";
                editor.SetCodeSource(() => generated, editor.GetCode);
                editor.SetReferenceSnapshotProvider(() =>
                {
                    var snapshot = Snapshot(new ScriptParameters().AssemblyReferences.ToArray());
                    return (ScriptAnalysisReferences)snapshot.GetType().GetProperty("AnalysisReferences")!.GetValue(snapshot)!;
                });
                editor.LoadCode("public partial class UserScript { public void P() { input.ToString(); } }");
                editor.InitializeWorkspace();
                var solution = editor.Workspace!.CurrentSolution;
                editor.RebuildWorkspace();
                Assert.Same(solution, editor.Workspace.CurrentSolution);
                var original = editor.AnalysisSession.Capture();
                var colorizer = typeof(ScriptEditorCore).GetField("_semanticColorizer", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var oldColorizer = colorizer.GetValue(editor);
                generated = "public partial class UserScript { public string input; }";
                editor.RebuildWorkspace();
                Assert.False(editor.AnalysisSession.IsCurrent(original));
                Assert.NotSame(oldColorizer, colorizer.GetValue(editor));
                window = new Window { Content = editor, Width = 480, Height = 300, ShowInTaskbar = false };
                window.Show();
                editor.ScheduleDiagnosticAsync();
                var session = editor.AnalysisSession;
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.NotEqual(session.Id, editor.AnalysisSession.Id);
                editor.ScheduleDiagnosticAsync();
                Assert.Contains("string input", editor.Workspace!.CurrentSolution.Projects.Single().Documents
                    .Single(d => d.Name == "ScriptGenerated.cs").GetTextAsync().GetAwaiter().GetResult().ToString());
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Editor STA test timed out");
        Assert.Null(failure);
    }

    [Fact]
    public void DirectoryAndSelectedReferences_ArePartOfIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "s1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "a"));
        Directory.CreateDirectory(Path.Combine(root, "b"));
        try
        {
            WriteLibrary(Path.Combine(root, "a", "SnapshotFixture.dll"), 1);
            File.Copy(Path.Combine(root, "a", "SnapshotFixture.dll"), Path.Combine(root, "b", "SnapshotFixture.dll"));
            var a = Snapshot(["SnapshotFixture"], Path.Combine(root, "a"));
            var b = Snapshot(["SnapshotFixture"], Path.Combine(root, "b"));
            Assert.NotEqual(Identity(a), Identity(b));
            var defaults = new ScriptParameters().AssemblyReferences.ToArray();
            Assert.NotEqual(Identity(Snapshot(defaults)), Identity(Snapshot(defaults.Concat(["SnapshotFixture"]).ToArray(), Path.Combine(root, "a"))));
            Assert.Throws<TargetInvocationException>(() => Snapshot([]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CompilationCache_IsolatesReturnedBuffersAndCodePartBoundaries()
    {
        var tag = Guid.NewGuid().ToString("N");
        var parts = new[] { "public partial class P" + tag + " {}", "// second" };
        var first = Compile(parts);
        Assert.True(Value<bool>(first, "Success"));
        Value<byte[]>(first, "AssemblyBytes")[0] = 0;
        Value<List<string>>(first, "Diagnostics").Add("polluted");
        var hit = Compile(parts);
        Assert.True(Value<bool>(hit, "CacheHit"));
        Assert.NotEqual(0, Value<byte[]>(hit, "AssemblyBytes")[0]);
        Assert.Empty(Value<List<string>>(hit, "Diagnostics"));
        Assert.Equal(0, Value<double>(hit, "EmitMs"));
        var snapshot = Snapshot(new ScriptParameters().AssemblyReferences.ToArray());
        var key = Engine("ScriptCompiler").GetMethod("ComputeCompilationKey", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.NotEqual(key.Invoke(null, [new[] { "ab", "c" }, snapshot]), key.Invoke(null, [new[] { "a", "bc" }, snapshot]));
        Assert.False(Value<bool>(Compile(["not valid C#"]), "Success"));
        Assert.False(Value<bool>(Compile(["not valid C#"]), "CacheHit"));
    }

    [Fact]
    public void SessionStamp_RejectsOldDocumentEnvironmentAndClosedSession()
    {
        var session = new ScriptAnalysisSession();
        var initial = session.Capture();
        session.AdvanceDocument();
        Assert.False(session.IsCurrent(initial));
        session.SetEnvironment("refs1", "fields1");
        var stamp = session.Capture();
        session.SetEnvironment("refs1", "fields1");
        Assert.True(session.IsCurrent(stamp));
        session.SetEnvironment("refs1", "fields2");
        Assert.False(session.IsCurrent(stamp));
        stamp = session.Capture();
        var token = session.LifetimeToken;
        session.Dispose();
        Assert.True(token.IsCancellationRequested);
        Assert.False(session.IsCurrent(stamp));
        using var reopened = new ScriptAnalysisSession();
        Assert.NotEqual(session.Id, reopened.Id);
    }

    [Fact]
    public void Warmup_DoesNotConstructUserInstance_AndExecutionReportsSeparateTimings()
    {
        var path = Path.Combine(Path.GetTempPath(), "s1-constructor-" + Guid.NewGuid().ToString("N"));
        var user = "using SunEyeVision.Plugin.SDK.Scripting;\npublic class SideEffectScript : Script { " +
            "public SideEffectScript() { System.IO.File.WriteAllText(@\"" + path + "\", \"constructed\"); } " +
            "public override void Init() {} public override void Process() {} }";
        var manager = Activator.CreateInstance(Engine("ScriptLifecycleManager"))!;
        try
        {
            var errors = (List<string>)manager.GetType().GetMethod("CompileOnly")!.Invoke(manager, ["", user, null, null])!;
            Assert.Empty(errors);
            manager.GetType().GetMethod("WarmupSandbox")!.Invoke(manager, ["", user, null, null]);
            Assert.False(File.Exists(path));
            errors = (List<string>)manager.GetType().GetMethod("Execute")!.Invoke(manager, ["", user, null, null, null, null, null])!;
            Assert.Empty(errors);
            Assert.True(File.Exists(path));
            Assert.True(Value<bool>(manager, "CompilationCacheHit"));
            Assert.Equal(0, Value<double>(manager, "EmitTimeMs"));
            Assert.True(Value<double>(manager, "ConstructionTimeMs") > 0);
            Assert.True(Value<double>(manager, "LoadTimeMs") >= 0);
        }
        finally { ((IDisposable)manager).Dispose(); if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ConflictingUserAssembly_IsReturnedAsExecutionDiagnostic()
    {
        var root = Path.Combine(Path.GetTempPath(), "s1-conflict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var name = "ConflictFixture_" + Guid.NewGuid().ToString("N");
        try
        {
            var path = Path.Combine(root, name + ".dll");
            WriteLibrary(path, 1, name);
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
            var p = new ScriptParameters();
            var manager = Activator.CreateInstance(Engine("ScriptLifecycleManager"))!;
            try
            {
                var errors = (List<string>)manager.GetType().GetMethod("Execute")!.Invoke(manager,
                    [p.GeneratedCode, p.ScriptCode, root, null, null, null, p.AssemblyReferences.Concat([name]).ToArray()])!;
                Assert.Contains(errors, e => e.Contains("加载脚本程序集失败") && e.Contains("冲突"));
            }
            finally { ((IDisposable)manager).Dispose(); }
        }
        finally { Directory.Delete(root, true); }
    }
}
