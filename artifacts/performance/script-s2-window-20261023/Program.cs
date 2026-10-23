using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

internal static class WindowProbe
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object value, string name) => value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
    private static void Set(object value, string name, object? field) => value.GetType().GetProperty(name)!.SetValue(value, field);
    private static object? Call(object value, string name, params object?[] args) => value.GetType().GetMethod(name)!.Invoke(value, args);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    [STAThread]
    private static int Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
        var lineCount = int.Parse(args[2]); var rounds = int.Parse(args[3]);
        var result = new Dictionary<string, object?> { ["root"] = root, ["lineCount"] = lineCount,
            ["scope"] = "actual ScriptDebugControl in DefaultDebugWindow; no product App/device/services/workflow",
            ["inputProtocol"] = "WPF routed TextInput to actual AvalonEdit TextArea; no OS keyboard injection",
            ["ctrlSpaceProtocol"] = "candidate routed command with CtrlSpace InputGesture; baseline absent command uses explicit-query compatibility hook and is marked unsupported",
            ["samples"] = new List<object>() };
        var clock = Stopwatch.StartNew();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var file = Path.Combine(root, name.Name + ".dll");
            if (!File.Exists(file)) file = Directory.EnumerateFiles(Path.Combine(root, "plugins"), name.Name + ".dll", SearchOption.AllDirectories).FirstOrDefault() ?? file;
            return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
        };
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(90) };
        watchdog.Tick += (_, _) => { result["success"] = false; result["timeout"] = true; Save(); app.Shutdown(2); };
        watchdog.Start();
        app.DispatcherUnhandledException += (_, e) => { result["dispatcherError"] = e.Exception.ToString(); e.Handled = true; Save(); app.Shutdown(3); };
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                var toolFile = Path.Combine(root, "plugins", "SunEyeVision.Tool.Script", "SunEyeVision.Tool.Script.dll");
                var tool = AssemblyLoadContext.Default.LoadFromAssemblyPath(toolFile);
                var toolType = tool.GetType("SunEyeVision.Tool.Script.ScriptTool")!;
                // 复刻插件发现触发的基础预热，立即继续开窗，不人为等待预热完成。
                Activator.CreateInstance(toolType);
                var controlType = tool.GetType("SunEyeVision.Tool.Script.Views.ScriptDebugControl")!;
                var parameterType = tool.GetType("SunEyeVision.Tool.Script.Models.ScriptParameters")!;
                var sdk = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, "SunEyeVision.Plugin.SDK.dll"));
                var shellType = sdk.GetType("SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow")!;
                result["binaryHashes"] = new[] { toolFile, Path.Combine(root, "CommonControl.dll"), Path.Combine(root, "SunEyeVision.Plugin.SDK.dll") }
                    .ToDictionary(Path.GetFileName, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
                for (var round = 0; round < rounds; round++)
                {
                    var sample = new Dictionary<string, object?> { ["round"] = round };
                    var before = clock.Elapsed.TotalMilliseconds;
                    var previousTick = before; double maxGap = 0;
                    var heartbeat = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(10) };
                    heartbeat.Tick += (_, _) => { var now = clock.Elapsed.TotalMilliseconds; maxGap = Math.Max(maxGap, now - previousTick); previousTick = now; };
                    heartbeat.Start();
                    var parameters = Activator.CreateInstance(parameterType)!;
                    var body = "using System;\nusing SunEyeVision.Plugin.SDK.Scripting;\npublic partial class UserScript : Script\n{\n public override void Init() {}\n public override void Process()\n {\n  string name = \"abc\";\n  var result = name/*caret*/;\n }\n}";
                    var source = string.Concat(Enumerable.Repeat("// padding\n", Math.Max(1, lineCount - body.Split('\n').Length))) + body;
                    var caret = source.IndexOf("/*caret*/", StringComparison.Ordinal); source = source.Replace("/*caret*/", "");
                    Set(parameters, "ScriptCode", source);
                    sample["sourceSha256"] = Hash(source);
                    sample["references"] = ((IEnumerable)Get(parameters, "AssemblyReferences")).Cast<string>().ToArray();
                    var control = (UserControl)Activator.CreateInstance(controlType)!;
                    Call(control, "SetParameters", parameters);
                    var core = control.FindName("EditorCore");
                    var editor = Get(core, "Editor");
                    var textArea = (UIElement)Get(editor, "TextArea");
                    var initialization = clock.Elapsed.TotalMilliseconds;
                    await (Task)Call(control, "InitializeAsync")!;
                    sample["initializeMs"] = clock.Elapsed.TotalMilliseconds - initialization;
                    var semanticProperty = core.GetType().GetProperty("IsSemanticReady");
                    double? semanticAt = semanticProperty == null ? clock.Elapsed.TotalMilliseconds - before : null;
                    var semanticTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                    semanticTimer.Tick += (_, _) => { if (semanticAt == null && (bool)semanticProperty!.GetValue(core)!) semanticAt = clock.Elapsed.TotalMilliseconds - before; };
                    if (semanticProperty != null) semanticTimer.Start();
                    var shell = (Window)Activator.CreateInstance(shellType, control)!;
                    shell.Title = "S2 isolated full script window timing";
                    var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    shell.ContentRendered += (_, _) => rendered.TrySetResult();
                    shell.Show(); await rendered.Task;
                    sample["visibleMs"] = clock.Elapsed.TotalMilliseconds - before;
                    shell.Activate(); textArea.Focus();
                    var editorCaret = Get(textArea, "Caret");
                    Set(editorCaret, "Offset", 2);
                    var inputStart = clock.Elapsed.TotalMilliseconds;
                    SendText(textArea, "x");
                    var afterInput = (string)Get(editor, "Text");
                    if (afterInput.Length != source.Length + 1) throw new InvalidOperationException("Actual routed input was not applied");
                    sample["inputMs"] = clock.Elapsed.TotalMilliseconds - before;
                    sample["inputAckMs"] = clock.Elapsed.TotalMilliseconds - inputStart;
                    Set(editorCaret, "Offset", caret + 1);
                    var dotStart = clock.Elapsed.TotalMilliseconds;
                    SendText(textArea, ".");
                    var popup = await WaitPopup(core);
                    sample["dotPopupMs"] = clock.Elapsed.TotalMilliseconds - dotStart;
                    sample["dotCandidates"] = Candidates(popup);
                    if (!Candidates(popup).Contains("Length")) throw new InvalidOperationException("String Length candidate missing");
                    Call(popup, "Close");
                    var completeCommand = core.GetType().GetField("CompleteCommand", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as RoutedCommand;
                    sample["ctrlSpaceBound"] = completeCommand != null;
                    var queries = new List<double>();
                    var stages = new List<object?>();
                    for (var query = 0; query < 5; query++)
                    {
                        var started = clock.Elapsed.TotalMilliseconds;
                        if (completeCommand != null) completeCommand.Execute(null, textArea);
                        else await (Task)core.GetType().GetMethod("ThrottledRefreshAndShow", PrivateInstance)!.Invoke(core, new object[] { '\0', CancellationToken.None })!;
                        popup = await WaitPopup(core);
                        queries.Add(clock.Elapsed.TotalMilliseconds - started);
                        stages.Add(core.GetType().GetProperty("LastCompletionTiming")?.GetValue(core));
                        if (!Candidates(popup).Contains("Length")) throw new InvalidOperationException("Explicit completion candidate missing");
                        Call(popup, "Close");
                    }
                    sample["fiveExplicitPopupMs"] = queries;
                    sample["fiveExplicitStages"] = stages;
                    var deadline = DateTime.UtcNow.AddSeconds(30);
                    while (semanticAt == null && DateTime.UtcNow < deadline) await Task.Delay(10);
                    sample["semanticMs"] = semanticAt;
                    sample["heartbeatMaxGapMs"] = maxGap;
                    Call(core, "LoadCode", source);
                    var compileSamples = new List<object>();
                    for (var compileRound = 0; compileRound < 2; compileRound++)
                    {
                        maxGap = 0; previousTick = clock.Elapsed.TotalMilliseconds;
                        var startCompile = clock.Elapsed.TotalMilliseconds;
                        var asyncCompile = controlType.GetMethod("PreCompileAsync");
                        if (asyncCompile != null)
                        {
                            var work = (Task)asyncCompile.Invoke(control, new object[] { CancellationToken.None })!;
                            await work;
                            var errors = (IEnumerable)work.GetType().GetProperty("Result")!.GetValue(work)!;
                            if (errors.Cast<object>().Any()) throw new InvalidOperationException("Valid source compilation returned errors");
                        }
                        else controlType.GetMethod("PreCompileLatestCode", PrivateInstance)!.Invoke(control, null);
                        var duration = clock.Elapsed.TotalMilliseconds - startCompile;
                        await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        compileSamples.Add(new { durationMs = duration, heartbeatMaxGapMs = maxGap,
                            timings = controlType.GetProperty("LastCompilationTiming")?.GetValue(control),
                            protocol = asyncCompile != null ? "actual async precompile entry" : "baseline synchronous UI precompile compatibility entry" });
                    }
                    sample["compilationSamples"] = compileSamples;
                    heartbeat.Stop(); semanticTimer.Stop();
                    // 编译错误内容也必须保留；关闭本身不应要求Emit成功。
                    Call(core, "LoadCode", source.Replace("var result = name;", "var result = name.;"));
                    var closing = clock.Elapsed.TotalMilliseconds;
                    shell.Close();
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    sample["closeMs"] = clock.Elapsed.TotalMilliseconds - closing;
                    sample["inputPreservedOnClose"] = ((string)Get(parameters, "ScriptCode")) == (string)Get(editor, "Text");
                    ((List<object>)result["samples"]!).Add(sample);
                    Save(); await Task.Delay(30);
                }
                result["success"] = true;
            }
            catch (Exception e) { result["success"] = false; result["error"] = e.ToString(); }
            finally { Save(); watchdog.Stop(); app.Shutdown(); }
        }));
        void Save() { result["totalMs"] = clock.Elapsed.TotalMilliseconds; Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })); }
        return app.Run();
    }
    private static void SendText(UIElement target, string text)
    {
        var composition = new TextComposition(InputManager.Current, target, text);
        target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent });
    }
    private static async Task<object> WaitPopup(object core)
    {
        var field = core.GetType().GetField("_completionWindow", PrivateInstance)!;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var popup = field.GetValue(core);
            if (popup is Window window && window.IsVisible && window.IsLoaded && Candidates(popup).Length > 0) return popup;
            await Task.Delay(5);
        }
        throw new TimeoutException("Actual completion popup did not appear");
    }
    private static string[] Candidates(object popup) => ((IEnumerable)Get(Get(popup, "CompletionList"), "CompletionData"))
        .Cast<object>().Select(item => (string)Get(item, "Text")).ToArray();
}
