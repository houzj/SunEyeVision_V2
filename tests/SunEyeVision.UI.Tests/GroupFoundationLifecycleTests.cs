using System.IO;
using System.Reflection;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Xunit;
using Flow = SunEyeVision.Workflow.Workflow;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class GroupFoundationLifecycleTests
{
    [Fact]
    public async Task Created_and_published_nested_Group_survives_copy_file_reopen_real_execution_delete_and_undo()
    {
        var release = typeof(GroupFoundationLifecycleTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "HostDebugRoot").Value!;
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.Combine(release, "plugins", "SunEyeVision.Tool.Subroutine", "SunEyeVision.Tool.Subroutine.dll"));
        ToolRegistry.RegisterTool(assembly.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool", true)!);
        ToolRegistry.RegisterTool(typeof(ProtocolParseTool));
        PluginLoader.MarkAsLoaded();
        var folder = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")
            ?? throw new InvalidOperationException("Missing validation root"), "foundation-file", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        SolutionManager Manager(string name) => new(Path.Combine(folder, name + "-solutions"),
            Path.Combine(folder, name + "-settings.json"), Path.Combine(folder, name + "-recovery"), folder);
        var logger = VisionLogger.Instance;
        var catalog = new WorkflowEngine(logger);
        var root = catalog.CreateWorkflow("root", "Root");
        var solution = new Solution { Workflows = new() { root } };
        WorkflowNodeBase Group(string id) => new(id, id, id, "Subroutine") { Parameters = new SubroutineParameters() };
        var outer = Group("outer"); var createOuter = new AddGroupNodeCommand(root, outer, catalog, solution); createOuter.Execute(null);
        var middle = createOuter.OwnedWorkflow;
        var inner = Group("inner"); var createInner = new AddGroupNodeCommand(middle, inner, catalog, solution); createInner.Execute(null);
        var leaf = createInner.OwnedWorkflow;
        var parser = new ProtocolParseParameters { InputText = "21", Mode = "Text" }; parser.Outputs[0].DataType = "int";
        leaf.AddNode(new("producer", "producer", "producer", "ProtocolParse") { Parameters = parser });
        var field = parser.Outputs[0].OutputDefinition();
        AvailableDataSource Source(Flow scope, string node, string key, Guid fieldId) => new()
        { SourceWorkflowId = scope.Id, SourceNodeId = node, PropertyName = key, SourceFieldId = fieldId,
          PropertyType = typeof(long), VariableType = "Integer" };
        var manager = Manager("writer"); manager.SetCurrentSolution(solution, registerInCatalog: false);
        var session = manager.Session!;
        using (var editor = new ProductionEditorContext(session.Editing.Host, middle.Id, inner,
            () => ReferenceEquals(manager.Session, session), _ => { }, documentEdits: session.Editing))
        {
            ((SubroutineParameters)editor.DraftNode.Parameters).Outputs.Add(new() { Name = "Value", TypeName = "Integer",
                Source = Source(leaf, "producer", field.BindingKey, field.FieldId), BindingSource = "producer." + field.BindingKey });
            Assert.Empty(((SubroutineParameters)inner.Parameters).Outputs);
            Assert.True(editor.TryCommitEditor(out var error), error);
        }
        var innerOutput = Assert.Single(((SubroutineParameters)inner.Parameters).Outputs);
        using (var editor = new ProductionEditorContext(session.Editing.Host, root.Id, outer,
            () => ReferenceEquals(manager.Session, session), _ => { }, documentEdits: session.Editing))
        {
            ((SubroutineParameters)editor.DraftNode.Parameters).Outputs.Add(new() { Name = "Final", TypeName = "Integer",
                Source = Source(middle, inner.Id, "Value", innerOutput.FieldId), BindingSource = inner.Id + ".Value" });
            Assert.True(editor.TryCommitEditor(out var error), error);
        }
        var fragment = WorkflowFragmentCopy.Prepare(root, new[] { outer.Id }, root.Id, catalog.GetWorkflowById,
            (original, _) => new WorkflowNodeBase(Guid.NewGuid().ToString("N"), original.Name, original.DispName, original.ToolType));
        var paste = new PasteWorkflowFragmentCommand(root, fragment, catalog, solution); paste.Execute(null);
        var copyId = Assert.Single(fragment.Nodes).Id;
        Assert.Equal(5, solution.Workflows.Count); paste.Undo(); Assert.Equal(3, solution.Workflows.Count);
        paste.Execute(null); Assert.Equal(copyId, Assert.Single(fragment.Nodes).Id); Assert.Equal(5, solution.Workflows.Count);
        var path = Path.Combine(folder, "group.solution");
        var write = manager.SaveCurrentDocument(path); Assert.True(write.FileWritten, write.Error); manager.CloseSolution(saveChanges: false);
        var reader = Manager("reader"); var reopened = reader.OpenSolution(path)!; Assert.NotNull(reopened);
        var reopenedRoot = reopened.Workflows.Single(w => w.Id == root.Id);
        var original = reopenedRoot.Nodes.Single(n => n.Id == outer.Id); var copied = reopenedRoot.Nodes.Single(n => n.Id == copyId);
        var copiedMiddle = reopened.Workflows.Single(w => w.Id == copied.SubroutineId);
        var copiedInner = Assert.Single(copiedMiddle.Nodes);
        var copiedLeaf = reopened.Workflows.Single(w => w.Id == copiedInner.SubroutineId);
        ((ProtocolParseParameters)Assert.Single(copiedLeaf.Nodes).Parameters).InputText = "34";
        Assert.NotEqual(original.SubroutineId, copied.SubroutineId);
        var copiedParameters = (SubroutineParameters)copied.Parameters;
        Assert.Equal(copiedMiddle.Id, Assert.Single(copiedParameters.Outputs).Source!.SourceWorkflowId);
        Assert.Equal(copiedInner.Id, Assert.Single(copiedParameters.Outputs).Source!.SourceNodeId);
        var liveCatalog = new WorkflowEngine(logger); foreach (var flow in reopened.Workflows) liveCatalog.RegisterWorkflow(flow);
        var runner = new WorkflowExecutionEngine(liveCatalog, new PluginManager(logger), logger);
        async Task Run(bool expectCopy)
        {
            using var result = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(reopenedRoot.Id)
                { ExecutionOwner = reopened.ExecutionOwner }).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(result.Success, string.Join(";", result.Errors));
            Assert.Equal(21L, OutputVariableResolver.Resolve(result.NodeResults[original.Id].Result!, "Final"));
            if (expectCopy) Assert.Equal(34L, OutputVariableResolver.Resolve(result.NodeResults[copyId].Result!, "Final"));
            else Assert.False(result.NodeResults.ContainsKey(copyId));
            Assert.Equal(WorkflowExecutionState.Idle, runner.CurrentState);
            Assert.Null(((SubroutineParameters)original.Parameters).ExecuteSubWorkflowAsync);
            Assert.Null(copiedParameters.ExecuteSubWorkflowAsync);
        }
        try
        {
            await Run(true);
            // No external consumer exists in this fixture: graph/owned definitions are real commands;
            // separate binding reconciliation tests cover external subscribers.
            var delete = new DeleteWorkflowItemsCommand(reopenedRoot, new[] { copied }, Array.Empty<WorkflowConnection>(),
                liveCatalog, reopened, () => { }, () => { });
            delete.Execute(null); Assert.Equal(3, reopened.Workflows.Count); Assert.Null(liveCatalog.GetWorkflowById(copiedMiddle.Id));
            await Run(false);
            delete.Undo(); Assert.Equal(5, reopened.Workflows.Count); Assert.Same(copied, reopenedRoot.Nodes.Single(n => n.Id == copyId));
            await Run(true);
            var afterUndo = reader.SaveCurrentDocument(Path.Combine(folder, "after-undo.solution"));
            Assert.True(afterUndo.FileWritten, afterUndo.Error);
        }
        finally { reader.CloseSolution(saveChanges: false); }
    }
}
