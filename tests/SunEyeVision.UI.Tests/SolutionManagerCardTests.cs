using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed partial class SolutionManagerUiTests
{
    [Fact]
    public void Cards_with_and_without_description_have_equal_height_and_footer_alignment()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8); var window = fixture.Open();
            foreach (var theme in new[] { "Dark", "Light" })
            {
                Render(window, theme, 1040, 680);
                var list = (ListBox)window.FindName("CardList");
                var panel = VisualChildren(list).OfType<SolutionCardPanel>().Single();
                var items = Enumerable.Range(0, 6).Select(i => (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i)!).ToArray();
                Assert.All(items, item => Assert.Equal(112, item.ActualHeight, 3));
                var baseline = items[0].TranslatePoint(new Point(), panel).Y;
                Assert.All(items.Take(panel.ColumnCount), item => Assert.Equal(baseline, item.TranslatePoint(new Point(), panel).Y, 3));
                var footerOffsets = items.Select(item => VisualChildren(item).OfType<TextBlock>()
                    .Single(text => text.Text.StartsWith("修改：")).TranslatePoint(new Point(), item).Y).ToArray();
                Assert.All(footerOffsets, offset => Assert.Equal(footerOffsets[0], offset, 3));
            }
        });
    }

    [Fact]
    public void First_items_host_measure_connects_generator_without_pre_rendering()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8);
            var window = fixture.Open();
            window.Resources.MergedDictionaries.Add(Theme("Dark"));
            var list = (ListBox)window.FindName("CardList");
            list.ItemsSource = ((SolutionConfigurationDialogViewModel)window.DataContext).FilteredMetadatas;
            // Minimal host: avoid ScrollViewer materializing the panel as a side effect.
            list.Template = new ControlTemplate(typeof(ListBox))
            { VisualTree = new FrameworkElementFactory(typeof(ItemsPresenter)) };
            list.ApplyTemplate();
            var presenter = VisualChildren(list).OfType<ItemsPresenter>().First();
            presenter.ApplyTemplate();
            var panel = (SolutionCardPanel)VisualTreeHelper.GetChild(presenter, 0);
            Assert.Same(list, ItemsControl.GetItemsOwner(panel));
            Assert.True(panel.IsItemsHost);
            Assert.Equal(8, list.Items.Count);
            // Exercise the first measure directly, before enumerating panel children.
            typeof(SolutionCardPanel).GetMethod("MeasureOverride", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(panel, new object[] { new Size(800, 400) });
            Assert.True(VisualTreeHelper.GetChildrenCount(panel) > 0);
            Render(window, "Dark", 1040, 680);
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(0));
        });
    }

    [Fact]
    public void Disabled_manager_lists_keep_themed_background_and_selection()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8);
            var window = fixture.Open();
            var vm = (SolutionConfigurationDialogViewModel)window.DataContext;
            vm.SelectedMetadata = vm.FilteredMetadatas[0];
            foreach (var theme in new[] { "Dark", "Light" })
            {
                typeof(SolutionConfigurationDialogViewModel).GetProperty(nameof(vm.IsBusy))!.SetValue(vm, true);
                Render(window, theme, 1040, 680);
                foreach (var name in new[] { "CardList", "RecentList" })
                {
                    var list = (ListBox)window.FindName(name);
                    Assert.False(list.IsEnabled);
                    var border = VisualChildren(list).OfType<Border>().First();
                    Assert.Equal(0, ((SolidColorBrush)border.Background).Color.A);
                }
                var cards = (ListBox)window.FindName("CardList");
                var selected = (ListBoxItem)cards.ItemContainerGenerator.ContainerFromIndex(0)!;
                var card = (Border)selected.Template.FindName("Card", selected);
                var expected = (SolidColorBrush)window.FindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.TableRowSelectedBackgroundBrush);
                Assert.Equal(expected.Color, ((SolidColorBrush)card.Background).Color);
                typeof(SolutionConfigurationDialogViewModel).GetProperty(nameof(vm.IsBusy))!.SetValue(vm, false);
                window.UpdateLayout(); Assert.True(cards.IsEnabled);
            }
        });
    }

    [Fact]
    public void Manager_reopens_modally_after_startup_window_closes_with_current_solution()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture();
            var metadata = fixture.Manager.CreateNewSolution("重新打开方案");
            var first = fixture.Open();
            // Do not pre-measure: production goes straight from construction to ShowDialog.
            first.Resources.MergedDictionaries.Add(Theme("Dark"));
            first.Dispatcher.BeginInvoke(new Action(() => first.Close()));
            first.ShowDialog();
            fixture.Manager.SetCurrentSolution(fixture.Manager.LoadSolutionOnly(metadata.FilePath)!);
            var owner = new Window { Width = 1, Height = 1, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            owner.Show();
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var next = fixture.Open(); next.Owner = owner;
                    Assert.NotSame(System.Windows.Shell.WindowChrome.GetWindowChrome(first),
                        System.Windows.Shell.WindowChrome.GetWindowChrome(next));
                    next.Resources.MergedDictionaries.Add(Theme(i == 0 ? "Dark" : "Light"));
                    next.Dispatcher.BeginInvoke(new Action(() => next.Close()));
                    next.ShowDialog();
                }
            }
            finally { owner.Close(); }
        });
    }

    private sealed class CardFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "SunEyeVision-Cards", Guid.NewGuid().ToString("N"));
        public SolutionManager Manager { get; }
        public SolutionConfigurationDialog? Window { get; private set; }
        public CardFixture()
        {
            Directory.CreateDirectory(Root); PluginLoader.MarkAsLoaded();
            Manager = new SolutionManager(new ApplicationDataLocationService(Path.Combine(Root, "locator.json")).Prepare(Path.Combine(Root, "data")));
        }
        public void Fill(int count)
        {
            for (var i = 0; i < count; i++) Manager.RegisterKnownSolution(new SolutionMetadata
            { Id = "card-" + i, Name = "方案 " + i, FilePath = Path.Combine(Root, "missing-" + i + ".solution"),
              Description = i % 3 == 0 ? "一行摘要" : "", ModifiedTime = new DateTime(2026, 10, 9).AddMinutes(-i),
              LastAccessTime = i < 3 ? new DateTime(2026, 10, 9).AddHours(-i) : DateTime.MinValue });
        }
        public SolutionConfigurationDialog Open()
        {
            Window = new SolutionConfigurationDialog(Manager); return Window;
        }
        public void Dispose() { Window?.Close(); Directory.Delete(Root, true); }
    }
    [Fact]
    public void Valid_registered_cards_render_current_and_default_without_global_details()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture();
            var names = new[] { "工位1检测", "工位2定位", "外观检查", "尺寸测量" };
            var metadata = names.Select((name, index) => fixture.Manager.CreateNewSolution(name, index == 1 ? "螺钉位置及角度检测" : "")).ToArray();
            fixture.Manager.SetCurrentSolution(fixture.Manager.LoadSolutionOnly(metadata[0].FilePath)!);
            fixture.Manager.SetDefaultSolution(metadata[1].Id);
            var window = fixture.Open();
            Render(window, "Dark", 1040, 680); Render(window, "Light", 1040, 680);
            Assert.Contains(VisualChildren((Grid)window.Content).OfType<TextBlock>(), text => text.Text == "已打开" && text.Visibility == Visibility.Visible);
            Assert.Contains(VisualChildren((Grid)window.Content).OfType<TextBlock>(), text => text.Text == "启动默认" && text.Visibility == Visibility.Visible);
            Assert.DoesNotContain(VisualChildren((Grid)window.Content).OfType<Button>(), button => button.Content?.ToString() is "⋯" or "打开选中方案" or "编辑…");
        });
    }

    [Fact]
    public void Adding_existing_solution_clears_search_and_selects_registered_card_without_opening()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture();
            var metadata = fixture.Manager.CreateNewSolution("新登记方案");
            using var vm = new SolutionConfigurationDialogViewModel(fixture.Manager);
            vm.SearchText = "隐藏新方案的搜索条件"; Assert.Empty(vm.FilteredMetadatas);
            var registered = vm.AddExistingFile(metadata.FilePath);
            Assert.NotNull(registered); Assert.Equal("", vm.SearchText);
            Assert.Equal(metadata.Id, vm.SelectedMetadata!.Id);
            Assert.Contains(vm.SelectedMetadata, vm.FilteredMetadatas);
            Assert.Null(fixture.Manager.CurrentSolution);
        });
    }

    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in VisualChildren(child)) yield return nested; }
    }
    private static void AssertCardsFillRow(ListBox list, SolutionCardPanel panel)
    {
        var first = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)!;
        var last = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(panel.ColumnCount - 1)!;
        Assert.InRange(first.ActualWidth, 240, 492);
        Assert.Equal(first.ActualWidth, last.ActualWidth, 3);
        var right = last.TranslatePoint(new Point(last.ActualWidth, 0), panel).X;
        Assert.InRange(Math.Abs(right - panel.ViewportWidth), 0, 1);
    }

    private static object? Call(SolutionConfigurationDialog window, string name, params object[] arguments) =>
        typeof(SolutionConfigurationDialog).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);

    [Fact]
    public void Cards_fill_available_width_and_virtualize_large_catalogue()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(500); var window = fixture.Open();
            Render(window, "Dark", 1008, 600);
            var list = (ListBox)window.FindName("CardList");
            var panel = VisualChildren(list).OfType<SolutionCardPanel>().Single();
            Assert.Equal(3, panel.ColumnCount);
            AssertCardsFillRow(list, panel);
            Assert.InRange(VisualChildren(list).OfType<ListBoxItem>().Count(), 1, 40);
            Assert.True(panel.ExtentHeight > panel.ViewportHeight);
            panel.SetVerticalOffset(panel.ExtentHeight / 2); list.UpdateLayout();
            Assert.InRange(VisualChildren(list).OfType<ListBoxItem>().Count(), 1, 40);
            Assert.True(panel.VerticalOffset > 0);
            panel.SetVerticalOffset(0); list.UpdateLayout();
            Render(window, "Light", 760, 520);
            Assert.Equal(2, panel.ColumnCount);
            AssertCardsFillRow(list, panel);
            Render(window, "Dark", 1440, 760);
            Assert.True(panel.ColumnCount >= 3);
            AssertCardsFillRow(list, panel);
            list.SelectedItem = list.Items[list.Items.Count - 1]; list.ScrollIntoView(list.SelectedItem); list.UpdateLayout();
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem));
            Assert.True(panel.VerticalOffset > 0);
        });
    }
    [Fact]
    public void Recently_opened_shows_names_only_and_keeps_last_access_order()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8); var window = fixture.Open();
            Render(window, "Dark", 1008, 600);
            var vm = (SolutionConfigurationDialogViewModel)window.DataContext;
            Assert.Equal(new[] { "card-0", "card-1", "card-2" }, vm.RecentSolutions.Select(m => m.Id));
            var recent = (ListBox)window.FindName("RecentList");
            var values = VisualChildren(recent).OfType<TextBlock>().Select(text => text.Text).Where(text => !string.IsNullOrEmpty(text)).ToArray();
            Assert.Equal(vm.RecentSolutions.Select(m => m.Name), values);
            Assert.DoesNotContain(values, text => text.Contains("2026") || text.Contains("打开时间"));
            vm.SearchText = "missing-7";
            Assert.Single(vm.FilteredMetadatas);
            Assert.True(vm.SelectRecent(vm.RecentSolutions[1]));
            Assert.Equal("", vm.SearchText); Assert.Equal("card-1", vm.SelectedMetadata!.Id);
            Assert.Null(fixture.Manager.CurrentSolution);
        });
    }
    [Fact]
    public void Context_menu_targets_clicked_card_and_hides_global_save_as_and_recovery()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8); var window = fixture.Open();
            Render(window, "Dark", 1008, 600);
            var list = (ListBox)window.FindName("CardList");
            var vm = (SolutionConfigurationDialogViewModel)window.DataContext;
            vm.SelectedMetadata = vm.FilteredMetadatas[0];
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1)!;
            var constructor = typeof(ContextMenuEventArgs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(ctor => ctor.GetParameters().Length == 4);
            var args = (ContextMenuEventArgs)constructor.Invoke(new object[] { item, true, -1d, -1d });
            args.RoutedEvent = ContextMenuService.ContextMenuOpeningEvent; args.Source = item;
            item.RaiseEvent(args);
            Assert.Equal(item.DataContext, vm.SelectedMetadata);
            var menu = item.ContextMenu; menu.PlacementTarget = item;
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.Same(vm, menu.DataContext);
            var headers = menu.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToArray();
            Assert.Contains("属性…", headers);
            Assert.DoesNotContain(headers, text => text?.Contains("保存版本") == true || text?.Contains("恢复") == true);
            var save = menu.Items.OfType<MenuItem>().Single(m => m.Header?.ToString() == "另存为…");
            Assert.Equal(Visibility.Collapsed, save.Visibility);
            Assert.False(vm.LaunchCommand.CanExecute(null));
            Assert.True(vm.LocateCommand.CanExecute(null));
        });
    }
    [Fact]
    public void Save_as_only_applies_to_selected_current_solution()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture();
            var first = fixture.Manager.CreateNewSolution("当前方案");
            var other = fixture.Manager.CreateNewSolution("其他方案");
            fixture.Manager.SetCurrentSolution(fixture.Manager.LoadSolutionOnly(first.FilePath)!);
            using var vm = new SolutionConfigurationDialogViewModel(fixture.Manager);
            vm.SelectedMetadata = vm.SolutionMetadatas.Single(m => m.Id == other.Id);
            Assert.False(vm.IsSelectedCurrent); Assert.False(vm.SaveSolutionAsCommand.CanExecute(null));
            vm.SelectedMetadata = vm.SolutionMetadatas.Single(m => m.Id == first.Id);
            Assert.True(vm.IsSelectedCurrent); Assert.True(vm.SaveSolutionAsCommand.CanExecute(null));
            Assert.False(vm.LocateCommand.CanExecute(null));
            Assert.Equal(first.Id, fixture.Manager.CurrentSolution!.Id);
        });
    }
    [Fact]
    public void Search_enter_does_not_launch_and_list_enter_is_local()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(6); var window = fixture.Open();
            Render(window, "Dark", 1008, 600);
            Assert.Empty(window.InputBindings);
            var list = (ListBox)window.FindName("CardList");
            var vm = (SolutionConfigurationDialogViewModel)window.DataContext;
            vm.SelectedMetadata = vm.FilteredMetadatas[0];
            using var source = new HwndSource(new HwndSourceParameters("SunEyeVision-key-test") { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000) });
            var search = (TextBox)window.FindName("SearchBox");
            var searchKey = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            search.RaiseEvent(searchKey);
            Assert.False(window.IsLaunched); Assert.False(searchKey.Handled);
            var listKey = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            list.RaiseEvent(listKey);
            Assert.True(listKey.Handled); Assert.False(window.IsLaunched);
            var right = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            list.RaiseEvent(right); Assert.Equal(1, list.SelectedIndex);
        });
    }
    [Fact]
    public void Card_has_modified_time_no_workflow_statistics_no_ellipsis_and_multiline_preview_is_compact()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8); var window = fixture.Open();
            Render(window, "Dark", 1008, 600);
            var list = (ListBox)window.FindName("CardList");
            var texts = VisualChildren(list).OfType<TextBlock>().Select(m => m.Text).ToArray();
            Assert.Contains(texts, text => text.StartsWith("修改：2026-10-09"));
            Assert.DoesNotContain(texts, text => text.Contains("工作流") || text.Contains("格式版本"));
            Assert.DoesNotContain(VisualChildren(list).OfType<Button>(), button => button.Content?.ToString() is "⋯" or "编辑…" or "打开选中方案");
            var converter = new SolutionDescriptionPreviewConverter();
            Assert.Equal("第一行 第二行", converter.Convert("第一行\n第二行", typeof(string), null!, CultureInfo.InvariantCulture));
            var panel = VisualChildren(list).OfType<SolutionCardPanel>().Single();
            AssertCardsFillRow(list, panel);
        });
    }
    [Fact]
    public void Filtering_clears_hidden_selection_and_default_changes_do_not_open_solution()
    {
        Sta(() =>
        {
            using var fixture = new CardFixture(); fixture.Fill(8);
            using var vm = new SolutionConfigurationDialogViewModel(fixture.Manager);
            Assert.Equal(fixture.Manager.GetAllMetadata().OrderByDescending(m => m.ModifiedTime).Select(m => m.Id), vm.FilteredMetadatas.Select(m => m.Id));
            vm.SelectedMetadata = vm.FilteredMetadatas[0]; vm.SearchText = "missing-7";
            Assert.Null(vm.SelectedMetadata); Assert.Null(fixture.Manager.CurrentSolution);
            vm.ClearSearchCommand.Execute(null);
            vm.SelectedMetadata = vm.FilteredMetadatas[1]; vm.ToggleDefaultCommand.Execute(null);
            Assert.True(vm.SelectedMetadata!.IsDefault); Assert.Null(fixture.Manager.CurrentSolution);
        });
    }
    [Fact]
    public void Properties_information_window_is_themed_read_only_and_scrollable()
    {
        Sta(() =>
        {
            var fields = new[] { new KeyValuePair<string, string>("文件路径", @"D:\传递方案\工位.solution"), new KeyValuePair<string, string>("描述", new string('长', 2000)) };
            var window = new SolutionInformationDialog("方案属性", fields);
            try
            {
                Render(window, "Dark", 580, 420); Render(window, "Light", 520, 380);
                Assert.All(VisualChildren((Grid)window.Content).OfType<TextBox>(), box => Assert.True(box.IsReadOnly));
                Assert.True(VisualChildren((Grid)window.Content).OfType<ScrollViewer>().Any());
            }
            finally { window.Close(); }
        });
    }
}
