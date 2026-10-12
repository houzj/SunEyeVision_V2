using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.Tool.ProtocolAssemble.Views;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ProtocolAssembleLayoutTests
{
    [Fact]
    public void Compact_window_and_add_button_follow_content_sized_grid()
    {
        OnSta(() =>
        {
            var editor = MakeEditor(2); Layout(editor);
            Assert.Equal(new Size(400, 550), editor.PreferredDebugWindowSize);
            var window = new DefaultDebugWindow(editor);
            Assert.Equal(400, window.Width); Assert.Equal(550, window.Height);
            var grid = (DataGrid)editor.FindName("PartsGrid");
            Assert.Equal(0, grid.MinHeight); Assert.False(grid.CanUserSortColumns);
            Assert.InRange(grid.ActualHeight, 90, 125);
            Assert.True(grid.Columns[1].ActualWidth > 180, "内容列应填满剩余宽度，不能留下空白占位列。");
            Assert.DoesNotContain(Descendants<TextBlock>(editor), text => text.Text.Contains("不发送设备") || text.Text.Contains("模块结果"));
            Assert.Contains(Descendants<TextBlock>(editor), text => text.ToolTip is string tip && tip.Contains("模块结果"));
            AssertAddFollowsGrid(editor);
            window.Close();
        });
    }

    [Fact]
    public void Long_list_scrolls_without_hiding_add_button()
    {
        OnSta(() =>
        {
            var editor = MakeEditor(30); Layout(editor);
            var grid = (DataGrid)editor.FindName("PartsGrid");
            var viewport = (Grid)editor.FindName("PartsListViewport");
            Assert.True(grid.ActualHeight <= viewport.ActualHeight - 32 + 1);
            AssertAddFollowsGrid(editor);
            var scroll = Descendants<ScrollViewer>(grid).First();
            Assert.True(scroll.ScrollableHeight > 0);
        });
    }

    [Fact]
    public void Row_actions_move_clicked_item_and_preserve_identity_and_binding()
    {
        OnSta(() =>
        {
            var editor = MakeEditor(2); Layout(editor);
            var parameters = (ProtocolAssembleParameters)editor.DataContext;
            var first = parameters.Parts[0]; var second = parameters.Parts[1];
            first.InputText = "A"; second.InputText = "__GlobalVars__.code";
            Layout(editor);
            Assert.Equal("__GlobalVars__.code", second.InputText);
            var grid = (DataGrid)editor.FindName("PartsGrid"); grid.SelectedIndex = 0;
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(1);
            var up = Descendants<Button>(row).Single(b => Equals(b.Content, "↑"));
            Assert.True(up.IsEnabled); up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(editor);
            Assert.Same(second, parameters.Parts[0]); Assert.Same(first, parameters.Parts[1]);
            Assert.Equal("__GlobalVars__.code", parameters.Parts[0].InputText);
            row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.False(Descendants<Button>(row).Single(b => Equals(b.Content, "↑")).IsEnabled);
            var beforeAdd = parameters.Parts.Count;
            ((Button)editor.FindName("AddPartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(editor);
            Assert.Equal(beforeAdd + 1, parameters.Parts.Count);
            Assert.Same(parameters.Parts.Last(), grid.SelectedItem);
            row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
            Descendants<Button>(row).Single(b => Equals(b.Content, "×")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(editor);
            Assert.DoesNotContain(second, parameters.Parts); Assert.Contains(first, parameters.Parts);
        });
    }

    [Fact]
    public void Rows_cells_binding_and_dropdown_items_follow_live_theme_switch()
    {
        OnSta(() =>
        {
            var editor = MakeEditor(2); var theme = Theme("Dark"); editor.Resources.MergedDictionaries.Add(theme);
            Layout(editor); AssertTheme(editor);
            SavePreview(editor, "assemble-dark.png");
            editor.Resources.MergedDictionaries.Remove(theme); editor.Resources.MergedDictionaries.Add(Theme("Light"));
            Layout(editor); AssertTheme(editor); SavePreview(editor, "assemble-light.png");
        });
    }

    private static ProtocolAssembleEditor MakeEditor(int count)
    {
        var parameters = new ProtocolAssembleParameters { Parts = new() };
        for (var i = 0; i < count; i++) parameters.Parts.Add(new() { InputText = i == 0 ? "READY" : "1" });
        var editor = new ProtocolAssembleEditor(); editor.SetParameters(parameters); return editor;
    }
    private static ResourceDictionary Theme(string name) => new() { Source = new Uri($"/SunEyeVision.UI;component/Themes/{name}/{name}Theme.xaml", UriKind.Relative) };
    private static void Layout(FrameworkElement editor)
    {
        editor.Measure(new Size(400, 464)); editor.Arrange(new Rect(0, 0, 400, 464)); editor.UpdateLayout();
        editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        editor.UpdateLayout();
    }
    private static void AssertAddFollowsGrid(ProtocolAssembleEditor editor)
    {
        var grid = (DataGrid)editor.FindName("PartsGrid"); var add = (Button)editor.FindName("AddPartButton");
        var viewport = (Grid)editor.FindName("PartsListViewport");
        var gridTop = grid.TranslatePoint(new Point(), viewport).Y; var addTop = add.TranslatePoint(new Point(), viewport).Y;
        Assert.InRange(addTop - gridTop - grid.ActualHeight, 3, 5);
        Assert.True(addTop + add.ActualHeight <= viewport.ActualHeight + 1);
        Assert.Equal("＋", add.Content);
    }
    private static void AssertTheme(ProtocolAssembleEditor editor)
    {
        Color BrushColor(string key) => ((SolidColorBrush)editor.FindResource(key)).Color;
        var grid = (DataGrid)editor.FindName("PartsGrid"); grid.SelectedIndex = -1; Layout(editor);
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.Equal(BrushColor("ThemeTableRowBackgroundBrush"), ((SolidColorBrush)row.Background).Color);
        Assert.Equal(BrushColor("ThemeTextPrimaryBrush"), ((SolidColorBrush)row.Foreground).Color);
        var binding = Descendants<ParamBinding>(row).Single();
        Assert.Same(editor.FindResource("ProtocolBindingInputStyle"), binding.Style);
        Assert.Equal(BrushColor("ThemeInputBackgroundBrush"), ((SolidColorBrush)binding.Background).Color);
        Assert.Equal(BrushColor("ThemeInputBorderBrush"), ((SolidColorBrush)binding.BorderBrush).Color);
        Assert.All(Descendants<DataGridCell>(row), c => Assert.Equal(Colors.Transparent, ((SolidColorBrush)c.Background).Color));
        grid.SelectedIndex = 0; Layout(editor);
        Assert.Equal(BrushColor("ThemeTableRowSelectedBackgroundBrush"), ((SolidColorBrush)row.Background).Color);
        grid.SelectedIndex = -1; Layout(editor);
        var combo = (ComboBox)editor.FindName("DelimiterOptions");
        Assert.NotNull(combo.ItemContainerStyle);
        var item = new ComboBoxItem { Style = combo.ItemContainerStyle, Content = "自定义" };
        item.Resources.MergedDictionaries.Add(Theme(BrushColor("ThemeTableRowBackgroundBrush").R < 128 ? "Dark" : "Light"));
        item.Measure(new Size(120, 30)); item.Arrange(new Rect(0, 0, 120, 30));
        Assert.Equal(BrushColor("ThemeTextPrimaryBrush"), ((SolidColorBrush)item.Foreground).Color);
        item.IsSelected = true;
        Assert.Equal(BrushColor("ThemeControlSelectedBackgroundBrush"), ((SolidColorBrush)item.Background).Color);
    }
    private static void SavePreview(FrameworkElement editor, string name)
    {
        var bitmap = new RenderTargetBitmap(400, 464, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(root, "SunEyeVision.slnx"))) root = System.IO.Directory.GetParent(root)!.FullName;
        var directory = System.IO.Path.Combine(root, "artifacts", "validation", "protocol-assemble-layout-20261012"); System.IO.Directory.CreateDirectory(directory);
        using var file = System.IO.File.Create(System.IO.Path.Combine(directory, name)); encoder.Save(file);
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
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)), "组装布局STA测试超时");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
