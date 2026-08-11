using System.Windows;

namespace SunEyeVision.UI.Views.Windows;

/// <summary>
/// EditSolutionDialog.xaml 的交互逻辑
/// </summary>
public partial class EditSolutionDialog : Window
{
    /// <summary>
    /// 解决方案名称
    /// </summary>
    public string SolutionName => SolutionNameTextBox?.Text?.Trim() ?? string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description => DescriptionTextBox?.Text?.Trim();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="solutionName">当前解决方案名称</param>
    /// <param name="description">当前描述</param>
    public EditSolutionDialog(string solutionName, string? description)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(solutionName))
        {
            SolutionNameTextBox.Text = solutionName;
        }

        if (!string.IsNullOrEmpty(description))
        {
            DescriptionTextBox.Text = description;
        }

        // 设置焦点到解决方案名称文本框
        Loaded += (s, e) => SolutionNameTextBox.Focus();
    }

    /// <summary>
    /// 解决方案名称文本变化时，启用/禁用保存按钮
    /// </summary>
    private void OnSolutionNameTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(SolutionNameTextBox?.Text?.Trim());
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
    /// 保存按钮点击事件
    /// </summary>
    private void OnSaveButtonClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SolutionNameTextBox?.Text?.Trim()))
        {
            MessageBox.Show("请输入解决方案名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
