using SunEyeVision.Plugin.Editing.Wpf;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Windows;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Tool.SendData.Views;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class SendDataProductionEditorTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Solution Document;
        public readonly SunEyeVision.UI.Shared.WorkflowNode Node;
        public readonly GlobalVariable Variable = new("发送值", "DESIGN", "String") { Id = "send-target" };
        public readonly MainWindowViewModel Main;
        public readonly SendDataEditor Editor;
        public readonly DefaultDebugWindow Window;
        public readonly ProductionEditorContext Context;
        public readonly DataSourceQueryService Query;
        public readonly SolutionSession Session;
        public readonly string Directory;
        private readonly object? _base = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        public SendDataParameters Draft => Assert.IsType<SendDataParameters>(Context.DraftNode.Parameters);
        public SendDataParameters Formal => Assert.IsType<SendDataParameters>(Node.Parameters);
        public Fixture()
        {
            if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var host = typeof(SendDataProductionEditorTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(a => a.Key == "HostDebugRoot").Value!;
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", Path.TrimEndingDirectorySeparator(Path.GetFullPath(host)) + Path.DirectorySeparatorChar);
            ToolRegistry.RegisterTool(typeof(SendDataTool));
            Directory = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? Path.Combine(host, "..", "..", "_validation", "GlobalBinding"), "send-editor-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            ServiceInitializer.InitializeServices(); ServiceInitializer.InitializeSolutionManager(Directory);
            Node = new("send", "发送", "发送", "SendData") { Parameters = new SendDataParameters() };
            var flow = new SunEyeVision.Workflow.Workflow("send-flow", "正式流程"); flow.Nodes.Add(Node);
            Document = new Solution { Workflows = new() { flow } }; Document.GlobalVariables.Add(Variable);
            ServiceInitializer.SolutionManager.SetCurrentSolution(Document, false);
            Main = new MainWindowViewModel(); Session = ServiceInitializer.SolutionManager.Session!;
            // The real startup path must load the already-open document, not only a default empty tab.
            // Do not call private loaders or mark a scope loaded to conceal a missing engine definition.
            Assert.Same(Document, Session.Document);
            var actualTab = Assert.Single(Main.WorkflowTabViewModel.Tabs);
            Assert.Equal(flow.Id, actualTab.RootWorkflowId); Assert.Equal(flow.Id, actualTab.CurrentWorkflowId);
            Assert.Same(actualTab, Main.WorkflowTabViewModel.SelectedTab);
            Assert.Same(flow.Nodes, actualTab.WorkflowNodes);
            Assert.Same(Node, Assert.Single(actualTab.WorkflowNodes));
            var tool = new SendDataTool(); Editor = Assert.IsType<SendDataEditor>(tool.CreateDebugControl(tool));
            Window = new DefaultDebugWindow(Editor) { Left = -10000, Top = -10000, ShowActivated = false };
            // Exercise the production Main factory, not a hand-written substitute for its service wiring.
            var args = new object?[] { Window, Node, null };
            Context = Assert.IsType<ProductionEditorContext>(typeof(MainWindowViewModel).GetMethod("CreateProductionEditorContext", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Main, args));
            Query = Assert.IsType<DataSourceQueryService>(args[2]);
            Assert.Equal(flow.Id, Query.WorkflowId); Assert.Same(Node, Context.FormalNode);
            var initializer = (IToolControlInitializer)Window;
            initializer.SetCurrentNode(Context.DraftNode); initializer.SetParameters(Draft); initializer.SetDataProvider(Query);
            Window.Show(); Window.UpdateLayout();
            Assert.True(Editor.IsLoaded); Assert.NotNull(PresentationSource.FromVisual(Editor));
        }
        public SendDataRow SelectVariable(string name = "value")
        {
            Draft.DestinationKind = "Variable";
            var row = Draft.Rows[0]; row.Name = name; row.DataType = "String"; row.TargetId = Variable.Id; row.TargetPath = "全局." + Variable.Name; row.InputText = "CURRENT";
            Assert.True(Editor.TryValidateEditor(out var error), error);
            return row;
        }
        public void Dispose() { Context.Discard(); Window.Close(); Main.CloseResultPresentation(); AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", _base); }
    }

    [Fact]
    public void Real_send_window_uses_one_private_source_and_variable_candidate_then_one_history_and_saved_endpoint()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); var initial = f.Session.History.Position; var row = f.SelectVariable();
        Assert.Equal("Device", f.Formal.DestinationKind); Assert.Null(f.Variable.ProducerBinding); Assert.Equal("DESIGN", f.Variable.Value);
        var candidate = f.Context.GetCommunicationConfiguration();
        var projected = candidate.Workflows.Single(w => w.Id == "send-flow").Nodes.Single(n => n.Id == f.Node.Id);
        Assert.Same(f.Draft, projected.Parameters); Assert.Same(f.Context.DraftNode.ParamSettings, projected.ParamSettings);
        Assert.NotNull(candidate.GlobalVariables.Single().ProducerBinding);
        var plan = f.Query.CommunicationSendService!.PrepareAsync(new("Variable", "", "",
            new[] { new CommunicationSendInput(row.FieldId, f.Variable.Id, "CURRENT", "String") }, new(), f.Node.Id, "send-flow")).GetAwaiter().GetResult();
        Assert.Equal(1, plan.SubmitAsync(default).GetAwaiter().GetResult());
        Assert.Null(f.Variable.ProducerBinding); Assert.Equal(initial, f.Session.History.Position);
        Assert.True(ToolEditorCommit.TryCommit(f.Editor, out var error), error);
        Assert.Equal(initial + 1, f.Session.History.Position); Assert.Equal("Variable", f.Formal.DestinationKind);
        var expression = f.Variable.ProducerBinding!.ReferenceExpression;
        var endpoint = f.Variable.ProducerBinding.SourceReference!;
        Assert.Equal("send-flow", endpoint.ScopeId); Assert.Equal(f.Node.Id, endpoint.NodeId);
        Assert.Equal(row.FieldId, f.Formal.Rows[0].FieldId); Assert.Equal("DESIGN", f.Variable.Value);
        Assert.NotSame(f.Formal, f.Draft);
        f.Main.UndoCommand.Execute(null); Assert.Equal(initial, f.Session.History.Position);
        Assert.Equal("Device", f.Formal.DestinationKind); Assert.Null(f.Variable.ProducerBinding);
        f.Main.RedoCommand.Execute(null); Assert.Equal(initial + 1, f.Session.History.Position);
        Assert.Equal(expression, f.Variable.ProducerBinding!.ReferenceExpression); Assert.Equal("Variable", f.Formal.DestinationKind);
        var path = Path.Combine(f.Directory, "send-accepted.solution");
        var saved = ServiceInitializer.SolutionManager.SaveCurrentDocument(path); Assert.True(saved.FileWritten, saved.Error);
        var reopened = new SolutionRepository().Load(path)!;
        Assert.Equal(expression, reopened.GlobalVariables.Single().ProducerBinding!.ReferenceExpression);
        var reopenedParameters = Assert.IsType<SendDataParameters>(reopened.Workflows.Single().Nodes.Single().Parameters);
        Assert.Equal("Variable", reopenedParameters.DestinationKind); Assert.Equal(row.FieldId, reopenedParameters.Rows[0].FieldId);
        Assert.Equal("CURRENT", reopenedParameters.Rows[0].InputText); Assert.Equal("DESIGN", reopened.GlobalVariables.Single().Value);
    });

    [Fact]
    public void Invalid_private_rows_do_not_partially_publish_and_mode_change_unbinds_in_the_same_history()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); var initial = f.Session.History.Position; var row = f.SelectVariable();
        f.Draft.Rows.Add(new SendDataRow { Name = "duplicate", TargetId = f.Variable.Id, DataType = "String", InputText = "SECOND" });
        Assert.False(ToolEditorCommit.TryCommit(f.Editor, out _));
        Assert.Equal(initial, f.Session.History.Position); Assert.Null(f.Variable.ProducerBinding);
        Assert.Equal("Device", f.Formal.DestinationKind); Assert.Equal(2, f.Draft.Rows.Count);
        f.Draft.Rows.RemoveAt(1);
        Assert.True(ToolEditorCommit.TryCommit(f.Editor, out var error), error);
        var expression = f.Variable.ProducerBinding!.ReferenceExpression;
        f.Draft.DestinationKind = "Device";
        Assert.True(f.Editor.TryValidateEditor(out error), error);
        Assert.Equal(expression, f.Variable.ProducerBinding!.ReferenceExpression);
        Assert.Equal("Variable", f.Formal.DestinationKind);
        Assert.Null(f.Context.GetCommunicationConfiguration().GlobalVariables.Single().ProducerBinding);
        Assert.True(ToolEditorCommit.TryCommit(f.Editor, out error), error);
        Assert.Equal(initial + 2, f.Session.History.Position); Assert.Null(f.Variable.ProducerBinding);
        Assert.Equal("Device", f.Formal.DestinationKind);
        f.Main.UndoCommand.Execute(null);
        Assert.Equal("Variable", f.Formal.DestinationKind); Assert.Equal(expression, f.Variable.ProducerBinding!.ReferenceExpression);
    });

    [Fact]
    public void Closing_real_send_window_cancels_private_mapping_and_rejects_old_debug_plan()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); var initial = f.Session.History.Position; var row = f.SelectVariable();
        var service = f.Query.CommunicationSendService!;
        var plan = service.PrepareAsync(new("Variable", "", "", new[] { new CommunicationSendInput(row.FieldId, f.Variable.Id, "CURRENT", "String") }, new(), f.Node.Id, "send-flow")).GetAwaiter().GetResult();
        f.Window.Close(); Assert.False(f.Context.IsActive); Assert.Null(f.Variable.ProducerBinding);
        Assert.Equal("Device", f.Formal.DestinationKind); Assert.Equal(initial, f.Session.History.Position);
        Assert.Throws<InvalidOperationException>(() => service.GetTargets());
        Assert.Throws<InvalidOperationException>(() => plan.SubmitAsync(default).GetAwaiter().GetResult());
        Assert.False(f.Context.TryCommitEditor(out _));
    });

    [Fact]
    public void Accepted_variable_notification_preserves_newer_private_rows_and_mapping_for_next_atomic_apply()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); var initial = f.Session.History.Position; f.SelectVariable("first");
        var changed = false;
        PropertyChangedEventHandler onChanged = (_, e) =>
        {
            if (changed || e.PropertyName != nameof(GlobalVariable.ProducerBinding)) return;
            changed = true; f.Draft.Rows[0].Name = "newer";
            Assert.True(f.Editor.TryValidateEditor(out var message), message);
        };
        f.Variable.PropertyChanged += onChanged;
        try { Assert.True(ToolEditorCommit.TryCommit(f.Editor, out var error), error); }
        finally { f.Variable.PropertyChanged -= onChanged; }
        Assert.True(changed); Assert.Equal("first", f.Formal.Rows[0].Name);
        Assert.Equal("newer", f.Draft.Rows[0].Name); Assert.True(f.Context.HasPendingChanges);
        Assert.Same(f.Draft, f.Context.GetCommunicationConfiguration().Workflows.Single().Nodes.Single().Parameters);
        Assert.Equal(initial + 1, f.Session.History.Position);
        Assert.True(ToolEditorCommit.TryCommit(f.Editor, out var nextError), nextError);
        Assert.Equal(initial + 2, f.Session.History.Position); Assert.Equal("newer", f.Formal.Rows[0].Name);
        Assert.Equal("newer", f.Variable.ProducerBinding!.ResultDisplayName);
    });
}
