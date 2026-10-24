using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolParse.Views;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ProductionEditorBoundaryTests
{
    private static (Solution Model, WorkflowNodeBase Node) Model()
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var node = new WorkflowNodeBase("parse", "parse", "parse", "ProtocolParse") { Parameters = new ProtocolParseParameters { InputText = "one" } };
        flow.Nodes.Add(node); var model = new Solution(); model.Workflows.Add(flow); return (model, node);
    }
    private static ProductionEditorContext Context(NodeConfigurationEditHost host, WorkflowNodeBase node, ToolDebugControlBase control)
    {
        var context = new ProductionEditorContext(host, "flow", node, () => true, _ => { });
        control.SetCurrentNode(context.DraftNode); control.SetParameters(context.DraftNode.Parameters);
        ToolEditorCommit.AttachDraftContext(control, context); return context;
    }
    [Fact]
    public void Production_control_tracking_survives_temporary_unload_and_releases_owner_on_close_reopen() => Sta(() =>
    {
        var f = Model(); NodeConfigurationEditHost? host = new(f.Model); var originalHost = host;
        var active = new HashSet<ProductionEditorContext>(); var releaseCount = 0;
        var control = new ProtocolParseEditor(); using var context = Context(host, f.Node, control);
        MainWindowViewModel.TrackProductionEditor(active, context, control, () => { releaseCount++; host = null; });
        var window = new Window { Content = control, ShowActivated = false, Left = -10000, Width = 560, Height = 620 };
        try
        {
            Assert.Single(active); window.Show(); window.UpdateLayout();
            window.Content = null; Assert.Single(active); Assert.True(context.IsActive);
            window.Content = control; window.UpdateLayout(); Assert.Single(active);
            window.Close(); Assert.Empty(active); Assert.False(context.IsActive); Assert.Null(host); Assert.Equal(1, releaseCount);
            context.Discard(); context.Dispose(); Assert.Equal(1, releaseCount);
            ((ProtocolParseParameters)f.Node.Parameters).Separator = ";"; // A legacy edit while no editor owns the old baseline.
            Assert.Throws<InvalidOperationException>(() => originalHost.BeginEdit("flow", "parse"));
            host = new(f.Model); Assert.NotSame(originalHost, host);
            var nextControl = new ProtocolParseEditor(); using var next = Context(host, f.Node, nextControl);
            MainWindowViewModel.TrackProductionEditor(active, next, nextControl, () => { releaseCount++; host = null; });
            Assert.Single(active); next.Discard(); Assert.Empty(active); Assert.Equal(2, releaseCount);
        }
        finally { window.Close(); }
    });
    [Fact]
    public void Production_tracking_releases_discard_initialization_failure_and_session_switch_once() => Sta(() =>
    {
        var f = Model(); var host = new NodeConfigurationEditHost(f.Model); var active = new HashSet<ProductionEditorContext>(); var emptyCount = 0;
        var firstControl = new ProtocolParseEditor(); using var first = Context(host, f.Node, firstControl);
        MainWindowViewModel.TrackProductionEditor(active, first, firstControl, () => emptyCount++);
        MainWindowViewModel.TrackProductionEditor(active, first, firstControl, () => emptyCount++); // Duplicate tracking is idempotent.
        first.Discard(); first.Dispose(); Assert.Empty(active); Assert.Equal(1, emptyCount);
        var secondControl = new ProtocolParseEditor(); using var second = Context(host, f.Node, secondControl);
        MainWindowViewModel.TrackProductionEditor(active, second, secondControl, () => emptyCount++);
        second.Dispose(); // The production initialization catch uses this same release notification.
        Assert.Empty(active); Assert.Equal(2, emptyCount);
        var thirdControl = new ProtocolParseEditor(); using var third = Context(host, f.Node, thirdControl);
        var window = new Window { Content = thirdControl };
        MainWindowViewModel.TrackProductionEditor(active, third, window, () => emptyCount++);
        var fourthControl = new ProtocolParseEditor(); using var fourth = Context(host, f.Node, fourthControl);
        MainWindowViewModel.TrackProductionEditor(active, fourth, fourthControl, () => emptyCount++);
        Assert.Equal(2, active.Count);
        MainWindowViewModel.ReleaseProductionEditorContexts(active); // Exact session-change production hook.
        Assert.Empty(active); Assert.False(third.IsActive); Assert.False(fourth.IsActive); Assert.Equal(3, emptyCount);
        MainWindowViewModel.ReleaseProductionEditorContexts(active); window.Close(); Assert.Equal(3, emptyCount);
    });
    private sealed class IntegerModel
    {
        public long Signed { get; set; } = 128;
        public ulong Unsigned { get; set; } = 128;
    }
    private static void WithInteger(Type type, Action<NumericUpDown, TextBox, IntegerModel, ConfigSetting> action)
    {
        var model = new IntegerModel();
        var config = new ConfigSetting { ParameterName = "Exact", DataType = type, DecimalPlaces = 0,
            Minimum = type == typeof(long) ? (object)long.MinValue : ulong.MinValue,
            Maximum = type == typeof(long) ? (object)long.MaxValue : ulong.MaxValue };
        config.SetBinding(ConfigSetting.ValueProperty, new Binding(type == typeof(long) ? nameof(IntegerModel.Signed) : nameof(IntegerModel.Unsigned))
        { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        var window = new Window { Content = config, ShowActivated = false, Left = -10000, Width = 600, Height = 180 };
        try
        {
            window.Show(); window.UpdateLayout(); config.ApplyTemplate();
            var numeric = Assert.IsType<NumericUpDown>(config.Template.FindName("PART_NumericEditor", config));
            numeric.ApplyTemplate(); var text = Assert.IsType<TextBox>(numeric.Template.FindName("PART_TextBox", numeric));
            Assert.True(numeric.Maximum > 9007199254740992d); // The configured production path really reaches the defect range.
            if (type == typeof(ulong))
            {
                // Legacy ConfigSetting has no visible UInt64 numeric adapter; preserve that boundary.
                Assert.Equal(Visibility.Collapsed, numeric.Visibility); Assert.Equal(0d, numeric.Value); Assert.Equal(128UL, model.Unsigned);
            }
            action(numeric, text, model, config); numeric.DiscardPendingEdit();
        }
        finally { window.Close(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Config_setting_wide_integer_rejects_lossy_and_out_of_clr_range_without_changing_value(bool unsigned) => Sta(() =>
    {
        WithInteger(unsigned ? typeof(ulong) : typeof(long), (numeric, text, model, config) =>
        {
            var originalNumeric = numeric.Value;
            var inputs = unsigned
                ? new[] { "9007199254740993", "18446744073709551615", "18446744073709551616", "-1", "1.000000000000000000000000000000000001" }
                : new[] { "9007199254740993", "9223372036854775807", "9223372036854775808", "-9223372036854775809", "1.000000000000000000000000000000000001" };
            foreach (var input in inputs)
            {
                text.Text = input; Assert.False(numeric.TryValidatePendingEdit(out _));
                Assert.False(ToolEditorCommit.TryCommit(config, out _));
                Assert.Equal(input, text.Text); Assert.Equal(originalNumeric, numeric.Value); Assert.Equal(128L, model.Signed); Assert.Equal(128UL, model.Unsigned);
            }
        });
    });
    [Fact]
    public void Config_setting_wide_integer_preserves_exact_scientific_input_and_current_culture() => Sta(() =>
    {
        WithInteger(typeof(long), (numeric, text, model, config) =>
        {
            text.Text = "9.007199254740992e15";
            Assert.True(ToolEditorCommit.TryCommit(config, out var error), error); Assert.Equal(9007199254740992L, model.Signed);
            text.Text = "9.007199254740993e15"; Assert.False(numeric.TryCommitPendingEdit(out _)); Assert.Equal(9007199254740992L, model.Signed);
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                text.Text = "1,81e2"; Assert.True(ToolEditorCommit.TryCommit(config, out error), error); Assert.Equal(181L, model.Signed);
                text.Text = "1,811e2"; Assert.False(numeric.TryValidatePendingEdit(out _)); Assert.Equal("1,811e2", text.Text); Assert.Equal(181L, model.Signed);
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        });
    });
    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
