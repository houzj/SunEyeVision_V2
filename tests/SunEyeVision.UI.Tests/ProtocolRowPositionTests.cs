using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.Tool.ProtocolAssemble.Views;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolParse.Views;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ProtocolRowPositionTests
{
    [Theory]
    [InlineData("Text", 4)]
    [InlineData("Bytes", 4)]
    [InlineData("Assemble", 4)]
    [InlineData("Text", 30)]
    [InlineData("Bytes", 30)]
    [InlineData("Assemble", 30)]
    public void Repeated_moves_add_delete_and_recycled_rows_keep_actual_ordinals(string mode, int count)
    {
        OnSta(() =>
        {
            FrameworkElement editor;
            DataGrid grid;
            Action<int, int> move;
            Action add, delete;
            if (mode == "Assemble")
            {
                var p = new ProtocolAssembleParameters { Parts = new() };
                for (var i = 0; i < count; i++) p.Parts.Add(new() { InputText = i.ToString() });
                var view = new ProtocolAssembleEditor(); view.SetParameters(p); editor = view;
                grid = (DataGrid)view.FindName("PartsGrid"); move = p.Parts.Move;
                add = () => p.Parts.Add(new()); delete = () => p.Parts.RemoveAt(1);
            }
            else
            {
                var p = new ProtocolParseParameters { Mode = mode, Outputs = new() };
                for (var i = 0; i < count; i++) p.Outputs.Add(new() { Name = "out" + i });
                var view = new ProtocolParseEditor(); view.SetParameters(p); editor = view;
                grid = (DataGrid)view.FindName("OutputsGrid"); move = p.Outputs.Move;
                add = () => p.Outputs.Add(new()); delete = () => p.Outputs.RemoveAt(1);
            }
            Layout(editor); AssertRows(grid);
            // Exercise actual row buttons, not only collection moves.
            for (var repeat = 0; repeat < 8; repeat++)
            {
                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(1);
                var item = row.Item;
                Descendants<Button>(row).Single(b => Equals(b.Content, "↑")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(editor); Assert.Same(item, grid.Items[0]); AssertRows(grid);
                row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
                Descendants<Button>(row).Single(b => Equals(b.Content, "↓")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(editor); Assert.Same(item, grid.Items[1]); AssertRows(grid);
            }
            move(0, count - 1); Layout(editor);
            Descendants<ScrollViewer>(grid).First().ScrollToBottom(); Layout(editor); AssertRows(grid);
            var last = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(count - 1);
            Assert.NotNull(last); Assert.False(Descendants<Button>(last).Single(b => Equals(b.Content, "↓")).IsEnabled);
            add(); Layout(editor); AssertRows(grid);
            delete(); Layout(editor); AssertRows(grid);
            Descendants<ScrollViewer>(grid).First().ScrollToTop(); Layout(editor); AssertRows(grid);
        });
    }

    [Fact]
    public void Reordering_keeps_field_identity_text_mapping_changes_byte_mapping_does_not()
    {
        var first = new ProtocolParseField { Name = "out0", DataType = "string", Start = 0, End = 0 };
        var second = new ProtocolParseField { Name = "out1", DataType = "string", Start = 1, End = 1 };
        var id = first.FieldId;
        var p = new ProtocolParseParameters { InputText = "A,B", Outputs = new() { first, second } };
        Assert.Equal("A", Read(new ProtocolParseTool().Run(p), first));
        p.Outputs.Move(0, 1);
        Assert.Equal(id, first.FieldId); Assert.Equal("B", Read(new ProtocolParseTool().Run(p), first));
        p.Mode = "Bytes"; p.InputText = "41 42";
        Assert.Equal("A", Read(new ProtocolParseTool().Run(p), first));
        p.Outputs.Move(1, 0);
        Assert.Equal("A", Read(new ProtocolParseTool().Run(p), first));
    }
    private static string Read(CommunicationToolResults result, ProtocolParseField field)
    {
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return Assert.Single(Assert.IsType<string[]>(result.GetOutputVariables().Single(v => v.Name == field.OutputDefinition().BindingKey).RuntimeValue));
    }
    private static void AssertRows(DataGrid grid)
    {
        for (var i = 0; i < grid.Items.Count; i++)
        {
            if (grid.ItemContainerGenerator.ContainerFromIndex(i) is not DataGridRow row) continue;
            Assert.Equal(i, ProtocolRowPosition.GetIndex(row));
            var cell = grid.Columns[0].GetCellContent(row);
            Assert.NotNull(cell);
            Assert.Equal((i + 1).ToString(), Descendants<TextBlock>(cell).Single().Text);
            Assert.Equal(i > 0, Descendants<Button>(row).Single(b => Equals(b.Content, "↑")).IsEnabled);
            Assert.Equal(i < grid.Items.Count - 1, Descendants<Button>(row).Single(b => Equals(b.Content, "↓")).IsEnabled);
        }
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
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
