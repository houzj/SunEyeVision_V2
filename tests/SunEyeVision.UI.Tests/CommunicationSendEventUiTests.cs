using System.Runtime.ExceptionServices;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationSendEventUiTests
{
    public sealed class EditorContext
    {
        public CommunicationSendEventViewModel? SelectedSendEvent { get; set; }
        public ObservableCollection<CommunicationSendEventViewModel> SendEvents { get; } = new();
        public bool HasSelectedSendEvent => SelectedSendEvent != null;
    }
    [Fact]
    public void Creation_dialog_has_text_only_and_two_output_modes_and_editor_switches_panels()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new CommunicationSendEventDialog();
                Assert.Single(((ComboBox)dialog.FindName("ProcessingSelector")).Items.Cast<object>());
                Assert.Equal(2, ((ComboBox)dialog.FindName("ModeSelector")).Items.Count);
                var model = new CommunicationSendEventViewModel { Mode = "Assembly" };
                var input = new CommunicationSendParameterViewModel { Name = "in1", DataType = "string", Content = "READY" };
                model.Parameters.Add(input);
                var output = new CommunicationAssemblyOutputViewModel { Name = "out1", DataType = "string", AvailableDataSources = model.InputSources };
                model.AssemblyOutputs.Add(output);
                output.ApplyBindingSelection("SendEventInputs.in1", "参数列表.in1");
                var context = new EditorContext { SelectedSendEvent = model }; context.SendEvents.Add(model);
                model.IsMessageExpanded = true;
                var view = new CommunicationSendEventsView { DataContext = context };
                view.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/SunEyeVision.UI;component/Resources/Styles/CommonStyleResources.xaml", UriKind.Relative)
                });
                var textBrush = System.Windows.Media.Brushes.White;
                var accentBrush = System.Windows.Media.Brushes.DeepSkyBlue;
                view.Resources[SunEyeVision.UI.Themes.Base.ThemeKeys.TextPrimaryBrush] = textBrush;
                view.Resources[SunEyeVision.UI.Themes.Base.ThemeKeys.AccentBrush] = accentBrush;
                view.Measure(new Size(955, 760)); view.Arrange(new Rect(0, 0, 955, 760));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var inputs = (FrameworkElement)view.FindName("InputConfigurationPanel");
                var assembly = (FrameworkElement)view.FindName("AssemblyConfigurationPanel");
                var parameters = (DataGrid)view.FindName("SendParametersGrid");
                Assert.Equal(7, parameters.Columns.Count);
                var testColumn = Assert.IsType<DataGridTemplateColumn>(parameters.Columns.Single(column => Equals(column.Header, "测试值")));
                var editor = Assert.Single(VisualDescendants(parameters).OfType<TextBox>(), box =>
                    ReferenceEquals(box.DataContext, input) &&
                    box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "Content");
                editor.SetCurrentValue(TextBox.TextProperty, "UPDATED");
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal("UPDATED", input.Content);
                Assert.Equal(TextAlignment.Center, editor.TextAlignment);
                Assert.Equal(VerticalAlignment.Center, editor.VerticalContentAlignment);
                foreach (var column in parameters.Columns.OfType<DataGridTextColumn>())
                {
                    var resultText = new TextBlock { Style = column.ElementStyle };
                    Assert.Equal(TextAlignment.Center, resultText.TextAlignment);
                    Assert.Equal(VerticalAlignment.Center, resultText.VerticalAlignment);
                }
                var typeSelector = Assert.Single(VisualDescendants(parameters).OfType<ComboBox>(), box => ReferenceEquals(box.DataContext, input));
                Assert.Equal(HorizontalAlignment.Center, typeSelector.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Center, typeSelector.VerticalContentAlignment);
                var sendButton = Assert.IsType<Button>(view.FindName("SendTestButton"));
                var assemblySendButton = Assert.IsType<Button>(view.FindName("AssemblySendTestButton"));
                Assert.Same(view.FindName("SendParametersHeader"), sendButton.Parent);
                Assert.Same(view.FindName("SendAssemblyHeader"), assemblySendButton.Parent);
                Assert.Equal(Dock.Right, DockPanel.GetDock(sendButton));
                Assert.Equal(Dock.Right, DockPanel.GetDock(assemblySendButton));
                Assert.DoesNotContain(VisualDescendants(view).OfType<TextBox>(), box =>
                    box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "SelectedSendEvent.PreviewText");
                Assert.Single(VisualDescendants(view).OfType<TextBox>(), box =>
                    box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "SelectedSendEvent.HistoryText");
                model.IsBusy = true;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.False(sendButton.IsEnabled);
                Assert.False(assemblySendButton.IsEnabled);
                model.IsBusy = false;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.DoesNotContain(VisualDescendants(view).OfType<Button>(), button => Equals(button.Content, "预览") || Equals(button.Content, "保存配置"));
                Assert.Single(VisualDescendants(view).OfType<DataGrid>(), grid => ReferenceEquals(grid.ItemsSource, model.Parameters));
                Assert.Equal(Visibility.Visible, inputs.Visibility);
                Assert.Equal(Visibility.Collapsed, assembly.Visibility);
                var inputTab = (Button)view.FindName("SendInputTab");
                var assemblyTab = (Button)view.FindName("SendAssemblyTab");
                Assert.NotNull(inputTab.Template);
                Assert.Same(accentBrush, inputTab.BorderBrush);
                Assert.Same(textBrush, assemblyTab.Foreground);
                view.Resources[SunEyeVision.UI.Themes.Base.ThemeKeys.TextPrimaryBrush] = System.Windows.Media.Brushes.Black;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Same(System.Windows.Media.Brushes.Black, assemblyTab.Foreground);
                var directSeparator = (ComboBox)view.FindName("DirectSendSeparator");
                var assemblySeparator = (ComboBox)view.FindName("AssemblySendSeparator");
                Assert.False(directSeparator.IsEditable);
                foreach (var option in model.SeparatorOptions)
                {
                    Assert.Equal(option.DisplayName, option.ToString());
                    var label = Assert.IsType<TextBlock>(directSeparator.ItemTemplate.LoadContent());
                    label.DataContext = option;
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(option.DisplayName, label.Text);
                }
                Assert.Equal(8, directSeparator.Items.Count);
                Assert.Equal(";", directSeparator.SelectedValue);
                foreach (var value in new[] { ",", " ", "\r\n", "", "|" })
                {
                    directSeparator.SetCurrentValue(ComboBox.SelectedValueProperty, value);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(value, model.Separator);
                    Assert.Equal(value, assemblySeparator.SelectedValue);
                }
                model.Separator = "::";
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal("::", directSeparator.SelectedValue);
                Assert.Equal(9, directSeparator.Items.Count);
                Assert.Equal(@"\r\n", model.SeparatorOptions.Single(option => option.Value == "\r\n").DisplayName);
                var otherEvent = new CommunicationSendEventViewModel { Separator = " " };
                view.DataContext = new EditorContext { SelectedSendEvent = otherEvent };
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(" ", directSeparator.SelectedValue);
                view.DataContext = context;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal("::", directSeparator.SelectedValue);
                Assert.Equal("::", model.Separator);
                model.IsAssemblyConfigTab = true;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Collapsed, inputs.Visibility);
                Assert.Equal(Visibility.Visible, assembly.Visibility);
                Assert.Equal(Visibility.Visible, assemblySendButton.Visibility);
                Assert.True(assemblySendButton.IsEnabled);
                Assert.Same(accentBrush, assemblyTab.BorderBrush);
                Assert.Same(System.Windows.Media.Brushes.Black, inputTab.Foreground);
                model.Mode = "Direct";
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(Visibility.Visible, inputs.Visibility);
                Assert.Equal(Visibility.Collapsed, assembly.Visibility);
                view.Measure(new Size(955, 760)); view.Arrange(new Rect(0, 0, 955, 760));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Contains(VisualDescendants(directSeparator).OfType<TextBlock>(), block => block.Text == "自定义：::");
                dialog.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "发送事件 UI 初始化超时");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void Send_configuration_syncs_immediately_without_separate_save_or_opcua_dirty_prompt()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            CommunicationManagerViewModel? editor = null;
            var directory = Path.Combine(Path.GetTempPath(), "SunEyeVisionSendSync", Guid.NewGuid().ToString("N"));
            try
            {
                ServiceInitializer.InitializeSolutionManager(directory);
                var manager = new SolutionManager(directory);
                var solution = new Solution();
                manager.SetCurrentSolution(solution);
                var file = Path.Combine(directory, "test.solution");
                Assert.True(manager.SaveSolution(file));
                var saved = File.ReadAllText(file);
                editor = new CommunicationManagerViewModel(manager);
                var definition = new CommunicationSendEventViewModel { Mode = "Assembly" };
                editor.AddSendEvent(definition);
                var parameter = new CommunicationSendParameterViewModel { Name = "in1", DataType = "int", Content = "1" };
                definition.Parameters.Add(parameter);
                parameter.Content = "2";
                var output = new CommunicationAssemblyOutputViewModel { Name = "out1", DataType = "int", AvailableDataSources = definition.InputSources };
                definition.AssemblyOutputs.Add(output);
                output.ApplyBindingSelection("SendEventInputs.in1", "参数列表.in1");
                definition.Separator = ",";
                var shared = Assert.Single(solution.CommunicationSendEvents);
                Assert.Equal("2", Assert.Single(shared.Parameters).Content);
                Assert.Equal(parameter.FieldId, Assert.Single(shared.AssemblyOutputs).SourceFieldId);
                Assert.Equal(",", shared.Separator);
                Assert.False(editor.HasUnsavedChanges);
                var receiveEvent = new CommunicationReceiveEventViewModel();
                Assert.Equal(",", receiveEvent.Separator);
                Assert.Equal(",", definition.Separator);
                Assert.Same(receiveEvent.SeparatorOptions[0], definition.SeparatorOptions[0]);
                receiveEvent.Separator = "::";
                Assert.Equal(9, receiveEvent.SeparatorOptions.Count);
                Assert.Equal(8, definition.SeparatorOptions.Count);
                var dialog = new CommunicationManagerDialog(editor);
                foreach (var name in new[] { "ReceiveParseSeparator", "ReceiveCompareSeparator", "ReceiveAssemblySeparator" })
                {
                    var selector = Assert.IsType<ComboBox>(dialog.FindName(name));
                    Assert.Equal("Value", selector.SelectedValuePath);
                    Assert.NotNull(selector.ItemTemplate);
                    Assert.False(selector.IsEditable);
                }
                Assert.Equal(saved, File.ReadAllText(file));
                parameter.Result = "2"; parameter.Status = "OK";
                definition.AddHistory("2", [50], true);
                Assert.False(editor.HasUnsavedChanges);
                Assert.Equal(saved, File.ReadAllText(file));
                Assert.True(manager.SaveSolution(file));
                var restored = System.Text.Json.JsonSerializer.Deserialize<Solution>(
                    File.ReadAllText(file), WorkflowSerializationOptions.Default)!;
                Assert.Equal("2", Assert.Single(Assert.Single(restored.CommunicationSendEvents).Parameters).Content);
                definition.Parameters.Remove(parameter);
                Assert.Empty(Assert.Single(solution.CommunicationSendEvents).Parameters);
                editor.RemoveSendEvent(definition);
                Assert.Empty(solution.CommunicationSendEvents);
                Assert.False(editor.HasUnsavedChanges);
                dialog.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { editor?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "发送事件共享方案同步超时");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in VisualDescendants(child)) yield return descendant;
        }
    }
}
