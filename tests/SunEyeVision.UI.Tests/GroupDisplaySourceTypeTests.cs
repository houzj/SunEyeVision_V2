using System.Reflection;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class GroupDisplaySourceTypeTests
{
    [Fact]
    public void Actual_Group_text_binding_lists_text_boolean_numeric_sources_and_updates_on_type_change() => Sta(() =>
    {
        var root = typeof(GroupDisplaySourceTypeTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "HostDebugRoot").Value!;
        var plugin = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, "plugins", "SunEyeVision.Tool.Subroutine", "SunEyeVision.Tool.Subroutine.dll"));
        AvailableDataSource Source(string name, Type type) => new() { SourceWorkflowId = "child", SourceNodeId = "inside", SourceNodeName = "内部模块", PropertyName = name, DisplayName = name, PropertyType = type };
        var sources = new List<AvailableDataSource> { Source("Text", typeof(string[])), Source("Flag", typeof(bool)), Source("Value", typeof(double[])), Source("Image", typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage)), Source("Circle", typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d[])) };
        var display = new SubroutineDisplayItem { DisplayName = "文本", DisplayType = SubroutineDisplayType.Text, TextStyle = new() };
        var parameters = new SubroutineParameters { SubroutineId = "child" }; parameters.DisplayConfigs.Add(display);
        var control = (UserControl)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineDebugControl", true)!, new object?[] { "child", "Group", "owner", null, sources, parameters, null })!;
        var window = new Window { Content = control, Width = 1100, Height = 680, Left = -10000, ShowActivated = false };
        try
        {
            foreach (var resource in new[] { "Shell/Resources/AppResources.xaml", "Resources/Styles/CommonStyleResources.xaml", "Themes/Dark/DarkTheme.xaml" }) window.Resources.MergedDictionaries.Add(new() { Source = new Uri("/SunEyeVision.UI;component/" + resource, UriKind.Relative) });
            window.Show(); Visual<TabControl>(control).First().SelectedIndex = 3; window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
            var binding = Visual<ParamBinding>(control).Single(b => ReferenceEquals(b.DataContext, display));
            IEnumerable<TreeNodeData> Flatten(IEnumerable<TreeNodeData> nodes) => nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));
            var candidates = Flatten(binding.TreeNodes!).Where(node => node.DataSource != null).Select(node => node.DataSource!.PropertyName).ToArray();
            Assert.Contains("Text", candidates); Assert.Contains("Flag", candidates); Assert.Contains("Value", candidates); Assert.DoesNotContain("Image", candidates);
            var text = Assert.Single(Flatten(binding.TreeNodes!), node => node.DataSource?.PropertyName == "Text"); Assert.True(text.IsSelectable);
            binding.ApplyTemplate();
            var button = Assert.IsType<Button>(binding.Template.FindName("PART_BindingButton", binding));
            var popup = Assert.IsType<System.Windows.Controls.Primitives.Popup>(binding.Template.FindName("PART_BindingPopup", binding));
            var tree = Assert.IsType<TreeView>(binding.Template.FindName("PART_BindingTreeView", binding));
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.True(popup.IsOpen); tree.UpdateLayout();
            var module = binding.TreeNodes!.Single(node => node.Text == "内部模块");
            var moduleRow = Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromItem(module));
            moduleRow.IsExpanded = true; tree.UpdateLayout();
            var content = (FrameworkElement)popup.Child; content.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, "group-text-source-tree-Dark.png"))) encoder.Save(file);
            var textRow = Assert.IsType<TreeViewItem>(moduleRow.ItemContainerGenerator.ContainerFromItem(text));
            textRow.IsSelected = true;
            Assert.False(popup.IsOpen);
            Assert.Equal("child", display.BoundDataSource!.SourceWorkflowId); Assert.Equal("Text", display.BoundDataSource.PropertyName);
            display.DisplayType = SubroutineDisplayType.Circle;
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
            var changed = Flatten(binding.TreeNodes!).Where(node => node.DataSource != null).Select(node => node.DataSource!.PropertyName).ToArray();
            Assert.Contains("Circle", changed); Assert.DoesNotContain("Text", changed); Assert.DoesNotContain("Flag", changed); Assert.DoesNotContain("Value", changed);
        }
        finally { window.Close(); ((ISubroutineParameterEditor)control).Dispose(); }
    });
    private static IEnumerable<T> Visual<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T found) yield return found;
            foreach (var nested in Visual<T>(child)) yield return nested;
        }
    }
    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(45)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
