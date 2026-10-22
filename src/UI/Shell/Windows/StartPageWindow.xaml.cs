using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.UI.Services.Startup;
using SunEyeVision.UI.Features.Dialogs;

namespace SunEyeVision.UI.Shell.Windows;
public enum StartPageAction { EmptyWorkspace, NewSolution, OpenFile, ManageSolutions }
public partial class StartPageWindow : Window
{
    private readonly Action<bool>? _savePreference;
    private bool _updatingPreference;
    public StartPageAction SelectedAction { get; private set; }
    public string? SelectedPath { get; private set; }
    public StartPageWindow() : this(false, Array.Empty<RecentSolutionFile>(), null) { }
    public StartPageWindow(bool hide, IEnumerable<RecentSolutionFile> recent, Action<bool>? savePreference)
    {
        InitializeComponent();
        HideStartPageBox.IsChecked = hide;
        _savePreference = savePreference;
        var files = recent.ToArray();
        RecentFilesList.ItemsSource = files;
        EmptyRecentText.Visibility = files.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
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
    private void Manage_Click(object sender, RoutedEventArgs e) => Finish(StartPageAction.ManageSolutions);
    private void Close_Click(object sender, RoutedEventArgs e) => Finish(StartPageAction.EmptyWorkspace);
    private void Recent_DoubleClick(object sender, MouseButtonEventArgs e) => OpenRecent();
    private void Recent_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { OpenRecent(); e.Handled = true; } }
    private void OpenRecent()
    {
        if (RecentFilesList.SelectedItem is not RecentSolutionFile recent) return;
        if (!File.Exists(recent.Path)) { StatusText.Text = "文件已移动或不可用，请使用“打开方案”重新选择文件。"; return; }
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
        if (e.OriginalSource is TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void Capability_Click(object sender, RoutedEventArgs e)
    {
        var name = (sender as Button)?.Tag?.ToString() ?? "能力介绍";
        var description = name switch
        {
            "图像处理" => "现有工具方向：图像加载、滤波、二值化、形态学。\n用于为后续定位和测量准备图像。",
            "定位与测量" => "现有工具方向：轮廓匹配、直线与圆检测、点／线／圆几何测量。",
            "标定与坐标" => "N 点标定用于建立图像坐标与物理坐标的映射。\n这里是能力介绍，不创建预设标定方案。",
            _ => "现有工具方向：条件分支、子程序、脚本、协议组装与解析、数据发送与接收。"
        };
        ThemeMessageDialog.Show(description + "\n\n实际模式和精度以工具设置及验收为准。", name,
            MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info, this);
    }
}
