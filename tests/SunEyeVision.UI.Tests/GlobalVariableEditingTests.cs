using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class GlobalVariableEditingTests
{
    private static SolutionManager Manager()
    {
        var root = Path.Combine(Path.GetTempPath(), "SunEye-variable-edit", Guid.NewGuid().ToString("N"));
        return new SolutionManager(Path.Combine(root, "solutions"), Path.Combine(root, "settings.json"), Path.Combine(root, "recovery"), root);
    }
    [Fact]
    public void Variable_window_edits_are_private_and_join_pending_node_in_one_publication() => Sta(() =>
    {
        var manager = Manager(); var model = manager.CreateUntitledSolution();
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var node = new WorkflowNodeBase("parse", "parse", "parse", "ProtocolParse")
        { Parameters = new ProtocolParseParameters { Separator = ",", InputText = "hello" } };
        flow.Nodes.Add(node); model.Workflows.Add(flow);
        var formal = new GlobalVariable("Count", new long[] { 10 }, "Integer"); model.GlobalVariables.Add(formal);
        var host = new NodeConfigurationEditHost(model);
        var vm = new GlobalVariableManagerViewModel(manager, host);
        var variable = Assert.Single(vm.CurrentVariables);
        Assert.NotSame(formal, variable);
        Assert.True(vm.ConfirmVariableRename(variable, "CountSaved", "Count"));
        vm.SetDraftValue(variable, new long[] { 19 }); variable.Description = "private";
        var nodeDraft = host.BeginEdit("flow", "parse");
        ((ProtocolParseParameters)nodeDraft.Node.Parameters).Separator = ";";
        Assert.Equal("Count", formal.Name); Assert.Equal(10, Assert.IsType<long[]>(formal.Value)[0]);
        Assert.Empty(formal.Description); Assert.Equal(",", ((ProtocolParseParameters)node.Parameters).Separator);
        var before = host.Capture().Version;
        host.CommitBatch(new[] { nodeDraft }, vm.Draft);
        Assert.Equal(before + 1, host.Capture().Version);
        Assert.Same(formal, Assert.Single(model.GlobalVariables)); Assert.Equal("CountSaved", formal.Name);
        Assert.Equal(19, Assert.IsType<long[]>(formal.Value)[0]); Assert.Equal(";", ((ProtocolParseParameters)node.Parameters).Separator);
        vm.AcceptPublication(); Assert.NotSame(formal, Assert.Single(vm.CurrentVariables));
    });
    [Fact]
    public void Stale_variable_session_keeps_private_edits_and_cannot_be_reactivated() => Sta(() =>
    {
        var manager = Manager(); var model = manager.CreateUntitledSolution();
        var variable = new GlobalVariable("Count", new long[] { 10 }, "Integer"); model.GlobalVariables.Add(variable);
        var current = true;
        var vm = new GlobalVariableManagerViewModel(manager, new NodeConfigurationEditHost(model), () => current);
        vm.SetDraftValue(Assert.Single(vm.CurrentVariables), new long[] { 19 });
        current = false; Assert.False(vm.TryPrepare(out _)); current = true;
        Assert.False(vm.TryPrepare(out _)); Assert.Equal(10, Assert.IsType<long[]>(variable.Value)[0]);
        Assert.Equal(19, Assert.IsType<long[]>(Assert.Single(vm.CurrentVariables).Value)[0]);
    });
    [Fact]
    public void Disk_failure_after_acceptance_retains_dirty_and_retry_does_not_publish_again() => Sta(() =>
    {
        var manager = Manager(); var model = manager.CreateUntitledSolution();
        var variable = new GlobalVariable("Count", new long[] { 10 }, "Integer"); model.GlobalVariables.Add(variable);
        var path = Path.Combine(manager.DataRoot, "variables.solution");
        Assert.True(manager.SaveCurrentDocument(path).FileWritten); var old = File.ReadAllBytes(path);
        var host = new NodeConfigurationEditHost(model); var vm = new GlobalVariableManagerViewModel(manager, host);
        vm.SetDraftValue(Assert.Single(vm.CurrentVariables), new long[] { 19 });
        host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), vm.Draft); vm.AcceptPublication();
        var version = host.Capture().Version;
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            Assert.False(manager.SaveCurrentDocument().FileWritten); Assert.True(manager.HasUnsavedChanges);
            Assert.Equal(old, File.ReadAllBytes(path)); Assert.Equal(19, Assert.IsType<long[]>(variable.Value)[0]);
        }
        host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), vm.Draft); vm.AcceptPublication();
        Assert.Equal(version, host.Capture().Version);
        Assert.True(manager.SaveCurrentDocument().FileWritten); Assert.False(manager.HasUnsavedChanges);
        var loaded = new SolutionRepository().LoadCandidate(path);
        Assert.False(loaded.HasErrors); Assert.Equal(19, Assert.IsType<long[]>(Assert.Single(loaded.Candidate!.GlobalVariables).Value)[0]);
    });
    [Fact]
    public async Task Concurrent_save_snapshots_never_mix_explicit_batch_values_or_shared_serializer_state()
    {
        var model = new Solution();
        model.GlobalVariables.Add(new GlobalVariable("A0", new long[] { 0 }, "Integer"));
        model.GlobalVariables.Add(new GlobalVariable("B0", new long[] { 0 }, "Integer"));
        var host = new NodeConfigurationEditHost(model);
        var other = new Solution(); other.GlobalVariables.Add(new GlobalVariable("Other", new long[] { -1 }, "Integer"));
        var path = Path.Combine(Path.GetTempPath(), "concurrent.solution");
        var writer = Task.Run(() =>
        {
            for (long i = 1; i <= 40; i++)
            {
                var draft = host.BeginVariableEdit();
                foreach (var v in draft.Configuration.GlobalVariables.Take(2))
                {
                    v.Name = v.Name[..1] + i;
                    draft.SetValue(v.Id, new[] { i });
                }
                draft.Configuration.GlobalVariables.RemoveAll(v => v.Name.StartsWith("C", StringComparison.Ordinal));
                if (i % 2 == 0) draft.Configuration.GlobalVariables.Add(new GlobalVariable("C" + i, new[] { i }, "Integer"));
                host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), draft);
            }
        });
        var reader = Task.Run(() =>
        {
            for (var i = 0; i < 80; i++)
            {
                var json = SolutionSnapshot.Prepare(model, path).Json;
                var restored = System.Text.Json.JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
                var values = restored.GlobalVariables.Select(v => Assert.IsType<long[]>(v.Value)[0]).ToArray();
                Assert.Equal(values[0], values[1]);
                Assert.All(restored.GlobalVariables, v => Assert.Equal(long.Parse(v.Name[1..]), Assert.IsType<long[]>(v.Value)[0]));
            }
        });
        var otherReader = Task.Run(() =>
        {
            for (var i = 0; i < 80; i++)
            {
                var json = SolutionSnapshot.Prepare(other, path).Json;
                var restored = System.Text.Json.JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
                Assert.Equal(-1, Assert.IsType<long[]>(Assert.Single(restored.GlobalVariables).Value)[0]);
            }
        });
        await Task.WhenAll(writer, reader, otherReader).WaitAsync(TimeSpan.FromSeconds(30));
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Variable editor test timed out.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
