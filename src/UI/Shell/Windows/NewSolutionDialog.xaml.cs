using System;
using System.Windows;
using SunEyeVision.UI.Controls.Helpers;

namespace SunEyeVision.UI.Views.Windows;

/// <summary>
/// NewSolutionDialog.xaml 的交互逻辑
/// </summary>
public partial class NewSolutionDialog : Window
{
    /// <summary>
    /// 解决方案名称
    /// </summary>
    public string SolutionName
    {
        get => SolutionNameTextBox?.Text?.Trim() ?? string.Empty;
        set
        {
            if (SolutionNameTextBox != null)
            {
                SolutionNameTextBox.Text = value;
            }
        }
    }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description
    {
        get => DescriptionTextBox?.Text?.Trim();
        set
        {
            if (DescriptionTextBox != null)
            {
                DescriptionTextBox.Text = value;
            }
        }
    }

    /// <summary>
    /// 解决方案路径
    /// </summary>
    public string SolutionPath => SolutionPathTextBox?.Text ?? string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="defaultPath">默认路径</param>
    public NewSolutionDialog(string defaultPath)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(defaultPath))
        {
            SolutionPathTextBox.Text = defaultPath;
        }

        // 设置焦点到解决方案名称文本框
        Loaded += (s, e) => SolutionNameTextBox.Focus();
    }

    /// <summary>
    /// 解决方案名称文本变化时，启用/禁用创建按钮
    /// </summary>
    private void OnSolutionNameTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(SolutionNameTextBox?.Text?.Trim());
    }

    /// <summary>
    /// 浏览按钮点击事件
    /// </summary>
    private void OnBrowseButtonClick(object sender, RoutedEventArgs e)
    {
        var initialPath = SolutionPathTextBox?.Text ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var selectedPath = FolderBrowserHelper.BrowseForFolder("选择解决方案存储路径", initialPath);

        if (!string.IsNullOrEmpty(selectedPath))
        {
            SolutionPathTextBox.Text = selectedPath;
        }
    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 创建按钮点击事件
    /// </summary>
    private void OnCreateButtonClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SolutionNameTextBox?.Text?.Trim()))
        {
            MessageBox.Show("请输入解决方案名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(SolutionPathTextBox?.Text))
        {
            MessageBox.Show("请选择存储路径", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
