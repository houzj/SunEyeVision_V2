using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SunEyeVision.UI.Services.Startup;

namespace SunEyeVision.UI.Shell.Windows;
public enum StartPageAction { EmptyWorkspace, NewSolution, OpenFile }
public partial class StartPageWindow : Window
{
    private readonly Action<bool>? _savePreference;
    private bool _updatingPreference;
    private readonly List<RecentSolutionFile> _files = new();
    private readonly Action<string>? _removeRecent;
    public StartPageAction SelectedAction { get; private set; }
    public string? SelectedPath { get; private set; }
    public StartPageWindow() : this(false, Array.Empty<RecentSolutionFile>(), null) { }
    public StartPageWindow(bool hide, IEnumerable<RecentSolutionFile> recent, Action<bool>? savePreference,
        Action<string>? removeRecent = null)
    {
        InitializeComponent();
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false,
            NonClientFrameEdges = System.Windows.Shell.NonClientFrameEdges.None
        });
        HideStartPageBox.IsChecked = hide;
        _savePreference = savePreference;
        _removeRecent = removeRecent;
        _files.AddRange(recent.OrderByDescending(x => x.LastOpened));
        RefreshRows();
    }
    private void Finish(StartPageAction action, string? path = null)
    {
        SelectedAction = action; SelectedPath = path; DialogResult = true;
    }
    private void New_Click(object sender, RoutedEventArgs e) => Finish(StartPageAction.NewSolution);
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "方案文件 (*.solution)|*.solution", CheckFileExists = true, Title = "打开方案" };
        if (dialog.ShowDialog(this) == true) Finish(StartPageAction.OpenFile, dialog.FileName);
    }
    private void RefreshRows()
    {
        RecentFilesList.ItemsSource = _files.Take(6).ToArray();
        EmptyRecentText.Visibility = _files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Recent_OpenClick(object sender, RoutedEventArgs e)
    { if ((sender as FrameworkElement)?.DataContext is RecentSolutionFile item) OpenRecent(item); }
    private void Remove_Click(object sender, RoutedEventArgs e)
    { if ((sender as FrameworkElement)?.DataContext is RecentSolutionFile item) RemoveRecent(item); e.Handled = true; }
    private void RemoveRecent(RecentSolutionFile item)
    {
        try
        {
            _removeRecent?.Invoke(item.Path);
            _files.Remove(item); RefreshRows();
            StatusText.Text = "已移除记录，方案文件和当前工作区均保留。";
        }
        catch (Exception ex) { StatusText.Text = "记录未移除：" + ex.Message; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Finish(StartPageAction.EmptyWorkspace);
    private void Recent_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && (sender as ListBox)?.SelectedItem is RecentSolutionFile item) { OpenRecent(item); e.Handled = true; } }
    private void OpenRecent(RecentSolutionFile recent)
    {
        if (!File.Exists(recent.Path))
        {
            StatusText.Text = "文件已移动或不可用，请重新选择方案文件；取消不会切换工作区。";
            Open_Click(this, new RoutedEventArgs()); return;
        }
        Finish(StartPageAction.OpenFile, recent.Path);
    }
    private void HideStartPage_Changed(object sender, RoutedEventArgs e)
    {
        if (_savePreference == null || _updatingPreference) return;
        try { _savePreference(HideStartPageBox.IsChecked == true); StatusText.Text = "启动偏好已保存；文件启动始终直接打开方案。"; }
        catch (Exception ex) { _updatingPreference = true; HideStartPageBox.IsChecked = HideStartPageBox.IsChecked != true; _updatingPreference = false; StatusText.Text = "设置未保存：" + ex.Message; }
    }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (IsInteractiveSource(e.OriginalSource as DependencyObject))
            return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* window is closing */ }
        e.Handled = true;
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
            return;
        // Borderless start page has no native caption. Let blank surface move the
        // window, while controls retain their own click, selection and scrolling semantics.
        if (IsInteractiveSource(e.OriginalSource as DependencyObject))
            return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* window is closing */ }
        e.Handled = true;
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is Control)
                return true;
        return false;
    }
}
