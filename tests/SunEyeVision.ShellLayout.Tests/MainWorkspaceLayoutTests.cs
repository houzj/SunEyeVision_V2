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
        });
    }

    private sealed class TopBarState : System.ComponentModel.INotifyPropertyChanged
    {
        public FakeCommand Save { get; } = new();
        public System.Windows.Input.ICommand SaveCurrentSolutionCommand => Save;
        public System.Windows.Input.ICommand ToggleContinuousAllCommand { get; } = new FakeCommand();
        private bool _running;
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
