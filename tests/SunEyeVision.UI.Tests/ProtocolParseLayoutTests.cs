using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolParse.Views;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ProtocolParseLayoutTests
{
    [Theory]
    [InlineData("Text", 2)]
    [InlineData("Bytes", 2)]
    [InlineData("Bytes", 30)]
    public void Compact_list_modes_theme_and_row_actions(string mode, int count)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var parameters = new ProtocolParseParameters { Mode = mode, Outputs = new() };
                for (var i = 0; i < count; i++) parameters.Outputs.Add(new() { Name = "out" + i });
                var editor = new ProtocolParseEditor(); editor.SetParameters(parameters);
                Assert.Equal(new Size(520, 600), editor.PreferredDebugWindowSize);
                ResourceDictionary? previous = null;
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    if (previous != null) editor.Resources.MergedDictionaries.Remove(previous);
                    previous = new() { Source = new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml", UriKind.Relative) };
                    editor.Resources.MergedDictionaries.Add(previous);
                    Layout(editor);
                    var grid = (DataGrid)editor.FindName("OutputsGrid");
                    Assert.DoesNotContain(Descendants<TextBlock>(editor), text => text.Text.Contains("模块结果") || text.Text.Contains("位置包含结束"));
                    Assert.Contains(Descendants<TextBlock>(editor), text => text.ToolTip is string tip && tip.Contains("模块结果"));
                    var viewport = (Grid)editor.FindName("OutputsListViewport");
                    var add = (Button)editor.FindName("AddFieldButton");
                    Assert.Equal(0, grid.MinHeight); Assert.False(grid.CanUserSortColumns);
                    Assert.InRange(add.TranslatePoint(new Point(), viewport).Y - grid.ActualHeight, 3, 5);
                    Assert.True(add.TranslatePoint(new Point(), viewport).Y + add.ActualHeight <= viewport.ActualHeight + 1);
                    Assert.Equal(mode == "Bytes" ? Visibility.Visible : Visibility.Collapsed, grid.Columns[3].Visibility);
                    if (mode == "Bytes")
                    {
                        if (count == 2) Assert.Equal(0, Descendants<ScrollViewer>(grid).First().ScrollableWidth);
                        Assert.True(grid.Columns[2].ActualWidth >= 86);
                        Assert.False(((Expander)editor.FindName("AdvancedSettings")).IsExpanded);
                        Assert.DoesNotContain(Descendants<TextBlock>(editor), text => text.Text == "第一个字节编号");
                        Assert.Equal(7, grid.Columns.Count);
                    }
                    var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
                    Assert.Equal(((SolidColorBrush)editor.FindResource(row.IsSelected ? "ThemeTableRowSelectedBackgroundBrush" : "ThemeTableRowBackgroundBrush")).Color, ((SolidColorBrush)row.Background).Color);
                    Assert.All(Descendants<DataGridCell>(row), cell => Assert.Equal(Colors.Transparent, ((SolidColorBrush)cell.Background).Color));
                    foreach (var combo in Descendants<ComboBox>(row))
                        Assert.Equal(((SolidColorBrush)editor.FindResource("ThemeInputBackgroundBrush")).Color, ((SolidColorBrush)combo.Background).Color);
                    if (count == 2)
                    {
                        Assert.InRange(grid.ActualHeight, 90, 145);
                        Save(editor, $"parse-{mode.ToLowerInvariant()}-{theme.ToLowerInvariant()}.png");
                    }
                    else Assert.True(Descendants<ScrollViewer>(grid).First().ScrollableHeight > 0);
                }
                var outputs = (DataGrid)editor.FindName("OutputsGrid");
                var second = parameters.Outputs[1]; var id = second.FieldId;
                var secondRow = (DataGridRow)outputs.ItemContainerGenerator.ContainerFromIndex(1);
                Descendants<Button>(secondRow).Single(b => Equals(b.Content, "↑")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(editor); Assert.Same(second, parameters.Outputs[0]); Assert.Equal(id, second.FieldId);
                var firstRow = (DataGridRow)outputs.ItemContainerGenerator.ContainerFromIndex(0);
                Assert.False(Descendants<Button>(firstRow).Single(b => Equals(b.Content, "↑")).IsEnabled);
                ((Button)editor.FindName("AddFieldButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(count + 1, parameters.Outputs.Count); Assert.Same(parameters.Outputs.Last(), outputs.SelectedItem);
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Fact]
    public void Advanced_settings_follow_declared_input_type_ascii_and_selected_field()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var parameters = new ProtocolParseParameters { Mode = "Bytes", Outputs = new()
                    { new() { Name = "number", DataType = "int" }, new() { Name = "text", DataType = "string" } } };
                var editor = new ProtocolParseEditor(); editor.SetParameters(parameters);
                var advanced = (Expander)editor.FindName("AdvancedSettings");
                Assert.False(advanced.IsExpanded); advanced.IsExpanded = true; Layout(editor);
                Assert.Equal(Visibility.Visible, ((Grid)editor.FindName("InputFormatRow")).Visibility);
                Assert.Equal(Visibility.Visible, ((Grid)editor.FindName("NumericRow")).Visibility);
                var fields = (ComboBox)editor.FindName("SelectedFieldOptions");
                fields.SelectedItem = parameters.Outputs[1];
                Assert.Same(parameters.Outputs[1], ((DataGrid)editor.FindName("OutputsGrid")).SelectedItem);
                Assert.Equal(Visibility.Collapsed, ((Grid)editor.FindName("NumericRow")).Visibility);
                var source = new SunEyeVision.Plugin.SDK.Execution.Parameters.AvailableDataSource
                { SourceNodeId = Guid.NewGuid().ToString(), SourceNodeName = "接收数据", PropertyName = "payload",
                    DisplayName = "payload", LeafDisplayName = "payload",
                    PropertyType = typeof(SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload[]), VariableType = "Binary" };
                editor.AvailableDataSources.Add(source); parameters.InputText = source.GetBindingPath();
                Assert.Equal(Visibility.Collapsed, ((Grid)editor.FindName("InputFormatRow")).Visibility);
                Assert.Equal(Visibility.Visible, ((Grid)editor.FindName("EncodingRow")).Visibility);
                parameters.AsciiData = true;
                Assert.Equal(Visibility.Collapsed, ((Grid)editor.FindName("EncodingRow")).Visibility);
                fields.SelectedItem = parameters.Outputs[0];
                Assert.Equal(Visibility.Collapsed, ((Grid)editor.FindName("NumericRow")).Visibility);
                parameters.AsciiData = false;
                Assert.Equal(Visibility.Visible, ((Grid)editor.FindName("NumericRow")).Visibility);
                parameters.Outputs[0].NumericInterpretation = "Signed";
                parameters.Outputs.Move(0, 1);
                Assert.Equal("Signed", parameters.Outputs[1].NumericInterpretation);
                Layout(editor);
                Save(editor, "parse-advanced.png");
                var previousOutputs = parameters.Outputs;
                parameters.Outputs = new() { new() { Name = "replacement", DataType = "int" } };
                Layout(editor);
                Assert.Same(parameters.Outputs[0], fields.SelectedItem);
                Assert.Same(parameters.Outputs[0], ((DataGrid)editor.FindName("OutputsGrid")).SelectedItem);
                previousOutputs.Clear();
                Assert.Same(parameters.Outputs[0], fields.SelectedItem);
                Assert.Equal(Visibility.Visible, ((Grid)editor.FindName("NumericRow")).Visibility);
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Layout(FrameworkElement editor)
    {
        editor.Measure(new Size(520, 514)); editor.Arrange(new Rect(0, 0, 520, 514)); editor.UpdateLayout();
        editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); editor.UpdateLayout();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Save(FrameworkElement editor, string name)
    {
        var bitmap = new RenderTargetBitmap(520, 514, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(root, "SunEyeVision.slnx"))) root = System.IO.Directory.GetParent(root)!.FullName;
        var directory = System.IO.Path.Combine(root, "artifacts", "validation", "protocol-assemble-layout-20261012");
        System.IO.Directory.CreateDirectory(directory);
        using var file = System.IO.File.Create(System.IO.Path.Combine(directory, name)); encoder.Save(file);
    }
}
