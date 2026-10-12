using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.UI.Themes.Base;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GlobalVariableSelectThemeTests
{
    [Fact]
    public void Rows_cells_hover_selection_and_live_theme_switch_use_themed_brushes()
    {
        RunSta(() =>
        {
            var variables = Enumerable.Range(1, 4).Select(n => new GlobalVariable("变量" + n, "值" + n, "String", "分组1") { Index = n }).ToList();
            var model = new GlobalVariableSelectViewModel(variables);
            var window = new GlobalVariableSelectDialog(model);
            try
            {
                var content = (Grid)window.Content;
                var grid = (DataGrid)window.FindName("VariableList");
                Assert.True(grid.IsReadOnly); Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);
                foreach (var theme in new[] { "Dark", "Light", "Dark" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml", UriKind.Relative) });
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SunEyeVision.UI;component/Resources/Styles/CommonStyleResources.xaml", UriKind.Relative) });
                    grid.SelectedItem = null;
                    content.Measure(new Size(700, 440)); content.Arrange(new Rect(0, 0, 700, 440)); content.UpdateLayout();
                    content.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    var row = Assert.IsType<DataGridRow>(grid.ItemContainerGenerator.ContainerFromIndex(0));
                    var other = Assert.IsType<DataGridRow>(grid.ItemContainerGenerator.ContainerFromIndex(1));
                    Assert.Same(window.Resources["VariableSelectRowStyle"], row.Style);
                    Assert.Equal(Color(window, ThemeKeys.TableRowBackgroundBrush), Brush(row.Background));
                    Assert.Equal(Color(window, ThemeKeys.TextPrimaryBrush), Brush(row.Foreground));
                    Assert.Equal(Color(window, ThemeKeys.TableRowBackgroundBrush), Brush(other.Background));
                    var cell = Descendants<DataGridCell>(row).First();
                    Assert.Same(window.Resources["VariableSelectCellStyle"], cell.Style);
                    Assert.Equal(Colors.Transparent, Brush(cell.Background));
                    Assert.Equal(Color(window, ThemeKeys.TextPrimaryBrush), Brush(cell.Foreground));
                    if (theme == "Dark") Assert.NotEqual(Colors.White, Brush(row.Background));
                    var hoverKey = (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                    row.SetValue(hoverKey, true);
                    Assert.Equal(Color(window, ThemeKeys.TableRowHoverBackgroundBrush), Brush(row.Background));
                    row.SetValue(hoverKey, false);
                    grid.SelectedItem = variables[0]; content.UpdateLayout();
                    Assert.True(row.IsSelected); Assert.False(row.IsKeyboardFocusWithin);
                    Assert.Equal(Color(window, ThemeKeys.TableRowSelectedBackgroundBrush), Brush(row.Background));
                    Assert.Equal(Color(window, ThemeKeys.TextPrimaryBrush), Brush(cell.Foreground));
                    grid.SelectedItem = null; content.UpdateLayout();
                    Assert.Equal(Color(window, ThemeKeys.TableRowBackgroundBrush), Brush(row.Background));
                    SavePreview(content, theme, window.Background);
                }
                model.SelectedGroup = "分组1"; model.SearchText = "变量2";
                Assert.Single(model.FilteredVariables); Assert.Equal("变量2", model.FilteredVariables[0].Name);
            }
            finally { window.Close(); }
        });
    }
    private static Color Color(FrameworkElement view, object key) => Brush((Brush)view.FindResource(key));
    private static Color Brush(Brush brush) => Assert.IsAssignableFrom<SolidColorBrush>(brush).Color;
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var n = 0; n < VisualTreeHelper.GetChildrenCount(root); n++)
        { var child = VisualTreeHelper.GetChild(root, n); if (child is T typed) yield return typed; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    private static void SavePreview(FrameworkElement content, string theme, Brush background)
    {
        var root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "SunEyeVision.slnx"))) root = Directory.GetParent(root)!.FullName;
        var directory = Path.Combine(root, "artifacts", "validation", "variable-select-theme-20261012"); Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(700, 440, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual(); using (var drawing = backdrop.RenderOpen()) drawing.DrawRectangle(background, null, new Rect(0, 0, 700, 440));
        bitmap.Render(backdrop); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"variables-{theme.ToLowerInvariant()}.png")); encoder.Save(file);
    }
    private static void RunSta(Action action)
    {
        Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
