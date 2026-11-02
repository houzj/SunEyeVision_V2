using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Common;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

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
            var app = new SunEyeVision.UI.App(); app.InitializeComponent();
            var state = new TopBarState();
            var top = new SunEyeVision.UI.Shell.Controls.Shell.ShellTopBarControl { DataContext = state };
            top.Measure(new Size(1600, 64)); top.Arrange(new Rect(0, 0, 1600, 64)); top.UpdateLayout();
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
            var geometry = Assert.IsType<System.Windows.Shapes.Path>(Assert.IsType<Viewbox>(theme.Content).Child);
            var darkIcon = geometry.Data.ToString();
            state.IsDarkTheme = false; top.UpdateLayout();
            Assert.NotEqual(darkIcon, geometry.Data.ToString());
            var buttons = new List<Button>(); CollectButtons(top, buttons);
            var save = Assert.Single(buttons.Where(b => Equals(b.ToolTip, "保存 (Ctrl+S)")));
            var image = Assert.IsType<Image>(save.Content);
            Assert.NotNull(image.Source);
            Assert.Contains("toolbarsave_", image.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Same(state.SaveCurrentSolutionCommand, save.Command);
            state.Save.Enabled = false; state.Save.Notify(); top.UpdateLayout();
            Assert.False(save.IsEnabled);
            Assert.Contains("toolbarsave_disable", image.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            var continuous = Assert.Single(buttons.Where(b => (b.ToolTip as string)?.StartsWith("全流程连续运行尚未接入") == true));
            Assert.False(continuous.IsEnabled); Assert.Null(continuous.Command);
            var runImage = Assert.IsType<Image>(continuous.Content);
            Assert.Contains("toolbarrunrepeat_disable", runImage.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            state.IsAllWorkflowsRunning = true; top.UpdateLayout();
            Assert.False(continuous.IsEnabled);
            Assert.Contains("toolbarrunrepeat_disable", runImage.Source.ToString(), StringComparison.OrdinalIgnoreCase);
            foreach (var name in new[] { "锁定", "触发", "脚本", "综合配置", "运行界面" })
                Assert.False(Assert.Single(buttons.Where(b => Equals(b.Content, name))).IsEnabled);
            Assert.DoesNotContain(buttons, b => Equals(b.ToolTip, "工作流"));
            VerifyToolboxCatalog();
        });
    }

    private static void VerifyToolboxCatalog()
    {
        var empty = new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel(null);
        Assert.Equal(15, empty.Categories.Count);
        Assert.Equal(143, empty.AllTools.Count);
        Assert.All(empty.AllTools, item => { Assert.False(item.IsAvailable); Assert.False(item.CanActivate()); });
        empty.IsCompactMode = false;
        empty.Categories[0].IsExpanded = true;
        var toolbox = new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxControl { DataContext = empty };
        toolbox.Measure(new Size(230, 650)); toolbox.Arrange(new Rect(0, 0, 230, 650)); toolbox.UpdateLayout();
        var borders = new List<Border>(); CollectBorders(toolbox, borders);
        Assert.Contains(borders, b => b.DataContext is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem { Name: "图像源" } && !b.IsEnabled);
        var path = Path.Combine(Path.GetTempPath(), "suneye-toolbox-" + Guid.NewGuid() + ".xml");
        try
        {
            File.WriteAllText(path, "<ToolGroups SchemaVersion='1'><Group Name='采集' Order='1' Icon='a.png' HoverIcon='b.png'/><Group Name='定位' Order='2' Icon='c.png' HoverIcon='d.png'/><Group Name='测量' Order='3' Icon='e.png' HoverIcon='f.png'/></ToolGroups>");
            var tools = new[] {
                new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool("ImageLoad", "图像源", "采集", 1, "image", "", "entry", "image.dll", true),
                new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool("BlobAnalysis", "Blob分析", "定位", 1, "blob", "", "entry", "blob.dll", true),
                new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool("LineCircleMeasure", "线圆测量", "测量", 1, "generic", "", "entry", "extra.dll", true)
            };
            var directory = new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog(path, tools);
            var current = true; var admitted = true;
            SunEyeVision.Plugin.SDK.Metadata.ToolMetadata? Resolve(string id) => id == "BlobAnalysis" ? throw new InvalidOperationException("模拟加载失败") :
                new() { Id = id, Category = id == "ImageLoad" ? "采集" : "测量" };
            var type = typeof(SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel);
            var ctor = type.GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog), typeof(Func<string, SunEyeVision.Plugin.SDK.Metadata.ToolMetadata>), typeof(Func<string, bool>), typeof(Func<bool>) }, null)!;
            var model = (SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel)ctor.Invoke(new object[] { directory, (Func<string, SunEyeVision.Plugin.SDK.Metadata.ToolMetadata?>)Resolve, (Func<string, bool>)(_ => admitted), (Func<bool>)(() => current) });
            Assert.Equal(15, model.Categories.Count);
            Assert.Equal(144, model.AllTools.Count);
            var image = Assert.Single(model.GetCategoryTools("采集"), t => t.ToolId == "ImageLoad");
            Assert.True(image.IsAvailable); Assert.True(image.CanActivate());
            var blob = Assert.Single(model.GetCategoryTools("定位"), t => t.ToolId == "BlobAnalysis");
            Assert.False(blob.IsAvailable); Assert.Contains("加载失败", blob.ToolTip);
            var measure = model.GetCategoryTools("测量").Where(t => t.ToolId == "LineCircleMeasure").ToArray();
            Assert.Equal(2, measure.Length);
            Assert.Contains(measure, t => !t.IsAvailable && t.ToolTip.Contains("映射尚未核实"));
            Assert.Contains(measure, t => t.IsAvailable && t.Name.Contains("已部署，未匹配目录"));
            var absentDirectory = new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog(path, new[] { tools[0] });
            var absent = (SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxViewModel)ctor.Invoke(new object[] { absentDirectory, (Func<string, SunEyeVision.Plugin.SDK.Metadata.ToolMetadata?>)Resolve, (Func<string, bool>)(_ => admitted), (Func<bool>)(() => true) });
            Assert.Contains("未部署或未准入", Assert.Single(absent.GetCategoryTools("定位"), t => t.ToolId == "BlobAnalysis").ToolTip);
            model.SelectedCategory = "采集";
            Assert.True(Assert.Single(model.SelectedCategoryTools, t => t.ToolId == "ImageLoad").IsAvailable);
            var host = new SunEyeVision.UI.Shell.Controls.Toolbox.ToolboxControl { DataContext = model };
            var hostType = host.GetType();
            var down = hostType.GetMethod("ToolDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var up = hostType.GetMethod("ToolUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var armed = hostType.GetField("_tool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var pointer = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
            down.Invoke(host, new object[] { new Border { Tag = blob }, pointer }); Assert.Null(armed.GetValue(host));
            down.Invoke(host, new object[] { new Border { Tag = image }, pointer }); Assert.Same(image, armed.GetValue(host));
            up.Invoke(host, new object[] { host, pointer }); Assert.Null(armed.GetValue(host));
            admitted = false;
            Assert.False(image.CanActivate());
            model.SelectedCategory = "采集";
            Assert.Contains("运行组件未准入", Assert.Single(model.SelectedCategoryTools, t => t.ToolId == "ImageLoad").ToolTip);
            admitted = true;
            current = false;
            down.Invoke(host, new object[] { new Border { Tag = image }, pointer }); Assert.Null(armed.GetValue(host));
            Assert.False(image.CanActivate());
            model.SelectedCategory = "采集";
            var refreshed = Assert.Single(model.SelectedCategoryTools, t => t.ToolId == "ImageLoad");
            Assert.False(refreshed.IsAvailable); Assert.Contains("目录已变化", refreshed.ToolTip);
            Assert.Same(refreshed, Assert.Single(model.Categories.First(c => c.Name == "采集").Tools!, t => t.ToolId == "ImageLoad"));
        }
        finally { File.Delete(path); }
    }
    private sealed class TopBarState : System.ComponentModel.INotifyPropertyChanged
    {
        public FakeCommand Save { get; } = new();
        public System.Windows.Input.ICommand SaveCurrentSolutionCommand => Save;
        public System.Windows.Input.ICommand ShowStartPageCommand { get; } = new FakeCommand();
        public System.Windows.Input.ICommand ToggleThemeCommand { get; } = new FakeCommand();
        public System.Windows.Input.ICommand ToggleContinuousAllCommand { get; } = new FakeCommand();
        private bool _running;
        private bool _dark = true;
        public bool IsDarkTheme { get => _dark; set { _dark = value; PropertyChanged?.Invoke(this, new(nameof(IsDarkTheme))); } }
        public bool IsAllWorkflowsRunning { get => _running; set { _running = value; PropertyChanged?.Invoke(this, new(nameof(IsAllWorkflowsRunning))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class FakeCommand : System.Windows.Input.ICommand
    {
        public bool Enabled { get; set; } = true;
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) { }
        public event EventHandler? CanExecuteChanged;
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
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
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure is not null) throw new AggregateException(failure);
    }
}
