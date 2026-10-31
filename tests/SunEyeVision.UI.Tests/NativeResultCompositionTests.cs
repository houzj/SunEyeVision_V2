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
        var historyTab = Assert.IsType<TabItem>(shell.FindName("HistoryTab"));
        var historyMenu = historyTab.ContextMenu!;
        historyMenu.PlacementTarget = historyTab;
        historyMenu.IsOpen = true; Drain();
        Assert.Equal(((SolidColorBrush)shell.FindResource("Results.Shell.Col_ComboBox_Popup")).Color,
            Assert.IsType<SolidColorBrush>(historyMenu.Background).Color);
        Assert.Equal(((SolidColorBrush)shell.FindResource("Results.Shell.Col_ComboBox_BorderBrush")).Color,
            Assert.IsType<SolidColorBrush>(historyMenu.BorderBrush).Color);
        Assert.All(historyMenu.Items.Cast<MenuItem>(), item => Assert.False(item.IsEnabled));
        historyMenu.IsOpen = false;
        shell.SelectedResultTabIndex = 0;
        Drain();
        var fullscreen = Assert.IsType<Button>(shell.FindName("BtnToolFullScreen"));
        fullscreen.ApplyTemplate();
        Assert.Contains(Descendants<Image>(fullscreen), image => image.Source is not null);
        Assert.False(fullscreen.IsEnabled); // No real host fullscreen command was supplied by this design scene.
        Assert.Same(shell.OutputViewport, design.OutputViewportOwner!.View);
        Assert.Equal(design.OutputRaster!.Frame, design.OutputViewportOwner.CurrentFrame!.Frame);
        Assert.Equal(new byte[] { 45 }, design.OutputViewportOwner.ReadPixel(0, 0));
        Assert.Same(shell.InputViewport, design.InputViewportOwner!.View);
        Assert.Equal(design.InputRaster!.Frame, design.InputViewportOwner.CurrentFrame!.Frame);
        Assert.NotEqual(design.InputRaster.Frame, design.OutputRaster.Frame);
        Assert.Equal(new byte[] { 45 }, design.InputViewportOwner.ReadPixel(0, 0));
    });

    [Fact]
    public void Property_list_headers_use_shared_localization_in_standalone_host() => Sta(() =>
    {
        var view = new PropertyListView();
        using var host = new LoadedHost(view);
        var text = Descendants<TextBlock>(view).Select(item => item.Text).ToArray();
        Assert.Contains((string)view.FindResource("Lang_ResultName"), text);
        Assert.Contains((string)view.FindResource("Lang_CurValue"), text);
        Assert.Contains((string)view.FindResource("Lang_VariableManager"), text);
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
        host.Detach();
        var replacement=rows[0].Children[0];
        view.SelectedItem=replacement;
        host.Attach(view);tree.UpdateLayout();Drain();
        Assert.Same(replacement,view.SelectedItem);
        Assert.Same(replacement,tree.SelectedItem);
        Assert.Empty(calls.Arguments);
    });

    [Fact]
    public void Restoring_selection_reentry_keeps_new_target_and_null_clears_actual_tree_selection()=>Sta(()=>
    {
        var rows=PropertyListDesignView.CreateRows();var calls=new RecordingCommand();
        var view=new PropertyListView{ItemsSource=rows,SelectedCommand=calls};using var host=new LoadedHost(view);
        var tree=Assert.IsType<TreeView>(view.FindName("resultTreeView"));
        var first=rows[0].Children[0];var next=rows[0].Children[1];var replaced=false;
        tree.SelectedItemChanged+=(_,args)=>
        {
            if(replaced||!ReferenceEquals(args.NewValue,first))return;
            replaced=true;view.SelectedItem=next;
        };
        view.SelectedItem=first;Drain();
        Assert.True(replaced);Assert.Same(next,view.SelectedItem);Assert.Same(next,tree.SelectedItem);
        view.SelectedItem=null;Drain();
        Assert.Null(view.SelectedItem);Assert.Null(tree.SelectedItem);Assert.Empty(calls.Arguments);
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
        var rowContent=Assert.IsType<Grid>(container.Template.FindName("RowContent",container));
        // Original row Grid input is independent of the deliberately unfocusable TreeViewItem.
        rowContent.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent });
        rowContent.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        Drain();
        Assert.Same(target, view.SelectedItem);
        Assert.Same(target, Assert.Single(calls.Arguments));
        rowContent.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left)
            {RoutedEvent=UIElement.MouseLeftButtonDownEvent});
        Assert.Equal(2,calls.Arguments.Count);Assert.All(calls.Arguments,item=>Assert.Same(target,item));
        rowContent.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Right)
            {RoutedEvent=UIElement.MouseRightButtonDownEvent});
        rowContent.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left)
            {RoutedEvent=UIElement.MouseLeftButtonDownEvent,Handled=true});
        view.SelectedItem = rows[0].Children[0];
        Drain();
        Assert.Equal(2,calls.Arguments.Count);
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
        var oldInput = design.InputRaster!; var oldInputOwner = design.InputViewportOwner!;
        var oldOutput = design.OutputRaster!; var oldOutputOwner = design.OutputViewportOwner!;
        using var retainedInput = oldInput.AcquirePixels(); using var retainedOutput = oldOutput.AcquirePixels();
        var oldInputFrame = retainedInput.Frame; var oldOutputFrame = retainedOutput.Frame;
        host.Detach();
        Assert.False(shell.IsLoaded);
        Assert.Null(design.InputViewportOwner); Assert.Null(design.OutputViewportOwner);
        Assert.Null(design.InputRaster); Assert.Null(design.OutputRaster);
        Assert.Null(oldInputOwner.CurrentFrame); Assert.Null(oldOutputOwner.CurrentFrame);
        Assert.Throws<ObjectDisposedException>(() => oldInput.CopyPixels());
        Assert.Throws<ObjectDisposedException>(() => oldOutput.CopyPixels());
        Assert.Equal(45, retainedInput.GetRow(0)[0]); Assert.Equal(45, retainedOutput.GetRow(0)[0]);
        host.Attach(design);
        Assert.True(shell.IsLoaded);
        Assert.Same(input, shell.FindName("imageViewIn"));
        Assert.Same(output, shell.FindName("imageView"));
        Assert.Same(shell.InputViewport, design.InputViewportOwner!.View); Assert.Same(input, design.InputViewportOwner.View);
        Assert.Equal(design.InputRaster!.Frame, design.InputViewportOwner.CurrentFrame!.Frame);
        Assert.NotEqual(oldInputFrame, design.InputRaster.Frame); Assert.NotSame(oldInputOwner, design.InputViewportOwner);
        Assert.Equal(new byte[] { 45 }, design.InputViewportOwner.ReadPixel(0, 0));
        Assert.Same(shell.OutputViewport, design.OutputViewportOwner!.View); Assert.Same(output, design.OutputViewportOwner.View);
        Assert.Equal(design.OutputRaster!.Frame, design.OutputViewportOwner.CurrentFrame!.Frame);
        Assert.NotEqual(oldOutputFrame, design.OutputRaster.Frame); Assert.NotSame(oldOutputOwner, design.OutputViewportOwner);
        Assert.Equal(new byte[] { 45 }, design.OutputViewportOwner.ReadPixel(0, 0));
        var history = Assert.IsType<TabItem>(shell.FindName("HistoryTab"));
        history.IsSelected = true; Drain();
        var historyGrid = Assert.IsType<DataGrid>(shell.FindName("HistoryGrid"));
        Assert.False(historyGrid.IsEnabled);
        Assert.Null(historyGrid.ItemsSource);
        Assert.Equal(new[] { "执行序号", "时间", "模块数据" }, historyGrid.Columns.Select(column => column.Header));
        Assert.All(history.ContextMenu!.Items.Cast<MenuItem>(), item => Assert.False(item.IsEnabled));
        Assert.Contains(Descendants<TextBlock>(shell), text => text.Text == "历史记录尚未接入");
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
    private static void Sta(Action action) => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(action);
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
