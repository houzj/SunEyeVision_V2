using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Tool.SendData.Views;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using Xunit;
using SendTarget = SunEyeVision.Plugin.SDK.Models.Communication.CommunicationSendTarget;

namespace SunEyeVision.UI.Tests;

public sealed class SendEventSelectionTests
{
    [Theory]
    [InlineData("", "Direct", "文本-直接输出")]
    [InlineData("  ", "Assembly", "文本-组装输出")]
    [InlineData("检测结果", "Direct", "检测结果")]
    public async Task Manager_and_workflow_share_default_name_without_rewriting_persisted_name(string name, string mode, string expected)
    {
        var definition = new CommunicationSendEventDefinition { Name = name, Mode = mode, DeviceId = "device" };
        var model = CommunicationSendEventViewModel.FromDefinition(definition);
        Assert.Equal(expected, model.DisplayName); Assert.Equal(name, model.Capture().Name);
        var solution = new Solution { Communications = new() { new Communication { Id = "device", Name = "OPC UA" } }, CommunicationSendEvents = new() { definition } };
        await using var runtime = new CommunicationRuntimeManager(); var service = new WorkflowCommunicationSendService(() => solution, runtime);
        var target = Assert.Single(service.GetTargets().Where(t => t.Kind == "Event"));
        Assert.Equal(expected + " · OPC UA", target.Name); Assert.Equal(definition.EventId.ToString(), target.Id);
        var copy = JsonSerializer.Deserialize<CommunicationSendEventDefinition>(JsonSerializer.Serialize(definition))!;
        Assert.Equal(name, copy.Name); Assert.Equal(definition.EventId, copy.EventId);
    }

    [Fact]
    public async Task Duplicate_names_use_device_then_stable_identity_and_handle_short_id_collision()
    {
        var first = new CommunicationSendEventDefinition { Name = "结果", DeviceId = "a", EventId = new("11111111-0000-0000-0000-000000000001") };
        var second = new CommunicationSendEventDefinition { Name = "结果", DeviceId = "a", EventId = new("11111111-0000-0000-0000-000000000002") };
        var third = new CommunicationSendEventDefinition { Name = "结果", DeviceId = "b" };
        var solution = new Solution { Communications = new() { new Communication { Id = "a", Name = "设备A" }, new Communication { Id = "b", Name = "设备B" } },
            CommunicationSendEvents = new() { first, second, third } };
        await using var runtime = new CommunicationRuntimeManager(); var service = new WorkflowCommunicationSendService(() => solution, runtime);
        var events = service.GetTargets().Where(t => t.Kind == "Event").ToArray();
        Assert.Equal(3, events.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.EndsWith(first.EventId.ToString("N"), events.Single(e => e.Id == first.EventId.ToString()).Name);
        Assert.EndsWith(second.EventId.ToString("N"), events.Single(e => e.Id == second.EventId.ToString()).Name);
        Assert.Equal("结果 · 设备B", events.Single(e => e.Id == third.EventId.ToString()).Name);
        solution.CommunicationSendEvents.Reverse();
        Assert.Equal(events.ToDictionary(e => e.Id, e => e.Name).OrderBy(e => e.Key), service.GetTargets().Where(t => t.Kind == "Event").ToDictionary(e => e.Id, e => e.Name).OrderBy(e => e.Key));
    }

    [Fact]
    public async Task Missing_device_still_has_visible_label_and_deleted_event_is_not_replaced_by_name()
    {
        var removed = new CommunicationSendEventDefinition { Name = "结果", DeviceId = "missing" };
        var replacement = new CommunicationSendEventDefinition { Name = "结果", DeviceId = "missing" };
        var solution = new Solution { CommunicationSendEvents = new() { removed } };
        await using var runtime = new CommunicationRuntimeManager(); var service = new WorkflowCommunicationSendService(() => solution, runtime);
        Assert.Contains("设备已失效", Assert.Single(service.GetTargets()).Name);
        solution.CommunicationSendEvents = new() { replacement };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(new("Event", "", removed.EventId.ToString(),
            new[] { new CommunicationSendInput(Guid.NewGuid(), "", "x", "String") }, new())));
        Assert.Equal(replacement.EventId.ToString(), Assert.Single(service.GetTargets()).Id);
    }

    [Fact]
    public async Task Editor_selects_by_id_loads_parameters_and_preserves_values_across_refresh_and_rename()
    {
        var field = new CommunicationSendParameterDefinition { Name = "in1", DataType = "Double", Content = "TEST" };
        var first = new CommunicationSendEventDefinition { DeviceId = "d" };
        var second = new CommunicationSendEventDefinition { DeviceId = "d", Parameters = new() { field } };
        var solution = new Solution { Communications = new() { new Communication { Id = "d", Name = "OPC UA" } }, CommunicationSendEvents = new() { first, second } };
        await using var runtime = new CommunicationRuntimeManager(); var service = new WorkflowCommunicationSendService(() => solution, runtime);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workflow = new SunEyeVision.Workflow.Workflow("flow", "flow");
                var query = new DataSourceQueryService(workflow, workflow) { CommunicationSendService = service };
                var p = new SendDataParameters { DestinationKind = "Event" };
                var editor = new SendDataEditor(); editor.SetParameters(p); editor.SetDataProvider(query);
                var combo = (ComboBox)editor.FindName("Events"); var grid = (DataGrid)editor.FindName("RowsGrid");
                Assert.Equal(2, combo.Items.Count); Assert.All(combo.Items.Cast<SendTarget>(), t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
                Assert.Equal(Visibility.Collapsed, grid.Visibility);
                combo.SelectedValue = second.EventId.ToString();
                Assert.Equal(second.EventId.ToString(), p.EventId);
                var row = Assert.Single(p.Rows); Assert.Equal("in1", row.Name); Assert.Equal(field.FieldId, row.EventFieldId); Assert.Equal("Double", row.DataType);
                Assert.Equal("", row.InputText); Assert.Equal(Visibility.Visible, grid.Visibility); // Never copy the event's test value.
                row.InputText = "__GlobalVars__.score"; var outputId = row.FieldId;
                second.Name = "重命名"; field.Name = "score";
                typeof(SendDataEditor).GetMethod("RefreshChoices", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, new object?[] { combo, EventArgs.Empty });
                Assert.Equal(second.EventId.ToString(), combo.SelectedValue); Assert.Equal(outputId, p.Rows[0].FieldId);
                Assert.Equal("__GlobalVars__.score", p.Rows[0].InputText); Assert.Contains("重命名", ((SendTarget)combo.SelectedItem).Name);
                Assert.Equal("score", p.Rows[0].Name); Assert.Equal(field.FieldId, p.Rows[0].EventFieldId);
                ResourceDictionary? previous = null;
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    if (previous != null) editor.Resources.MergedDictionaries.Remove(previous);
                    previous = new() { Source = new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml", UriKind.Relative) }; editor.Resources.MergedDictionaries.Add(previous);
                    editor.Measure(new Size(520, 514)); editor.Arrange(new Rect(0, 0, 520, 514)); editor.UpdateLayout();
                    Assert.Equal(second.EventId.ToString(), combo.SelectedValue); Assert.Equal("__GlobalVars__.score", p.Rows[0].InputText);
                    var root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "SunEyeVision.slnx"))) root = Directory.GetParent(root)!.FullName;
                    var directory = Path.Combine(root, "artifacts", "validation", "send-event-selection-20261013"); Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap(520, 514, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, $"editor-{theme.ToLowerInvariant()}.png")); encoder.Save(file);
                }
                var replacement = new CommunicationSendEventDefinition { Name = second.Name, DeviceId = "d" };
                solution.CommunicationSendEvents.Remove(second); solution.CommunicationSendEvents.Add(replacement);
                typeof(SendDataEditor).GetMethod("RefreshChoices", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, new object?[] { combo, EventArgs.Empty });
                Assert.Null(combo.SelectedItem); Assert.Equal(second.EventId.ToString(), p.EventId);
                Assert.Equal("__GlobalVars__.score", p.Rows[0].InputText);
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
