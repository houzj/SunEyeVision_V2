using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Common;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("正式WPF owner")]
public sealed class MainWorkspaceLayoutTests
{
    [Fact]
    public void Default_layout_uses_proportions_and_preserves_explicit_width()
    {
        var fresh = new LayoutConfig();
        fresh.ValidateAndCorrect();
        Assert.Equal(0, fresh.RightColumnWidth);
        fresh.RightColumnWidth = 1234;
        fresh.ValidateAndCorrect();
        Assert.Equal(1234, fresh.RightColumnWidth);
        fresh.RightColumnWidth = double.PositiveInfinity; fresh.ValidateAndCorrect();
        Assert.Equal(0, fresh.RightColumnWidth);
    }

    [Fact]
    public void Splitter_collapses_past_minimum_and_restores_star_or_dragged_width()
    {
        RunSta(() =>
        {
            var grid = new Grid { Width = 1200, Height = 600 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 400 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var right = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 400 };
            grid.ColumnDefinitions.Add(right);
            var splitter = new CollapsibleSplitterBox { Width = 8, ResizeDirection = GridResizeDirection.Columns, CollapseDirection = CollapseDirectionType.Right };
            Grid.SetColumn(splitter, 1); grid.Children.Add(splitter);
            grid.Measure(new Size(1200, 600)); grid.Arrange(new Rect(0, 0, 1200, 600));
            var initial = right.ActualWidth;
            Assert.InRange(initial, 590, 600);
            splitter.ToggleCollapsed();
            grid.Measure(new Size(1200, 600)); grid.Arrange(new Rect(0, 0, 1200, 600));
            Assert.True(splitter.IsCollapsed); Assert.Equal(0, right.ActualWidth); Assert.Equal(0, right.MinWidth);
            Assert.InRange(splitter.ExpandedPixelSize, 590, 600);
            splitter.ToggleCollapsed();
            Assert.True(right.Width.IsStar); Assert.Equal(400, right.MinWidth);
            var excessive = new LayoutConfig { RightColumnWidth = 100000 };
            InvokeLayout("Restore", excessive, right, splitter);
            Assert.InRange(right.Width.Value, 400, 800);
            right.Width = new GridLength(720); // represents a user-adjusted splitter width
            splitter.ToggleCollapsed(); Assert.Equal(720, splitter.ExpandedPixelSize);
            var saved = new LayoutConfig();
            InvokeLayout("Capture", saved, right, splitter);
            var json = System.Text.Json.JsonSerializer.Serialize(saved);
            var restored = System.Text.Json.JsonSerializer.Deserialize<LayoutConfig>(json)!;
            Assert.True(restored.IsRightPanelCollapsed); Assert.Equal(720, restored.RightColumnWidth);
            splitter.ToggleCollapsed(); // a fresh host begins expanded
            InvokeLayout("Restore", restored, right, splitter);
            Assert.True(splitter.IsCollapsed); Assert.Equal(0, right.MinWidth);
            InvokeLayout("Restore", restored, right, splitter);
            Assert.True(splitter.IsCollapsed); Assert.Equal(new GridLength(0), right.Width);
            splitter.ToggleCollapsed(); Assert.Equal(new GridLength(720), right.Width);
        });
    }

    [Fact]
    public void Row_splitter_restores_height_and_minimum()
    {
        RunSta(() =>
        {
            var grid = new Grid { Width = 800, Height = 600 };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bottom = new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 150 };
            grid.RowDefinitions.Add(bottom);
            var splitter = new CollapsibleSplitterBox { Height = 8, ResizeDirection = GridResizeDirection.Rows, CollapseDirection = CollapseDirectionType.Bottom };
            Grid.SetRow(splitter, 1); grid.Children.Add(splitter);
            grid.Measure(new Size(800, 600)); grid.Arrange(new Rect(0, 0, 800, 600));
            splitter.ToggleCollapsed(); Assert.Equal(0, bottom.MinHeight); Assert.Equal(new GridLength(0), bottom.Height);
            splitter.ToggleCollapsed(); Assert.Equal(150, bottom.MinHeight); Assert.True(bottom.Height.IsStar);
        });
    }
    [Fact]
    public void Actual_top_bar_loads_original_images_and_disables_unavailable_entries()
    {
        RunSta(() =>
        {
            var app = System.Windows.Application.Current!;
            foreach(var resource in new[]{"Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml"})
                app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
            var state = new TopBarState();
            var top = new SunEyeVision.UI.Shell.Controls.Shell.ShellTopBarControl { DataContext = state, Height = 81 };
            var firstWindow = new Window { Content = top, Width = 1600, Height = 160, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false };
            firstWindow.Show();
            top.Measure(new Size(1600, 81)); top.Arrange(new Rect(0, 0, 1600, 81)); top.UpdateLayout();
            if (Environment.GetEnvironmentVariable("SUN_EYE_TOPBAR_CAPTURE") is { Length: > 0 } capturePath)
            {
                CaptureTopBar(top, capturePath);
            }
            var menu = Assert.IsType<Menu>(top.FindName("MainMenu"));
            Assert.Equal(new[] { "文件(_F)", "设置", "工具(_T)", "系统", "帮助(_H)" },
                menu.Items.Cast<MenuItem>().Select(item => item.Header.ToString()).ToArray());
            var settings = (MenuItem)menu.Items[1];
            Assert.False(settings.Items.OfType<MenuItem>().First(item => Equals(item.Header, "权限设置")).IsEnabled);
            Assert.False(((MenuItem)menu.Items[2]).Items.OfType<MenuItem>().Single().IsEnabled);
            var welcome = Assert.IsType<MenuItem>(top.FindName("WelcomeMenuItem"));
            Assert.Same(state.ShowStartPageCommand, welcome.Command);
            Assert.Contains(welcome, ((MenuItem)menu.Items[4]).Items.Cast<object>());
            Assert.NotNull(top.FindName("UndoHistoryHotspot"));
            Assert.Null(top.FindName("EditHistoryButton"));
            var theme = Assert.IsType<Button>(top.FindName("ThemeToggleButton"));
            var themeIcon = Assert.IsType<Viewbox>(theme.Content);
            Assert.Equal(26, themeIcon.Width); Assert.Equal(26, themeIcon.Height);
            var geometry = Assert.IsType<System.Windows.Shapes.Path>(themeIcon.Child);
            Assert.Equal(2.1, geometry.StrokeThickness);
            var darkIcon = geometry.Data.ToString();
            Assert.Equal("切换到浅色主题", theme.ToolTip);
            state.IsDarkTheme = false; top.UpdateLayout();
            Assert.NotEqual(darkIcon, geometry.Data.ToString());
            Assert.Equal(0, geometry.StrokeThickness); Assert.NotNull(geometry.Fill);
            Assert.Equal("切换到深色主题", theme.ToolTip);
            if (Environment.GetEnvironmentVariable("SUN_EYE_TOPBAR_CAPTURE") is { Length: > 0 } lightCapture)
            {
                var light = new ResourceDictionary { Source = new Uri("/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml", UriKind.Relative) };
                app.Resources.MergedDictionaries.Add(light); top.UpdateLayout();
                CaptureTopBar(top, lightCapture + ".light.png");
                app.Resources.MergedDictionaries.Remove(light); top.UpdateLayout();
            }
            var buttons = new List<Button>(); CollectButtons(top, buttons);
            foreach (var iconButton in buttons.Where(button => button.Content is Image))
            {
                var icon = Assert.IsType<Image>(iconButton.Content);
                Assert.Equal(36, icon.Width); Assert.Equal(36, icon.Height);
                Assert.True(Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(icon.Source).PixelWidth > 0);
                if (!iconButton.IsEnabled) continue;
                iconButton.IsEnabled = false; top.UpdateLayout();
                Assert.True(Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(icon.Source).PixelWidth > 0);
                iconButton.ClearValue(UIElement.IsEnabledProperty); top.UpdateLayout();
            }
            var save = Assert.Single(buttons.Where(b => Equals(b.ToolTip, "保存 (Ctrl+S)")));
            var saveMenu = ((MenuItem)menu.Items[0]).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "保存方案(_S)"));
            Assert.Same(saveMenu.Tag, save.Tag); // the toolbar is a projection of the actual menu declaration
            Assert.Same(saveMenu.Command, save.Command);
            var toolbar = Assert.IsType<StackPanel>(top.FindName("ToolbarItems"));
            var lightMenu = ((MenuItem)menu.Items[3]).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "光源设置"));
            Assert.False(lightMenu.IsEnabled);
            Assert.DoesNotContain(buttons, b => ReferenceEquals(b.Tag, lightMenu.Tag));
            Assert.Same(save, toolbar.Children[0]);
            var openMenu = ((MenuItem)menu.Items[0]).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "打开方案(_O)"));
            Assert.Same(openMenu.Tag, Assert.IsType<Button>(toolbar.Children[1]).Tag);
            var image = Assert.IsType<Image>(save.Content);
            Assert.NotNull(image.Source);
            Assert.Contains("toolbarsave_", image.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Same(state.SaveCurrentSolutionCommand, save.Command);
            state.Save.Enabled = false; state.Save.Notify(); top.UpdateLayout();
            Assert.False(save.IsEnabled);
            Assert.Contains("toolbarsave_disable", image.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            var continuous = Assert.Single(buttons.Where(b => Equals(b.ToolTip, "连续运行")));
            Assert.True(continuous.IsEnabled); Assert.Same(state.ToggleContinuousAllCommand, continuous.Command);
            var runImage = Assert.IsType<Image>(continuous.Content);
            Assert.Contains("toolbarrunrepeat_normal", runImage.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            state.IsContinuousRunning = true; top.UpdateLayout();
            Assert.True(continuous.IsEnabled);
            Assert.Contains("toolbarstop_n", runImage.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            state.Continuous.Enabled = false; state.Continuous.Notify(); top.UpdateLayout();
            Assert.False(continuous.IsEnabled);
            Assert.Contains("toolbarstop_disable", runImage.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            state.IsContinuousRunning = false; top.UpdateLayout();
            Assert.Contains("toolbarrunrepeat_disable", runImage.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            foreach (var name in new[] { "锁定", "触发", "脚本", "综合配置", "运行界面" })
            {
                var placeholder = Assert.Single(buttons.Where(b => (b.ToolTip as string)?.StartsWith(name + "：") == true));
                Assert.False(placeholder.IsEnabled); Assert.IsType<Image>(placeholder.Content);
                Assert.Equal(36, placeholder.Width); Assert.Equal(36, placeholder.Height);
            }
            Assert.DoesNotContain(buttons, b => Equals(b.ToolTip, "工作流"));
            var runMode = Assert.IsType<Button>(top.FindName("RunModeButton"));
            Assert.False(runMode.IsEnabled); Assert.Null(runMode.Command);
            Assert.IsType<Image>(Assert.IsType<StackPanel>(runMode.Content).Children[0]);
            var maximize = Assert.IsType<Button>(top.FindName("MaximizeButton"));
            Assert.Equal("最大化", maximize.ToolTip);
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            var frame = new System.Windows.Threading.DispatcherFrame();
            Exception? asynchronousFailure = null;
            Window? secondWindow = null;
            using var done = new ManualResetEventSlim();
            void Finish(Exception? error = null)
            {
                asynchronousFailure = error;
                ((MenuItem)menu.Items[0]).IsSubmenuOpen = false;
                firstWindow.Close(); secondWindow?.Close(); done.Set(); frame.Continue=false;
            }
            void Step(Action work) => dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try { work(); } catch (Exception error) { Finish(error); }
            }));
            var watchdog = new Thread(() =>
            {
                if (!done.Wait(TimeSpan.FromSeconds(8)) && !dispatcher.HasShutdownStarted)
                    dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => Finish(new TimeoutException("真实菜单和窗口事件未在限定时间内完成。"))));
            }) { IsBackground = true };
            ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
            Step(() =>
            {
                Assert.False(saveMenu.Command.CanExecute(saveMenu.CommandParameter));
                var click = typeof(MenuItem).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                click.Invoke(saveMenu, null);
                Step(() =>
                {
                    Assert.Equal(0, state.Save.ExecutionCount);
                    state.Save.Enabled = true; state.Save.Notify();
                    Assert.True(saveMenu.Command.CanExecute(saveMenu.CommandParameter));
                    click.Invoke(saveMenu, null);
                    Step(() =>
                    {
                        Assert.Equal(1, state.Save.ExecutionCount);
                        firstWindow.Content = null;
                        Step(() =>
                        {
                            firstWindow.WindowState = WindowState.Maximized;
                            Step(() =>
                            {
                                Assert.Equal("最大化", maximize.ToolTip);
                                secondWindow = new Window { Content = top, Width = 300, Height = 100, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false };
                                secondWindow.Show(); secondWindow.WindowState = WindowState.Maximized;
                                Step(() =>
                                {
                                    Assert.Equal("向下还原", maximize.ToolTip);
                                    secondWindow.Content = null;
                                    Step(() =>
                                    {
                                        secondWindow.WindowState = WindowState.Normal;
                                        Step(() =>
                                        {
                                            Assert.Equal("向下还原", maximize.ToolTip);
                                            var replacement = new TopBarState(); replacement.Save.Enabled = false;
                                            top.DataContext = replacement; top.UpdateLayout();
                                            Assert.Same(replacement.SaveCurrentSolutionCommand, save.Command);
                                            state.Save.Enabled = true; state.Save.Notify(); top.UpdateLayout();
                                            Assert.False(save.IsEnabled);
                                            VerifyToolboxCatalog(); Finish();
                                        });
                                    });
                                });
                            });
                        });
                    });
                });
            });
            watchdog.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            if (asynchronousFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(asynchronousFailure).Throw();
        });
    }

    private static void VerifyToolboxCatalog()
    {
        var productRoot=Environment.GetEnvironmentVariable("SUNEYE_INFRA_PRODUCT_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(productRoot), "必须提供本工作区正式载荷根。");
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(Path.GetFullPath(productRoot!))+Path.DirectorySeparatorChar);
        var manager=new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginManager();
        manager.LoadPluginsAsync().GetAwaiter().GetResult();
        Assert.Null(manager.LastAdmissionError);
        var admission=Assert.IsType<SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DesktopToolAdmission>(manager.DesktopAdmission);
        SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel NewCatalog() => new(admission);
        var empty=NewCatalog();
        Assert.Equal(15,empty.Categories.Count);
        Assert.True(empty.AllTools.Count>=143);
        Assert.Contains(empty.AllTools,item=>!item.IsAvailable&&!item.CanActivate());
        empty.IsCompactMode = false;
        empty.Categories[0].IsExpanded = true;
        var toolbox = new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxControl { DataContext = empty };
        var compact = Assert.IsType<ScrollViewer>(toolbox.FindName("Compact"));
        var items = Assert.IsType<ItemsControl>(compact.Content);
        var category = Assert.IsType<Grid>(items.ItemTemplate.LoadContent());
        category.DataContext = empty.Categories[0];
        var popup = Assert.Single(category.Children.OfType<Popup>());
        var setter = Assert.Single(popup.Style.Setters.OfType<Setter>(), x => x.Property == Popup.IsOpenProperty);
        var mouseBinding = Assert.IsType<System.Windows.Data.Binding>(setter.Value);
        Assert.Equal(System.Windows.Data.BindingMode.OneWay, mouseBinding.Mode);
        category.Measure(new Size(52, 50)); category.Arrange(new Rect(0, 0, 52, 50));
        Assert.False(popup.IsOpen);
        empty.IsCompactMode = true;
        empty.SelectedCategory = "采集";
        Assert.True(empty.IsCompactModePopupOpen);
        var replacement = NewCatalog();
        toolbox.DataContext = replacement;
        Assert.False(empty.IsCompactModePopupOpen);
        Assert.Null(empty.SelectedCategory);
        Assert.False(replacement.IsCompactModePopupOpen);
        toolbox.DataContext = empty;
        empty.SelectedCategory = "采集";
        typeof(SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxControl)
            .GetMethod("ToolboxLeave", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(toolbox, new object[] { toolbox, new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) });
        Assert.False(empty.IsCompactModePopupOpen);
        Assert.Null(empty.SelectedCategory);
        empty.IsCompactMode = false;
        toolbox.Measure(new Size(230, 650)); toolbox.Arrange(new Rect(0, 0, 230, 650)); toolbox.UpdateLayout();
        var borders = new List<Border>(); CollectBorders(toolbox, borders);
        Assert.Contains(borders, b => b.DataContext is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem item && !item.IsAvailable && !b.IsEnabled);
        try
        {
            var model=NewCatalog();
            Assert.Equal(15, model.Categories.Count);
            Assert.True(model.AllTools.Count >= 143);
            var image = Assert.Single(model.GetCategoryTools("采集"), t => t.ToolId == "ImageLoad");
            Assert.True(image.IsAvailable); Assert.True(image.CanActivate());
            var canvasHost = new SunEyeVision.UI.Features.WorkflowUI.Canvas.WorkflowCanvasControl();
            var previewHandler = new SunEyeVision.UI.Services.Interaction.WorkflowDragDropHandler(canvasHost);
            var handlerType = previewHandler.GetType();
            var showPreview = handlerType.GetMethod("ShowTempNode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var hidePreview = handlerType.GetMethod("HideTempNode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var canvasA = new Canvas(); var canvasB = new Canvas();
            showPreview.Invoke(previewHandler, new object[] { image, new Point(100, 100), canvasA });
            var preview = Assert.IsType<Border>(Assert.Single(canvasA.Children.Cast<UIElement>()));
            var previewPresenter = Assert.IsType<ContentPresenter>(preview.Child);
            Assert.Same(Assert.IsType<ItemsControl>(canvasHost.FindName("VmNodeItems")).ItemTemplate, previewPresenter.ContentTemplate);
            var previewNode = Assert.IsAssignableFrom<object>(previewPresenter.Content);
            Assert.Equal(admission.Runtime.GetCatalog("ImageLoad")!.IconKey,
                Assert.IsAssignableFrom<SunEyeVision.UI.Features.WorkflowUI.Canvas.ICanvasToolIconSource>(previewNode).ToolIconKey);
            object? Read(string name)=>previewNode.GetType().GetProperty(name)!.GetValue(previewNode);
            Assert.Equal(true,Read("IsToolboxPreview")); Assert.Equal("图像源", Read("DispName"));
            Assert.Equal("ImageLoad", Read("ToolType")); Assert.False(preview.IsHitTestVisible);
            hidePreview.Invoke(previewHandler, null);
            Assert.Equal(Visibility.Collapsed, preview.Visibility);
            showPreview.Invoke(previewHandler, new object[] { image, new Point(150, 100), canvasA });
            Assert.Same(preview, Assert.Single(canvasA.Children.Cast<UIElement>()));
            Assert.Equal(Visibility.Visible, preview.Visibility);
            showPreview.Invoke(previewHandler, new object[] { image, new Point(200, 100), canvasB });
            Assert.Empty(canvasA.Children.Cast<UIElement>());
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(Assert.Single(canvasB.Children.Cast<UIElement>())).Visibility);
            var canvasWindow = new Window { Content = canvasHost, Width = 400, Height = 300, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                canvasWindow.Show(); canvasWindow.UpdateLayout();
                var surface = Assert.IsType<Canvas>(canvasHost.FindName("WorkflowCanvas"));
                showPreview.Invoke(previewHandler, new object[] { image, new Point(150, 100), surface });
                canvasWindow.UpdateLayout();
                var actualPreview = surface.Children.OfType<Border>().Single(b => Equals(b.Tag, "ImageLoad"));
                var presenter = Assert.IsType<ContentPresenter>(actualPreview.Child);
                var index = Assert.IsType<TextBlock>(presenter.ContentTemplate.FindName("NodeIndexText", presenter));
                var title = Assert.IsType<TextBlock>(presenter.ContentTemplate.FindName("NodeNameText", presenter));
                Assert.Equal(Visibility.Collapsed, index.Visibility); Assert.Equal("图像源", title.Text);
            }
            finally { canvasWindow.Content = null; canvasWindow.Close(); }
            var dragEventConstructor = typeof(DragEventArgs).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point) }, null)!;
            DragEventArgs MakeDrag(Canvas target, RoutedEvent routedEvent)
            {
                var args = (DragEventArgs)dragEventConstructor.Invoke(new object[] {
                    new DataObject("ToolItem", image), DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, target, new Point(180, 100) });
                args.RoutedEvent = routedEvent; return args;
            }
            previewHandler.Canvas_DragEnter(canvasA, MakeDrag(canvasA, DragDrop.DragEnterEvent));
            var entered = Assert.IsType<Border>(Assert.Single(canvasA.Children.Cast<UIElement>()));
            previewHandler.Canvas_DragLeave(canvasA, MakeDrag(canvasA, DragDrop.DragLeaveEvent));
            Assert.Equal(Visibility.Collapsed, entered.Visibility);
            var over = MakeDrag(canvasA, DragDrop.DragOverEvent);
            previewHandler.Canvas_DragOver(canvasA, over);
            Assert.Equal(DragDropEffects.Copy, over.Effects);
            Assert.Equal(Visibility.Visible, entered.Visibility);
            var blob=Assert.Single(model.GetCategoryTools("定位"),t=>t.ToolId=="BlobAnalysis");
            Assert.True(blob.IsAvailable);
            var disabled=Assert.Single(model.GetCategoryTools("测量"),t=>t.ToolId=="LineCircleMeasure");
            Assert.False(disabled.IsAvailable);
            model.SelectedCategory = "采集";
            Assert.True(Assert.Single(model.SelectedCategoryTools, t => t.ToolId == "ImageLoad").IsAvailable);
            var host = new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxControl { DataContext = model };
            var hostType = host.GetType();
            var down = hostType.GetMethod("ToolDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var up = hostType.GetMethod("ToolUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var armed = hostType.GetField("_tool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var pointer = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
            var openCategory = hostType.GetMethod("Open", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var acquisition = model.Categories.Single(c => c.Name == "采集");
            var retainedTools = acquisition.Tools;
            var categoryElement = new Grid { Tag = acquisition };
            openCategory.Invoke(host, new object[] { categoryElement });
            Assert.Same(retainedTools, acquisition.Tools);
            var drag = hostType.GetMethod("StartToolDrag", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var beginDrag = hostType.GetProperty("BeginDrag", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var dragSource = new Border { Tag = image };
            var calls = 0;
            beginDrag.SetValue(host, (Func<DependencyObject, object, DragDropEffects, DragDropEffects>)((source, data, effect) => {
                calls++; Assert.Same(dragSource, source); Assert.True(host.IsDragging);
                Assert.Equal("采集", model.SelectedCategory);
                Assert.Same(image, Assert.IsType<DataObject>(data).GetData("ToolItem"));
                Assert.Equal(DragDropEffects.Copy, effect); return DragDropEffects.None;
            }));
            drag.Invoke(host, new object[] { dragSource, image });
            Assert.Equal(1, calls); Assert.False(host.IsDragging); Assert.Null(model.SelectedCategory);
            model.SelectedCategory = "采集";
            drag.Invoke(host, new object[] { dragSource, image });
            Assert.Equal(2, calls); Assert.False(host.IsDragging); Assert.Null(model.SelectedCategory);
            model.SelectedCategory = "采集";
            beginDrag.SetValue(host, (Func<DependencyObject, object, DragDropEffects, DragDropEffects>)((_, _, _) => throw new InvalidOperationException("模拟拖动失败")));
            drag.Invoke(host, new object[] { dragSource, image });
            Assert.False(host.IsDragging); Assert.Null(model.SelectedCategory);
            down.Invoke(host, new object[] { new Border { Tag = disabled }, pointer }); Assert.Null(armed.GetValue(host));
            down.Invoke(host, new object[] { new Border { Tag = image }, pointer }); Assert.Same(image, armed.GetValue(host));
            down.Invoke(host, new object[] { new Border { Tag = disabled }, pointer }); Assert.Null(armed.GetValue(host));
            down.Invoke(host, new object[] { new Border { Tag = image }, pointer }); Assert.Same(image, armed.GetValue(host));
            up.Invoke(host, new object[] { host, pointer }); Assert.Null(armed.GetValue(host));
            manager.UnloadPluginsAsync().GetAwaiter().GetResult();
            Assert.Null(Assert.IsAssignableFrom<SunEyeVision.UI.Features.WorkflowUI.Canvas.ICanvasToolIconSource>(previewNode).ToolIconKey);
            down.Invoke(host, new object[] { new Border { Tag = image }, pointer }); Assert.Null(armed.GetValue(host));
            Assert.False(image.CanActivate());
            model.SelectedCategory = "采集";
            var refreshed = Assert.Single(model.SelectedCategoryTools, t => t.ToolId == "ImageLoad");
            Assert.False(refreshed.IsAvailable); Assert.Contains("目录已变化", refreshed.ToolTip);
            Assert.Same(refreshed, Assert.Single(model.Categories.First(c => c.Name == "采集").Tools!, t => t.ToolId == "ImageLoad"));
        }
        finally { manager.UnloadPluginsAsync().GetAwaiter().GetResult(); }
    }
    private sealed class TopBarState : System.ComponentModel.INotifyPropertyChanged
    {
        public FakeCommand Save { get; } = new();
        public System.Windows.Input.ICommand SaveCurrentSolutionCommand => Save;
        public System.Windows.Input.ICommand ShowStartPageCommand { get; } = new FakeCommand();
        public System.Windows.Input.ICommand ToggleThemeCommand { get; } = new FakeCommand();
        public FakeCommand Continuous { get; } = new();
        public System.Windows.Input.ICommand ToggleContinuousAllCommand => Continuous;
        private bool _running;
        private bool _dark = true;
        public bool IsDarkTheme { get => _dark; set { _dark = value; PropertyChanged?.Invoke(this, new(nameof(IsDarkTheme))); } }
        public bool IsContinuousRunning { get => _running; set { _running = value; PropertyChanged?.Invoke(this, new(nameof(IsContinuousRunning))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class FakeCommand : System.Windows.Input.ICommand
    {
        public bool Enabled { get; set; } = true;
        public bool CanExecute(object? parameter) => Enabled;
        public int ExecutionCount { get; private set; }
        public void Execute(object? parameter) => ExecutionCount++;
        public event EventHandler? CanExecuteChanged;
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
    private static void CaptureTopBar(FrameworkElement top, string path)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1600, 81, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(top);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void CollectButtons(DependencyObject node, List<Button> buttons)
    {
        if (node is Button button) buttons.Add(button);
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
            CollectButtons(System.Windows.Media.VisualTreeHelper.GetChild(node, i), buttons);
    }
    private static void CollectBorders(DependencyObject node, List<Border> borders)
    {
        if (node is Border border) borders.Add(border);
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
            CollectBorders(System.Windows.Media.VisualTreeHelper.GetChild(node, i), borders);
    }
    private static void InvokeLayout(string method, LayoutConfig config, ColumnDefinition right, CollapsibleSplitterBox splitter)
    {
        var type = typeof(MainWindow).Assembly.GetType("SunEyeVision.UI.Shell.Controls.Shell.MainWorkspaceLayout", throwOnError: true)!;
        type.GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { config, right, splitter });
    }
    private static void RunSta(Action action) => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(action);
}
