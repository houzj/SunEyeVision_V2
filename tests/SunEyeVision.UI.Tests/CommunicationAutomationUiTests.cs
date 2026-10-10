using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Themes.Base;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationAutomationUiTests
{
    [Fact]
    public void Navigation_pages_have_confirmed_layout_bindings_theme_and_shared_solution_sync()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            CommunicationManagerViewModel? manager = null;
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "SunEyeAutomationUi", Guid.NewGuid().ToString("N"));
                ServiceInitializer.InitializeSolutionManager(directory);
                var solutions = new SolutionManager(directory);
                var device = new Communication { ProtocolKey = "modbus", IsEnabled = false };
                var point = new ModbusAddressPoint { StartAddress = 40, Count = 4 }; device.ModbusAddressPoints.Add(point);
                var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id,
                    Outputs = new() { new() { Name = "in1", DataType = "string" } },
                    AssemblyOutputs = new() { new() { Name = "out1", DataType = "string" } } };
                var flow = new SunEyeVision.Workflow.Workflow { Name = "流程1" };
                var solution = new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, Workflows = new() { flow },
                    CommunicationTriggers = new() { new() { Source = "String", Text = "OLD" } } };
                solutions.SetCurrentSolution(solution);
                manager = new CommunicationManagerViewModel(solutions);
                manager.AddEventTriggerCommand.Execute(null); manager.AddStringTriggerCommand.Execute(null);
                var rule = Assert.Single(manager.EventTriggers); rule.Definition.EventId = receive.EventId;
                rule.Definition.TargetId = flow.Id; rule.Definition.FieldId = receive.AssemblyOutputs[0].FieldId;
                manager.StringTriggers[0].Definition.Text = "GO";
                var triggerView = new CommunicationTriggersView { DataContext = manager };
                triggerView.Resources[ThemeKeys.TextPrimaryBrush] = Brushes.White;
                triggerView.Resources[ThemeKeys.AccentBrush] = Brushes.DeepSkyBlue;
                triggerView.Measure(new Size(1300, 750)); triggerView.Arrange(new Rect(0, 0, 1300, 750));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var tabs = Descendants(triggerView).OfType<TabControl>().Single();
                Assert.Equal(2, tabs.Items.Count);
                var eventGrid = (DataGrid)triggerView.FindName("EventTriggerGrid");
                var stringGrid = (DataGrid)triggerView.FindName("StringTriggerGrid");
                Assert.Equal(6, eventGrid.Columns.Count); Assert.Equal(6, stringGrid.Columns.Count);
                var header = Descendants(triggerView).OfType<TabItem>().First();
                Assert.Same(Brushes.White, header.Foreground);
                Assert.Contains(rule.Fields, field => field.Id == receive.Outputs[0].FieldId && field.Name == "in1");
                Assert.Contains(rule.ConditionSources, field => field.SourceFieldId == receive.Outputs[0].FieldId);
                var parameter = Descendants(triggerView).OfType<SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding>().Single();
                Assert.True(parameter.SyncEditingToSource);
                Assert.True(parameter.IsEnabled);
                Assert.Empty(Descendants(triggerView).OfType<SunEyeVision.Plugin.SDK.UI.Controls.ToggleSwitch>());
                Assert.Equal(receive.Outputs[0].FieldId, rule.ComparisonOutputId);
                Assert.Equal("GlobalData", manager.StringTriggers[0].Definition.Source);
                Assert.Equal(new[] { "等于", "不等于", "包含", "不检查内容" }, manager.StringTriggers[0].MatchModes);
                Assert.DoesNotContain(stringGrid.Columns, c => Equals(c.Header, "来源设备"));
                Assert.NotEmpty(manager.GlobalRuleLoadNotice);
                Assert.DoesNotContain(Descendants(triggerView).OfType<Button>(),
                    button => button.Content?.ToString()?.Contains("迁移") == true);
                Assert.DoesNotContain(rule.Targets, t => t.Name.Contains("编号"));
                Assert.Same(rule.ConditionSources, parameter.AvailableDataSources);
                parameter.BindingSource = "START";
                Assert.Equal("START", rule.Definition.TriggerParameterSource);
                parameter.BindingSource = rule.ConditionSources.Single().GetBindingPath();
                Assert.Equal(parameter.BindingSource, rule.TriggerParameterSource);
                Assert.DoesNotContain(Descendants(triggerView).OfType<Button>(), b => Equals(b.Content, "绑定…"));
                Assert.Equal("触发参数", eventGrid.Columns[4].Header);
                Assert.DoesNotContain(eventGrid.Columns, c => Equals(c.Header, "判断输出（等于）"));
                var outputSelector = Descendants(triggerView).OfType<ComboBox>().Single(c => c.Name == "ComparisonOutputSelector");
                Assert.Equal(Visibility.Collapsed, outputSelector.Visibility);
                var receiveRow = manager.ReceiveEvents.Single();
                var additionalOutput = new CommunicationEventOutputViewModel { Name = "in2", DataType = "string" };
                receiveRow.Outputs.Add(additionalOutput);
                manager.RefreshAutomationChoices();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Visible, outputSelector.Visibility);
                Assert.Equal(receive.Outputs[0].FieldId, rule.ComparisonOutputId);
                receiveRow.Outputs.Remove(additionalOutput);
                manager.RefreshAutomationChoices();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Collapsed, outputSelector.Visibility);
                rule.Definition.ComparisonOutputId = Guid.NewGuid();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Visible, outputSelector.Visibility);
                rule.Definition.ComparisonOutputId = receive.Outputs[0].FieldId;
                Assert.Equal(280, eventGrid.Columns[3].Width.Value);
                Assert.All(Descendants(triggerView).OfType<Button>().Where(b => Equals(b.Content, "×")), b => Assert.Equal(new Thickness(0), b.Padding));
                manager.RefreshAutomationChoices();
                Assert.Equal(receive.EventId, rule.Definition.EventId);
                Assert.Equal(flow.Id, rule.Definition.TargetId);
                Assert.Equal(receive.AssemblyOutputs[0].FieldId, rule.Definition.FieldId);
                Assert.Equal(2, solution.CommunicationTriggers.Count);
                Assert.Equal("GO", solution.CommunicationTriggers.Single(r => r.Source == "GlobalData").Text);

                var response = manager.Responses[1];
                var target = response.Targets.Single(t => t.Children.Count > 0).Children.Single();
                response.SelectTarget(target);
                Assert.Equal(point.Id, response.Definition.TargetId);
                Assert.Contains(".1 ", response.TargetLabel);
                Assert.Equal("int", response.Definition.DataType);
                response.Definition.Content = "0"; response.Definition.AlternateContent = "1,2";
                var responseView = new CommunicationResponsesView { DataContext = manager };
                responseView.Resources[ThemeKeys.TextPrimaryBrush] = Brushes.White;
                responseView.Measure(new Size(1000, 900)); responseView.Arrange(new Rect(0, 0, 1000, 900));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(3, manager.Responses.Count);
                var panels = Descendants(responseView).OfType<StackPanel>()
                    .Where(panel => panel.Name == "ResponseConfigurationPanel").ToArray();
                Assert.Equal(3, panels.Length);
                Assert.All(panels, panel => Assert.Equal(Visibility.Collapsed, panel.Visibility));
                var body = panels.Single(panel => ReferenceEquals(panel.DataContext, response));
                var responseToggle = Descendants(responseView).OfType<SunEyeVision.Plugin.SDK.UI.Controls.ToggleSwitch>()
                    .Single(toggle => ReferenceEquals(toggle.DataContext, response));
                responseToggle.IsChecked = true;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Visible, body.Visibility);
                Assert.True(solution.CommunicationResponses.WorkflowState.Enabled);
                Assert.All(panels.Where(panel => panel != body), panel => Assert.Equal(Visibility.Collapsed, panel.Visibility));
                Assert.Equal("1,2", solution.CommunicationResponses.WorkflowState.AlternateContent);
                Assert.False(manager.HasUnsavedChanges);
                Assert.DoesNotContain(Descendants(responseView).OfType<Button>(), button =>
                    Equals(button.Content, "保存配置") || Equals(button.Content, "预览"));
                response.Definition.DataType = "string";
                response.Definition.Terminator = "\r\n";
                response.Definition.DataType = "int";
                Assert.Equal("", response.Definition.Terminator);
                response.IsTargetOpen = true;
                responseToggle.IsChecked = false;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Collapsed, body.Visibility);
                Assert.False(response.IsTargetOpen);
                Assert.Equal("1,2", response.Definition.AlternateContent);
                Assert.Equal(point.Id, response.Definition.TargetId);
                responseToggle.IsChecked = true;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Visible, body.Visibility);
                Assert.Equal("1,2", response.Definition.AlternateContent);
                foreach (var input in Descendants(triggerView).OfType<TextBox>())
                    Assert.Equal(VerticalAlignment.Center, input.VerticalContentAlignment);
                manager.RemoveAutomationTrigger(rule);
                Assert.Single(solution.CommunicationTriggers);
                triggerView.Resources[ThemeKeys.TextPrimaryBrush] = Brushes.Black;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Same(Brushes.Black, header.Foreground);
            }
            catch (Exception ex) { failure = ex; }
            finally { manager?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "触发/响应 UI 测试超时");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
