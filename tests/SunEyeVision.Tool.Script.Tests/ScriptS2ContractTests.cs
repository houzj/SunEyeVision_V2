using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommonControl;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.Views;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

public class ScriptS2ContractTests
{
    private const string EmptyUser = "public partial class UserScript { public override void Init() {} public override void Process() {} }";
    private static Type Engine(string name) => typeof(ScriptParameters).Assembly.GetType("SunEyeVision.Tool.Script.ScriptEngine." + name)!;
    private static T Property<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
    private static object Compile(string[] parts) => Engine("ScriptCompiler").GetMethod("Compile")!.Invoke(
        Activator.CreateInstance(Engine("ScriptCompiler")), [parts, "S2", null, null])!;
    private static Task CompileAsync(string[] parts, CancellationToken token) => (Task)Engine("ScriptCompiler").GetMethod("CompileAsync")!.Invoke(
        Activator.CreateInstance(Engine("ScriptCompiler")), [parts, "S2", null, null, token])!;
    private static int EmitCount(string key) => (int)Engine("ScriptCompilationCoordinator").GetMethod("GetEmitCount", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [key])!;

    public static IEnumerable<object[]> Types => SdkTypeVocabulary.GetAllTypeOptions().Select(t => new object[] { t.Value })
        .Concat(new[] { "Integer[]", "Double[]", "String[]", "Circle[]", "Point[]", "Float", "Float[]" }.Select(t => new object[] { t }));

    [Theory, MemberData(nameof(Types))]
    public void EveryType_FieldDefaultAndDeclaredClrShapeAgree(string vocabulary)
    {
        var p = new ScriptParameters(); p.Inputs.Clear(); p.Outputs.Clear();
        var input = new ScriptInputConfig { Name = "source", TypeName = vocabulary };
        var output = new ScriptOutputConfig { Name = "target", TypeName = vocabulary };
        p.Inputs.Add(input); p.Outputs.Add(output);
        Assert.True(p.TryPublishIoContract(out var error), error);
        var compiled = Compile([p.GeneratedCode, EmptyUser]);
        Assert.True(Property<bool>(compiled, "Success"), string.Join("\n", Property<List<string>>(compiled, "Diagnostics")));
        var alc = new AssemblyLoadContext("s2-shape-" + Guid.NewGuid(), true);
        try
        {
            using var image = new MemoryStream(Property<byte[]>(compiled, "AssemblyBytes"));
            var type = alc.LoadFromStream(image).GetType("UserScript")!;
            Assert.Equal(input.ParamType, type.GetField("source")!.FieldType);
            Assert.Equal(output.ParamType, type.GetField("target")!.FieldType);
            var instance = Activator.CreateInstance(type)!;
            if (input.ParamType!.IsArray) Assert.Equal(0, ((Array)type.GetField("source")!.GetValue(instance)!).Length);
            var declared = ((IOutputContractProvider)p).GetOutputContract().Single(v => v.Name == "target");
            Assert.Equal(output.ParamType, declared.DeclaredType);
            Assert.Equal(output.ParamType.IsArray, declared.IsCollection);
        }
        finally { alc.Unload(); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(50)]
    public void ZeroAndManyIo_CompileAndRetainIdsAcrossRename(int count)
    {
        var p = new ScriptParameters(); p.Inputs.Clear(); p.Outputs.Clear();
        for (var i = 0; i < count; i++) { p.Inputs.Add(new() { Name = "in" + i, TypeName = "Integer" }); p.Outputs.Add(new() { Name = "out" + i, TypeName = "Circle" }); }
        Assert.True(p.TryPublishIoContract(out _));
        Assert.True(Property<bool>(Compile([p.GeneratedCode, EmptyUser]), "Success"));
        if (count == 0) return;
        var oldId = p.Inputs[0].FieldId;
        p.Inputs[0].Name = "renamed";
        Assert.Equal(oldId, p.Inputs[0].FieldId);
        var before = p.GeneratedCode; var ioIdentity = p.IoIdentity;
        p.Inputs.RemoveAt(0); p.Inputs.Add(new() { Name = "renamed", TypeName = "Integer" });
        Assert.NotEqual(oldId, p.Inputs[^1].FieldId);
        Assert.Equal(before, p.GeneratedCode); Assert.NotEqual(ioIdentity, p.IoIdentity);
    }

    [Theory]
    [InlineData("")] [InlineData("bad name")] [InlineData("class")] [InlineData("@foo")] [InlineData("@class")] [InlineData("Process")]
    public void InvalidDraft_IsRetainedAndCannotBePublished(string name)
    {
        var p = new ScriptParameters(); var valid = p.GeneratedCode;
        p.Inputs[0].Name = name;
        Assert.False(p.TryPublishIoContract(out var error)); Assert.NotEmpty(error);
        Assert.Equal(name, p.Inputs[0].Name);
        Assert.Equal(valid, p.GeneratedCode);
        Assert.False(p.Validate().IsValid);
    }

    [Fact]
    public void CrossDirectionNameAndIdConflicts_Reject_AndBindingOnlyChangesDoNotEmit()
    {
        var p = new ScriptParameters(); var original = p.GeneratedCode;
        var user = p.ScriptCode + "\n//" + Guid.NewGuid();
        var a = Compile([original, user]); var key = Property<string>(a, "CacheKey");
        p.Inputs[0].BindingSource = "node.value"; p.Inputs[0].BindingDisplayText = "new label";
        Assert.Equal(original, p.GeneratedCode);
        var b = Compile([p.GeneratedCode, user]);
        Assert.True(Property<bool>(b, "CacheHit")); Assert.Equal(1, EmitCount(key));
        p.Outputs[0].Name = p.Inputs[0].Name; Assert.False(p.TryPublishIoContract(out _));
        p.Outputs[0].Name = "other"; p.Outputs[0].FieldId = p.Inputs[0].FieldId;
        Assert.False(p.TryPublishIoContract(out _));
    }

    [Fact]
    public void SourceArraysAndValues_UseSameActualFieldShape()
    {
        var input = new ScriptInputConfig { Name = "numbers", Source = new AvailableDataSource { PropertyType = typeof(long[]) } };
        Assert.True(input.ParamType!.IsArray);
        input.BindingSource = "__GlobalVars__.numbers";
        var resolver = Engine("ScriptBindingResolver").GetMethod("Resolve")!;
        var values = (Dictionary<string, object?>)resolver.Invoke(null, [new[] { input }, new Dictionary<string, object?> { ["numbers"] = new long[] { 3, 5 } }, null])!;
        Assert.Equal(input.ParamType, values["numbers"]!.GetType());
        input.TypeName = "Integer";
        values = (Dictionary<string, object?>)resolver.Invoke(null, [new[] { input }, new Dictionary<string, object?> { ["numbers"] = "42" }, null])!;
        Assert.Equal(42L, values["numbers"]);
    }

    [Fact]
    public async Task SingleFlight_AllConsumersShareOneEmit_AndOneCancellationDoesNotCancelOtherWaiter()
    {
        var code = new[] { "public class P" + Guid.NewGuid().ToString("N") + " {}" };
        var gate = (SemaphoreSlim)Engine("ScriptCompilationCoordinator").GetField("CompileSlot", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        await gate.WaitAsync();
        using var cancel = new CancellationTokenSource();
        Task one; Task[] others;
        try
        {
            one = CompileAsync(code, cancel.Token);
            others = Enumerable.Range(0, 8).Select(_ => CompileAsync(code, CancellationToken.None)).ToArray();
            await Task.Delay(100);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await one);
        }
        finally { gate.Release(); }
        await Task.WhenAll(others);
        var result = others[0].GetType().GetProperty("Result")!.GetValue(others[0])!;
        Assert.True(Property<bool>(result, "Success"));
        Assert.Equal(1, EmitCount(Property<string>(result, "CacheKey")));
    }

    [Fact]
    public void InvalidIoOrFailedNewSource_DoesNotExecuteOldCachedSuccess()
    {
        var marker = Path.Combine(Path.GetTempPath(), "s2-no-old-" + Guid.NewGuid().ToString("N"));
        var p = new ScriptParameters { ScriptCode = "public partial class UserScript { public override void Init() {} public override void Process() { System.IO.File.WriteAllText(@\"" + marker + "\", \"ran\"); } }" };
        var tool = new ScriptTool();
        try
        {
            Assert.True(tool.Run(p).IsSuccess); Assert.True(File.Exists(marker)); File.Delete(marker);
            p.Inputs[0].Name = "bad name"; Assert.False(tool.Run(p).IsSuccess); Assert.False(File.Exists(marker));
            p.Inputs[0].Name = "input1"; p.ScriptCode = "not C#";
            Assert.False(tool.Run(p).IsSuccess); Assert.False(File.Exists(marker));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact]
    public void ActualWindow_PendingIoCommitAndSyntaxErrorSave_DoNotRunOldDefinition()
    {
        RunSta(async () =>
        {
            var p = new ScriptParameters(); var control = new ScriptDebugControl(); control.SetParameters(p);
            var core = (ScriptEditorCore)control.FindName("EditorCore");
            await control.InitializeAsync(); Assert.False(core.IsWorkspaceReady);
            var window = new DefaultDebugWindow(control);
            try
            {
                window.Show();
                await WaitUntil(() => core.IsWorkspaceReady);
                var tab = (ScriptVariableTab)control.FindName("InputTabControl"); tab.ShowEditMode();
                var grid = (DataGrid)tab.FindName("EditGrid"); grid.UpdateLayout();
                grid.SelectedItem = p.Inputs[0]; grid.CurrentCell = new DataGridCellInfo(p.Inputs[0], grid.Columns[0]); grid.BeginEdit(); grid.UpdateLayout();
                var box = Descendants(grid).OfType<TextBox>().First(t => t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "Name");
                box.Text = "bad name";
                Assert.False(ToolEditorCommit.TryCommit(control, out _)); Assert.Equal("bad name", p.Inputs[0].Name);
                Assert.False(ToolEditorClose.TryClose(control, window, _ => false));
                grid.UpdateLayout();
                box = Descendants(grid).OfType<TextBox>().First(t => t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "Name");
                box.Text = "input1"; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.True(ToolEditorCommit.TryCommit(control, out var error), error);
                var invalidCode = "this code has errors but must be saved";
                core.LoadCode(invalidCode);
                Assert.True(ToolEditorCommit.TryCommit(control, out error), error);
                Assert.Equal(invalidCode, p.ScriptCode);
            }
            finally { p.Inputs[0].Name = "input1"; window.Close(); }
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { yield return root; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child; }
    private static async Task WaitUntil(Func<bool> predicate)
    { var deadline = DateTime.UtcNow.AddSeconds(30); while (!predicate()) { if (DateTime.UtcNow > deadline) throw new TimeoutException(); await Task.Delay(10); } }
    private static void RunSta(Func<Task> action)
    {
        ScriptTestDispatcher.Run(action, 60);
    }
}
