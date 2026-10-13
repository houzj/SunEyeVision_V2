using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationAnalysisThemeTests
{
    [Fact]
    public void Analysis_rows_headers_and_selected_cells_follow_live_theme()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var control = new CommunicationDataAnalysisControl();
                var result = new CommunicationToolResults { ToolName = "发送数据" };
                result.SetAnalysis(new[] { new CommunicationAnalysisRow(null, "out0", "OPC UA1 / test1", "Boolean", "当前值：False；数据时间 2026-10-13 08:00:00.000 +08:00", true), new CommunicationAnalysisRow(null, "out1", "全局 / 分组1 / 变量1", "String", "当前值：ABC,2；读取时间 2026-10-13 08:00:00.000 +08:00", true) });
                result.SetSuccess(1); control.OnResultUpdated(result);
                var grid = (DataGrid)control.FindName("AnalysisGrid");
                ResourceDictionary? previous = null;
                foreach (var theme in new[] { "Dark", "Light", "Dark" })
                {
                    if (previous != null) control.Resources.MergedDictionaries.Remove(previous);
                    previous = new() { Source = new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml", UriKind.Relative) };
                    control.Resources.MergedDictionaries.Add(previous);
                    control.Measure(new Size(1000, 240)); control.Arrange(new Rect(0, 0, 1000, 240)); control.UpdateLayout();
                    Assert.True(grid.Columns[1].ActualWidth >= 160);
                    Assert.True(grid.Columns[2].ActualWidth >= 140);
                    Assert.True(grid.Columns[3].ActualWidth >= 240);
                    var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
                    var sourceText = Descendants<TextBlock>(row).Single(t => t.Text == "OPC UA1 / test1");
                    Assert.Equal(sourceText.Text, sourceText.ToolTip);
                    Assert.Equal(((SolidColorBrush)control.FindResource("ThemeTableRowBackgroundBrush")).Color, ((SolidColorBrush)row.Background).Color);
                    var header = Descendants<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid).First(h => Equals(h.Content, "字段／目标"));
                    Assert.Equal(((SolidColorBrush)control.FindResource("ThemeTableHeaderBackgroundBrush")).Color, ((SolidColorBrush)header.Background).Color);
                    grid.SelectedIndex = 0; control.UpdateLayout();
                    Assert.Equal(((SolidColorBrush)control.FindResource("ThemeTableRowSelectedBackgroundBrush")).Color, ((SolidColorBrush)row.Background).Color);
                    Assert.All(Descendants<DataGridCell>(row), cell => Assert.Equal(((SolidColorBrush)control.FindResource("ThemeTextPrimaryBrush")).Color, ((SolidColorBrush)cell.Foreground).Color));
                    var root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "SunEyeVision.slnx"))) root = Directory.GetParent(root)!.FullName;
                    var directory = Path.Combine(root, "artifacts", "validation", "communication-results-20261013"); Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap(1000, 240, 96, 96, PixelFormats.Pbgra32); bitmap.Render(control);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, $"analysis-{theme.ToLowerInvariant()}.png")); encoder.Save(file);
                    grid.SelectedIndex = -1; Assert.Same(result, control.CurrentResult);
                }
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
