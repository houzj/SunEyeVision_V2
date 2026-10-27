using System.Reflection;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolParse.Views;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ProductionEditorContextTests
{
    [Fact]
    public void Group_candidate_is_private_and_document_acceptance_rebinds_then_discard_unregisters()
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var child = new SunEyeVision.Workflow.Workflow("child", "child") { IsSubroutine = true };
        var node = new WorkflowNodeBase("group", "group", "group", "Subroutine")
        { SubroutineId = child.Id, Parameters = new SubroutineParameters { SubroutineId = child.Id } };
        flow.Nodes.Add(node);
        var model = new Solution(); model.Workflows.Add(flow); model.Workflows.Add(child);
        var editing = (DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[] { model }, null)!;
        WorkflowNodeBase? rebound = null;
        using var context = new ProductionEditorContext(editing.Host, flow.Id, node, () => true,
            draft => rebound = draft, documentEdits: editing);
        var original = (SubroutineParameters)node.Parameters;
        var pending = (SubroutineParameters)context.DraftNode.Parameters;
        pending.Inputs.Add(new() { Name = "input", TypeName = "Integer" });
        pending.LoopConfig.LoopInterval = 25;
        Assert.Empty(original.Inputs); Assert.Equal(0, original.LoopConfig.LoopInterval);
        Assert.True(editing.HasPendingChanges); Assert.Equal(1, editing.RegisteredScopeCount);
        editing.ValidateAll();
        Assert.Same(original, node.Parameters); Assert.Null(rebound);
        editing.AcceptAll();
        var accepted = (SubroutineParameters)node.Parameters;
        Assert.Single(accepted.Inputs); Assert.Equal(25, accepted.LoopConfig.LoopInterval);
        Assert.Same(context.DraftNode, rebound); Assert.NotSame(accepted, rebound!.Parameters);
        ((SubroutineParameters)rebound.Parameters).LoopConfig.LoopInterval = 99;
        context.Discard();
        Assert.Equal(25, accepted.LoopConfig.LoopInterval); Assert.Equal(0, editing.RegisteredScopeCount);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("identity")]
    [InlineData("type")]
    [InlineData("interval")]
    public void Invalid_Group_candidate_never_publishes(string fault)
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var node = new WorkflowNodeBase("group", "group", "group", "Subroutine") { Parameters = new SubroutineParameters() };
        flow.Nodes.Add(node); var model = new Solution(); model.Workflows.Add(flow);
        var host = new NodeConfigurationEditHost(model);
        using var context = new ProductionEditorContext(host, flow.Id, node, () => true, _ => { });
        var candidate = (SubroutineParameters)context.DraftNode.Parameters;
        candidate.Inputs.Add(new() { Name = "one", TypeName = "Integer" });
        candidate.Inputs.Add(new() { Name = "two", TypeName = "Integer" });
        if (fault == "name") candidate.Inputs[1].Name = "one";
        if (fault == "identity") candidate.Inputs[1].FieldId = candidate.Inputs[0].FieldId;
        if (fault == "type") candidate.Inputs[1].TypeName = "not-a-type";
        if (fault == "interval") candidate.LoopConfig.LoopInterval = -1;
        Assert.False(context.TryCommitEditor(out var error)); Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Empty(((SubroutineParameters)node.Parameters).Inputs);
        Assert.Same(candidate, context.DraftNode.Parameters); Assert.False(host.CanUndo);
    }

    [Fact]
    public void Real_Group_control_rebinds_private_draft_and_standard_confirm_publishes() => Sta(() =>
    {
        var root = typeof(ProductionEditorContextTests).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .Single(a => a.Key == "HostDebugRoot").Value!;
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(System.IO.Path.Combine(root,
            "plugins", "SunEyeVision.Tool.Subroutine", "SunEyeVision.Tool.Subroutine.dll"));
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var node = new WorkflowNodeBase("group", "group", "group", "Subroutine") { Parameters = new SubroutineParameters() };
        flow.Nodes.Add(node); var model = new Solution(); model.Workflows.Add(flow);
        var host = new NodeConfigurationEditHost(model);
        ISubroutineParameterEditor? parameterEditor = null;
        using var context = new ProductionEditorContext(host, flow.Id, node, () => true,
            draft => parameterEditor!.BindParameters((SubroutineParameters)draft.Parameters, null));
        var control = (UserControl)Activator.CreateInstance(assembly.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineDebugControl", true)!,
            new object?[] { "child", "Group", "group", null, null, context.DraftNode.Parameters, null })!;
        parameterEditor = Assert.IsAssignableFrom<ISubroutineParameterEditor>(control);
        var window = new SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow(control);
        try
        {
            Assert.Same(control, window.ParameterControl); Assert.Null(window.EditorControl);
            ToolEditorCommit.AttachDraftContext(control, context);
            var oldViewModel = control.DataContext;
            ((SubroutineParameters)context.DraftNode.Parameters).LoopConfig.LoopInterval = 12;
            Assert.Equal(0, ((SubroutineParameters)node.Parameters).LoopConfig.LoopInterval);
            Assert.True(ToolEditorCommit.TryCommit(control, out var error), error);
            Assert.Equal(12, ((SubroutineParameters)node.Parameters).LoopConfig.LoopInterval);
            Assert.NotSame(oldViewModel, control.DataContext);
            ((SubroutineParameters)context.DraftNode.Parameters).LoopConfig.LoopInterval = 88;
            ToolEditorCommit.DiscardPendingEdits(control);
            Assert.Equal(12, ((SubroutineParameters)node.Parameters).LoopConfig.LoopInterval);
        }
        finally { parameterEditor.Dispose(); window.Close(); }
    });

    private static (Solution Model, NodeConfigurationEditHost Host, WorkflowNodeBase Node) Model()
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var node = new WorkflowNodeBase("parse", "parse", "parse", "ProtocolParse")
        { Parameters = new ProtocolParseParameters { InputText = "first,second" } };
        flow.Nodes.Add(node); var model = new Solution(); model.Workflows.Add(flow);
        return (model, new NodeConfigurationEditHost(model), node);
    }
    private static ProductionEditorContext Attach(NodeConfigurationEditHost host, WorkflowNodeBase node, ToolDebugControlBase control, Func<bool>? current = null)
    {
        void Bind(WorkflowNodeBase draft) { control.SetCurrentNode(draft); control.SetParameters(draft.Parameters); }
        var context = new ProductionEditorContext(host, "flow", node, current ?? (() => true), Bind);
        Bind(context.DraftNode); ToolEditorCommit.AttachDraftContext(control, context); return context;
    }
    [Fact]
    public void Real_protocol_editor_edits_private_parameters_and_commit_rebinds_another_private_copy() => Sta(() =>
    {
        var f = Model(); var editor = new ProtocolParseEditor(); using var context = Attach(f.Host, f.Node, editor);
        var draft = (ProtocolParseParameters)context.DraftNode.Parameters; draft.Separator = ";";
        draft.Outputs[0].Name = "renamed";
        Assert.Equal(",", ((ProtocolParseParameters)f.Node.Parameters).Separator);
        Assert.True(ToolEditorCommit.TryCommit(editor, out var error), error);
        Assert.Equal(";", ((ProtocolParseParameters)f.Node.Parameters).Separator);
        Assert.Equal("renamed", ((ProtocolParseParameters)f.Node.Parameters).Outputs[0].Name);
        Assert.NotSame(f.Node.Parameters, context.DraftNode.Parameters);
        ((ProtocolParseParameters)context.DraftNode.Parameters).Separator = "|";
        Assert.Equal(";", ((ProtocolParseParameters)f.Node.Parameters).Separator);
    });
    [Fact]
    public void Discard_and_close_never_publish_or_invoke_live_output_callback() => Sta(() =>
    {
        var f = Model(); var editor = new ProtocolParseEditor(); using var context = Attach(f.Host, f.Node, editor);
        var calls = 0; editor.SetDataProvider(new DataSourceQueryService(f.Model.Workflows[0], f.Model.Workflows[0]) { CommitOutputConfiguration = _ => calls++ });
        ((ProtocolParseParameters)context.DraftNode.Parameters).Separator = ";";
        var owner = new Window();
        Assert.True(ToolEditorClose.TryClose(editor, owner, _ => throw new Exception("Draft close must not ask to commit.")));
        Assert.Equal(",", ((ProtocolParseParameters)f.Node.Parameters).Separator); Assert.Equal(0, calls);
        Assert.False(ToolEditorCommit.TryCommit(editor, out _)); Assert.False(f.Host.CanUndo);
    });
    private sealed class RejectingEditor : ToolDebugControlBase
    {
        public override bool ValidateParameters() => false;
        protected override object ExecuteTool() => new SunEyeVision.Plugin.SDK.Execution.Results.GenericToolResults();
    }
    [Fact]
    public void Confirm_validation_failure_precedes_formal_commit_and_preserves_draft() => Sta(() =>
    {
        var f = Model(); var editor = new RejectingEditor(); using var context = Attach(f.Host, f.Node, editor);
        ((ProtocolParseParameters)context.DraftNode.Parameters).Separator = ";";
        Assert.False(ToolEditorCommit.TryCommit(editor, out _));
        Assert.Equal(",", ((ProtocolParseParameters)f.Node.Parameters).Separator);
        Assert.Equal(";", ((ProtocolParseParameters)context.DraftNode.Parameters).Separator); Assert.False(f.Host.CanUndo);
    });
    [Fact]
    public void Same_local_id_in_other_document_and_recreated_node_cannot_publish()
    {
        var a = Model(); var b = Model();
        Assert.Throws<InvalidOperationException>(() => new ProductionEditorContext(a.Host, "flow", b.Node, () => true, _ => { }));
        var current = true; using var context = new ProductionEditorContext(a.Host, "flow", a.Node, () => current, _ => { });
        ((ProtocolParseParameters)context.DraftNode.Parameters).Separator = ";";
        current = false; Assert.False(context.TryCommitEditor(out _)); current = true;
        a.Model.Workflows[0].Nodes[0] = new WorkflowNodeBase("parse", "parse", "parse", "ProtocolParse") { Parameters = a.Node.Parameters };
        Assert.False(context.TryCommitEditor(out _)); Assert.Equal(",", ((ProtocolParseParameters)a.Node.Parameters).Separator);
    }
    [Fact]
    public void Invalid_literal_and_output_contract_are_validated_on_candidate_without_publishing()
    {
        var f = Model(); using var context = new ProductionEditorContext(f.Host, "flow", f.Node, () => true, _ => { });
        var p = (ProtocolParseParameters)context.DraftNode.Parameters;
        p.Mode = "Bytes"; p.InputFormat = "Hex"; p.InputText = "XX";
        Assert.False(context.TryValidateEditor(out _)); Assert.False(context.TryCommitEditor(out _));
        Assert.Equal("Text", ((ProtocolParseParameters)f.Node.Parameters).Mode); Assert.False(f.Host.CanUndo);
        p.Mode = "Text"; p.InputText = "valid"; p.Outputs[0].DataType = "unsupported";
        Assert.False(context.TryValidateEditor(out _)); Assert.False(f.Host.CanUndo);
    }
    [Fact]
    public void Scoped_open_editor_commit_uses_formal_instance_across_documents_with_same_id() => Sta(() =>
    {
        var a = Model(); var b = Model(); var ae = new ProtocolParseEditor(); var be = new ProtocolParseEditor();
        using var ac = Attach(a.Host, a.Node, ae); using var bc = Attach(b.Host, b.Node, be);
        var aw = new Window { Content = ae, ShowActivated = false, Left = -10000, Width = 640, Height = 600 };
        var bw = new Window { Content = be, ShowActivated = false, Left = -10000, Width = 640, Height = 600 };
        try
        {
            aw.Show(); bw.Show(); ae.UpdateLayout(); be.UpdateLayout();
            ((ProtocolParseParameters)ac.DraftNode.Parameters).Separator = ";";
            ((ProtocolParseParameters)bc.DraftNode.Parameters).Separator = "|";
            Assert.True(ToolEditorCommit.TryCommitOpenEditors(id => id == "parse", out var error, node => ReferenceEquals(node, a.Node)), error);
            Assert.Equal(";", ((ProtocolParseParameters)a.Node.Parameters).Separator);
            Assert.Equal(",", ((ProtocolParseParameters)b.Node.Parameters).Separator);
            Assert.Equal("|", ((ProtocolParseParameters)bc.DraftNode.Parameters).Separator);
        }
        finally { ToolEditorCommit.DiscardPendingEdits(ae); ToolEditorCommit.DiscardPendingEdits(be); aw.Close(); bw.Close(); }
    });
    [Fact]
    public void Published_commit_stays_successful_when_rebind_fails_and_closed_context_never_reactivates()
    {
        var f = Model(); var callbacks = 0;
        using var context = new ProductionEditorContext(f.Host, "flow", f.Node, () => true,
            _ => throw new InvalidOperationException("view retired"), _ => callbacks++);
        ((ProtocolParseParameters)context.DraftNode.Parameters).Separator = ";";
        Assert.True(context.TryCommitEditor(out var error), error);
        Assert.Equal(";", ((ProtocolParseParameters)f.Node.Parameters).Separator);
        Assert.Equal("view retired", context.LastRefreshError); Assert.Equal(0, callbacks);
        Assert.False(context.TryCommitEditor(out _));
    }
    [Fact]
    public void Closing_during_rebind_suppresses_pending_view_notification()
    {
        var f = Model(); ProductionEditorContext? context = null; var callbacks = 0;
        context = new(f.Host, "flow", f.Node, () => true, _ => context!.Discard(), _ => callbacks++);
        using (context)
        {
            ((ProtocolParseParameters)context.DraftNode.Parameters).Separator = ";";
            Assert.True(context.TryCommitEditor(out _)); Assert.Equal(0, callbacks);
            Assert.False(context.TryCommitEditor(out _));
        }
    }    private sealed class NumericModel { public int Threshold { get; set; } = 128; }
    private static (NumericUpDown Numeric, TextBox Text, Window Window, StackPanel Root, NumericModel Model) NumericFixture()
    {
        var model = new NumericModel();
        var numeric = new NumericUpDown { Minimum = 0, Maximum = 255, DecimalPlaces = 0, ShowSlider = false };
        numeric.SetBinding(NumericUpDown.IntValueProperty, new System.Windows.Data.Binding(nameof(NumericModel.Threshold))
        { Source = model, Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        var root = new StackPanel(); root.Children.Add(numeric);
        var window = new Window { Content = root, Width = 400, Height = 200, Left = -10000, ShowActivated = false };
        window.Show(); window.UpdateLayout(); numeric.ApplyTemplate();
        var text = Assert.IsType<TextBox>(numeric.Template.FindName("PART_TextBox", numeric));
        return (numeric, text, window, root, model);
    }
    [Fact]
    public void Real_numeric_template_pending_181_and_173_flush_without_focus_loss() => Sta(() =>
    {
        var f = NumericFixture();
        try
        {
            foreach (var value in new[] { 181, 173 })
            {
                f.Text.Text = value.ToString(); var previous = f.Model.Threshold;
                Assert.NotEqual(value, previous);
                Assert.True(ToolEditorCommit.TryValidate(f.Root, out var error), error);
                Assert.Equal(previous, f.Model.Threshold);
                Assert.True(ToolEditorCommit.TryCommit(f.Root, out error), error);
                Assert.Equal(value, f.Model.Threshold);
            }
        }
        finally { f.Window.Close(); }
    });
    [Fact]
    public void Invalid_numeric_blocks_save_without_rewrite_and_keeps_pending_text() => Sta(() =>
    {
        var f = NumericFixture();
        var validationRoot = Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(
            typeof(ProductionEditorContextTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "HostDebugRoot").Value!,
            "..", "..", "_validation", "NodeEditing"));
        var directory = System.IO.Path.Combine(validationRoot, "numeric-save");
        System.IO.Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, Guid.NewGuid().ToString("N") + ".solution");
        System.IO.File.WriteAllText(path, "original");
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc); System.IO.File.SetLastWriteTimeUtc(path, stamp);
        var sha = System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path));
        try
        {
            foreach (var invalid in new[] { "not-int", "999", "1.5", "NaN", "Infinity" })
            {
                f.Text.Text = invalid;
                f.Text.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                Assert.False(f.Numeric.TryValidatePendingEdit(out _));
                if (ToolEditorCommit.TryValidate(f.Root, out _) && ToolEditorCommit.TryCommit(f.Root, out _))
                    System.IO.File.WriteAllText(path, f.Model.Threshold.ToString());
                Assert.Equal(invalid, f.Text.Text); Assert.Equal(128, f.Model.Threshold);
                Assert.Equal(stamp, System.IO.File.GetLastWriteTimeUtc(path));
                Assert.Equal(sha, System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path)));
            }
        }
        finally { f.Numeric.DiscardPendingEdit(); f.Window.Close(); }
    });
    [Fact]
    public void Every_numeric_is_validated_before_any_numeric_flush_and_current_culture_is_kept() => Sta(() =>
    {
        var f = NumericFixture();
        try
        {
            var other = new NumericUpDown { Minimum = 0, Maximum = 255, ShowSlider = false };
            f.Root.Children.Add(other); f.Window.UpdateLayout(); other.ApplyTemplate();
            var text = Assert.IsType<TextBox>(other.Template.FindName("PART_TextBox", other));
            f.Text.Text = "181"; text.Text = "bad";
            Assert.False(ToolEditorCommit.TryCommit(f.Root, out _)); Assert.Equal(128, f.Model.Threshold); Assert.Equal("181", f.Text.Text);
            text.Text = "2"; Assert.True(ToolEditorCommit.TryCommit(f.Root, out var error), error);
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
                other.DecimalPlaces = 2; text.Text = "1,75";
                Assert.True(other.TryCommitPendingEdit(out error), error); Assert.Equal(1.75, other.Value);
            }
            finally { Thread.CurrentThread.CurrentCulture = culture; }
        }
        finally { f.Numeric.DiscardPendingEdit(); f.Window.Close(); }
    });    [Fact]
    public void Actual_config_setting_numeric_bridge_flushes_pending_and_preserves_invalid_text() => Sta(() =>
    {
        var model = new NumericModel();
        var config = new ConfigSetting { ParameterName = "Threshold", DataType = typeof(int), Minimum = 0, Maximum = 255 };
        config.SetBinding(ConfigSetting.ValueProperty, new System.Windows.Data.Binding(nameof(NumericModel.Threshold))
        { Source = model, Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        var window = new Window { Content = config, Width = 480, Height = 200, Left = -10000, ShowActivated = false };
        try
        {
            window.Show(); window.UpdateLayout(); config.ApplyTemplate();
            var numeric = Assert.IsType<NumericUpDown>(config.Template.FindName("PART_NumericEditor", config));
            numeric.ApplyTemplate(); var text = Assert.IsType<TextBox>(numeric.Template.FindName("PART_TextBox", numeric));
            text.Text = "181"; Assert.Equal(128, model.Threshold);
            Assert.True(ToolEditorCommit.TryCommit(config, out var error), error); Assert.Equal(181, model.Threshold);
            text.Text = "not-int"; text.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Assert.False(ToolEditorCommit.TryValidate(config, out _)); Assert.False(ToolEditorCommit.TryCommit(config, out _));
            Assert.Equal(181, model.Threshold); Assert.Equal("not-int", text.Text);
            numeric.DiscardPendingEdit();
        }
        finally { window.Close(); }
    });    [Fact]
    public void Stale_context_cannot_overwrite_accepted_node_and_keeps_its_private_draft()
    {
        var f = Model();
        using var first = new ProductionEditorContext(f.Host, "flow", f.Node, () => true, _ => { });
        using var stale = new ProductionEditorContext(f.Host, "flow", f.Node, () => true, _ => { });
        var original = f.Node.Parameters;
        ((ProtocolParseParameters)first.DraftNode.Parameters).Separator = ";";
        ((ProtocolParseParameters)stale.DraftNode.Parameters).Separator = "|";
        Assert.True(first.TryCommitEditor(out var error), error);
        Assert.NotSame(original, f.Node.Parameters);
        Assert.False(stale.TryValidateEditor(out _)); Assert.False(stale.TryCommitEditor(out _));
        Assert.Equal(";", ((ProtocolParseParameters)f.Node.Parameters).Separator);
        Assert.Equal("|", ((ProtocolParseParameters)stale.DraftNode.Parameters).Separator);
        Assert.Equal(",", ((ProtocolParseParameters)original).Separator);
    }    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
