using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shell.Controls.Shell;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class WorkflowTabButtonTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Native_button_resources_follow_empty_single_busy_and_continuous_stop_states(string theme) => Sta(() =>
    {
        var tab = new WorkflowTabViewModel();
        var shell = new ShellWorkspaceControl { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Height = 40, DataContext = tab };
        var single = new Button { Style = (Style)shell.FindResource("WorkflowSingleButton") };
        var continuous = new Button { Style = (Style)shell.FindResource("WorkflowContinuousButton") };
        var add = new Button { Style = (Style)shell.FindResource("WorkflowAddButton") };
        panel.Children.Add(single); panel.Children.Add(continuous); panel.Children.Add(add);
        var frame = new Border { Child = panel, Padding = new Thickness(8, 0, 8, 0) };
        frame.SetResourceReference(Border.BackgroundProperty, SunEyeVision.UI.Themes.Base.ThemeKeys.TabStripBackgroundBrush);
        shell.Content = frame;
        var owner = new Window { Content = shell, Width = 500, Height = 300, Left = -10000, ShowActivated = false };
        foreach (var resource in new[] { "Shell/Resources/AppResources.xaml", "Resources/Styles/CommonStyleResources.xaml", "Themes/" + theme + "/" + theme + "Theme.xaml" })
            owner.Resources.MergedDictionaries.Add(new() { Source = new Uri("/SunEyeVision.UI;component/" + resource, UriKind.Relative) });
        try
        {
            owner.Show(); owner.UpdateLayout(); Pump();
            Assert.False(single.IsEnabled); Assert.False(continuous.IsEnabled);
            shell.WorkflowEditingAllowed = false; Pump(); Assert.False(add.IsEnabled); Assert.Contains("disable", Icon(add));
            shell.WorkflowEditingAllowed = true; Pump(); Assert.True(add.IsEnabled);
            Assert.Contains("disable", Icon(single)); Assert.Contains("disable", Icon(continuous));
            tab.WorkflowNodes.Add(new("node", "图像", "图像", "ImageLoad")); Pump();
            Assert.True(single.IsEnabled); Assert.True(continuous.IsEnabled);
            Assert.Contains("ic_play_单流程_normal", Uri.UnescapeDataString(Icon(single)));
            Assert.Contains("ic_多次运行_normal", Uri.UnescapeDataString(Icon(continuous)));
            Assert.Equal(32, single.ActualWidth); Assert.Equal(32, continuous.ActualHeight);
            Save(frame, "workflow-tab-buttons-idle-" + theme + ".png");
            shell.SingleExecutionBusy = true; Pump();
            Assert.False(single.IsEnabled); Assert.False(continuous.IsEnabled);
            Save(frame, "workflow-tab-buttons-busy-" + theme + ".png");
            tab.IsRunning = true; Pump();
            Assert.False(single.IsEnabled); Assert.True(continuous.IsEnabled);
            Assert.Equal("停止运行", continuous.ToolTip);
            Assert.Contains("ic_停止运行_normal", Uri.UnescapeDataString(Icon(continuous)));
            Save(frame, "workflow-tab-buttons-stop-" + theme + ".png");
            shell.SingleExecutionBusy = false; tab.IsRunning = false; Pump();
            Assert.True(single.IsEnabled); Assert.True(continuous.IsEnabled);
            Assert.Equal("连续运行", continuous.ToolTip);
            tab.WorkflowNodes.Clear(); Pump(); Assert.False(single.IsEnabled); Assert.False(continuous.IsEnabled);
        }
        finally { owner.Close(); }
    });
    private static string Icon(Button button)
    {
        button.ApplyTemplate();
        return ((Image)button.Template.FindName("Icon", button)).Source.ToString();
    }
    private static void Save(FrameworkElement element, string name)
    {
        element.UpdateLayout(); var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, name)); encoder.Save(file);
    }
    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(45)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
