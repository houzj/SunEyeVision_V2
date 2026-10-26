using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shell.Controls.Shell;
using Xunit;
using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class WorkflowLocationTests
{
    private static Flow[] Definitions()
    {
        var root = new Flow("root", "流程一");
        var child = new Flow("child", "内部") { IsSubroutine = true };
        var nested = new Flow("nested", "内部二") { IsSubroutine = true };
        root.Nodes.Add(new("image", "图像源", "图像源", "ImageLoad"));
        root.Nodes.Add(new("owner", "组一", "组一", "Subroutine") { SubroutineId = child.Id });
        child.Nodes.Add(new("owner", "组二", "组二", "Subroutine") { SubroutineId = nested.Id });
        nested.Nodes.Add(new("loop", "循环", "循环", "Subroutine") { SubroutineId = child.Id });
        return new[] { root, child, nested, new Flow("other", "流程二"), new Flow("orphan", "孤立") { IsSubroutine = true } };
    }
    [Fact]
    public void Tree_contains_only_reachable_containers_and_rejects_replaced_or_reowned_paths()
    {
        var flows = Definitions(); var roots = WorkflowLocationCatalog.Build(flows);
        Assert.Equal(2, roots.Count); Assert.Empty(roots[1].Children);
        var child = Assert.Single(roots[0].Children); var nested = Assert.Single(child.Children);
        Assert.Equal("组一", child.Name); Assert.Equal("组二", nested.Name); Assert.Empty(nested.Children);
        Assert.Equal(2, nested.Hops.Count); Assert.True(WorkflowLocationCatalog.IsCurrent(nested, flows));
        flows[1].Nodes[0].SubroutineId = "orphan"; Assert.False(WorkflowLocationCatalog.IsCurrent(nested, flows));
        flows[1].Nodes[0].SubroutineId = "nested";
        var replaced = flows.ToArray(); replaced[2] = new Flow("nested", "替换") { IsSubroutine = true };
        Assert.False(WorkflowLocationCatalog.IsCurrent(nested, replaced));
        Assert.False(WorkflowLocationCatalog.IsCurrent(roots[0], flows.Skip(1)));
    }
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Actual_tree_navigates_headers_and_clears_popup_on_close_and_unload(string theme) => Sta(() =>
    {
        var flows = Definitions(); WorkflowLocationEntry? target = null;
        var control = new WorkflowLocationControl { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        control.GetLocations = () => WorkflowLocationCatalog.Build(flows);
        control.Navigate = entry => { target = entry; return WorkflowLocationCatalog.IsCurrent(entry, flows); };
        var owner = new Window { Content = control, Width = 500, Height = 400, Left = -10000, ShowActivated = false };
        foreach (var resource in new[] { "Shell/Resources/AppResources.xaml", "Resources/Styles/CommonStyleResources.xaml", "Themes/" + theme + "/" + theme + "Theme.xaml" })
            owner.Resources.MergedDictionaries.Add(new() { Source = new Uri("/SunEyeVision.UI;component/" + resource, UriKind.Relative) });
        try
        {
            owner.Show(); owner.UpdateLayout();
            var expand = (ToggleButton)control.FindName("Expand");
            var popup = (Popup)control.FindName("LocationsPopup");
            var tree = (TreeView)control.FindName("Locations");
            expand.IsChecked = true; owner.UpdateLayout(); tree.UpdateLayout();
            Assert.True(popup.IsOpen); Assert.Equal(2, tree.Items.Count);
            var rootRow = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.True(rootRow.IsExpanded); rootRow.UpdateLayout();
            var childRow = (TreeViewItem)rootRow.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.True(childRow.IsExpanded); childRow.UpdateLayout();
            Assert.Equal(((SolidColorBrush)owner.FindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.TextPrimaryBrush)).Color,
                ((SolidColorBrush)childRow.Foreground).Color);
            var content = (FrameworkElement)popup.Child; content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, "workflow-locations-" + theme + ".png"))) encoder.Save(file);
            var header = FindText(childRow, "组一")!; Assert.NotNull(header);
            header.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = header });
            Assert.Same(flows[1], target!.Container); Assert.False(popup.IsOpen); Assert.Empty(tree.Items); Assert.False(expand.IsChecked);
            expand.IsChecked = true; popup.IsOpen = false; Pump();
            Assert.False(expand.IsChecked); Assert.Empty(tree.Items);
            expand.IsChecked = true; control.SetValue(WorkflowLocationControl.DefinitionIdentityProperty, new object());
            Assert.False(popup.IsOpen); Assert.Empty(tree.Items);
            expand.IsChecked = true; control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.False(popup.IsOpen); Assert.Empty(tree.Items);
        }
        finally { owner.Close(); }
    });
    private static TextBlock? FindText(DependencyObject root, string text)
    {
        if (root is TextBlock block && block.Text == text) return block;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindText(VisualTreeHelper.GetChild(root, i), text) is {} found) return found;
        return null;
    }
    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(45)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
