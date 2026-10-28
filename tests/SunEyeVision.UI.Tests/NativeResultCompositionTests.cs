using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.Plugin.Wpf.Design.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Design.Results.ResultShell;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>Exercises the real native compositions on a loaded WPF tree. No host/algorithm/image-frame model is fabricated.</summary>
public sealed class NativeResultCompositionTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Instance_palettes_update_real_result_surfaces_and_native_templates(string theme) => Sta(() =>
    {
        var design = new ResultWorkspaceDesignView();
        var shell = Assert.IsType<ResultWorkspaceShell>(design.FindName("Workspace"));
        var results = Assert.IsType<PropertyListView>(shell.PropertyListContent);
        using var host = new LoadedHost(design);
        var initial = Theme(shell, "ResultShell", "Results.Shell.Col_DisplayImage_Background", "Dark");
        Theme(results, "PropertyList", "Results.PropertyList.Col_ShowResults_ModuleResult_Background", theme);
        var expected = Theme(shell, "ResultShell", "Results.Shell.Col_DisplayImage_Background", theme);
        shell.SelectedResultTabIndex = 1;
        Drain();
        var grid = Assert.IsType<Grid>(results.FindName("resultGrid"));
        Assert.Equal(((SolidColorBrush)results.FindResource("Results.PropertyList.Col_ShowResults_ModuleResult_Background")).Color,
            Assert.IsType<SolidColorBrush>(grid.Background).Color);
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(Assert.IsType<Grid>(shell.Content).Background).Color);
        if (theme == "Light") Assert.NotEqual(initial, expected);
        shell.SelectedResultTabIndex = 0;
        Drain();
        var fullscreen = Assert.IsType<Button>(shell.FindName("BtnToolFullScreen"));
        fullscreen.ApplyTemplate();
        Assert.Contains(Descendants<Image>(fullscreen), image => image.Source is not null);
        Assert.False(fullscreen.IsEnabled); // No real host fullscreen command was supplied by this design scene.
        Assert.Same(shell.OutputImage, Assert.IsType<ImageViewBox>(shell.FindName("imageView")).ImageDataSource);
        Assert.Same(shell.InputImage, Assert.IsType<ImageViewBox>(shell.FindName("imageViewIn")).ImageDataSource);
    });

    [Fact]
    public void Hierarchical_binding_keeps_same_rows_and_restores_selection_without_user_command() => Sta(() =>
    {
        var rows = PropertyListDesignView.CreateRows();
        var items = new ObservableCollection<PropertyListDesignView.SampleRow>(rows);
        var calls = new RecordingCommand();
        var view = new PropertyListView { DataContext = items, SelectedCommand = calls };
        BindingOperations.SetBinding(view, PropertyListView.ItemsSourceProperty, new Binding());
        using var host = new LoadedHost(view);
        var tree = Assert.IsType<TreeView>(view.FindName("resultTreeView"));
        Assert.Same(items, tree.ItemsSource);
        var root = Container(tree, rows[0]);
        Assert.Same(rows[0], root.DataContext);
        Assert.True(root.IsExpanded);
        var child = rows[0].Children[1];
        var childContainer = Container(root, child);
        Assert.Same(child, childContainer.DataContext);
        view.SelectedItem = child;
        Drain();
        Assert.True(childContainer.IsSelected);
        Assert.Same(child, tree.SelectedItem);
        Assert.Empty(calls.Arguments);
        root.IsExpanded = false;
        Drain();
        Assert.False(rows[0].IsExpanded);
        root.IsExpanded = true;
        Drain();
        Assert.True(rows[0].IsExpanded);
        host.Detach();
        Assert.False(view.IsLoaded);
        host.Attach(view);
        tree.UpdateLayout();
        Assert.Same(child, view.SelectedItem);
        Assert.Same(child, tree.SelectedItem);
        Assert.Empty(calls.Arguments);
    });

    [Fact]
    public void Routed_user_selection_passes_actual_row_once_but_programmatic_selection_does_not() => Sta(() =>
    {
        var rows = PropertyListDesignView.CreateRows();
        var calls = new RecordingCommand();
        var view = new PropertyListView { ItemsSource = rows, SelectedCommand = calls };
        using var host = new LoadedHost(view);
        var tree = Assert.IsType<TreeView>(view.FindName("resultTreeView"));
        var parent = Container(tree, rows[0]);
        view.SelectedItem = rows[0].Children[0];
        Drain();
        Assert.Empty(calls.Arguments);
        var target = rows[0].Children[1];
        var container = Container(parent, target);
        // Real routed preview + item mouse input runs the production selection handlers; no private methods are invoked.
        container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent });
        container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        Drain();
        Assert.Same(target, view.SelectedItem);
        Assert.Same(target, Assert.Single(calls.Arguments));
        view.SelectedItem = rows[0].Children[0];
        Drain();
        Assert.Single(calls.Arguments);
    });

    [Fact]
    public void Read_only_tree_and_absent_variable_services_leave_real_link_buttons_disabled() => Sta(() =>
    {
        var view = new PropertyListView { ItemsSource = PropertyListDesignView.CreateRows(), IsReadOnly = true };
        using var host = new LoadedHost(view);
        var tree = Assert.IsType<TreeView>(view.FindName("resultTreeView"));
        var root = Container(tree, tree.Items[0]);
        root.ApplyTemplate();
        var link = Assert.IsType<Button>(root.Template.FindName("btnLink", root));
        var unlink = Assert.IsType<Button>(root.Template.FindName("btnDel", root));
        Assert.False(link.IsEnabled);
        Assert.False(unlink.IsEnabled);
        Assert.Null(link.Command);
        Assert.Null(unlink.Command);
        Assert.Contains(Descendants<Image>(link), image => image.Source is not null);
    });

    [Fact]
    public void Actual_shell_reloads_same_image_views_and_preserves_independent_selection_and_layout() => Sta(() =>
    {
        var design = new ResultWorkspaceDesignView();
        var shell = Assert.IsType<ResultWorkspaceShell>(design.FindName("Workspace"));
        using var host = new LoadedHost(design);
        var input = Assert.IsType<ImageViewBox>(shell.FindName("imageViewIn"));
        var output = Assert.IsType<ImageViewBox>(shell.FindName("imageView"));
        Assert.NotSame(input, output);
        var inputChoice = shell.SelectedInputItem;
        var outputChoice = shell.SelectedOutputItem;
        Assert.NotSame(inputChoice, outputChoice);
        var inputColumn = Assert.IsType<ColumnDefinition>(shell.FindName("colInput"));
        var infoRow = Assert.IsType<RowDefinition>(shell.FindName("InfoRow"));
        shell.IsDoubleImage = false;
        shell.IsInfoExpanded = false;
        Drain();
        Assert.Equal(new GridLength(0), inputColumn.Width);
        Assert.Equal(40d, infoRow.Height.Value);
        shell.IsDoubleImage = true;
        shell.IsInfoExpanded = true;
        Drain();
        Assert.True(inputColumn.Width.IsStar);
        Assert.Equal(235d, infoRow.Height.Value);
        Assert.Same(inputChoice, shell.SelectedInputItem);
        Assert.Same(outputChoice, shell.SelectedOutputItem);
        host.Detach();
        Assert.False(shell.IsLoaded);
        host.Attach(design);
        Assert.True(shell.IsLoaded);
        Assert.Same(input, shell.FindName("imageViewIn"));
        Assert.Same(output, shell.FindName("imageView"));
        Assert.Same(shell.InputImage, input.ImageDataSource);
        Assert.Same(shell.OutputImage, output.ImageDataSource);
        Assert.Contains(Descendants<TabItem>(shell), tab => Equals(tab.Header, "历史结果") && !tab.IsEnabled);
    });

    private static Color Theme(FrameworkElement view, string family, string key, string theme)
    {
        view.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Results/{family}/{theme}.xaml", UriKind.Relative)
        });
        view.UpdateLayout(); Drain();
        return Assert.IsType<SolidColorBrush>(view.FindResource(key)).Color;
    }
    private static TreeViewItem Container(ItemsControl owner, object item)
    {
        owner.UpdateLayout(); Drain();
        var container = Assert.IsType<TreeViewItem>(owner.ItemContainerGenerator.ContainerFromItem(item));
        container.ApplyTemplate(); container.UpdateLayout(); Drain();
        return container;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) yield return result;
            foreach (var resultChild in Descendants<T>(child)) yield return resultChild;
        }
    }
    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(35)), "真实 WPF 组合验证未在限定时间退出。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private sealed class LoadedHost : IDisposable
    {
        private readonly Window _window = new() { Width = 920, Height = 780, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000 };
        public LoadedHost(FrameworkElement content) { _window.Content = content; _window.Show(); Drain(); Assert.True(content.IsLoaded); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach(FrameworkElement content) { _window.Content = content; Drain(); Assert.True(content.IsLoaded); }
        public void Dispose() { _window.Content = null; _window.Close(); Drain(); }
    }
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Arguments { get; } = new();
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Arguments.Add(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
